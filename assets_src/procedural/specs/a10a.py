"""A-10A Thunderbolt II (1989, USAFE) for assets_src/procedural/aircraft.py.

Blender axes: +Y nose, +Z up, metres, gear down on the ground. Sourced (data/assets/a10a.json): length 16.26,
wingspan 17.53, height 4.47, 11 hardpoints. Layout measured from the distributable 3-view
(fairchild_republic_a_10_thunderbolt_ii_3_view_li_4d99.png) traced masks: the side-mask profile (top and bottom
per station, heights above the drawing's lowest point) sets the fuselage, canopy, pods and nacelles; the plan
mask sets widths, wing and tail planform. The 3-view is drawn gear-up; its lowest point (the gear pods) sits
4.47 - 3.16 = 1.31 m above the ground with gear down. The drawing's front view is ~19% taller than its side
view, so heights come from the side view only.
"""
N = 8.13  # nose at +Y = length / 2
G = 1.31  # height of the drawing's lowest point (gear pods) above the ground
PYLONS = (1.55, 3.70, 4.90, 6.00)  # underwing stations; 8 under the wing + 3 under the fuselage = 11


def y(pos):
    return N - pos


def wing_z(x):
    """Chord-plane height: flat centre section to x = 3.1, then dihedral to the tip."""
    return G + 1.35 if x <= 3.1 else G + 1.35 + 0.27 * (x - 3.1) / 5.2


def wing_half_t(x):
    return 0.25 if x <= 3.1 else (3.14 - 0.94 * (x - 3.1) / 5.2) * 0.15 / 2


# (pos from nose, half width, z centre, half height) above G, from the side-mask profile.
STATIONS = [
    (0.0, 0.28, 0.72, 0.14),   # blunt nose: mask 0.60-0.82
    (0.3, 0.45, 0.93, 0.33),   # 0.60-1.25
    (0.6, 0.55, 1.00, 0.42),   # 0.60-1.38
    (1.0, 0.62, 1.03, 0.55),   # 0.48-1.58
    (1.5, 0.68, 1.12, 0.64),   # 0.48-1.76
    (2.0, 0.72, 1.24, 0.76),   # 0.48-2.00 (windscreen above)
    (3.0, 0.74, 1.30, 0.78),   # spine 2.08 under the canopy
    (5.0, 0.74, 1.33, 0.85),   # 0.48-2.18
    (7.0, 0.74, 1.30, 0.81),   # top 2.11
    (9.0, 0.72, 1.18, 0.69),   # top 1.87
    (12.0, 0.62, 1.12, 0.64),  # belly 0.48
    (13.8, 0.45, 1.06, 0.58),  # belly 0.48, top 1.64
    (15.2, 0.32, 1.00, 0.45),
    (16.26, 0.15, 0.83, 0.33), # tail cone 0.48-1.17
]

SPEC = {
    "paint": (0.27, 0.31, 0.27),
    "fuselage": [(y(p), hw, G + zc, hh) for p, hw, zc, hh in STATIONS],
    "cockpit": (0.0, y(3.6), G + 1.9),
    # Bubble canopy on the raised cockpit: mask peak 2.57 at 3.5-4.0, from 2.2 to 5.0 m.
    "canopy": {"at": (0.0, y(3.6), G + 2.10), "scale": (0.52, 1.40, 0.47)},
    "surfaces": [
        # Constant-chord centre section out past the gear pods, then the tapered outer panel with dihedral.
        {"name": "WingInner", "root": (0.70, y(6.70), wing_z(0.7), 3.14), "tip": (3.10, y(6.70), wing_z(3.1), 3.14), "thickness_pct": 16,
         "controls": [{"name": "FlapInner", "span": (0.05, 0.95, 0.25)}]},
        {"name": "Wing", "root": (3.10, y(6.70), wing_z(3.1), 3.14), "tip": (8.30, y(7.20), wing_z(8.3), 2.20), "thickness_pct": 15,
         "controls": [{"name": "Flap", "span": (0.02, 0.40, 0.25)}, {"name": "Aileron", "span": (0.45, 0.97, 0.26)}]},
        {"name": "WingTip", "root": (8.30, y(7.20), wing_z(8.3), 2.20), "tip": (8.76, y(7.40), G + 1.45, 1.75), "thickness_pct": 10},
        # Straight tailplane, twin fins on its tips up to the sourced 4.47 m.
        {"name": "Tailplane", "root": (0.25, y(14.05), G + 1.10, 2.00), "tip": (3.20, y(14.10), G + 1.10, 1.95), "thickness_pct": 10,
         "controls": [{"name": "Elevator", "span": (0.08, 0.95, 0.35)}]},
        {"name": "Fin", "root": (2.95, y(14.00), G + 1.10, 2.00), "tip": (2.95, y(14.55), 4.47, 1.30), "thickness_pct": 9, "vertical": True,
         "controls": [{"name": "Rudder", "span": (0.10, 0.95, 0.35)}]},
    ],
    "nacelles": [
        # TF34 nacelles over the aft fuselage: side-mask top 2.52 at 10-11 m, tapering to 2.22 at 13 m.
        {"name": "Nacelle", "x": 1.55, "z": G + 1.85,
         "sections": [(y(9.85), 0.45), (y(10.1), 0.67), (y(11.2), 0.67), (y(12.2), 0.57), (y(13.2), 0.40)]},
        # Teardrop main-gear pods under the wing (mask: nose at 5.9 m, lowest at 7 m).
        {"name": "GearPod", "x": 2.60, "z": G + 0.42, "exhaust": False,
         "sections": [(y(5.9), 0.10), (y(6.4), 0.32), (y(7.0), 0.40), (y(8.4), 0.38), (y(9.6), 0.10)]},
    ],
    "parts": [
        # Fairing from each nacelle down to the fuselage side.
        {"name": "NacelleFairing", "size": (0.46, 3.0, 0.42), "at": (1.00, y(11.4), G + 1.70), "mirror": True},
        {"name": "PylonC", "size": (0.14, 1.5, 0.30), "at": (0.0, y(8.0), G + 0.36)},
        {"name": "PylonF", "size": (0.14, 1.5, 0.30), "at": (0.60, y(8.0), G + 0.42), "mirror": True},
    ] + [
        {"name": f"PylonW{k}", "size": (0.14, 1.5, 0.36), "at": (x, y(7.9), wing_z(x) - wing_half_t(x) - 0.12), "mirror": True}
        for k, x in enumerate(PYLONS)
    ] + [
        # GAU-8 muzzle cluster at the chin, 0.35 m across.
        {"name": "Gau8_Barrels", "size": (0.35, 0.60, 0.35), "at": (0.0, y(0.0) - 0.30, G + 0.68), "dark": True},
    ],
    "gear": [
        # Nose gear offset to starboard (the GAU-8 is on the centreline).
        {"name": "Nose", "at": (0.40, y(1.8), G + 0.50), "wheel_r": 0.32, "leg": 0.16},
        {"name": "Main", "at": (2.60, y(7.4), G + 0.25), "wheel_r": 0.42, "leg": 0.20, "mirror": True},
    ],
    "hardpoints": [(0.0, y(8.0), G + 0.21), (-0.60, y(8.0), G + 0.27), (0.60, y(8.0), G + 0.27)]
    + [(sx * x, y(7.9), wing_z(x) - wing_half_t(x) - 0.30) for x in PYLONS for sx in (-1, 1)],
    "muzzle": (0.0, y(0.0), G + 0.68),
}
