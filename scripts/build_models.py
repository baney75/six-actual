#!/usr/bin/env python3
"""Build one GLB per unit with Blender. Run:
blender --background --factory-startup --python scripts/build_models.py
"""

import json
import sys
from pathlib import Path

import bpy
import bmesh

ROOT = Path(__file__).resolve().parents[1]
UNITS = json.loads((ROOT / "data" / "units.json").read_text(encoding="utf-8"))["units"]
OUT = ROOT / "game" / "assets" / "units"
SRC = ROOT / "assets_src" / "generated"

COLORS = {
    "usa": (0.25, 0.32, 0.22, 1),
    "uk": (0.28, 0.33, 0.24, 1),
    "france": (0.30, 0.34, 0.26, 1),
    "west_germany": (0.32, 0.34, 0.27, 1),
    "germany": (0.34, 0.32, 0.24, 1),
    "ussr": (0.36, 0.34, 0.26, 1),
    "east_germany": (0.34, 0.33, 0.25, 1),
    "poland": (0.38, 0.36, 0.28, 1),
    "russia": (0.33, 0.32, 0.24, 1),
    "china": (0.30, 0.34, 0.27, 1),
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
    obj.empty_display_size = 0.2
    obj.location = location
    return link(obj, parent)


def box(name, parent, location, size, material):
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for vert in bm.verts:
        vert.co.x *= size[0]
        vert.co.y *= size[1]
        vert.co.z *= size[2]
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    obj.location = location
    obj.data.materials.append(material)
    return link(obj, parent)


def material_for(faction, burned):
    mat = bpy.data.materials.new("camo" if not burned else "burned")
    mat.use_nodes = True
    color = (0.05, 0.05, 0.05, 1) if burned else COLORS.get(faction, (0.3, 0.3, 0.3, 1))
    principled = mat.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = color
    principled.inputs["Roughness"].default_value = 0.8
    return mat


def build_vehicle(unit, burned):
    length = unit["length_mm"] / 1000
    width = unit["width_mm"] / 1000
    height = unit["height_mm"] / 1000
    mat = material_for(unit["faction"], burned)
    root = empty("Root", None, (0, 0, 0))
    hull = box("Hull", root, (0, 0, height * 0.35), (width, length, height * 0.45), mat)
    turret = box("Turret", hull, (0, -length * 0.05, height * 0.28), (width * 0.62, length * 0.34, height * 0.28), mat)
    box("Gun", turret, (0, -length * 0.42, 0), (width * 0.08, length * 0.55, height * 0.08), mat)
    empty("Muzzle_0", turret, (0, -length * 0.7, 0))
    for side, sign in (("L", -1), ("R", 1)):
        box(f"Track_{side}", hull, (sign * width * 0.42, 0, height * 0.12), (width * 0.16, length * 0.92, height * 0.22), mat)
        for index in range(1, 4):
            y = -length * 0.3 + (index - 1) * length * 0.3
            box(f"Wheel_{side}_0{index}", hull, (sign * width * 0.36, y, height * 0.12), (width * 0.12, width * 0.12, width * 0.12), mat)
    empty("Hatch_0", turret, (0, 0, height * 0.16))
    empty("Exhaust_0", hull, (0, length * 0.4, height * 0.2))
    return root


def build_air(unit, burned):
    length = unit["length_mm"] / 1000
    span = unit["width_mm"] / 1000
    height = unit["height_mm"] / 1000
    mat = material_for(unit["faction"], burned)
    root = empty("Root", None, (0, 0, height * 0.5))
    fuse = box("Fuselage", root, (0, 0, 0), (height * 0.35, length, height * 0.28), mat)
    box("Wing_L", fuse, (-span * 0.28, 0, 0), (span * 0.48, length * 0.22, 0.08), mat)
    box("Wing_R", fuse, (span * 0.28, 0, 0), (span * 0.48, length * 0.22, 0.08), mat)
    box("Aileron_L", fuse, (-span * 0.4, length * 0.02, 0), (span * 0.16, length * 0.06, 0.04), mat)
    box("Aileron_R", fuse, (span * 0.4, length * 0.02, 0), (span * 0.16, length * 0.06, 0.04), mat)
    box("Elevator_L", fuse, (-span * 0.12, length * 0.42, 0), (span * 0.16, length * 0.08, 0.04), mat)
    box("Elevator_R", fuse, (span * 0.12, length * 0.42, 0), (span * 0.16, length * 0.08, 0.04), mat)
    box("Rudder", fuse, (0, length * 0.42, height * 0.12), (0.06, length * 0.08, height * 0.2), mat)
    box("Flap_L", fuse, (-span * 0.2, -length * 0.02, 0), (span * 0.18, length * 0.05, 0.03), mat)
    empty("Gear_0", fuse, (0, 0, -height * 0.2))
    empty("Exhaust_0", fuse, (0, length * 0.2, 0))
    empty("Hardpoint_0", fuse, (-span * 0.2, -length * 0.05, -height * 0.1))
    box("Cockpit", fuse, (0, -length * 0.18, height * 0.12), (height * 0.22, length * 0.12, height * 0.12), mat)
    return root


def build_person(unit, burned):
    mat = material_for(unit["faction"], burned)
    root = empty("Root", None, (0, 0, 0))
    box("Body", root, (0, 0, 1.0), (0.45, 0.3, 0.7), mat)
    box("Head", root, (0, 0, 1.55), (0.22, 0.22, 0.22), mat)
    box("Arm_L", root, (-0.28, 0, 1.05), (0.12, 0.12, 0.45), mat)
    box("Arm_R", root, (0.28, -0.2, 1.0), (0.1, 0.45, 0.1), mat)
    return root


def export(unit, burned):
    reset()
    kind = unit["kind"]
    if kind == "air":
        build_air(unit, burned)
    elif kind in {"infantry", "aa"} and unit["length_mm"] < 2500:
        build_person(unit, burned)
    else:
        build_vehicle(unit, burned)
    name = unit["id"] + ("_Destroyed" if burned else "")
    path = OUT / f"{name}.glb"
    bpy.ops.export_scene.gltf(filepath=str(path), export_format="GLB")
    return path


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    SRC.mkdir(parents=True, exist_ok=True)
    only = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    written = []
    for unit in UNITS:
        if only and unit["id"] not in only:
            continue
        written.append(export(unit, burned=False).name)
        if unit["kind"] not in {"infantry"}:
            written.append(export(unit, burned=True).name)
    (SRC / "manifest.txt").write_text("\n".join(written) + "\n", encoding="utf-8")
    print(f"wrote {len(written)} models")


if __name__ == "__main__":
    main()
