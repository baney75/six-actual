#!/usr/bin/env python3
"""Build roster GLBs in Blender and refuse a model whose silhouette is still a box.

Run:
  blender --background --factory-startup --python scripts/build_models.py
"""

import json
import math
import sys
from pathlib import Path

import bpy
import bmesh
import mathutils

ROOT = Path(__file__).resolve().parents[1]
UNITS = json.loads((ROOT / "data" / "units.json").read_text(encoding="utf-8"))["units"]
OUT = ROOT / "game" / "assets" / "units"
SRC = ROOT / "assets_src" / "generated"
SHOTS = ROOT / "docs" / "shots" / "models"
HEROES = ("m1_abrams", "t80", "tiger", "spitfire", "f16", "us_rifle_cw")

COLORS = {
    "usa": (0.29, 0.34, 0.24, 1),
    "uk": (0.27, 0.33, 0.23, 1),
    "france": (0.31, 0.35, 0.26, 1),
    "west_germany": (0.33, 0.35, 0.27, 1),
    "germany": (0.36, 0.34, 0.26, 1),
    "ussr": (0.40, 0.38, 0.30, 1),
    "east_germany": (0.38, 0.36, 0.28, 1),
    "poland": (0.42, 0.40, 0.31, 1),
    "russia": (0.34, 0.36, 0.28, 1),
    "china": (0.32, 0.36, 0.28, 1),
}

# Fill of the silhouette's own bounding box. A box scores about 1.
# A glacis, barrel, wheel gap, or wing must pull the score down.
LIMITS = {
    "vehicle": {"side": 0.80, "top": 0.86, "min_tris": 180},
    "air": {"side": 0.62, "top": 0.55, "min_tris": 120},
    "person": {"side": 0.58, "top": 0.70, "min_tris": 40},
}


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def link(obj, parent):
    bpy.context.scene.collection.objects.link(obj)
    if parent is not None:
        obj.parent = parent
    return obj


def empty(name, parent, location):
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_size = 0.25
    obj.location = location
    return link(obj, parent)


def mesh_object(name, parent, location, bm, material):
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    obj.location = location
    if material is not None:
        obj.data.materials.append(material)
    return link(obj, parent)


def material_for(faction, burned):
    mat = bpy.data.materials.new("burned" if burned else "camo")
    mat.use_nodes = True
    color = (0.04, 0.04, 0.035, 1) if burned else COLORS.get(faction, (0.32, 0.32, 0.3, 1))
    principled = mat.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = color
    principled.inputs["Roughness"].default_value = 0.86 if not burned else 0.95
    mat.diffuse_color = color
    return mat


def dark(material_color=(0.02, 0.02, 0.018, 1)):
    mat = bpy.data.materials.new("track")
    mat.use_nodes = True
    principled = mat.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = material_color
    principled.inputs["Roughness"].default_value = 0.9
    mat.diffuse_color = material_color
    return mat


def hull_mesh(length, width, height):
    ring = [
        (-0.50, 0.16, 0.55),
        (-0.42, 0.34, 0.78),
        (-0.16, 0.62, 1.00),
        (0.08, 0.66, 1.00),
        (0.24, 0.56, 1.00),
        (0.40, 0.44, 0.96),
        (0.50, 0.36, 0.90),
        (0.50, 0.14, 0.90),
        (0.05, 0.10, 1.00),
        (-0.50, 0.10, 0.55),
    ]
    bm = bmesh.new()
    verts = []
    for y_frac, z_frac, x_scale in ring:
        y = length * y_frac
        z = height * z_frac
        x = width * 0.46 * x_scale
        verts.append(bm.verts.new((-x, y, z)))
        verts.append(bm.verts.new((x, y, z)))
    bm.verts.ensure_lookup_table()
    count = len(ring)
    for index in range(count - 1):
        a = index * 2
        bm.faces.new((verts[a], verts[a + 2], verts[a + 3], verts[a + 1]))
    bm.faces.new(tuple(verts[i * 2] for i in range(count)))
    bm.faces.new(tuple(verts[i * 2 + 1] for i in range(count - 1, -1, -1)))
    return bm


def cylinder(radius, depth, axis):
    bm = bmesh.new()
    bmesh.ops.create_cone(
        bm,
        cap_ends=True,
        segments=12,
        radius1=radius,
        radius2=radius,
        depth=depth,
    )
    if axis == "Y":
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=mathutils.Matrix.Rotation(math.pi / 2, 4, "X"))
    elif axis == "X":
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=mathutils.Matrix.Rotation(math.pi / 2, 4, "Y"))
    return bm


def uv_sphere(rx, ry, rz, segments=10):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=max(6, segments // 2), radius=1)
    for vert in bm.verts:
        vert.co.x *= rx
        vert.co.y *= ry
        vert.co.z *= rz
    return bm


def build_vehicle(unit, burned):
    length = unit["length_mm"] / 1000
    width = unit["width_mm"] / 1000
    height = max(unit["height_mm"] / 1000, 1.4)
    mat = material_for(unit["faction"], burned)
    track_mat = dark()
    root = empty("Root", None, (0, 0, 0))
    hull = mesh_object("Hull", root, (0, 0, 0), hull_mesh(length, width, height), mat)
    turret_z = height * 0.62
    turret = mesh_object(
        "Turret",
        hull,
        (0, -length * 0.02, turret_z),
        uv_sphere(width * 0.34, length * 0.22, height * 0.20, segments=12),
        mat,
    )
    barrel_len = length * (0.28 if burned else 0.62)
    barrel_r = max(0.045, width * 0.035)
    mesh_object(
        "Gun",
        turret,
        (0, -length * 0.16 - barrel_len * 0.5, height * 0.05),
        cylinder(barrel_r, barrel_len, "Y"),
        dark((0.08, 0.08, 0.07, 1)),
    )
    empty("Muzzle_0", turret, (0, -length * 0.16 - barrel_len, height * 0.05))
    wheel_r = max(0.22, height * 0.16)
    stations = 6
    for side, sign in (("L", -1), ("R", 1)):
        x = sign * width * 0.48
        skirt = bmesh.new()
        bmesh.ops.create_cube(skirt, size=1.0)
        for vert in skirt.verts:
            vert.co.x *= width * 0.06
            vert.co.y *= length * 0.92
            vert.co.z *= wheel_r * 0.55
        mesh_object(f"Track_{side}", hull, (x, 0, wheel_r * 1.15), skirt, track_mat)
        for index in range(stations):
            y = -length * 0.36 + index * (length * 0.72 / (stations - 1))
            mesh_object(
                f"Wheel_{side}_0{index + 1}",
                hull,
                (x, y, wheel_r),
                cylinder(wheel_r * 0.86, width * 0.08, "X"),
                track_mat,
            )
    empty("Hatch_0", turret, (0, 0, height * 0.18))
    empty("Exhaust_0", hull, (width * 0.2, length * 0.42, height * 0.32))
    return root


def loft_fuselage(length, height):
    stations = [
        (0.00, 0.015, 0.02),
        (0.05, 0.06, 0.07),
        (0.16, 0.11, 0.12),
        (0.38, 0.13, 0.14),
        (0.62, 0.10, 0.11),
        (0.86, 0.045, 0.05),
        (1.00, 0.012, 0.02),
    ]
    sides = 10
    bm = bmesh.new()
    rings = []
    for along, rx, rz in stations:
        y = -length * 0.5 + length * along
        ring = []
        for step in range(sides):
            angle = math.tau * step / sides
            ring.append(bm.verts.new((math.cos(angle) * length * rx * 0.22, y, math.sin(angle) * height * rz * 1.3)))
        rings.append(ring)
    for ring_index in range(len(rings) - 1):
        a = rings[ring_index]
        b = rings[ring_index + 1]
        for step in range(sides):
            nxt = (step + 1) % sides
            bm.faces.new((a[step], a[nxt], b[nxt], b[step]))
    bm.faces.new(tuple(rings[0]))
    bm.faces.new(tuple(reversed(rings[-1])))
    return bm


def wing_mesh(span, chord, thickness, sweep):
    bm = bmesh.new()
    root = [
        bm.verts.new((0, -chord * 0.5, 0)),
        bm.verts.new((0, chord * 0.5, 0)),
        bm.verts.new((0, chord * 0.35, thickness)),
        bm.verts.new((0, -chord * 0.45, thickness * 0.3)),
    ]
    tip = [
        bm.verts.new((span, -chord * 0.15 + sweep, thickness * 0.1)),
        bm.verts.new((span, chord * 0.15 + sweep, thickness * 0.1)),
        bm.verts.new((span, chord * 0.05 + sweep, thickness * 0.45)),
        bm.verts.new((span, -chord * 0.12 + sweep, thickness * 0.2)),
    ]
    for index in range(4):
        nxt = (index + 1) % 4
        bm.faces.new((root[index], root[nxt], tip[nxt], tip[index]))
    bm.faces.new(tuple(root))
    bm.faces.new(tuple(reversed(tip)))
    return bm


def build_air(unit, burned):
    length = unit["length_mm"] / 1000
    span = unit["width_mm"] / 1000
    height = max(unit["height_mm"] / 1000, 1.2)
    mat = material_for(unit["faction"], burned)
    root = empty("Root", None, (0, 0, height * 0.35))
    fuse = mesh_object("Fuselage", root, (0, 0, 0), loft_fuselage(length, height), mat)
    wing_z = 0
    chord = length * 0.22
    sweep = length * 0.08
    half = span * 0.48
    mesh_object("Wing_L", fuse, (-half, -length * 0.05, wing_z), wing_mesh(half, chord, height * 0.08, sweep), mat)
    mesh_object("Wing_R", fuse, (0, -length * 0.05, wing_z), wing_mesh(half, chord, height * 0.08, sweep), mat)
    mesh_object("Aileron_L", fuse, (-span * 0.32, length * 0.02, 0), wing_mesh(span * 0.16, chord * 0.28, 0.04, sweep * 0.3), mat)
    mesh_object("Aileron_R", fuse, (span * 0.32, length * 0.02, 0), wing_mesh(span * 0.16, chord * 0.28, 0.04, sweep * 0.3), mat)
    boom = bmesh.new()
    bmesh.ops.create_cube(boom, size=1.0)
    for vert in boom.verts:
        vert.co.x *= height * 0.10
        vert.co.y *= length * 0.18
        vert.co.z *= height * 0.09
    mesh_object("TailBoom", fuse, (0, length * 0.34, height * 0.02), boom, mat)
    tail_y = length * 0.42
    mesh_object("Elevator_L", fuse, (-span * 0.10, tail_y, height * 0.04), wing_mesh(span * 0.16, length * 0.10, 0.045, 0), mat)
    mesh_object("Elevator_R", fuse, (span * 0.10, tail_y, height * 0.04), wing_mesh(span * 0.16, length * 0.10, 0.045, 0), mat)
    rudder = mesh_object("Rudder", fuse, (0, tail_y, height * 0.02), wing_mesh(height * 0.42, length * 0.16, 0.05, 0), mat)
    rudder.rotation_euler = (0, math.pi / 2, 0)
    mesh_object("Flap_L", fuse, (-span * 0.18, -length * 0.02, -0.02), wing_mesh(span * 0.16, chord * 0.22, 0.03, 0), mat)
    empty("Gear_0", fuse, (0, length * 0.05, -height * 0.2))
    empty("Exhaust_0", fuse, (0, length * 0.15, 0))
    empty("Hardpoint_0", fuse, (-span * 0.18, -length * 0.02, -height * 0.08))
    mesh_object("Cockpit", fuse, (0, -length * 0.12, height * 0.12), uv_sphere(height * 0.16, length * 0.10, height * 0.10, segments=8), dark((0.02, 0.05, 0.06, 1)))
    return root


def build_person(unit, burned):
    mat = material_for(unit["faction"], burned)
    root = empty("Root", None, (0, 0, 0))
    mesh_object("Pelvis", root, (0, 0, 0.95), uv_sphere(0.16, 0.12, 0.12, segments=8), mat)
    mesh_object("Torso", root, (0, 0, 1.25), uv_sphere(0.20, 0.12, 0.28, segments=8), mat)
    mesh_object("Head", root, (0, 0, 1.62), uv_sphere(0.11, 0.12, 0.13, segments=8), mat)
    for side, sign in (("L", -1), ("R", 1)):
        mesh_object(f"Leg_{side}", root, (sign * 0.10, 0, 0.48), cylinder(0.07, 0.85, "Z"), mat)
        mesh_object(f"Arm_{side}", root, (sign * 0.28, -0.05, 1.25), cylinder(0.05, 0.55, "Z"), mat)
    mesh_object("Rifle", root, (0.18, -0.35, 1.15), cylinder(0.025, 0.85, "Y"), dark())
    return root


def world_triangles():
    bpy.context.view_layer.update()
    deps = bpy.context.evaluated_depsgraph_get()
    triangles = []
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        evaluated = obj.evaluated_get(deps)
        mesh = evaluated.to_mesh()
        mesh.calc_loop_triangles()
        matrix = evaluated.matrix_world
        for tri in mesh.loop_triangles:
            triangles.append(tuple(matrix @ mesh.vertices[index].co for index in tri.vertices))
        evaluated.to_mesh_clear()
    return triangles


def fill_ratio(triangles, axis_a, axis_b, resolution=56):
    if not triangles:
        return 0.0, 0
    points = [(point[axis_a], point[axis_b]) for tri in triangles for point in tri]
    min_a = min(point[0] for point in points)
    max_a = max(point[0] for point in points)
    min_b = min(point[1] for point in points)
    max_b = max(point[1] for point in points)
    span_a = max(max_a - min_a, 1e-6)
    span_b = max(max_b - min_b, 1e-6)
    mask = bytearray(resolution * resolution)

    def plot(px, py):
        if 0 <= px < resolution and 0 <= py < resolution:
            mask[py * resolution + px] = 1

    for tri in triangles:
        projected = []
        for point in tri:
            px = int((point[axis_a] - min_a) / span_a * (resolution - 1))
            py = int((point[axis_b] - min_b) / span_b * (resolution - 1))
            projected.append((px, py))
        fill_triangle(projected, plot)
    filled = sum(mask)
    return filled / float(resolution * resolution), len(triangles)


def fill_triangle(projected, plot):
    (x0, y0), (x1, y1), (x2, y2) = projected
    min_x = max(min(x0, x1, x2), 0)
    max_x = min(max(x0, x1, x2), 4096)
    min_y = max(min(y0, y1, y2), 0)
    max_y = min(max(y0, y1, y2), 4096)
    area = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0)
    if area == 0:
        plot(x0, y0)
        return
    for py in range(min_y, max_y + 1):
        for px in range(min_x, max_x + 1):
            w0 = (x1 - px) * (y2 - py) - (x2 - px) * (y1 - py)
            w1 = (x2 - px) * (y0 - py) - (x0 - px) * (y2 - py)
            w2 = (x0 - px) * (y1 - py) - (x1 - px) * (y0 - py)
            if area > 0 and w0 >= 0 and w1 >= 0 and w2 >= 0:
                plot(px, py)
            elif area < 0 and w0 <= 0 and w1 <= 0 and w2 <= 0:
                plot(px, py)


def grade(unit, kind_key):
    triangles = world_triangles()
    side, count = fill_ratio(triangles, 1, 2)
    top, _ = fill_ratio(triangles, 0, 1)
    limits = LIMITS[kind_key]
    problems = []
    if side > limits["side"]:
        problems.append(f"side silhouette fill {side:.2f} > {limits['side']:.2f} (still too rectangular)")
    if top > limits["top"]:
        problems.append(f"top silhouette fill {top:.2f} > {limits['top']:.2f}")
    if side < 0.08 or top < 0.05:
        problems.append(f"silhouette collapsed side {side:.2f} top {top:.2f}")
    if count < limits["min_tris"]:
        problems.append(f"only {count} triangles, need {limits['min_tris']}")
    names = {obj.name for obj in bpy.context.scene.objects}
    if kind_key == "vehicle" and not {"Hull", "Turret", "Gun", "Muzzle_0", "Track_L", "Wheel_L_01"} <= names:
        problems.append("missing vehicle nodes")
    if kind_key == "air" and not {"Fuselage", "Wing_L", "Wing_R", "Rudder", "Cockpit"} <= names:
        problems.append("missing aircraft nodes")
    return side, top, count, problems


def render_hero(unit_id):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = 720
    scene.render.resolution_y = 480
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    cam_data = bpy.data.cameras.new("HeroCam")
    cam_data.type = "PERSP"
    cam_data.lens = 50
    cam = bpy.data.objects.new("HeroCam", cam_data)
    scene.collection.objects.link(cam)
    bpy.context.view_layer.update()
    low = mathutils.Vector((1e9, 1e9, 1e9))
    high = mathutils.Vector((-1e9, -1e9, -1e9))
    for obj in scene.objects:
        if obj.type != "MESH":
            continue
        for corner in obj.bound_box:
            world = obj.matrix_world @ mathutils.Vector(corner)
            low.x, low.y, low.z = min(low.x, world.x), min(low.y, world.y), min(low.z, world.z)
            high.x, high.y, high.z = max(high.x, world.x), max(high.y, world.y), max(high.z, world.z)
    center = (low + high) * 0.5
    span = max(high.x - low.x, high.y - low.y, high.z - low.z, 1.0)
    cam.location = center + mathutils.Vector((span * 0.9, -span * 1.05, span * 0.38))
    direction = center - cam.location
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    SHOTS.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(SHOTS / f"{unit_id}.png")
    bpy.ops.render.render(write_still=True)


def kind_key(unit):
    if unit["kind"] == "air":
        return "air"
    if unit["kind"] in {"infantry", "aa"} and unit["length_mm"] < 2500:
        return "person"
    return "vehicle"


def export_one(unit, burned):
    reset()
    key = kind_key(unit)
    if key == "air":
        build_air(unit, burned)
    elif key == "person":
        build_person(unit, burned)
    else:
        build_vehicle(unit, burned)
    side, top, count, problems = grade(unit, key)
    name = unit["id"] + ("_Destroyed" if burned else "")
    if unit["id"] in HEROES and not burned:
        render_hero(unit["id"])
    if problems and not burned:
        raise SystemExit(f"{name} rejected: " + "; ".join(problems))
    path = OUT / f"{name}.glb"
    bpy.ops.export_scene.gltf(filepath=str(path), export_format="GLB")
    return path.name, side, top, count


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    SRC.mkdir(parents=True, exist_ok=True)
    only = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    lines = []
    for unit in UNITS:
        if only and unit["id"] not in only:
            continue
        name, side, top, count = export_one(unit, burned=False)
        lines.append(f"{name} side={side:.2f} top={top:.2f} tris={count}")
        if unit["kind"] not in {"infantry"}:
            destroyed, _, _, _ = export_one(unit, burned=True)
            lines.append(destroyed)
    (SRC / "grade.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"graded {len(lines)} models")


if __name__ == "__main__":
    main()
