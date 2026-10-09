"""Parametric fixed-wing aircraft generator (section M3 route P). Run headless:

    tools/blender/blender --background --factory-startup --python assets_src/procedural/aircraft.py -- <id>

Reads assets_src/procedural/specs/<id>.py (SPEC dict, metres, nose toward +Y, ground at z = 0, gear down).
Builds a lofted fuselage from cross-section stations, tapered wings and tail surfaces with thickness, engine
nacelles, a bubble canopy, gear legs, and separate control surfaces pivoted on their hinge lines. Node names
follow section M7: Root/Fuselage, Aileron_L/R, Elevator_L/R, Rudder(_L/_R), Flap_*, Gear_*, Exhaust_*,
Hardpoint_*, Cockpit, Canopy, Muzzle_0. Writes the same outputs as ground_vehicle.py.
"""
from __future__ import annotations

import importlib.util
import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from ground_vehicle import REPO, assign, box, empty, material, new_object, reset, triangles, tube_y  # noqa: E402


def fuselage(stations, segments=16):
    """stations: [(y, half_width, z_center, half_height)], nose first. Elliptical sections lofted along y."""
    bm = bmesh.new()
    rings = []
    for y, hw, zc, hh in stations:
        rings.append([bm.verts.new((max(hw, 0.005) * math.cos(2 * math.pi * k / segments), y,
                                    zc + max(hh, 0.005) * math.sin(2 * math.pi * k / segments))) for k in range(segments)])
    bm.faces.new(rings[0])
    bm.faces.new(list(reversed(rings[-1])))
    for a, b in zip(rings, rings[1:]):
        for i in range(segments):
            j = (i + 1) % segments
            bm.faces.new((a[i], b[i], b[j], a[j]))
    return bm


def hood_rings(sill_z, sections, crown=0.62):
    """Rings for a hood or fairing: sections [(y, half width at the sill, height)], crown = top width / sill width."""
    rings = []
    for yy, w, h in sections:
        pts = [(-w, 0.0), (-w * (1 + crown) / 2, h * 0.65), (-w * crown, h), (w * crown, h), (w * (1 + crown) / 2, h * 0.65), (w, 0.0)]
        rings.append([(x, yy, sill_z + max(zz, 0.01)) for x, zz in pts])
    return rings


def loft_rings(rings):
    """Closed loft through matching polygon rings (lists of 3D points), capped at both ends."""
    bm = bmesh.new()
    vs = [[bm.verts.new(p) for p in r] for r in rings]
    bm.faces.new(vs[0])
    bm.faces.new(list(reversed(vs[-1])))
    for a, b in zip(vs, vs[1:]):
        n = len(a)
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((a[i], b[i], b[j], a[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def surface(root, tip, thickness_pct=12, vertical=False):
    """A tapered lifting surface between two chord lines. root/tip = (x, y_le, z, chord), chord along -Y.
    The section is a lens (thickness at 30% chord), so the planform and side views read correctly.
    vertical=True puts the thickness on X (fins), otherwise on Z (wings, tailplanes)."""
    bm = bmesh.new()
    secs = []
    for x, yle, z, c in (root, tip):
        t = c * thickness_pct / 100 / 2
        pts = [(0.0, 0.0), (0.3, t), (1.0, 0.0), (0.3, -t)]
        secs.append([bm.verts.new((x + dz, yle - f * c, z) if vertical else (x, yle - f * c, z + dz)) for f, dz in pts])
    a, b = secs
    for i in range(4):
        j = (i + 1) % 4
        bm.faces.new((a[i], a[j], b[j], b[i]))
    bm.faces.new(list(reversed(a)))
    bm.faces.new(b)
    return bm


def build(spec):
    reset()
    paint = material("Paint", spec.get("paint", (0.25, 0.28, 0.24)))
    dark = material("Dark", (0.08, 0.08, 0.08))
    glass = material("Glass", (0.10, 0.14, 0.16))
    root = empty("Root")
    fus = new_object("Fuselage", fuselage(spec["fuselage"]), root)
    assign(fus, paint)
    empty("Cockpit", fus, spec["cockpit"])
    c = spec["canopy"]
    if c.get("type") == "hood":
        # Framed hood: tapered sections (sides leaning in to a narrower crown) lofted along y from the sill.
        can = new_object("Canopy", loft_rings(hood_rings(c["sill_z"], c["sections"], c.get("crown", 0.62))), fus)
    else:
        bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=1.0, location=c["at"])
        can = bpy.context.active_object
        can.name = "Canopy"
        can.scale = c["scale"]
        can.parent = fus
    assign(can, glass)

    # Wings and tail: each surface mirrored to L/R; control surfaces as separate hinged parts.
    for s in spec["surfaces"]:
        for side, sx in (("L", -1), ("R", 1)) if s.get("mirror", True) else (("", 1),):
            r = (sx * s["root"][0], *s["root"][1:])
            t = (sx * s["tip"][0], *s["tip"][1:])
            name = f"{s['name']}_{side}" if side else s["name"]
            o = new_object(name, surface(r, t, s.get("thickness_pct", 12), s.get("vertical", False)), fus)
            assign(o, paint)
            for cs in s.get("controls", []):
                # Control surface along the trailing edge between span fractions f0..f1, chord fraction cf.
                f0, f1, cf = cs["span"]
                pts = []
                for f in (f0, f1):
                    x = r[0] + (t[0] - r[0]) * f
                    yle = r[1] + (t[1] - r[1]) * f
                    z = r[2] + (t[2] - r[2]) * f
                    ch = r[3] + (t[3] - r[3]) * f
                    pts.append((x, yle - ch * (1 - cf), z, ch * cf))
                hinge_x = (pts[0][0] + pts[1][0]) / 2
                hinge_y = (pts[0][1] + pts[1][1]) / 2
                hinge_z = (pts[0][2] + pts[1][2]) / 2
                bm = surface(pts[0], pts[1], s.get("thickness_pct", 12) / 2, s.get("vertical", False))
                bmesh.ops.translate(bm, verts=bm.verts, vec=(-hinge_x, -hinge_y, -hinge_z))
                cname = f"{cs['name']}_{side}" if side else cs["name"]
                cobj = new_object(cname, bm, o, (hinge_x, hinge_y, hinge_z))
                assign(cobj, paint)

    for fr in spec.get("fairings", []):
        o = new_object(fr["name"], loft_rings(hood_rings(fr["sill_z"], fr["sections"], fr.get("crown", 0.62))), fus)
        assign(o, dark if fr.get("dark") else paint)
    for n in spec.get("nacelles", []):
        for side, sx in (("L", -1), ("R", 1)) if n.get("mirror", True) else (("", 1),):
            bm = tube_y(n["sections"], 16)
            if n.get("scale_z", 1.0) != 1.0:
                # Deep (oval) nacelles: stretch the round sections vertically about the axis.
                bmesh.ops.scale(bm, vec=(1.0, 1.0, n["scale_z"]), verts=bm.verts)
            o = new_object(f"{n['name']}_{side}" if side else n["name"], bm, fus, (sx * n["x"], 0, n["z"]))
            assign(o, paint)
            if n.get("exhaust", True):
                empty(f"Exhaust_{side or 'C'}", o, (0, n["sections"][-1][0], 0))
    for p in spec.get("parts", []):
        for side, sx in (("L", -1), ("R", 1)) if p.get("mirror") else (("", 1),):
            at = (sx * p["at"][0], p["at"][1], p["at"][2])
            o = new_object(f"{p['name']}_{side}" if side else p["name"], box(*p["size"]), fus, at)
            assign(o, dark if p.get("dark") else paint)
    for g in spec.get("gear", []):
        for side, sx in (("L", -1), ("R", 1)) if g.get("mirror") else (("", 1),):
            x, y, z_top = g["at"]
            leg = new_object(f"Gear_{g['name']}{('_' + side) if side else ''}", box(g.get("leg", 0.12), g.get("leg", 0.12), z_top - g["wheel_r"]), fus,
                             (sx * x, y, (z_top + g["wheel_r"]) / 2))
            assign(leg, dark)
            bpy.ops.mesh.primitive_cylinder_add(vertices=14, radius=g["wheel_r"], depth=0.25, location=(sx * x, y, g["wheel_r"]), rotation=(0, math.pi / 2, 0))
            wheel = bpy.context.active_object
            wheel.name = f"Wheel_{g['name']}{('_' + side) if side else ''}"
            wheel.parent = leg
            wheel.matrix_parent_inverse = leg.matrix_world.inverted()
            assign(wheel, dark)
    for k, hp in enumerate(spec.get("hardpoints", [])):
        empty(f"Hardpoint_{k:02d}", fus, hp)
    if "muzzle" in spec:
        empty("Muzzle_0", fus, spec["muzzle"])
    return root


def measure():
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for o in meshes:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            lo = Vector(map(min, lo, w))
            hi = Vector(map(max, hi, w))
    return {
        "triangles_lod0": sum(triangles(o) for o in meshes),
        "bbox_min": [round(v, 3) for v in lo], "bbox_max": [round(v, 3) for v in hi],
        "length_m": round(hi.y - lo.y, 3), "wingspan_m": round(hi.x - lo.x, 3), "height_m": round(hi.z - lo.z, 3),
        "nodes": sorted(o.name for o in bpy.data.objects),
    }


def main():
    vid = sys.argv[sys.argv.index("--") + 1]
    ms = importlib.util.spec_from_file_location(f"spec_{vid}", REPO / "assets_src" / "procedural" / "specs" / f"{vid}.py")
    mod = importlib.util.module_from_spec(ms)
    ms.loader.exec_module(mod)
    build(mod.SPEC)
    bpy.context.view_layer.update()
    work = REPO / "assets_src" / "work" / vid
    out = REPO / "assets_src" / "exports" / vid
    work.mkdir(parents=True, exist_ok=True)
    out.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(work / f"{vid}.blend"))
    bpy.ops.export_scene.gltf(filepath=str(out / f"{vid}_lod0.glb"), export_format="GLB", export_yup=True, export_apply=True)
    info = measure()
    info["id"] = vid
    (out / f"{vid}_build.json").write_text(json.dumps(info, indent=2) + "\n")
    print("BUILD", json.dumps({k: v for k, v in info.items() if k != "nodes"}))


if __name__ == "__main__":
    main()
