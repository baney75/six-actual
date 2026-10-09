"""Style-board look development (Blender half). Called by scripts/sheets/style_board.py:

    tools/blender/blender --background <blend> --python scripts/sheets/lookdev.py -- <asset_id> <out.png>

Replaces the blockout's flat materials with the sourced 1989 palette: camouflage textures from
assets_src/textures/camo_<scheme>.png, box-projected in object space at the scheme's physical tile size, and
sourced flat colours for building materials (assets_src/research/palettes_1989.json). Renders one outdoor
beauty shot (EEVEE, sun and sky colour, 3/4 elevation). Materials only; geometry is untouched.
"""
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

REPO = Path(__file__).resolve().parents[2]
vid, out = sys.argv[sys.argv.index("--") + 1], Path(sys.argv[sys.argv.index("--") + 2])
scene = bpy.context.scene
PAL = json.loads((REPO / "assets_src" / "research" / "palettes_1989.json").read_text())["schemes"]


def hexrgb(scheme, prefix, fallback):
    for c in PAL.get(scheme, {}).get("colors", []):
        if c["name"].startswith(prefix) and c.get("hex"):
            h = c["hex"]
            return tuple(((int(h[i:i + 2], 16) / 255.0) ** 2.2) for i in (1, 3, 5))  # sRGB -> linear (approx.)
    return fallback


def camo_material(name, scheme):
    meta = json.loads((REPO / "assets_src" / "textures" / f"camo_{scheme}.json").read_text())
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.85
    tc = nt.nodes.new("ShaderNodeTexCoord")
    mp = nt.nodes.new("ShaderNodeMapping")
    s = 1.0 / meta["tile_m"]
    mp.inputs["Scale"].default_value = (s, s, s)
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(str(REPO / "assets_src" / "textures" / f"camo_{scheme}.png"))
    tex.projection = "BOX"
    tex.projection_blend = 0.25
    nt.links.new(tc.outputs["Object"], mp.inputs["Vector"])
    nt.links.new(mp.outputs["Vector"], tex.inputs["Vector"])
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    return m


def flat(name, rgb, rough=0.8):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*rgb, 1.0)
    b.inputs["Roughness"].default_value = rough
    return m


HESSE = "hessian_village_materials"
REMAP = {
    "m1a1": {"Paint": ("camo", "nato3_carc")},
    "m60a3": {"Paint": ("camo", "nato3_carc")},
    "t80bv": {"Paint": ("camo", "soviet_4bo")},
    "a10a": {"Paint": ("camo", "a10_european1")},
    "su25": {"Paint": ("camo", "su25_1989")},
    "us_rifle_squad_1989": {"bdu": ("camo", "m81_woodland"), "vest": ("camo", "m81_woodland"), "helmet": ("camo", "m81_woodland")},
    "wgerman_town_block_1989": {
        "tile": ("flat", hexrgb(HESSE, "Red clay plain tile", (0.37, 0.05, 0.03))),
        "slate": ("flat", hexrgb(HESSE, "Slate", (0.04, 0.05, 0.05))),
        "plaster_white": ("flat", hexrgb(HESSE, "Lime render", (0.81, 0.75, 0.65))),
        "timber": ("flat", hexrgb(HESSE, "Fachwerk timber, ox-blood", (0.13, 0.03, 0.02))),
    },
}

made = {}
for o in scene.objects:
    if o.type != "MESH":
        continue
    for slot in o.material_slots:
        if slot.material is None:
            continue
        key = slot.material.name.split(".")[0]
        rule = REMAP.get(vid, {}).get(key)
        if rule is None:
            continue
        if key not in made:
            made[key] = camo_material(f"LD_{key}", rule[1]) if rule[0] == "camo" else flat(f"LD_{key}", rule[1])
        slot.material = made[key]

# Bounds and camera.
meshes = [o for o in scene.objects if o.type == "MESH" and o.name != "Ground"]
lo = Vector((1e9, 1e9, 1e9))
hi = Vector((-1e9, -1e9, -1e9))
for o in meshes:
    for c in o.bound_box:
        w = o.matrix_world @ Vector(c)
        lo = Vector(map(min, lo, w))
        hi = Vector(map(max, hi, w))
centre, size = (hi + lo) / 2, hi - lo
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x, scene.render.resolution_y = 1280, 720
scene.view_settings.view_transform = "AgX"
world = scene.world or bpy.data.worlds.new("World")
scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.64, 0.78, 1)
world.node_tree.nodes["Background"].inputs[1].default_value = 1.0
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
sun.data.energy = 4.0
sun.data.angle = math.radians(3)
sun.rotation_euler = (math.radians(48), 0, math.radians(140))
scene.collection.objects.link(sun)
if not any(o.name == "Ground" for o in scene.objects):
    bpy.ops.mesh.primitive_plane_add(size=max(size.x, size.y) * 6, location=(centre.x, centre.y, -0.003))
    g = bpy.context.active_object
    g.data.materials.append(flat("LD_ground", (0.12, 0.16, 0.07), 0.95))
radius = max(size.x, size.y, size.z) * 1.35
a = math.radians(215)
eye = Vector((centre.x + radius * math.sin(a), centre.y - radius * math.cos(a), centre.z + radius * 0.42))
cam = bpy.data.objects.new("LD_cam", bpy.data.cameras.new("LD_cam"))
scene.collection.objects.link(cam)
cam.location = eye
cam.rotation_euler = (centre - eye).to_track_quat("-Z", "Y").to_euler()
cam.data.lens = 50
scene.camera = cam
scene.render.filepath = str(out)
bpy.ops.render.render(write_still=True)
print("LOOKDEV_OK", out, sorted(made))
