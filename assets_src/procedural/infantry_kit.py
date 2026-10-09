"""Parametric infantry generator (section M3 route P): base soldier plus era equipment kit. Run headless:

    tools/blender/blender --background --factory-startup --python assets_src/procedural/infantry_kit.py -- <id>

Reads assets_src/procedural/specs/<id>.py (SPEC dict). The base soldier is proportioned from ANSUR I (1988) US
Army male means (assets_src/research/anthro/ansur1988_men_means.json). Blockout stage: rigid body segments are
lofted ellipse rings placed by forward kinematics, with analytic two-bone IK for arms (to the weapon grips) and
legs (to foot targets); the rig stage replaces this with a skinned mesh on an armature with the same joints.

Soldier frame: faces +Y, right hand on +X, ground at z = 0, metres. Each soldier is an empty
Soldier_<nn>_<role> holding its body segments, gear and weapon (Weapon_<type>, Muzzle_0 empty).
"""
from __future__ import annotations

import importlib.util
import json
import math
import random
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from ground_vehicle import REPO, empty, material, reset, triangles  # noqa: E402

A = {k: v / 1000.0 for k, v in json.loads((REPO / "assets_src" / "research" / "anthro" / "ansur1988_men_means.json").read_text())["means_mm"].items()}
TAU = 2 * math.pi
EASE = 0.012  # BDU fabric ease over the body, m

PALETTE = {
    "bdu": (0.30, 0.32, 0.22), "vest": (0.27, 0.29, 0.20), "helmet": (0.29, 0.30, 0.21), "web": (0.19, 0.20, 0.13),
    "boot": (0.04, 0.04, 0.04), "weapon": (0.06, 0.06, 0.06), "radio": (0.22, 0.24, 0.17), "foliage": (0.20, 0.30, 0.12),
    "skin_a": (0.72, 0.55, 0.43), "skin_b": (0.45, 0.31, 0.22), "skin_c": (0.62, 0.46, 0.34), "band": (0.10, 0.10, 0.08),
    "law": (0.30, 0.33, 0.20), "ground": (0.33, 0.36, 0.24),
}
_mats: dict[str, bpy.types.Material] = {}


def mat(name):
    if name not in _mats:
        _mats[name] = material(name, PALETTE[name])
    return _mats[name]


# ---------------------------------------------------------------- geometry helpers

def frame(a, b, fwd):
    """4x4 matrix at a with +Z along (b - a) and +Y toward fwd."""
    a, b, fwd = Vector(a), Vector(b), Vector(fwd)
    z = (b - a).normalized()
    y = fwd - z * fwd.dot(z)
    if y.length < 1e-6:
        y = Vector((0, 1, 0)) - z * z.y
    y.normalize()
    x = y.cross(z)
    m = Matrix((x, y, z)).transposed().to_4x4()
    m.translation = a
    return m


def rings_mesh(rings, segs=12, caps=True):
    """rings: [(z, half_x, half_y, cx, cy)] along local +Z; ellipse loft, capped unless caps=False."""
    bm = bmesh.new()
    vs = []
    for z, hx, hy, cx, cy in rings:
        vs.append([bm.verts.new((cx + hx * math.cos(TAU * k / segs), cy + hy * math.sin(TAU * k / segs), z)) for k in range(segs)])
    if caps:
        bm.faces.new(list(reversed(vs[0])))
        bm.faces.new(vs[-1])
    for r0, r1 in zip(vs, vs[1:]):
        for k in range(segs):
            j = (k + 1) % segs
            bm.faces.new((r0[k], r0[j], r1[j], r1[k]))
    return bm


def box_mesh(sx, sy, sz, offset=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Translation(offset) @ Matrix.Diagonal((sx, sy, sz, 1)))
    return bm


def cyl_mesh(r, length, segs=10, axis="y", offset=(0, 0, 0)):
    """Cylinder of radius r along +axis from 0 to length, then offset."""
    bm = bmesh.new()
    rot = {"y": Matrix.Rotation(-math.pi / 2, 4, "X"), "z": Matrix.Identity(4), "x": Matrix.Rotation(math.pi / 2, 4, "Y")}[axis]
    m = Matrix.Translation(offset) @ rot @ Matrix.Translation((0, 0, length / 2))
    bmesh.ops.create_cone(bm, cap_ends=True, segments=segs, radius1=r, radius2=r, depth=length, matrix=m)
    return bm


def prism_x(pts_yz, width):
    """Convex prism of a (y, z) polygon, width along x, centred on x = 0."""
    bm = bmesh.new()
    a = [bm.verts.new((-width / 2, y, z)) for y, z in pts_yz]
    b = [bm.verts.new((width / 2, y, z)) for y, z in pts_yz]
    bm.faces.new(list(reversed(a)))
    bm.faces.new(b)
    n = len(a)
    for k in range(n):
        bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def seg_box(a, b, w, t):
    """Thin strap box from point a to point b (local frame), width w (along x), thickness t."""
    a, b = Vector(a), Vector(b)
    d = b - a
    m = frame(a, b, Vector((0, 1, 0)) if abs(d.normalized().y) < 0.9 else Vector((0, 0, 1)))
    bm = box_mesh(w, t, d.length, offset=(0, 0, d.length / 2))
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    return bm


def merge(*bms):
    out = bmesh.new()
    for b in bms:
        tmp = bpy.data.meshes.new("tmp")
        b.to_mesh(tmp)
        b.free()
        out.from_mesh(tmp)
        bpy.data.meshes.remove(tmp)
    return out


def obj(name, bm, parent, matrix, m):
    mesh = bpy.data.meshes.new(name)
    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()
    o = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(o)
    o.parent = parent
    o.matrix_parent_inverse = Matrix.Identity(4)
    o.matrix_basis = matrix
    o.data.materials.append(mat(m))
    return o


def two_bone(a, t, l1, l2, hint):
    """Joint position for a two-bone chain from a toward target t; hint bends the joint."""
    a, t, hint = Vector(a), Vector(t), Vector(hint)
    d = t - a
    dist = min(max(d.length, 1e-4), (l1 + l2) * 0.999)
    dirv = d.normalized()
    cos_a = (l1 * l1 + dist * dist - l2 * l2) / (2 * l1 * dist)
    cos_a = max(-1.0, min(1.0, cos_a))
    p = hint - dirv * hint.dot(dirv)
    p = p.normalized() if p.length > 1e-6 else Vector((0, 0, -1))
    return a + dirv * l1 * cos_a + p * l1 * math.sqrt(1 - cos_a * cos_a), a + dirv * dist


# ---------------------------------------------------------------- body (ANSUR I 1988 male means)

STAT = A["STATURE"]
HIP_Z = A["TROCHANTERION_HT"] - 0.008          # hip joint centre ~ trochanterion
KNEE_Z = A["LATERAL_FEMORAL_EPICONDYLE_HT"]
ANKLE_Z = A["LATERAL-MALLEOUS_HT"] + 0.010
THIGH = HIP_Z - KNEE_Z
SHIN = KNEE_Z - ANKLE_Z
UPPER_ARM = A["SHOULDER_ELBOW_LNTH"] - 0.035   # acromion-olecranon to joint centres
FOREARM = A["RADIALE-STYLION_LNTH"]
HAND = A["HAND_LNTH"]
PELVIS_Z = HIP_Z + 0.03
WAIST_UP = A["WAIST_HT_NATURAL"] - PELVIS_Z     # pelvis -> waist joint
SHOULDER_UP = A["ACROMION_HT"] - 0.03 - A["WAIST_HT_NATURAL"]
NECK_UP = A["CERVIC_HT"] - A["WAIST_HT_NATURAL"]
SHOULDER_X = A["BIACROMIAL_BRTH"] / 2 - 0.025
HIP_X = A["HIP_BRTH"] / 4
HEAD = (A["HEAD_BRTH"] / 2, A["HEAD_LNTH"] / 2, (STAT - A["CERVIC_HT"]) / 2 + 0.01)


def r_of(circ):
    return circ / TAU


def build_body(root, P, U, H, legs, arms, skin, prone=False):
    """P, U, H: pelvis, upper torso and head frames (4x4). legs/arms: lists of (a, joint, end, fwd) per side."""
    hb, bd = A["HIP_BRTH"] / 2 + EASE, A["BUTT_DEPTH"] / 2 + EASE
    wb, wd = A["WAIST_BRTH_OMPHALION"] / 2 + EASE, A["WAIST_DEPTH-OMPHALION"] / 2 + EASE
    cb, cd = A["CHEST_BRTH"] / 2 + EASE, A["CHEST_DEPTH"] / 2 + EASE
    lower = [(-0.12, hb * 0.80, bd * 0.85, 0, 0), (-0.03, hb, bd, 0, -0.01), (WAIST_UP * 0.6, wb + 0.005, wd, 0, 0), (WAIST_UP + 0.02, wb, wd, 0, 0.005)]
    obj("Torso_Lower", rings_mesh(lower), root, P, "bdu")
    upper = [(-0.02, wb, wd, 0, 0.005), (A["CHEST_HT"] - A["WAIST_HT_NATURAL"], cb, cd, 0, 0.01),
             (SHOULDER_UP - 0.01, A["BIACROMIAL_BRTH"] / 2 + 0.01, cd * 0.85, 0, -0.005),
             (SHOULDER_UP + 0.06, 0.11, 0.075, 0, -0.01), (NECK_UP, r_of(A["NECK_CIRC-BASE"]), r_of(A["NECK_CIRC-BASE"]), 0, -0.01)]
    obj("Torso_Upper", rings_mesh(upper), root, U, "bdu")
    neck = [(NECK_UP - 0.02, 0.06, 0.06, 0, 0.0), (NECK_UP + 0.10, 0.055, 0.055, 0, 0.01)]
    obj("Neck", rings_mesh(neck, 10), root, U, skin)
    hx, hy, hz = HEAD
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=1.0, matrix=Matrix.Translation((0, 0.012, hz)) @ Matrix.Diagonal((hx, hy, hz, 1)))
    obj("Head", bm, root, H, skin)
    for side, (a, j, e, f, fu) in zip("RL", legs):
        th = [(0.0, r_of(A["THIGH_CIRC-PROXIMAL"]) + EASE, r_of(A["THIGH_CIRC-PROXIMAL"]) + EASE, 0, 0),
              ((j - a).length, r_of(A["KNEE_CIRC"]) + EASE, r_of(A["KNEE_CIRC"]) + EASE, 0, 0)]
        obj(f"Thigh_{side}", rings_mesh(th, 10), root, frame(a, j, f), "bdu")
        sl = (e - j).length
        rc = r_of(A["CALF_CIRC"]) + EASE
        hem = sl - 0.17   # trousers bloused over the boot top, ~0.24 m above the ground
        sh = [(0.0, r_of(A["KNEE_CIRC"]) + EASE, r_of(A["KNEE_CIRC"]) + EASE, 0, 0),
              (sl * 0.35, rc, rc, 0, 0),
              (hem - 0.04, 0.066, 0.066, 0, 0),
              (hem - 0.015, 0.074, 0.074, 0, 0),   # blousing roll
              (hem, 0.062, 0.062, 0, 0)]
        obj(f"Shin_{side}", rings_mesh(sh, 10), root, frame(j, e, f), "bdu")
        # Boot: shaft up the shin to 0.24 m above the ankle; the foot box follows the pose's foot frame
        # (toe direction f, sole normal fu), so standing feet sit flat and kneeling or prone toes dig in.
        shin_up = (j - e).normalized()
        shaft = [(0.0, 0.054, 0.056, 0, 0), (0.30, 0.060, 0.060, 0, 0)]
        obj(f"BootShaft_{side}", rings_mesh(shaft, 10), root, frame(e - shin_up * 0.06, e + shin_up * 0.24, f), "boot")
        fl, fb = A["FOOT_LNTH"] + 0.02, A["FOOT_BRTH"] + 0.008
        foot = box_mesh(fb, fl, 0.10, offset=(0, fl / 2 - 0.07, -0.025))
        obj(f"Boot_{side}", foot, root, frame(e, e + fu, f), "boot")
    for side, (a, j, e, f) in zip("RL", arms):
        ua = [(0.0, r_of(A["ARM_CIRC-AXILLARY"]) + EASE, r_of(A["ARM_CIRC-AXILLARY"]) + EASE, 0, 0),
              ((j - a).length, r_of(A["ELBOW_CIRC-EXTENDED"]) + EASE, r_of(A["ELBOW_CIRC-EXTENDED"]) + EASE, 0, 0)]
        obj(f"UpperArm_{side}", rings_mesh(ua, 10), root, frame(a, j, f), "bdu")
        fa = [(0.0, r_of(A["FOREARM_CIRC-FLEXED"]) + EASE, r_of(A["FOREARM_CIRC-FLEXED"]) + EASE, 0, 0),
              ((e - j).length * 0.92, 0.042, 0.042, 0, 0)]
        obj(f"Forearm_{side}", rings_mesh(fa, 10), root, frame(j, e, f), "bdu")
        obj(f"Hand_{side}", box_mesh(A["HAND_BRTH_AT_METACARPALE"], 0.05, 0.11, offset=(0, 0, 0.045)), root, frame(e, e + (e - j), f), skin)


# ---------------------------------------------------------------- equipment

def helmet(root, H, foliage, rng):
    """PASGT: Kevlar shell (19 plies) with the flared skirt over the ears and neck, in a woodland cover."""
    hx, hy, hz = HEAD
    bm = bmesh.new()
    # PASGT shell as a lofted profile: rim at the brow in front sloping to the nape behind (26 deg), skirt
    # flared to 0.28 m wide over the ears, crown 0.185 m above the rim and about 0.025 m above the head.
    rim = 0.09
    prof = [(0.000, 0.140, 0.162), (0.020, 0.134, 0.155), (0.060, 0.131, 0.152), (0.100, 0.122, 0.142),
            (0.135, 0.101, 0.118), (0.162, 0.068, 0.080), (0.178, 0.033, 0.040), (0.185, 0.006, 0.006)]
    bm = rings_mesh([(rim + dz, a, b, 0, -0.005) for dz, a, b in prof], 20, caps=False)
    shear = math.tan(math.radians(26))
    for v in bm.verts:
        w = max(0.0, 1.0 - (v.co.z - rim) / 0.10)   # full slope at the rim, none above 0.10 m
        v.co.z += shear * v.co.y * w
    obj("Helmet", bm, root, H, "helmet")
    band = rings_mesh([(rim + 0.115, 0.121, 0.142, 0, -0.005), (rim + 0.135, 0.112, 0.132, 0, -0.005)], 20, caps=False)
    obj("HelmetBand", band, root, H, "band")
    if foliage:
        bms = []
        for _ in range(16):
            a = rng.uniform(0, TAU)
            x, y = (hx + 0.045) * math.cos(a), (hy + 0.045) * math.sin(a)
            b = bmesh.new()
            # Tufts tucked under the band, leaning outward from the shell.
            lean_x, lean_y = 0.9 * math.sin(a) * -1, 0.9 * math.cos(a)
            bmesh.ops.create_cone(b, cap_ends=True, segments=4, radius1=0.016, radius2=0.0, depth=0.05,
                                  matrix=Matrix.Translation((x * 0.92, y * 0.92, rim + 0.13)) @ Matrix.Rotation(lean_x * rng.uniform(0.5, 1), 4, "X") @ Matrix.Rotation(lean_y * rng.uniform(0.5, 1), 4, "Y"))
            bms.append(b)
        obj("Foliage", merge(*bms), root, H, "foliage")


def vest_and_lbe(root, P, U, pack, rng):
    """PASGT vest over the torso; LC-2 pistol belt with two ammo pouches, canteens and E-tool; ALICE pack."""
    cb, cd = A["CHEST_BRTH"] / 2 + EASE + 0.04, A["CHEST_DEPTH"] / 2 + EASE + 0.04
    wb, wd = A["WAIST_BRTH_OMPHALION"] / 2 + EASE + 0.035, A["WAIST_DEPTH-OMPHALION"] / 2 + EASE + 0.035
    vest = [(-0.04, wb, wd, 0, 0.005), (A["CHEST_HT"] - A["WAIST_HT_NATURAL"], cb, cd, 0, 0.01),
            (SHOULDER_UP - 0.02, A["BIACROMIAL_BRTH"] / 2 - 0.015, cd * 0.88, 0, -0.005), (SHOULDER_UP + 0.07, 0.10, 0.085, 0, -0.012)]
    obj("Vest", rings_mesh(vest, 14), root, U, "vest")
    # PASGT raised collar around the neck.
    obj("VestCollar", rings_mesh([(SHOULDER_UP + 0.05, 0.095, 0.085, 0, -0.01), (SHOULDER_UP + 0.13, 0.088, 0.080, 0, -0.015)], 14, caps=False), root, U, "vest")
    belt_z = A["ILIOCRISTALE_HT"] - PELVIS_Z - 0.02
    hb, bd = A["HIP_BRTH"] / 2 + EASE + 0.02, A["BUTT_DEPTH"] / 2 + EASE + 0.01
    obj("PistolBelt", rings_mesh([(belt_z - 0.028, hb * 0.93, bd * 0.95, 0, 0), (belt_z + 0.028, hb * 0.93, bd * 0.95, 0, 0)], 14), root, P, "web")
    # Two ALICE 30-rd ammo pouches on the front of the belt, canteens on the rear hips.
    parts = [box_mesh(0.10, 0.065, 0.16, offset=(sx * 0.10, bd + 0.03, belt_z - 0.06)) for sx in (-1, 1)]
    parts += [cyl_mesh(0.062, 0.17, 10, "z", offset=(sx * 0.13, -bd - 0.035, belt_z - 0.20)) for sx in (-1, 1)]
    parts.append(box_mesh(0.12, 0.05, 0.22, offset=(-0.06, -bd - 0.03, belt_z - 0.13)))
    obj("LBE_Pouches", merge(*parts), root, P, "web")
    # Y-suspenders laid on the vest surface: waist -> chest -> over the shoulder -> down the back.
    straps = []
    chz = A["CHEST_HT"] - A["WAIST_HT_NATURAL"]
    for sx in (-1, 1):
        pts_f = [(sx * 0.10, wd + 0.008, -0.03), (sx * 0.10, cd + 0.010, chz), (sx * 0.10, cd * 0.88 + 0.01, SHOULDER_UP - 0.02),
                 (sx * 0.10, 0.07, SHOULDER_UP + 0.075), (sx * 0.10, -0.07, SHOULDER_UP + 0.075),
                 (sx * 0.10, -(cd * 0.88 + 0.01), SHOULDER_UP - 0.02), (sx * 0.10, -(cd + 0.010), chz), (sx * 0.10, -(wd + 0.008), -0.03)]
        for a, b in zip(pts_f, pts_f[1:]):
            straps.append(seg_box(a, b, 0.045, 0.012))
    obj("Suspenders", merge(*straps), root, U, "web")
    if pack == "alice":
        top = SHOULDER_UP + 0.08
        back = A["CHEST_DEPTH"] / 2 * 0.85 + EASE + 0.02  # vest back at shoulder-blade height
        bm = box_mesh(0.36, 0.23, 0.44, offset=(0, -back - 0.115, top - 0.22))
        bm2 = cyl_mesh(0.085, 0.40, 10, "x", offset=(-0.20, -back - 0.10, top - 0.50))  # bedroll under the pack
        pockets = [box_mesh(0.11, 0.06, 0.15, offset=(dx, -back - 0.26, top - 0.30)) for dx in (-0.12, 0.0, 0.12)]
        obj("AlicePack", merge(bm, bm2, *pockets), root, U, "web")
    elif pack == "radio":
        back = A["CHEST_DEPTH"] / 2 * 0.85 + EASE + 0.02
        rails = [box_mesh(0.025, 0.025, 0.48, offset=(sx * 0.15, -back - 0.015, SHOULDER_UP - 0.22)) for sx in (-1, 1)]
        rails += [box_mesh(0.32, 0.025, 0.025, offset=(0, -back - 0.015, SHOULDER_UP - 0.22 + dz)) for dz in (-0.22, 0.0, 0.22)]
        obj("AliceFrame", merge(*rails), root, U, "band")
        # AN/PRC-77 set (RT-841 with battery box), 0.273 x 0.273 x 0.100 m, strapped to the frame.
        obj("PRC77", box_mesh(0.273, 0.100, 0.273, offset=(0, -back - 0.08, SHOULDER_UP - 0.16)), root, U, "radio")
        obj("RTO_PackLoad", box_mesh(0.32, 0.17, 0.20, offset=(0, -back - 0.115, SHOULDER_UP - 0.42)), root, U, "web")
        whip = cyl_mesh(0.005, 3.0, 6, "z", offset=(-0.11, -back - 0.06, SHOULDER_UP - 0.02))
        obj("PRC77_Whip", whip, root, U @ Matrix.Translation((0, -back - 0.06, SHOULDER_UP - 0.02)) @ Matrix.Rotation(-0.18, 4, "X") @ Matrix.Translation((0, back + 0.06, -(SHOULDER_UP - 0.02))), "band")


def law_on_back(root, U):
    """M72A2 LAW, closed (0.630 m), strapped diagonally across the pack top."""
    cd = A["CHEST_DEPTH"] / 2 + EASE + 0.03
    m = U @ Matrix.Translation((0, -cd - 0.26, SHOULDER_UP + 0.02)) @ Matrix.Rotation(0.75, 4, "Y") @ Matrix.Translation((0, 0, -0.315))
    obj("M72_LAW", cyl_mesh(0.033, 0.630, 10, "z"), root, m, "law")


# Weapons in a local frame: bore along +Y, z = 0 on the bore, pistol grip hand at GRIP.
def weapon_mesh(kind):
    parts = []
    if kind in ("m16a2", "m16a2_m203"):
        L, barrel = 1.007, 0.508
        butt, muzzle = -0.430, -0.430 + L
        parts += [prism_x([(butt, 0.035), (butt, -0.085), (-0.10, -0.035), (-0.10, 0.02)], 0.042),  # tapered stock
                  box_mesh(0.048, 0.25, 0.072, offset=(0, 0.045, 0.0)),                       # upper and lower receivers
                  box_mesh(0.028, 0.18, 0.045, offset=(0, 0.05, 0.0585)),                     # carry handle, flush on the receiver
                  cyl_mesh(0.031, 0.30, 12, "y", offset=(0, 0.17, 0.0)),                      # round handguard
                  cyl_mesh(0.011, barrel - 0.10, 8, "y", offset=(0, muzzle - barrel + 0.10, 0.0)),
                  box_mesh(0.016, 0.03, 0.075, offset=(0, 0.48, 0.045)),                      # front sight post
                  cyl_mesh(0.013, 0.06, 8, "y", offset=(0, muzzle - 0.06, 0.0)),              # flash hider
                  box_mesh(0.026, 0.065, 0.19, offset=(0, 0.075, -0.13)),                     # 30-rd magazine
                  box_mesh(0.03, 0.045, 0.11, offset=(0, -0.045, -0.095))]                    # pistol grip
        if kind == "m16a2_m203":
            # M203: 0.39 m launcher tube under the handguard with its trigger housing and grip.
            parts += [cyl_mesh(0.030, 0.39, 12, "y", offset=(0, 0.085, -0.068)),
                      box_mesh(0.045, 0.09, 0.07, offset=(0, 0.11, -0.085)), box_mesh(0.03, 0.04, 0.07, offset=(0, 0.09, -0.135))]
        return merge(*parts), muzzle, (0, -0.045, -0.085), (0, 0.30, -0.035 if kind == "m16a2" else -0.085), (0, butt, -0.03)
    if kind == "m249":
        L, barrel = 1.035, 0.465
        butt, muzzle = -0.420, -0.420 + L
        parts += [box_mesh(0.045, 0.33, 0.10, offset=(0, butt + 0.165, -0.04)),
                  box_mesh(0.065, 0.27, 0.13, offset=(0, 0.075, 0.0)),                        # receiver and feed cover
                  box_mesh(0.11, 0.085, 0.12, offset=(-0.055, 0.06, -0.12)),                  # 200-rd box on the left
                  cyl_mesh(0.016, barrel, 10, "y", offset=(0, muzzle - barrel, 0.0)),
                  cyl_mesh(0.010, 0.30, 8, "y", offset=(0, muzzle - 0.40, -0.035)),            # gas tube
                  box_mesh(0.02, 0.10, 0.05, offset=(0, 0.29, 0.06)),                         # carry handle
                  box_mesh(0.05, 0.30, 0.02, offset=(0, muzzle - 0.28, -0.05)),               # folded bipod
                  box_mesh(0.03, 0.045, 0.11, offset=(0, -0.04, -0.10))]
        return merge(*parts), muzzle, (0, -0.04, -0.09), (0, 0.17, -0.08), (0, butt, -0.04)
    raise ValueError(kind)


def place_weapon(root, kind, M):
    bm, muzzle, grip, support, butt = weapon_mesh(kind)
    o = obj(f"Weapon_{kind}", bm, root, M, "weapon")
    e = empty("Muzzle_0", None, (0, 0, 0))
    e.parent = root
    e.matrix_basis = M @ Matrix.Translation((0, muzzle, 0))
    return M @ Vector(grip), M @ Vector(support), M @ Vector(butt)


def weapon_frame(origin, yaw, pitch, roll=0.0):
    return Matrix.Translation(origin) @ Matrix.Rotation(yaw, 4, "Z") @ Matrix.Rotation(pitch, 4, "X") @ Matrix.Rotation(roll, 4, "Y")


# ---------------------------------------------------------------- poses

def build_soldier(s, root_parent, rng):
    root = empty(f"Soldier_{s['idx']:02d}_{s['role']}", root_parent, s["at"])
    root.rotation_euler = (0, 0, math.radians(s.get("yaw", 0)))
    pose, kind = s["pose"], s["weapon"]
    skin = s.get("skin", "skin_a")
    if pose == "prone":
        pz = 0.21   # chest and vest clear of the ground
        P = Matrix.Translation((0, 0, pz)) @ Matrix.Rotation(-math.pi / 2 + 0.06, 4, "X")
        U = P @ Matrix.Translation((0, 0, WAIST_UP)) @ Matrix.Rotation(0.10, 4, "X")
        H = U @ Matrix.Translation((0, 0.0, NECK_UP + 0.02)) @ Matrix.Rotation(1.15, 4, "X")
        hip = [P @ Vector((sx * HIP_X, 0, -0.03)) for sx in (1, -1)]
        feet = [Vector((0.22, -0.98, 0.10)), Vector((-0.26, -0.95, 0.10))]
        knee_hint = Vector((0, 0, -1))
        # Legs flat behind, feet pointing back with the soles up (instep on the ground).
        feet = [Vector((0.22, -0.98, 0.10)), Vector((-0.26, -0.95, 0.10))]
        foot_frames = [(Vector((0.3, -1, 0)), Vector((0, 0, -1))), (Vector((-0.3, -1, 0)), Vector((0, 0, -1)))]
    else:
        crouch = {"aim": 0.0, "ready": 0.02, "walk": 0.03, "kneel": None}[pose]
        lean = {"aim": 0.10, "ready": 0.14, "walk": 0.10, "kneel": 0.22}[pose]
        if pose == "kneel":
            pz = 0.53   # hip over a grounded right knee (thigh ~vertical)
            P = Matrix.Translation((0, 0, pz)) @ Matrix.Rotation(0.12, 4, "X") @ Matrix.Rotation(-0.35, 4, "Z")
            hip = [P @ Vector((sx * HIP_X, 0, -0.03)) for sx in (1, -1)]
            feet = [Vector((0.16, -0.42, 0.21)), Vector((-0.26, 0.36, ANKLE_Z))]
            knee_hint = Vector((0, 1, 0))
            foot_frames = [(Vector((0, -0.35, -1)), Vector((0, -1, 0.35))), (Vector((-0.15, 1, 0)), Vector((0, 0, 1)))]
        else:
            pz = PELVIS_Z - crouch
            stride = 0.30 if pose == "walk" else 0.0
            P = Matrix.Translation((0, 0, pz)) @ Matrix.Rotation(0.05, 4, "X") @ Matrix.Rotation(-0.25 if pose == "aim" else -0.1, 4, "Z")
            hip = [P @ Vector((sx * HIP_X, 0, -0.03)) for sx in (1, -1)]
            stance = 0.17 if pose == "aim" else 0.12
            feet = [Vector((stance, -0.12 - stride / 2, ANKLE_Z + (0.11 if stride else 0))), Vector((-stance, 0.16 + stride / 2, ANKLE_Z))]
            knee_hint = Vector((0, 1, 0))
            rear = (Vector((0.15, 1, -0.45)), Vector((0, 0.45, 1))) if stride else (Vector((0.15, 1, 0)), Vector((0, 0, 1)))
            foot_frames = [rear, (Vector((-0.15, 1, 0)), Vector((0, 0, 1)))]
        U = P @ Matrix.Translation((0, 0, WAIST_UP)) @ Matrix.Rotation(lean, 4, "X") @ Matrix.Rotation(0.30 if pose in ("aim", "kneel") else 0.1, 4, "Z")
        H = U @ Matrix.Translation((0, 0.0, NECK_UP + 0.02)) @ Matrix.Rotation(0.30 if pose in ("aim", "kneel") else 0.05, 4, "X") @ Matrix.Rotation(-0.25 if pose in ("aim", "kneel") else -0.05, 4, "Z")
    shoulders = [U @ Vector((sx * SHOULDER_X, 0.0, SHOULDER_UP)) for sx in (1, -1)]
    # Weapon placement per pose (soldier frame).
    pocket = U @ Vector((SHOULDER_X - 0.06, 0.09, SHOULDER_UP - 0.04))
    if pose in ("aim", "kneel"):
        M = weapon_frame(pocket, 0.0, 0.0) @ Matrix.Translation((0, 0.43, 0.03))
    elif pose == "ready":
        M = weapon_frame(pocket, 0.0, -0.55) @ Matrix.Translation((0, 0.43, 0.03))
    elif pose == "walk":
        chest = U @ Vector((0.0, 0.26, SHOULDER_UP - 0.20))
        M = weapon_frame(chest, -0.25, -0.35, roll=0.4)
    else:  # prone: on the bipod in front, butt in the shoulder
        M = weapon_frame(Vector((pocket.x, pocket.y, pocket.z)), 0.0, 0.03) @ Matrix.Translation((0, 0.43, 0.03))
    grip, support, butt = place_weapon(root, kind, M)
    # Prone: elbows splayed out to rest on the ground beside the gun, not driven into it.
    elbow_hint_r = (U.to_3x3() @ Vector((1.0, -0.2, -0.8))) if pose != "prone" else Vector((1.0, 0, -0.25))
    elbow_hint_l = (U.to_3x3() @ Vector((-0.3, 0.0, -1.0))) if pose != "prone" else Vector((-1.0, 0, -0.25))
    arms = []
    for sh, target, hint in ((shoulders[0], grip, elbow_hint_r), (shoulders[1], support, elbow_hint_l)):
        elbow, wrist_t = two_bone(sh, target, UPPER_ARM, FOREARM + 0.05, hint)
        wrist = elbow + (wrist_t - elbow).normalized() * FOREARM
        arms.append((sh, elbow, wrist, U.to_3x3() @ Vector((0, 1, 0))))
    legs = []
    for h, f, (ff, fu) in zip(hip, feet, foot_frames):
        knee, ankle = two_bone(h, f, THIGH, SHIN, knee_hint)
        legs.append((h, knee, ankle, ff, fu))
    build_body(root, P, U, H, legs, arms, skin, prone=(pose == "prone"))
    helmet(root, H, s.get("foliage", True), rng)
    vest_and_lbe(root, P, U, s.get("pack", "alice"), rng)
    if s.get("law"):
        law_on_back(root, U)
    return root


def build(spec):
    reset()
    _mats.clear()
    rng = random.Random(spec.get("seed", 1989))
    top = empty("Root")
    g = spec.get("ground")
    if g:
        bm = box_mesh(g[2] - g[0], g[3] - g[1], 0.02, offset=((g[0] + g[2]) / 2, (g[1] + g[3]) / 2, -0.01))
        obj("Ground", bm, top, Matrix.Identity(4), "ground")
    for s in spec["soldiers"]:
        build_soldier(s, top, rng)


def measure(spec):
    bpy.context.view_layer.update()
    meshes = [o for o in bpy.data.objects if o.type == "MESH" and o.name != "Ground"]
    info = {"triangles_lod0": sum(triangles(o) for o in meshes), "soldiers": len(spec["soldiers"])}
    per = {}
    for o in meshes:
        r = o.parent
        per.setdefault(r.name, []).append(o)
    info["triangles_per_soldier_max"] = max(sum(triangles(o) for o in v) for v in per.values())
    for o in meshes:
        if o.name.startswith("Weapon_m16a2") and "rifle_m16a2_length_m" not in info:
            ys = [v.co.y for v in o.data.vertices]
            info["rifle_m16a2_length_m"] = round(max(ys) - min(ys), 3)
    info["stature_ansur_m"] = round(STAT, 4)
    info["nodes"] = sorted({o.name.split(".")[0] for o in bpy.data.objects})
    return info


def main():
    vid = sys.argv[sys.argv.index("--") + 1]
    ms = importlib.util.spec_from_file_location(f"spec_{vid}", REPO / "assets_src" / "procedural" / "specs" / f"{vid}.py")
    mod = importlib.util.module_from_spec(ms)
    ms.loader.exec_module(mod)
    build(mod.SPEC)
    work = REPO / "assets_src" / "work" / vid
    out = REPO / "assets_src" / "exports" / vid
    work.mkdir(parents=True, exist_ok=True)
    out.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(work / f"{vid}.blend"))
    bpy.ops.export_scene.gltf(filepath=str(out / f"{vid}_lod0.glb"), export_format="GLB", export_yup=True, export_apply=True)
    info = measure(mod.SPEC)
    info["id"] = vid
    (out / f"{vid}_build.json").write_text(json.dumps(info, indent=2) + "\n")
    print("BUILD", json.dumps({k: v for k, v in info.items() if k != "nodes"}))


if __name__ == "__main__":
    main()
