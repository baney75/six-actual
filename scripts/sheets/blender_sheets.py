"""Blender half of the contact-sheet renderer (section M4). Called by scripts/sheets/render_sheets.py:

    tools/blender/blender --background <blend> --python scripts/sheets/blender_sheets.py -- <out_dir>

Writes silhouette_{side,front,top}.png (orthographic, flat white on black, 100 px per metre, tight to the
model's bounding box) and turntable_{0..7}.png (EEVEE, 3/4 elevation, neutral grey, 1 m grid floor).
"""
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

PX_PER_M = 100
out = Path(sys.argv[sys.argv.index("--") + 1])
out.mkdir(parents=True, exist_ok=True)
scene = bpy.context.scene
SQUAD = "--squad" in sys.argv  # infantry: no silhouettes, own ground, hero close-ups, lineup and RTS views
KIT = "--kit" in sys.argv or SQUAD  # building kits: no silhouettes, own ground, elevations, street-level and RTS views
meshes = [o for o in scene.objects if o.type == "MESH" and not (KIT and o.name == "Ground")]
lo = Vector((1e9, 1e9, 1e9))
hi = Vector((-1e9, -1e9, -1e9))
for o in meshes:
    for c in o.bound_box:
        w = o.matrix_world @ Vector(c)
        lo = Vector(map(min, lo, w))
        hi = Vector(map(max, hi, w))
size = hi - lo
centre = (hi + lo) / 2


def camera(name, location, rotation, ortho_scale=None, lens=None):
    cam = bpy.data.objects.get(name) or bpy.data.objects.new(name, bpy.data.cameras.new(name))
    if cam.name not in scene.collection.objects:
        scene.collection.objects.link(cam)
    cam.location = location
    cam.rotation_euler = rotation
    if ortho_scale is not None:
        cam.data.type = "ORTHO"
        cam.data.ortho_scale = ortho_scale
    else:
        cam.data.type = "PERSP"
        cam.data.lens = lens
    scene.camera = cam
    return cam


# --gear-up: the reference drawing is drawn gear-up, so the silhouettes hide the landing gear.
hidden = [o for o in scene.objects if o.name.startswith(("Gear_", "Wheel_"))] if "--gear-up" in sys.argv else []
for o in hidden:
    o.hide_render = True

# --- Silhouettes: Workbench, flat white objects on black.
scene.render.engine = "BLENDER_WORKBENCH"
shading = scene.display.shading
shading.light = "FLAT"
shading.color_type = "SINGLE"
shading.single_color = (1, 1, 1)
scene.world = scene.world or bpy.data.worlds.new("World")
scene.render.film_transparent = False
shading.background_type = "VIEWPORT"
shading.background_color = (0, 0, 0)
scene.display_settings.display_device = "sRGB"
scene.view_settings.view_transform = "Standard"
views = {
    # name: (camera location, rotation, horizontal extent, vertical extent)
    "side": (Vector((hi.x + 20, centre.y, centre.z)), (math.pi / 2, 0, math.pi / 2), size.y, size.z),
    "front": (Vector((centre.x, hi.y + 20, centre.z)), (math.pi / 2, 0, math.pi), size.x, size.z),
    "top": (Vector((centre.x, centre.y, hi.z + 20)), (0, 0, math.pi / 2), size.y, size.x),
}
for name, (loc, rot, w, h) in ({} if KIT else views).items():
    scene.render.resolution_x = max(8, int(round(w * PX_PER_M)))
    scene.render.resolution_y = max(8, int(round(h * PX_PER_M)))
    scene.render.resolution_percentage = 100
    cam = camera(f"Cam_{name}", loc, rot, ortho_scale=max(w, h))
    cam.data.sensor_fit = "HORIZONTAL" if w >= h else "VERTICAL"
    scene.render.filepath = str(out / f"silhouette_{name}.png")
    bpy.ops.render.render(write_still=True)

for o in hidden:
    o.hide_render = False

# --- Turntable: EEVEE, neutral grey studio, 1 m grid floor.
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 960
scene.render.resolution_y = 600
world = scene.world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.45, 0.45, 0.47, 1)
world.node_tree.nodes["Background"].inputs[1].default_value = 0.8
sun = bpy.data.objects.new("Key", bpy.data.lights.new("Key", "SUN"))
sun.data.energy = 3.5
sun.rotation_euler = (math.radians(50), 0, math.radians(35))
scene.collection.objects.link(sun)
grid_mat = bpy.data.materials.new("Grid")
grid_mat.use_nodes = True
nodes = grid_mat.node_tree.nodes
bsdf = nodes["Principled BSDF"]
checker = nodes.new("ShaderNodeTexChecker")
checker.inputs["Scale"].default_value = 30.0
checker.inputs["Color1"].default_value = (0.36, 0.36, 0.37, 1)
checker.inputs["Color2"].default_value = (0.30, 0.30, 0.31, 1)
grid_mat.node_tree.links.new(checker.outputs["Color"], bsdf.inputs["Base Color"])
if not KIT:
    bpy.ops.mesh.primitive_plane_add(size=30, location=(centre.x, centre.y, -0.002))
    floor = bpy.context.active_object
    floor.data.materials.append(grid_mat)
radius = max(size.x, size.y) * 1.6
for k in range(8):
    a = math.radians(45 * k)
    loc = Vector((centre.x + radius * math.sin(a), centre.y - radius * math.cos(a), centre.z + radius * 0.55))
    direction = centre - loc
    rot = direction.to_track_quat("-Z", "Y").to_euler()
    camera("Cam_turn", loc, rot, lens=50)
    scene.render.filepath = str(out / f"turntable_{k}.png")
    bpy.ops.render.render(write_still=True)

# --- Close-ups for review (CS-2b): shaded orthographic side and top at 160 px per metre (kits: street
# elevation, side-street elevation and plan at 40 px per metre).
CLOSE_PX = 40 if KIT else 160
close_views = {"side": views["side"], "top": views["top"]}
if SQUAD:
    hero = [o for o in meshes if o.parent and o.parent.name.startswith("Soldier_00")]
    hlo = Vector((1e9, 1e9, 1e9))
    hhi = Vector((-1e9, -1e9, -1e9))
    for o in hero:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            hlo = Vector(map(min, hlo, w))
            hhi = Vector(map(max, hhi, w))
    hs, hc = hhi - hlo, (hhi + hlo) / 2
    hm = max(hs.x, hs.y)
    CLOSE_PX = 300
    close_views = {
        "front": (Vector((hc.x, hhi.y + 20, hc.z)), (math.pi / 2, 0, math.pi), hm, hs.z),
        "side": (Vector((hhi.x + 20, hc.y, hc.z)), (math.pi / 2, 0, math.pi / 2), hm, hs.z),
        "back": (Vector((hc.x, hlo.y - 20, hc.z)), (math.pi / 2, 0, 0), hm, hs.z),
        "lineup": (Vector((centre.x, hi.y + 40, centre.z)), (math.pi / 2, 0, math.pi), size.x, size.z),
    }
    # Further subjects: the RTO (radio and whip), a grenadier (M203) and the LAW rifleman.
    def subject_views(suffix, tag, views):
        objs = [o for o in meshes if o.parent and o.parent.name.endswith(suffix) and "Whip" not in o.name]
        if not objs:
            return []
        lo_, hi_ = Vector((1e9, 1e9, 1e9)), Vector((-1e9, -1e9, -1e9))
        for o in objs:
            for c in o.bound_box:
                w = o.matrix_world @ Vector(c)
                lo_, hi_ = Vector(map(min, lo_, w)), Vector(map(max, hi_, w))
        sz_, c_ = hi_ - lo_, (hi_ + lo_) / 2
        m_ = max(sz_.x, sz_.y)
        cams = {"side": (Vector((hi_.x + 20, c_.y, c_.z)), (math.pi / 2, 0, math.pi / 2), m_, sz_.z),
                "back": (Vector((c_.x, lo_.y - 20, c_.z)), (math.pi / 2, 0, 0), m_, sz_.z)}
        for v in views:
            close_views[f"{tag}_{v}"] = cams[v]
        return [o for o in meshes if o.parent and o.parent.name.endswith(suffix)]
    gr = subject_views("_B_GR", "gr", ["side"])
    law = subject_views("_A_R", "law", ["back"])
    rto = [o for o in meshes if o.parent and o.parent.name.endswith("_RTO")]
    if rto:
        rlo = Vector((1e9, 1e9, 1e9))
        rhi = Vector((-1e9, -1e9, -1e9))
        for o in rto:
            if "Whip" in o.name:
                continue
            for c in o.bound_box:
                w = o.matrix_world @ Vector(c)
                rlo = Vector(map(min, rlo, w))
                rhi = Vector(map(max, rhi, w))
        rs, rc = rhi - rlo, (rhi + rlo) / 2
        rm = max(rs.x, rs.y)
        close_views["rto_side"] = (Vector((rhi.x + 20, rc.y, rc.z)), (math.pi / 2, 0, math.pi / 2), rm, rs.z)
        close_views["rto_back"] = (Vector((rc.x, rlo.y - 20, rc.z)), (math.pi / 2, 0, 0), rm, rs.z)
elif KIT:
    close_views = {
        "front": (Vector((centre.x, lo.y - 40, centre.z)), (math.pi / 2, 0, 0), size.x, size.z),
        "side": (Vector((lo.x - 40, centre.y, centre.z)), (math.pi / 2, 0, -math.pi / 2), size.y, size.z),
        "top": views["top"],
    }
for name, (loc, rot, w, h) in close_views.items():
    if SQUAD:
        subject = rto if name.startswith("rto") else gr if name.startswith("gr_") else law if name.startswith("law_") else hero
        for o in [o for o in scene.objects if o.type == "MESH"]:  # close-ups show their subject alone
            o.hide_render = name != "lineup" and o.name != "Ground" and o not in subject
    px = 120 if name == "lineup" else CLOSE_PX
    # Exact scale: the view spans max(w, h) * 1.03 metres across the longer image side.
    scene.render.resolution_x = int(round(w * 1.03 * px))
    scene.render.resolution_y = int(round(h * 1.03 * px))
    camera(f"Cam_close_{name}", loc, rot, ortho_scale=max(w, h) * 1.03)
    scene.camera.data.sensor_fit = "HORIZONTAL" if w >= h else "VERTICAL"
    scene.render.filepath = str(out / f"closeup_{name}.png")
    bpy.ops.render.render(write_still=True)

# --- Kits: street-level views at 1.7 m eye height, and the RTS camera (about 150 m, 50 deg down).
if KIT and not SQUAD:
    scene.render.resolution_x = 960
    scene.render.resolution_y = 600
    ys = lo.y - 4.5
    xs = lo.x - 5.0
    shots = [
        ((xs + 1.0, ys, 1.7), (hi.x, ys + 6.0, 4.0)),
        ((hi.x + 4.0, ys, 1.7), (lo.x + 8.0, ys + 6.0, 4.0)),
        ((xs, ys + 1.0, 1.7), (xs + 6.0, hi.y, 4.0)),
        ((centre.x - 6.0, ys - 2.0, 1.7), (centre.x + 2.0, centre.y, 3.0)),
    ]
    for k, (eye, look) in enumerate(shots):
        d = Vector(look) - Vector(eye)
        camera("Cam_street", Vector(eye), d.to_track_quat("-Z", "Y").to_euler(), lens=24)
        scene.render.filepath = str(out / f"street_{k}.png")
        bpy.ops.render.render(write_still=True)
for o in scene.objects:
    if o.type == "MESH":
        o.hide_render = False  # restore everything after the isolated close-ups
if KIT:
    scene.render.resolution_x = 960
    scene.render.resolution_y = 600
    for k, az in enumerate((200, 300)):
        a = math.radians(az)
        dist = 60 if (SQUAD and k == 1) else 150  # squads: gameplay zoom and the closest tactical zoom
        eye = centre + Vector((dist * math.cos(math.radians(50)) * math.sin(a), -dist * math.cos(math.radians(50)) * math.cos(a), dist * math.sin(math.radians(50))))
        camera("Cam_rts", eye, (centre - eye).to_track_quat("-Z", "Y").to_euler(), lens=50)
        scene.render.filepath = str(out / f"rts_{k}.png")
        bpy.ops.render.render(write_still=True)

(out / "sheet_meta.json").write_text(json.dumps({
    "px_per_m": PX_PER_M,
    "bbox_min": list(lo), "bbox_max": list(hi),
    "engine_silhouette": "BLENDER_WORKBENCH flat", "engine_turntable": "BLENDER_EEVEE",
    "turntable": {"angles_deg": [45 * k for k in range(8)], "lens_mm": 50, "elevation": "radius * 0.55"},
}, indent=2))
print("SHEETS_OK", out)
