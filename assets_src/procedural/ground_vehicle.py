"""Parametric tracked-vehicle generator (section M3 route P). Run headless:

    tools/blender/blender --background --factory-startup --python assets_src/procedural/ground_vehicle.py -- <id>

Reads assets_src/procedural/specs/<id>.py (a SPEC dict, metres, front toward +Y in Blender), builds the
vehicle from parametric parts, names nodes per section M7 (Root/Hull/Turret/Gun/Muzzle_0, Wheel_L_01..,
Track_L/R, Hatch_*, Exhaust_*, Optic_*), and writes:
    assets_src/work/<id>/<id>.blend
    assets_src/exports/<id>/<id>_lod0.glb      (+Y up, front toward -Z, 1 unit = 1 m: glTF/Godot conventions)
    assets_src/exports/<id>/<id>_build.json    (triangle counts, bounding box, key measured points)
Everything is generated from the spec: no hand-placed geometry, no third-party mesh.
"""
from __future__ import annotations

import importlib.util
import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector

REPO = Path(__file__).resolve().parents[2]


# ----------------------------------------------------------------------------- scene helpers

def reset() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)


def new_object(name: str, bm: bmesh.types.BMesh, parent=None, location=(0.0, 0.0, 0.0)):
    mesh = bpy.data.meshes.new(name)
    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = location
    if parent is not None:
        obj.parent = parent
    return obj


def empty(name: str, parent=None, location=(0.0, 0.0, 0.0)):
    obj = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = location
    if parent is not None:
        obj.parent = parent
    return obj


def material(name: str, rgb) -> bpy.types.Material:
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.8
    return m


def assign(obj, mat) -> None:
    obj.data.materials.clear()
    obj.data.materials.append(mat)


# ----------------------------------------------------------------------------- primitives

def prism_from_profile(profile_yz, x0: float, x1: float) -> bmesh.types.BMesh:
    """Extrude a closed side profile (list of (y, z), counter-clockwise seen from +X) between x0 and x1."""
    bm = bmesh.new()
    left = [bm.verts.new((x0, y, z)) for y, z in profile_yz]
    right = [bm.verts.new((x1, y, z)) for y, z in profile_yz]
    n = len(profile_yz)
    bm.faces.new(list(reversed(left)))
    bm.faces.new(right)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((left[i], left[j], right[j], right[i]))
    return bm


def loft(rings) -> bmesh.types.BMesh:
    """Loft between rings: each ring is (z, [(x, y) or (x, y, z_point), ...]) with the same vertex count.
    A point with its own z overrides the ring's z (sloped roofs and faces)."""
    bm = bmesh.new()
    vs = [[bm.verts.new((p[0], p[1], p[2] if len(p) > 2 else z)) for p in pts] for z, pts in rings]
    n = len(vs[0])
    bm.faces.new(list(reversed(vs[0])))
    bm.faces.new(vs[-1])
    for a, b in zip(vs, vs[1:]):
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((a[i], a[j], b[j], b[i]))
    return bm


def cylinder_x(radius: float, width: float, segments: int) -> bmesh.types.BMesh:
    """Cylinder along X centred on the origin (a road wheel)."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segments, radius1=radius, radius2=radius, depth=width)
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi / 2, 4, "Y"))
    return bm


def tube_y(sections, segments: int) -> bmesh.types.BMesh:
    """Barrel along +Y: sections = [(y, radius), ...]."""
    bm = bmesh.new()
    rings = []
    for y, r in sections:
        rings.append([bm.verts.new((r * math.cos(2 * math.pi * k / segments), y, r * math.sin(2 * math.pi * k / segments))) for k in range(segments)])
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    for a, b in zip(rings, rings[1:]):
        for i in range(segments):
            j = (i + 1) % segments
            bm.faces.new((a[i], a[j], b[j], b[i]))
    return bm


def box(sx: float, sy: float, sz: float) -> bmesh.types.BMesh:
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=(sx, sy, sz), verts=bm.verts)
    return bm


def part_mesh(part: dict) -> bmesh.types.BMesh:
    """A detail part: box (default), or shape cyl_z (vertical drum: diameter size[0], height size[2]),
    cyl_x (crosswise: length size[0], diameter size[2]) or cyl_y (lengthwise: diameter size[0], length size[1]).
    Optional pitch_deg (about X, applied first) and yaw_deg (about Z) orient it, e.g. ERA bricks on a sloped plate."""
    sx, sy, sz = part["size"]
    shape = part.get("shape", "box")
    if shape == "box":
        bm = box(sx, sy, sz)
    else:
        bm = bmesh.new()
        if shape == "cyl_z":
            r, depth, rot = sx / 2, sz, Matrix.Identity(4)
        elif shape == "cyl_x":
            r, depth, rot = sz / 2, sx, Matrix.Rotation(math.pi / 2, 4, "Y")
        else:
            r, depth, rot = sx / 2, sy, Matrix.Rotation(math.pi / 2, 4, "X")
        bmesh.ops.create_cone(bm, cap_ends=True, segments=part.get("segments", 16), radius1=r, radius2=r, depth=depth, matrix=rot)
    if part.get("pitch_deg"):
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(part["pitch_deg"]), 3, "X"))
    if part.get("yaw_deg"):
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(part["yaw_deg"]), 3, "Z"))
    return bm


def track_belt(path_yz, half_width: float, thickness: float) -> bmesh.types.BMesh:
    """A closed belt following path_yz (ordered loop of (y, z) points on the track's inner surface)."""
    bm = bmesh.new()
    n = len(path_yz)
    inner_l, inner_r, outer_l, outer_r = [], [], [], []
    for i, (y, z) in enumerate(path_yz):
        py, pz = path_yz[i - 1]
        ny, nz = path_yz[(i + 1) % n]
        ty, tz = ny - py, nz - pz
        ln = math.hypot(ty, tz) or 1.0
        # Outward normal of a clockwise-in-yz loop (belt runs over the top, back along the ground).
        oy, oz = tz / ln, -ty / ln
        inner_l.append(bm.verts.new((-half_width, y, z)))
        inner_r.append(bm.verts.new((half_width, y, z)))
        outer_l.append(bm.verts.new((-half_width, y + oy * thickness, z + oz * thickness)))
        outer_r.append(bm.verts.new((half_width, y + oy * thickness, z + oz * thickness)))
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((outer_l[i], outer_l[j], outer_r[j], outer_r[i]))
        bm.faces.new((inner_r[i], inner_r[j], inner_l[j], inner_l[i]))
        bm.faces.new((inner_l[i], inner_l[j], outer_l[j], outer_l[i]))
        bm.faces.new((outer_r[i], outer_r[j], inner_r[j], inner_r[i]))
    return bm


def belt_path(wheels, segs: int = 10):
    """Inner surface of a track loop. wheels = [(y, z, r), ...] front->rear: idler first, sprocket last, road
    wheels between. The loop starts at the sprocket top, wraps round the back of the sprocket, runs rear->front
    along the ground under the road wheels, wraps round the front of the idler and closes along the top run.
    Seen from +X this is clockwise, so (tz, -ty) is the outward normal everywhere."""
    front, rear, road = wheels[0], wheels[-1], wheels[1:-1]
    pts = []
    for k in range(segs + 1):
        a = math.pi / 2 + math.pi * k / segs          # top -> back -> bottom of the sprocket
        pts.append((rear[0] + rear[2] * math.cos(a), rear[1] + rear[2] * math.sin(a)))
    ground = min(z - r for _, z, r in road)
    for y, _, _ in reversed(road):
        pts.append((y, ground))
    for k in range(segs + 1):
        a = -math.pi / 2 + math.pi * k / segs         # bottom -> front -> top of the idler
        pts.append((front[0] + front[2] * math.cos(a), front[1] + front[2] * math.sin(a)))
    return pts


# ----------------------------------------------------------------------------- vehicle

def build(spec: dict):
    reset()
    paint = material("Paint", spec.get("paint", (0.28, 0.30, 0.22)))
    dark = material("RunningGear", (0.08, 0.08, 0.08))
    metal = material("Metal", (0.20, 0.20, 0.20))
    root = empty("Root")
    hull_spec = spec["hull"]
    W = spec["width_m"]
    tw = spec["track"]["width_m"]

    # Hull: lower hull between the tracks, upper hull/fenders over them. Skirts are optional (M60: none).
    sk = spec.get("skirt", {"thickness_m": 0.0})
    inner = W / 2 - sk["thickness_m"] - tw - 0.02
    lower = prism_from_profile(hull_spec["lower_profile"], -inner, inner)
    hull = new_object("Hull", lower, root)
    upper_half = W / 2 - sk["thickness_m"]
    up = new_object("Hull_Upper", prism_from_profile(hull_spec["upper_profile"], -upper_half, upper_half), hull)
    assign(hull, paint)
    assign(up, paint)

    # Skirts.
    if "profile" in sk:
        for side, sx in (("L", -1), ("R", 1)):
            x = sx * (W / 2 - sk.get("inset_m", 0.0) - sk["thickness_m"] / 2)
            s = new_object(f"Skirt_{side}", prism_from_profile(sk["profile"], -sk["thickness_m"] / 2, sk["thickness_m"] / 2), hull, (x, 0, 0))
            assign(s, paint)

    # Running gear.
    rg = spec["running_gear"]
    track_x = W / 2 - sk["thickness_m"] - tw / 2 - 0.01
    for side, sx in (("L", -1), ("R", 1)):
        for k, (y, z, r) in enumerate(rg["road_wheels"], start=1):
            wheel = new_object(f"Wheel_{side}_{k:02d}", cylinder_x(r, tw * 0.85, rg.get("wheel_segments", 16)), hull, (sx * track_x, y, z))
            assign(wheel, dark)
        for k, (y, z, r) in enumerate(rg.get("return_rollers", []), start=1):
            roller = new_object(f"Roller_{side}_{k:02d}", cylinder_x(r, tw * 0.5, 10), hull, (sx * track_x, y, z))
            assign(roller, dark)
        iy, iz, ir = rg["idler"]
        idler = new_object(f"Idler_{side}", cylinder_x(ir, tw * 0.8, 16), hull, (sx * track_x, iy, iz))
        sy, sz, sr = rg["sprocket"]
        sprocket = new_object(f"Sprocket_{side}", cylinder_x(sr, tw * 0.8, 20), hull, (sx * track_x, sy, sz))
        assign(idler, dark)
        assign(sprocket, dark)
        wheels = [rg["idler"], *rg["road_wheels"], rg["sprocket"]]
        path = belt_path([(y, z, r + 0.005) for y, z, r in wheels])
        tr = new_object(f"Track_{side}", track_belt(path, tw / 2, spec["track"]["thickness_m"]), hull, (sx * track_x, 0, 0))
        assign(tr, dark)

    # Turret: loft between plan rings (z above ground).
    t = spec["turret"]
    tx, ty, tz = t["ring_center"]
    turret = new_object("Turret", loft([(z, pts) for z, pts in t["rings"]]), hull, (tx, ty, tz))
    assign(turret, paint)
    bpy.context.view_layer.update()
    for part in t.get("parts", []):
        at = list(part["at"])
        if part.get("rest"):
            # Drop the part onto the turret surface below its (x, y): raycast the turret mesh from above.
            hit = roof_z(turret, at[0], at[1])
            if hit is not None:
                at[2] = hit + part["size"][2] / 2 + part.get("lift", 0.0)
        bm = part_mesh(part)
        o = new_object(part["name"], bm, turret, tuple(at))
        assign(o, paint if part.get("paint", True) else metal)
    if "bustle_rack" in t:
        br = t["bustle_rack"]
        o = new_object("BustleRack", box(*br["size"]), turret, br["at"])
        assign(o, metal)

    for part in spec.get("hull_parts", []):
        if "profile" in part:
            # Side-profile prism between x0 and x1 (sloped plates such as a hinged front skirt).
            x0, x1 = part["x"]
            o = new_object(part["name"], prism_from_profile(part["profile"], x0, x1), hull)
        else:
            o = new_object(part["name"], part_mesh(part), hull, part["at"])
        assign(o, paint if part.get("paint", True) else metal)

    # Gun: pivot (trunnion) under the turret; barrel along +Y.
    g = spec["gun"]
    gun = empty("Gun", turret, g["trunnion"])
    mantlet = new_object("Mantlet", box(*g["mantlet_size"]), gun, g.get("mantlet_at", (0, g["mantlet_size"][1] / 2, 0)))
    assign(mantlet, paint)
    barrel = new_object("Barrel", tube_y(g["sections"], g.get("segments", 16)), gun)
    assign(barrel, paint)
    muzzle_y = g["sections"][-1][0]
    empty("Muzzle_0", gun, (0, muzzle_y, 0))

    # Named detail sockets the engine uses (exhaust smoke, optics glint, hatches).
    for name, at in spec.get("sockets", {}).items():
        parent = turret if name.startswith(("Hatch_Cmd", "Hatch_Loader", "Optic_")) else hull
        empty(name, parent, at)
    return root


def roof_z(obj, x: float, y: float):
    """Local z of obj's top surface under local (x, y), or None if the ray misses."""
    from mathutils.bvhtree import BVHTree
    bvh = BVHTree.FromObject(obj, bpy.context.evaluated_depsgraph_get())
    hit = bvh.ray_cast(Vector((x, y, 50.0)), Vector((0.0, 0.0, -1.0)))
    return None if hit[0] is None else hit[0].z


def triangles(obj) -> int:
    if obj.type != "MESH":
        return 0
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def measure(root) -> dict:
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for o in meshes:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            lo = Vector(map(min, lo, w))
            hi = Vector(map(max, hi, w))
    muzzle = bpy.data.objects["Muzzle_0"].matrix_world.translation
    hull_meshes = [o for o in meshes if o.name in ("Hull", "Hull_Upper")]
    hlo = min((o.matrix_world @ Vector(c)).y for o in hull_meshes for c in o.bound_box)
    hhi = max((o.matrix_world @ Vector(c)).y for o in hull_meshes for c in o.bound_box)
    turret = bpy.data.objects.get("Turret")
    roof = max((turret.matrix_world @ Vector(c)).z for c in turret.bound_box) if turret else None
    return {
        "height_turret_roof_m": round(roof, 3) if roof is not None else None,
        "triangles_lod0": sum(triangles(o) for o in meshes),
        "bbox_min": [round(v, 3) for v in lo],
        "bbox_max": [round(v, 3) for v in hi],
        "length_hull_m": round(hhi - hlo, 3),
        "length_gun_forward_m": round(muzzle.y - hlo, 3),
        "width_m": round(hi.x - lo.x, 3),
        "height_m": round(hi.z - lo.z, 3),
        "nodes": sorted(o.name for o in bpy.data.objects),
    }


def main() -> None:
    vid = sys.argv[sys.argv.index("--") + 1]
    spec_path = REPO / "assets_src" / "procedural" / "specs" / f"{vid}.py"
    mod_spec = importlib.util.spec_from_file_location(f"spec_{vid}", spec_path)
    mod = importlib.util.module_from_spec(mod_spec)
    mod_spec.loader.exec_module(mod)
    spec = mod.SPEC
    root = build(spec)
    work = REPO / "assets_src" / "work" / vid
    out = REPO / "assets_src" / "exports" / vid
    work.mkdir(parents=True, exist_ok=True)
    out.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(work / f"{vid}.blend"))
    bpy.ops.export_scene.gltf(filepath=str(out / f"{vid}_lod0.glb"), export_format="GLB", export_yup=True, export_apply=True)
    info = measure(root)
    info["id"] = vid
    (out / f"{vid}_build.json").write_text(json.dumps(info, indent=2) + "\n")
    print("BUILD", json.dumps({k: v for k, v in info.items() if k != "nodes"}))


if __name__ == "__main__":
    main()
