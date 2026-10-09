"""T-80BV (1985-89) for assets_src/procedural/ground_vehicle.py.

Blender axes: +Y front, +Z up, metres. Sourced (data/assets/t80bv.json): hull length 6.982, gun forward
9.651, width 3.582, turret roof 2.219, ground clearance 0.451, track width 0.58, 6 road wheels and 5
return rollers per side. Shape from the distributable photos and look-only static-display photos
(studied, not traced): low cast dome turret set forward of centre, Kontakt-1 bricks in chevron rows on the
turret front arc and a block grid on the roof and glacis, fuel drums on the hull rear, snorkel on the
turret rear, IR searchlight right of the gun.
"""
import math

L = 6.982
HALF = L / 2


def oval(a, b, z, n=20, front_squash=0.80):
    """Oval plan ring: semi-axes a (half width) and b (half length); the front half is slightly flatter."""
    pts = []
    for k in range(n):
        t = 2 * math.pi * k / n + math.pi / 2  # start at the front (+Y), counter-clockwise seen from above
        x = a * math.cos(t)
        y = b * math.sin(t)
        if y > 0:
            y *= front_squash
        pts.append((x, y, z))
    return pts


# Kontakt-1 bricks (0.25 x 0.13 x 0.075 m).
# Turret: two stacked rows on each front cheek (arc 25-70 deg, ~1.0 m long, 0.30 m tall), upright and yawed
# along the casting; a two-row chevron on the roof, arms ~0.9 m long swept 30 deg back, seated by raycast.
era = []
for side in (-1, 1):
    tag = "L" if side < 0 else "R"
    # Cheeks: two rows of 5 bricks over the front +-60 deg, standing 0.09 m proud as a flat-faced band ~0.35 m tall.
    for row, z in enumerate((0.11, 0.27)):
        a_, b_ = 1.30 - 0.08 * z / 0.40, 1.24 - 0.08 * z / 0.40
        for i in range(5):
            th = math.radians(14 + i * 11.5)
            x, y = side * a_ * math.sin(th), b_ * math.cos(th)
            n = (math.sin(th) / a_, math.cos(th) / b_)
            nl = math.hypot(*n)
            era.append({"name": f"ERA_Cheek_{tag}{row}{i}", "size": (0.26, 0.07, 0.15),
                        "at": (x + side * 0.09 * n[0] / nl, y + 0.09 * n[1] / nl, z), "yaw_deg": -side * math.degrees(th)})
    # Roof chevron: two rows per side from 0.15 m behind the turret front running 0.6 m back, a V opening toward
    # the gun, bricks seated proud of the roof (bottom on the surface).
    for row in range(2):
        for i in range(3):
            d = 0.10 + i * 0.25
            x = side * (0.14 + d * math.cos(math.radians(30)) + row * 0.07)
            y = 0.03 + d * math.sin(math.radians(30)) * 2.0 - row * 0.12
            era.append({"name": f"ERA_Roof_{tag}{row}{i}", "size": (0.26, 0.13, 0.07), "at": (x, y, 0.0),
                        "rest": True, "yaw_deg": side * 30})

# Glacis: 5 rows x 17 bricks in a shallow V on the 22 deg upper glacis (nose at z 0.86, deck 1.46 at 1.45 m aft),
# bricks pitched flat onto the plate and standing 0.075 m proud, ~86% coverage.
GLACIS_A = math.atan2(0.60, 1.45)
glacis_era = []
for r in range(5):
    for c in range(17):
        x = -1.20 + c * 0.15
        t = 0.16 + r * 0.27 + abs(x) * 0.10          # distance up the slope from the nose; V toward the sides
        if t > 1.50:
            continue
        sy = HALF - t * math.cos(GLACIS_A)
        sz = 0.86 + t * math.sin(GLACIS_A)
        glacis_era.append({"name": f"ERA_Glacis_{r}{c:02d}", "size": (0.13, 0.25, 0.075),
                           "at": (x, sy + 0.037 * math.sin(GLACIS_A), sz + 0.037 * math.cos(GLACIS_A)),
                           "pitch_deg": -math.degrees(GLACIS_A)})

SPEC = {
    "width_m": 3.582,
    "paint": (0.24, 0.29, 0.18),
    "track": {"width_m": 0.58, "thickness_m": 0.08},
    "skirt": {
        "thickness_m": 0.05,
        # Rubber side skirt over the upper road wheels, flap plates toward the front.
        # Ends where the glacis slope starts (1.4 m behind the nose); front mudguards take over from there.
        # Runs to the front, ending in an angled flap (~0.6 m) over the idler.
        "profile": [(-HALF + 0.15, 0.80), (HALF - 0.75, 0.80), (HALF - 0.30, 1.25), (HALF - 0.35, 1.42), (-HALF + 0.10, 1.42)],
    },
    "hull": {
        # Pointed upper glacis (68 deg) from the nose to the deck; belly at the 0.451 m clearance.
        "lower_profile": [(-HALF, 0.80), (-HALF + 0.22, 0.451), (HALF - 0.55, 0.46), (HALF, 0.86), (HALF - 1.45, 1.46), (-HALF, 1.46)],
        "upper_profile": [(-HALF, 1.10), (HALF - 0.12, 1.10), (HALF, 0.98), (HALF - 1.45, 1.46), (-HALF, 1.46)],
    },
    "running_gear": {
        "wheel_segments": 18,
        "road_wheels": [(y, 0.415, 0.335) for y in (2.05, 1.27, 0.49, -0.29, -1.07, -1.85)],
        "return_rollers": [(y, 0.98, 0.07) for y in (1.65, 0.85, 0.05, -0.75, -1.50)],
        "idler": (2.88, 0.56, 0.30),
        "sprocket": (-2.92, 0.62, 0.32),
    },
    "turret": {
        "ring_center": (0.0, 0.15, 1.46),
        # Low dome: three oval rings narrowing toward the roof (roof at 2.219 m).
        "rings": [
            (0.00, oval(1.30, 1.55, 0.00)),
            (0.40, oval(1.22, 1.45, 0.40)),
            (0.72, oval(0.80, 0.98, 0.76)),
        ],
        "parts": era + [
            # Commander right of the gun, gunner left (T-72/T-80 layout).
            {"name": "Cupola_Cmd", "size": (0.62, 0.62, 0.20), "at": (0.48, -0.15, 0.82)},
            {"name": "Optic_Gunner", "size": (0.30, 0.34, 0.26), "at": (-0.62, 0.30, 0.86)},
            {"name": "Searchlight_IR", "size": (0.34, 0.26, 0.34), "at": (0.62, 1.30, 0.42)},
            {"name": "SmokeLaunchers_L", "size": (0.20, 0.42, 0.18), "at": (-1.10, 0.85, 0.48)},
            {"name": "SmokeLaunchers_R", "size": (0.20, 0.42, 0.18), "at": (1.10, 0.85, 0.48)},
            {"name": "Mg_NSVT", "size": (0.08, 1.10, 0.09), "at": (0.48, 0.25, 1.02), "paint": False},
            # OPVT snorkel stowed full length across the turret rear.
            {"name": "Snorkel", "shape": "cyl_x", "size": (1.90, 0.36, 0.36), "at": (0.0, -1.55, 0.32), "segments": 14},
            {"name": "Stowage_Box_L", "size": (0.40, 0.55, 0.32), "at": (-1.15, -0.65, 0.30)},
            {"name": "Stowage_Box_R", "size": (0.40, 0.55, 0.32), "at": (1.15, -0.65, 0.30)},
        ],
    },
    "gun": {
        "trunnion": (0.0, 0.95, 0.38),
        "mantlet_size": (0.50, 0.40, 0.42),
        "mantlet_at": (0.0, 0.18, 0.0),
        # Muzzle at hull y = -HALF + 9.651 = 6.160 -> turret-local 6.010 -> gun-local 5.06.
        "sections": [(0.0, 0.14), (0.40, 0.14), (0.41, 0.100), (2.30, 0.098), (2.34, 0.125), (3.00, 0.125), (3.05, 0.090), (5.04, 0.085), (5.06, 0.078)],
        "segments": 16,
    },
    "hull_parts": [
        # Two 200 l drums on the hull rear; Kontakt-1 array across the upper glacis.
        {"name": "Drum_L", "shape": "cyl_x", "size": (0.88, 0.58, 0.58), "at": (-0.50, -HALF - 0.40, 1.72), "segments": 16},
        {"name": "Drum_R", "shape": "cyl_x", "size": (0.88, 0.58, 0.58), "at": (0.50, -HALF - 0.40, 1.72), "segments": 16},
        # GTD-1000 gas-turbine louvre grille on the rear deck and the exhaust grille on the rear plate.
        {"name": "Grille_Deck_Turbine", "size": (1.80, 2.20, 0.03), "at": (0.0, -HALF + 1.20, 1.475), "paint": False},
        {"name": "Grille_Exhaust", "size": (1.40, 0.05, 0.35), "at": (0.0, -HALF - 0.02, 1.12), "paint": False},
        # Front mudguards over the tracks, following the glacis slope down to the nose.
        # Hinged front mudguards over each track, 0.6 x 0.55 m, angled 45 deg up and forward.
        {"name": "Mudguard_L", "x": (-3.582 / 2, -3.582 / 2 + 0.60), "profile": [(HALF - 0.45, 1.10), (HALF - 0.06, 1.49), (HALF - 0.04, 1.47), (HALF - 0.43, 1.08)]},
        {"name": "Mudguard_R", "x": (3.582 / 2 - 0.60, 3.582 / 2), "profile": [(HALF - 0.45, 1.10), (HALF - 0.06, 1.49), (HALF - 0.04, 1.47), (HALF - 0.43, 1.08)]},
    ] + glacis_era + [
    ],
    "sockets": {
        "Exhaust_0": (0.0, -HALF - 0.02, 1.30),
        "Optic_Gunner": (-0.62, 0.45, 1.00),
        "Hatch_Cmd": (0.48, -0.15, 0.92),
        "Hatch_Driver": (0.0, 2.35, 1.40),
    },
}
