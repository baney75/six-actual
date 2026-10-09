"""Parametric building kit and town-block generator (section M3 route P). Run headless:

    tools/blender/blender --background --factory-startup --python assets_src/procedural/building_kit.py -- <id>

Reads assets_src/procedural/specs/<id>.py (SPEC dict). Buildings sit on the sim chunk grid: chunk width and
storey height come from data/rules.json (structure.chunk_m, structure.floor_dm), so the visual kit and the
SIM-10 collapse grid cannot drift apart. Every wall chunk is its own node (Building_<n>/Chunk_f<f>_x<i>_y<j>)
holding its outer wall faces with real openings, its inner faces and its floor slab, so a destroyed chunk
removes exactly what the sim removed and shows the interior. Roofs are split into one bay per chunk along the
ridge (Attic_<i> gable/attic volume, Roof_<i> skin, dormers and chimneys parented to the bay).

Local building frame: x along the front facade, y into the plot (front face at y = 0 faces -y), z up, metres.
"""
from __future__ import annotations

import importlib.util
import json
import math
import statistics
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from ground_vehicle import REPO, empty, material, reset, triangles  # noqa: E402

RULES = json.loads((REPO / "data" / "rules.json").read_text())["structure"]
C = float(RULES["chunk_m"])            # chunk width, m
H = RULES["floor_dm"] / 10.0           # storey height, m

PALETTE = {
    "plaster_white": (0.88, 0.87, 0.83), "plaster_cream": (0.88, 0.82, 0.62), "plaster_grey": (0.78, 0.78, 0.76),
    "plinth": (0.55, 0.55, 0.55), "timber": (0.24, 0.14, 0.09), "sandstone": (0.60, 0.36, 0.29),
    "rubble": (0.36, 0.34, 0.31), "tile": (0.68, 0.29, 0.17), "tile_dark": (0.33, 0.20, 0.15), "slate": (0.17, 0.18, 0.21),
    "glass": (0.07, 0.09, 0.11), "frame_white": (0.92, 0.92, 0.90), "frame_alu": (0.62, 0.63, 0.65),
    "shutter": (0.52, 0.53, 0.57), "wood": (0.32, 0.19, 0.11), "interior": (0.80, 0.74, 0.62), "slab": (0.50, 0.48, 0.45),
    "fascia": (0.20, 0.22, 0.25), "sign": (0.42, 0.25, 0.12), "iron": (0.12, 0.12, 0.12),
    "asphalt": (0.20, 0.20, 0.21), "pavement": (0.55, 0.54, 0.52), "cobble": (0.42, 0.39, 0.35), "grass": (0.25, 0.36, 0.18),
}
STYLE = {  # wall, upper wall, window frame, window size (w, h, sill), roof material
    "fachwerk": {"wall": "plaster_white", "upper": "plaster_white", "frame": "frame_white", "win": (0.85, 1.10, 0.85), "roof": "tile"},
    "plaster": {"wall": "plaster_cream", "upper": "plaster_cream", "frame": "frame_white", "win": (1.00, 1.30, 0.85), "roof": "tile"},
    "stone": {"wall": "rubble", "upper": "rubble", "frame": "wood", "win": (0.55, 0.70, 1.20), "roof": "slate"},
    "infill": {"wall": "plaster_grey", "upper": "plaster_white", "frame": "frame_alu", "win": (1.60, 1.35, 0.90), "roof": "tile_dark"},
}
_mats: dict[str, bpy.types.Material] = {}


def mat(name):
    if name not in _mats:
        _mats[name] = material(name, PALETTE[name])
    return _mats[name]


class Acc:
    """Accumulates boxes and prisms into one bmesh with per-face material slots."""

    def __init__(self):
        self.bm = bmesh.new()
        self.slots: list[str] = []

    def _slot(self, m):
        if m not in self.slots:
            self.slots.append(m)
        return self.slots.index(m)

    def box(self, centre, ax, ay, az, m):
        """Box with edge vectors ax, ay, az (full lengths) about centre."""
        M = Matrix.Identity(4)
        for col, v in enumerate((ax, ay, az)):
            for row in range(3):
                M[row][col] = v[row]
        M.translation = Vector(centre)
        verts = bmesh.ops.create_cube(self.bm, size=1.0, matrix=M)["verts"]
        i = self._slot(m)
        for f in {f for v in verts for f in v.link_faces}:
            f.material_index = i

    def abox(self, x0, y0, z0, x1, y1, z1, m):
        self.box(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), (x1 - x0, 0, 0), (0, y1 - y0, 0), (0, 0, z1 - z0), m)

    def prism(self, ring_a, ring_b, m):
        """Closed prism between two matching convex rings of 3D points."""
        a = [self.bm.verts.new(p) for p in ring_a]
        b = [self.bm.verts.new(p) for p in ring_b]
        i = self._slot(m)
        faces = [self.bm.faces.new(list(reversed(a))), self.bm.faces.new(b)]
        n = len(a)
        for k in range(n):
            faces.append(self.bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k])))
        for f in faces:
            f.material_index = i
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)

    def emit(self, name, parent):
        mesh = bpy.data.meshes.new(name)
        self.bm.normal_update()
        self.bm.to_mesh(mesh)
        self.bm.free()
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        obj.parent = parent
        for s in self.slots:
            obj.data.materials.append(mat(s))
        return obj


class Face:
    """A facade-local frame: u along the face (left to right seen from outside), v up, d inward from the face."""

    def __init__(self, origin, u, n):
        self.o, self.u, self.n = Vector(origin), Vector(u), Vector(n)

    def p(self, u, v, d):
        return self.o + self.u * u + Vector((0, 0, v)) - self.n * d

    def box(self, acc, u0, u1, v0, v1, d0, d1, m, angle=0.0):
        """Box spanning [u0,u1] x [v0,v1] x depth [d0,d1] (d negative = proud of the face)."""
        c = self.p((u0 + u1) / 2, (v0 + v1) / 2, (d0 + d1) / 2)
        z = Vector((0, 0, 1))
        if angle:
            ca, sa = math.cos(angle), math.sin(angle)
            acc.box(c, (self.u * ca + z * sa) * (u1 - u0), (-self.u * sa + z * ca) * (v1 - v0), self.n * (d1 - d0), m)
        else:
            acc.box(c, self.u * (u1 - u0), z * (v1 - v0), self.n * (d1 - d0), m)

    def poly(self, acc, pts_uv, d0, d1, m):
        """Prism of a convex face-plane polygon [(u, v)] between depths d0 and d1."""
        acc.prism([self.p(u, v, d0) for u, v in pts_uv], [self.p(u, v, d1) for u, v in pts_uv], m)

    def strut(self, acc, a, b, w, m, proud=0.03):
        """Timber member from face point a=(u, v) to b=(u, v), width w, standing proud of the face."""
        du, dv = b[0] - a[0], b[1] - a[1]
        length = math.hypot(du, dv)
        ang = math.atan2(dv, du)
        um, vm = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
        self.box(acc, um - length / 2, um + length / 2, vm - w / 2, vm + w / 2, -proud, 0.02, m, angle=ang)


def faces_for(L, D, side, i, z0, jetty=0.0):
    if side == "front":
        return Face((i * C, -jetty, z0), (1, 0, 0), (0, -1, 0))
    if side == "back":
        return Face(((i + 1) * C, D, z0), (-1, 0, 0), (0, 1, 0))
    if side == "left":
        return Face((0, (i + 1) * C, z0), (0, -1, 0), (-1, 0, 0))
    return Face((L, i * C, z0), (0, 1, 0), (1, 0, 0))


def openings(b, side, i, n_side, f):
    """Opening list [(kind, u0, u1, v0, v1)] for one chunk face, from the building's style and fields."""
    st = b["style"]
    w, h, sill = STYLE[st]["win"]
    mid = C / 2
    win = [("window", mid - w / 2, mid + w / 2, sill, sill + h)]
    if side in b.get("party", []):
        return []
    if side == "front" and [f, i] in [list(x) for x in b.get("balconies", [])]:
        return [("bdoor", mid - 0.50, mid + 0.50, 0.05, 2.25)]
    if side == "front" and f == 0:
        if i in b.get("gate", []):
            return [("arch", 0.30, 2.70, 0.0, 1.40)] if st == "stone" else [("barn", 0.25, 2.75, 0.0, 2.45)]
        if i in b.get("door", []):
            return [("door", mid - 0.55, mid + 0.55, 0.0, 2.15)]
        if b.get("shop"):
            return [("shop", 0.25, 2.75, 0.35, 2.45)]
    if st == "stone":
        return win if (i % 3 == 1 and side != "back") else []
    if side == "front":
        return win
    if side == "back":
        return win if (f > 0 or i % 2 == 0) else []
    if side in b.get("street", []):
        if f == 0 and b.get("shop"):
            return [("shop", 0.25, 2.75, 0.35, 2.45)]
        return win
    return win if i % 2 == 1 or n_side == 1 else []


def wall_panel(acc, face, b, f, ops, t):
    """One chunk face: wall boxes around the openings, plus frames, glass, doors, shutters and framing."""
    st = b["style"]
    s = STYLE[st]
    wallm = s["upper"] if f > 0 else s["wall"]
    ops_sorted = sorted(ops, key=lambda o: o[1])
    # Wall solid split around the (single) opening per chunk face.
    if not ops_sorted:
        face.box(acc, 0, C, 0, H, 0, t, wallm)
    for kind, u0, u1, v0, v1 in ops_sorted:
        face.box(acc, 0, u0, 0, H, 0, t, wallm)
        face.box(acc, u1, C, 0, H, 0, t, wallm)
        if v0 > 0:
            face.box(acc, u0, u1, 0, v0, 0, t, wallm)
        if kind == "arch":
            # Semicircular arch: slices above the springing line, with a sandstone voussoir band.
            r = (u1 - u0) / 2
            n = 24
            for k in range(n):
                a0 = u0 + (u1 - u0) * k / n
                a1 = u0 + (u1 - u0) * (k + 1) / n
                t0 = v1 + math.sqrt(max(r * r - (a0 - u0 - r) ** 2, 0.0))
                t1 = v1 + math.sqrt(max(r * r - (a1 - u0 - r) ** 2, 0.0))
                face.poly(acc, [(a0, t0), (a1, t1), (a1, min(t1 + 0.35, H)), (a0, min(t0 + 0.35, H))], -0.02, t, "sandstone")
                if max(t0, t1) + 0.35 < H:
                    face.poly(acc, [(a0, t0 + 0.35), (a1, t1 + 0.35), (a1, H), (a0, H)], 0, t, wallm)
            face.box(acc, u0 - 0.35, u0, 0, v1 + 0.1, -0.02, t, "sandstone")
            face.box(acc, u1, u1 + 0.35, 0, v1 + 0.1, -0.02, t, "sandstone")
            face.box(acc, u0, u1, 0, v1 + r * 0.95, t * 0.4, t * 0.4 + 0.08, "wood")
            continue
        face.box(acc, u0, u1, v1, H, 0, t, wallm)
        if kind in ("door", "barn"):
            face.box(acc, u0, u1, v0, v1, t * 0.35, t * 0.35 + 0.06, "wood")
            if st in ("plaster", "fachwerk"):
                face.box(acc, u0 - 0.12, u0, v0, v1 + 0.12, -0.03, 0.02, "sandstone")
                face.box(acc, u1, u1 + 0.12, v0, v1 + 0.12, -0.03, 0.02, "sandstone")
                face.box(acc, u0, u1, v1, v1 + 0.12, -0.03, 0.02, "sandstone")
            continue
        inset = 0.10 if kind != "shop" else 0.06
        face.box(acc, u0, u1, v0, v1, inset, inset + 0.03, "glass")
        frame = s["frame"]
        fw = 0.05
        face.box(acc, u0, u1, v0 + (v1 - v0) * 0.62, v0 + (v1 - v0) * 0.62 + fw, inset - 0.02, inset, frame)
        face.box(acc, (u0 + u1) / 2 - fw / 2, (u0 + u1) / 2 + fw / 2, v0, v1, inset - 0.02, inset, frame)
        if kind == "shop":
            face.box(acc, 0.05, C - 0.05, v1 + 0.05, v1 + 0.40, -0.06, 0.0, "fascia")
            continue
        if st in ("plaster", "stone"):
            # Sandstone surround and sill.
            sw = 0.12
            face.box(acc, u0 - sw, u0, v0, v1, -0.03, 0.02, "sandstone")
            face.box(acc, u1, u1 + sw, v0, v1, -0.03, 0.02, "sandstone")
            face.box(acc, u0 - sw, u1 + sw, v1, v1 + sw, -0.03, 0.02, "sandstone")
            face.box(acc, u0 - sw, u1 + sw, v0 - 0.08, v0, -0.07, 0.02, "sandstone")
        if b.get("shutters") and kind == "window" and st != "fachwerk":
            hw = (u1 - u0) / 2
            face.box(acc, u0 - 0.12 - hw, u0 - 0.12, v0, v1, -0.06, -0.02, "shutter")
            face.box(acc, u1 + 0.12, u1 + 0.12 + hw, v0, v1, -0.06, -0.02, "shutter")
    if st == "plaster" and f == 0:
        for kind, u0, u1, v0, v1 in ops_sorted or [("none", C, C, 0, 0)]:
            for a0, a1 in ((0.0, u0 - 0.12), (u1 + 0.12, C)) if kind in ("door", "barn") else ((0.0, C),):
                if a1 > a0:
                    face.box(acc, a0, a1, 0, 0.5, -0.02, 0.0, "plinth")
    if st == "fachwerk" and f == 0:
        # Sandstone plinth under the plastered ground floor.
        for kind, u0, u1, v0, v1 in ops_sorted or [("none", C, C, 0, 0)]:
            for a0, a1 in ((0.0, u0), (u1, C)) if kind in ("door", "barn") else ((0.0, C),):
                if a1 > a0:
                    face.box(acc, a0, a1, 0, 0.70, -0.04, 0.0, "sandstone")
    if st == "fachwerk" and (f > 0 or b.get("fachwerk_gf")):
        fachwerk(acc, face, ops_sorted)
    if st == "stone" and b.get("quoins"):
        pass


def fachwerk(acc, face, ops):
    """Half-timbering on one chunk face: sill, head, corner posts, window posts and rails, braces."""
    tw = 0.17
    face.strut(acc, (0, tw / 2), (C, tw / 2), tw, "timber")
    face.strut(acc, (0, H - tw / 2), (C, H - tw / 2), tw, "timber")
    face.strut(acc, (tw / 2, tw), (tw / 2, H - tw), tw, "timber")
    face.strut(acc, (C - tw / 2, tw), (C - tw / 2, H - tw), tw, "timber")
    if ops:
        _, u0, u1, v0, v1 = ops[0]
        face.strut(acc, (u0 - tw / 2, tw), (u0 - tw / 2, H - tw), tw, "timber")
        face.strut(acc, (u1 + tw / 2, tw), (u1 + tw / 2, H - tw), tw, "timber")
        face.strut(acc, (u0 - tw, v0 - tw / 2), (u1 + tw, v0 - tw / 2), tw * 0.8, "timber")
        face.strut(acc, (u0 - tw, v1 + tw / 2), (u1 + tw, v1 + tw / 2), tw * 0.8, "timber")
        # Short braces in the side panels below the sill rail.
        face.strut(acc, (tw, tw), (u0 - tw, v0 - tw), tw * 0.8, "timber")
        face.strut(acc, (C - tw, tw), (u1 + tw, v0 - tw), tw * 0.8, "timber")
    else:
        # "Mann" figure: centre post with two braces.
        face.strut(acc, (C / 2, tw), (C / 2, H - tw), tw, "timber")
        face.strut(acc, (tw, tw), (C / 2 - tw / 2, H * 0.62), tw, "timber")
        face.strut(acc, (C - tw, tw), (C / 2 + tw / 2, H * 0.62), tw, "timber")
        face.strut(acc, (tw, H * 0.5), (C - tw, H * 0.5), tw * 0.8, "timber")


def roof_chain(b, D):
    """Upper profile chain [(s, z)] across the plot (s from 0 to D), z above the eave."""
    r = b["roof"]
    kind = r["type"]
    if kind == "flat":
        return None
    if kind == "mansard":
        bh = 2.4
        a = bh / math.tan(math.radians(r.get("lower", 70)))
        top = bh + (D / 2 - a) * math.tan(math.radians(r.get("pitch", 30)))
        return [(0.0, 0.0), (a, bh), (D / 2, top), (D - a, bh), (D, 0.0)]
    p = math.radians(r.get("pitch", 45))
    return [(0.0, 0.0), (D / 2, D / 2 * math.tan(p)), (D, 0.0)]


def build_building(b, root):
    nx, ny, floors = b["nx"], b["ny"], b["floors"]
    L, D = nx * C, ny * C
    bo = empty(f"Building_{b['name']}", root, (b["at"][0], b["at"][1], 0.0))
    bo.rotation_euler = (0, 0, math.radians(b.get("rot", 0)))
    st = STYLE[b["style"]]
    t = 0.5 if b["style"] == "stone" else 0.3
    sides = {"front": nx, "back": nx, "left": ny, "right": ny}
    for f in range(floors):
        for ix in range(nx):
            for iy in range(ny):
                acc = Acc()
                z0 = f * H
                jet = b.get("jetty", 0.0) if f > 0 else 0.0
                acc.abox(ix * C + 0.01, iy * C + 0.01 - (jet if iy == 0 else 0.0), z0, (ix + 1) * C - 0.01, (iy + 1) * C - 0.01, z0 + 0.22, "slab")
                if [f, ix] in [list(x) for x in b.get("balconies", [])] and iy == 0:
                    # Cantilevered concrete balcony with a railing, 1.1 m deep.
                    acc.abox(ix * C + 0.15, -1.10, z0 - 0.02, (ix + 1) * C - 0.15, 0.0, z0 + 0.16, "plaster_grey")
                    acc.abox(ix * C + 0.15, -1.10, z0 + 0.16, (ix + 1) * C - 0.15, -1.05, z0 + 1.05, "fascia")
                    acc.abox(ix * C + 0.15, -1.10, z0 + 0.16, ix * C + 0.20, 0.0, z0 + 1.05, "fascia")
                    acc.abox((ix + 1) * C - 0.20, -1.10, z0 + 0.16, (ix + 1) * C - 0.15, 0.0, z0 + 1.05, "fascia")
                for side, i in (("front", ix if iy == 0 else None), ("back", nx - 1 - ix if iy == ny - 1 else None),
                                ("left", ny - 1 - iy if ix == 0 else None), ("right", iy if ix == nx - 1 else None)):
                    if i is None:
                        continue
                    face = faces_for(L, D, side, i, z0, jet if side == "front" else 0.0)
                    wall_panel(acc, face, b, f, openings(b, side, i, sides[side], f), t)
                    # Interior finish: a thin plaster skin on the inner face shows when the chunk is opened.
                    face.box(acc, 0.0, C, 0.22, H, t, t + 0.015, "interior")
                acc.emit(f"Chunk_f{f}_x{ix}_y{iy}", bo)
    eave = floors * H
    for side, nside in (("front", nx), ("back", nx), ("left", ny), ("right", ny)):
        pass
    chain = roof_chain(b, L if b["roof"].get("ridge", "x") == "y" else D)
    pitches = []
    if chain is None:
        # Flat roof: parapet bays.
        for ix in range(nx):
            acc = Acc()
            acc.abox(ix * C, 0, eave, (ix + 1) * C, D, eave + 0.25, "fascia")
            acc.abox(ix * C, 0, eave, (ix + 1) * C, 0.25, eave + 0.9, st["upper"])
            acc.abox(ix * C, D - 0.25, eave, (ix + 1) * C, D, eave + 0.9, st["upper"])
            acc.emit(f"Roof_{ix}", bo)
        return bo, pitches
    ridge_y = b["roof"].get("ridge", "x") == "y"
    n_along = ny if ridge_y else nx
    across = L if ridge_y else D

    def P(a, s, z):
        return (s, a, eave + z) if ridge_y else (a, s, eave + z)

    for k in range(len(chain) - 1):
        (s1, z1), (s2, z2) = chain[k], chain[k + 1]
        pitches.append(round(math.degrees(math.atan2(abs(z2 - z1), abs(s2 - s1))), 1))
    gable_m = st["upper"]
    roofm = "slate" if b.get("slate_roof") else st["roof"]
    jet_top = b.get("jetty", 0.0) if floors > 1 else 0.0
    for j in range(n_along):
        a0, a1 = j * C, (j + 1) * C
        if ridge_y and j == 0:
            a0 -= jet_top
        att = Acc()
        att.prism([P(a0, s, z) for s, z in chain], [P(a1, s, z) for s, z in chain], gable_m)
        ab = att.emit(f"Attic_{j}", bo)
        acc = Acc()
        v0 = a0 - (0.35 if j == 0 else 0.0)
        v1 = a1 + (0.35 if j == n_along - 1 else 0.0)
        th = 0.18
        for k in range(len(chain) - 1):
            (s1, z1), (s2, z2) = chain[k], chain[k + 1]
            ds, dz = s2 - s1, z2 - z1
            ln = math.hypot(ds, dz)
            es, ez = ds / ln, dz / ln
            ns, nz = (-ez, es) if es > 0 else (ez, -es)
            if nz < 0:
                ns, nz = -ns, -nz
            ov = 0.5
            if z1 == 0.0:
                s1, z1 = s1 - es * ov, z1 - ez * ov
            if z2 == 0.0:
                s2, z2 = s2 + es * ov, z2 + ez * ov
            quad = [(s1, z1), (s2, z2), (s2 + ns * th, z2 + nz * th), (s1 + ns * th, z1 + nz * th)]
            acc.prism([P(v0, s, z) for s, z in quad], [P(v1, s, z) for s, z in quad], roofm)
        # Ridge cap.
        sr, zr = max(chain, key=lambda q: q[1])
        cap = [(sr - 0.15, zr + 0.12), (sr + 0.15, zr + 0.12), (sr + 0.15, zr + 0.30), (sr - 0.15, zr + 0.30)]
        acc.prism([P(v0, s, z) for s, z in cap], [P(v1, s, z) for s, z in cap], roofm)
        if j in b.get("chimney", []):
            x, y, z = P((a0 + a1) / 2, sr + 0.6, zr - 0.3)
            acc.abox(x - 0.28, y - 0.28, z, x + 0.28, y + 0.28, z + 1.5, "plinth" if b["style"] == "infill" else "sandstone")
        if j in b.get("dormers", []) and 0 < j < n_along - 1:
            dormer(acc, chain, P, (a0 + a1) / 2, roofm, st)
        if j == 0 and b.get("gable_finish"):
            gable_finish(acc, chain, P, a0, b["gable_finish"])
        acc.emit(f"Roof_{j}", ab)
    # Gable windows on the gable ends.
    for end in (0, 1):
        if not b.get("gable_windows"):
            continue
        acc = Acc()
        a = (-jet_top if ridge_y else 0.0) if end == 0 else n_along * C
        sgn = -1 if end == 0 else 1
        zmax = max(z for _, z in chain)
        n = max(1, round(across / C) - 1)
        for w in range(n):
            s = across * (w + 1) / (n + 1)
            if zmax < 2.2:
                break
            for (u0, u1, v0, v1, dd, m) in ((-0.40, 0.40, 0.6, 1.6, 0.01, "glass"), (-0.47, 0.47, 0.53, 1.67, 0.0, STYLE[b["style"]]["frame"])):
                pa = Vector(P(a + sgn * (0.02 + dd), s + u0, v0))
                pb = Vector(P(a + sgn * (0.06 + dd), s + u1, v1))
                acc.abox(min(pa.x, pb.x), min(pa.y, pb.y), min(pa.z, pb.z), max(pa.x, pb.x), max(pa.y, pb.y), max(pa.z, pb.z), m)
        if acc.bm.verts:
            acc.emit(f"GableWindows_{end}", bo)
        else:
            acc.bm.free()
    return bo, pitches


def gable_finish(acc, chain, P, a, kind):
    """Front gable at along-position a: 'slate' hangs it in slate, 'fachwerk' frames it in timber."""
    zmax = max(z for _, z in chain)
    s_lo, s_hi = chain[0][0], chain[-1][0]

    def zline(s):
        for (s1, z1), (s2, z2) in zip(chain, chain[1:]):
            if s1 <= s <= s2:
                return z1 + (z2 - z1) * (s - s1) / max(s2 - s1, 1e-6)
        return 0.0

    if kind == "slate":
        ring_a = [P(a - 0.02, s, z) for s, z in chain]
        ring_b = [P(a - 0.07, s, z) for s, z in chain]
        acc.prism(ring_a, ring_b, "slate")
        return
    tw = 0.16
    n = 6
    for k in range(1, n):
        s = s_lo + (s_hi - s_lo) * k / n
        top = zline(s) - 0.1
        if top > 0.3:
            pa, pb = Vector(P(a - 0.05, s - tw / 2, 0.0)), Vector(P(a - 0.01, s + tw / 2, top))
            acc.abox(min(pa.x, pb.x), min(pa.y, pb.y), pa.z, max(pa.x, pb.x), max(pa.y, pb.y), pb.z, "timber")
    for zf in (0.0, zmax * 0.45):
        w = [s for s in (s_lo + (s_hi - s_lo) * t / 100 for t in range(101)) if zline(s) >= zf + 0.2]
        if w:
            pa, pb = Vector(P(a - 0.05, w[0], zf)), Vector(P(a - 0.01, w[-1], zf + tw))
            acc.abox(min(pa.x, pb.x), min(pa.y, pb.y), pa.z, max(pa.x, pb.x), max(pa.y, pb.y), pb.z, "timber")


def dormer(acc, chain, P, a, roofm, st):
    """Gable dormer on the front slope: cheeks, window and a small gable roof."""
    (s1, z1), (s2, z2) = chain[0], chain[1]
    sd = s1 + (s2 - s1) * 0.30
    zd = z1 + (z2 - z1) * 0.30
    w, h, depth = 1.3, 1.35, 2.2
    lo = Vector(P(a - w / 2, sd, zd - 0.2))
    hi = Vector(P(a + w / 2, sd + depth, zd + h))
    acc.abox(min(lo.x, hi.x), min(lo.y, hi.y), lo.z, max(lo.x, hi.x), max(lo.y, hi.y), hi.z, st["upper"] if st["roof"] != "slate" else "slate")
    g0 = Vector(P(a - 0.4, sd - 0.03, zd + 0.1))
    g1 = Vector(P(a + 0.4, sd + 0.02, zd + 1.15))
    acc.abox(min(g0.x, g1.x), min(g0.y, g1.y), g0.z, max(g0.x, g1.x), max(g0.y, g1.y), g1.z, "glass")
    ring_a = [P(a - w / 2 - 0.15, sd - 0.2, zd + h), P(a + w / 2 + 0.15, sd - 0.2, zd + h), P(a, sd - 0.2, zd + h + 0.75)]
    ring_b = [P(a - w / 2 - 0.15, sd + depth, zd + h), P(a + w / 2 + 0.15, sd + depth, zd + h), P(a, sd + depth, zd + h + 0.75)]
    acc.prism(ring_a, ring_b, roofm)


def build_gateway(g, root):
    """Hofreite gateway (Hoftor): sandstone wall with a semicircular arch, wooden gates and a tiled coping."""
    go = empty(f"Gateway_{g['name']}", root, (g["at"][0], g["at"][1], 0.0))
    go.rotation_euler = (0, 0, math.radians(g.get("rot", 0)))
    width, height, t = g["width"], g.get("height", 4.2), 0.6
    face = Face((0, 0, 0), (1, 0, 0), (0, -1, 0))
    acc = Acc()
    aw = g.get("arch", 3.2)
    u0, u1 = (width - aw) / 2, (width + aw) / 2
    spring = g.get("spring", 2.3)
    face.box(acc, 0, u0, 0, height, 0, t, "sandstone")
    face.box(acc, u1, width, 0, height, 0, t, "sandstone")
    r = aw / 2
    for k in range(28):
        a0, a1 = u0 + aw * k / 28, u0 + aw * (k + 1) / 28
        t0 = spring + math.sqrt(max(r * r - (a0 - u0 - r) ** 2, 0.0))
        t1 = spring + math.sqrt(max(r * r - (a1 - u0 - r) ** 2, 0.0))
        face.poly(acc, [(a0, t0), (a1, t1), (a1, height), (a0, height)], 0, t, "sandstone")
    face.box(acc, u0, u1, 0, spring + r * 0.97, t * 0.5, t * 0.5 + 0.08, "wood")
    ring = [(-0.15, 0.0), (t + 0.15, 0.0), (t / 2, 0.45)]
    acc.prism([(-0.1, d, height + z) for d, z in ring], [(width + 0.1, d, height + z) for d, z in ring], "tile")
    acc.emit("Gate", go)
    return go


def build_wall(wl, root, k):
    acc = Acc()
    (x0, y0), (x1, y1) = wl["from"], wl["to"]
    t, h = wl.get("t", 0.4), wl.get("h", 1.8)
    if abs(x1 - x0) >= abs(y1 - y0):
        acc.abox(min(x0, x1), y0 - t / 2, 0, max(x0, x1), y0 + t / 2, h, wl.get("mat", "sandstone"))
    else:
        acc.abox(x0 - t / 2, min(y0, y1), 0, x0 + t / 2, max(y0, y1), h, wl.get("mat", "sandstone"))
    acc.emit(f"Wall_{k}", root)


def build_ground(spec, root):
    acc = Acc()
    for g in spec.get("ground", []):
        x0, y0, x1, y1 = g["rect"]
        acc.abox(x0, y0, -0.05 + g.get("z", 0.0) - 0.05, x1, y1, g.get("z", 0.0), g["mat"])
    acc.emit("Ground", root)


def build(spec):
    reset()
    _mats.clear()
    root = empty("Root")
    build_ground(spec, root)
    footprints, pitches = [], []
    for b in spec["buildings"]:
        _, p = build_building(b, root)
        footprints.append(sorted((b["nx"] * C, b["ny"] * C)))
        pitches += p
    for g in spec.get("gateways", []):
        build_gateway(g, root)
    for k, wl in enumerate(spec.get("walls", [])):
        build_wall(wl, root, k)
    for s in spec.get("signs", []):
        acc = Acc()
        x, y, z = s["at"]
        acc.abox(x - 0.03, y - 0.9, z + 0.95, x + 0.03, y, z + 1.0, "iron")
        acc.abox(x - 0.04, y - 0.85, z, x + 0.04, y - 0.15, z + 0.9, "sign")
        acc.emit("Sign_Gasthaus", root)
    return footprints, pitches


def measure(footprints, pitches):
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for o in meshes:
        if o.name == "Ground":
            continue
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            lo = Vector(map(min, lo, w))
            hi = Vector(map(max, hi, w))
    steep = [p for p in pitches if p >= 30]
    return {
        "triangles_lod0": sum(triangles(o) for o in meshes),
        "bbox_min": [round(v, 2) for v in lo], "bbox_max": [round(v, 2) for v in hi],
        "chunk_m": C, "storey_height_m": H,
        "roof_pitches_deg": sorted(pitches),
        "roof_pitch_median_deg": statistics.median(steep) if steep else None,
        "footprints_m": footprints,
        "footprint_short_median_m": statistics.median(f[0] for f in footprints),
        "footprint_long_median_m": statistics.median(f[1] for f in footprints),
        "chunks": sum(1 for o in meshes if o.name.startswith("Chunk_")),
        "nodes": sorted({o.name.split(".")[0] for o in bpy.data.objects if not o.name.startswith(("Chunk_", "Attic_", "Roof_"))}),
    }


def main():
    vid = sys.argv[sys.argv.index("--") + 1]
    ms = importlib.util.spec_from_file_location(f"spec_{vid}", REPO / "assets_src" / "procedural" / "specs" / f"{vid}.py")
    mod = importlib.util.module_from_spec(ms)
    ms.loader.exec_module(mod)
    footprints, pitches = build(mod.SPEC)
    bpy.context.view_layer.update()
    work = REPO / "assets_src" / "work" / vid
    out = REPO / "assets_src" / "exports" / vid
    work.mkdir(parents=True, exist_ok=True)
    out.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(work / f"{vid}.blend"))
    bpy.ops.export_scene.gltf(filepath=str(out / f"{vid}_lod0.glb"), export_format="GLB", export_yup=True, export_apply=True)
    info = measure(footprints, pitches)
    info["id"] = vid
    (out / f"{vid}_build.json").write_text(json.dumps(info, indent=2) + "\n")
    print("BUILD", json.dumps({k: v for k, v in info.items() if k not in ("nodes", "footprints_m", "roof_pitches_deg")}))


if __name__ == "__main__":
    main()
