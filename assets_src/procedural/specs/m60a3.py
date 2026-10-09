"""M60A3 TTS (1989) for assets_src/procedural/ground_vehicle.py.

Blender axes: +Y front, +Z up, metres. Sourced (data/assets/m60a3.json): hull length 6.946, gun forward
9.309, width 3.631, height over cupola 3.27, ground clearance 0.463, track width 0.711, 6 road wheels and
3 return rollers per side, no side skirts. Proportions (fender line, turret length and nose, wheel and roller
positions, high front idler) measured from the distributable side photo
left_side_view_of_m60a3_tank_during_reforger_82_8d18.jpg at its hull-length scale (~174 px/m).
"""

L = 6.946
HALF = L / 2

SPEC = {
    "width_m": 3.631,
    "paint": (0.25, 0.27, 0.18),
    "track": {"width_m": 0.711, "thickness_m": 0.09},
    "hull": {
        # Tall hull: belly at 0.463, lower glacis up to the nose, short upper glacis to the deck at 1.70.
        "lower_profile": [(-HALF, 0.98), (-HALF + 0.20, 0.463), (HALF - 0.75, 0.463), (HALF, 1.12), (HALF - 0.55, 1.68), (-HALF + 0.30, 1.70), (-HALF, 1.30)],
        # Fenders and sponsons over the tracks; the rear deck slopes down to the rear grilles.
        "upper_profile": [(-HALF, 1.30), (-HALF, 1.25), (HALF - 0.02, 1.50), (HALF, 1.56), (HALF - 0.50, 1.70), (-HALF + 0.30, 1.70)],
    },
    "running_gear": {
        "wheel_segments": 18,
        "road_wheels": [(y, 0.41, 0.32) for y in (2.19, 1.32, 0.49, -0.35, -1.21, -2.08)],
        "return_rollers": [(1.99, 1.21, 0.12), (0.20, 1.21, 0.12), (-1.33, 1.21, 0.12)],
        "idler": (3.00, 1.00, 0.30),
        "sprocket": (-2.91, 0.86, 0.33),
    },
    "turret": {
        "ring_center": (0.0, 0.03, 1.70),
        # Needle-nose cast turret: roof ~2.85 m (side photo profile; consistent with the sourced 3.27 m
        # over the cupola), a long high front slope, and the bustle ending ~2.05 m behind the ring centre.
        "rings": [
            (0.00, [(0.0, 2.00), (-0.40, 1.86), (-1.22, 0.80), (-1.30, -0.40), (-1.20, -1.30), (-0.95, -1.80), (0.95, -1.80), (1.20, -1.30), (1.30, -0.40), (1.22, 0.80), (0.40, 1.86)]),
            (0.55, [(0.0, 1.95, 0.62), (-0.38, 1.80, 0.68), (-1.18, 0.75, 0.70), (-1.25, -0.40, 0.62), (-1.15, -1.35, 0.62), (-0.92, -1.90, 0.62), (0.92, -1.90, 0.62), (1.15, -1.35, 0.62), (1.25, -0.40, 0.62), (1.18, 0.75, 0.70), (0.38, 1.80, 0.68)]),
            (1.15, [(0.0, 0.85, 1.12), (-0.55, 0.75, 1.13), (-0.98, 0.45, 1.13), (-1.04, -0.40, 1.15), (-0.95, -1.35, 1.05), (-0.78, -1.86, 0.98), (0.78, -1.86, 0.98), (0.95, -1.35, 1.05), (1.04, -0.40, 1.15), (0.98, 0.45, 1.13), (0.55, 0.75, 1.13)]),
        ],
        "parts": [
            # M19 cupola on the right of the roof, M85 .50 cal on top: top of cupola + gun at the sourced 3.27 m.
            # M19 cupola: a round drum 0.85 m across, top at the sourced 3.27 m; the M85 sits in its front.
            {"name": "Cupola_M19", "shape": "cyl_z", "size": (0.85, 0.85, 0.42), "at": (0.42, -0.35, 0.0), "rest": True, "lift": 0.065, "segments": 20},
            {"name": "Mg_M85", "size": (0.09, 0.90, 0.11), "at": (0.42, 0.22, 1.40), "paint": False},
            {"name": "Hatch_Loader_Ring", "size": (0.62, 0.62, 0.10), "at": (-0.50, -0.30, 0.0), "rest": True},
            {"name": "Optic_TTS_Housing", "size": (0.40, 0.55, 0.18), "at": (0.70, 0.55, 0.0), "rest": True},
            {"name": "WindSensor_Mast", "size": (0.06, 0.06, 0.40), "at": (-0.40, -1.65, 0.0), "rest": True},
            {"name": "SmokeLauncher_L", "size": (0.22, 0.40, 0.22), "at": (-1.30, 0.55, 0.46)},
            {"name": "SmokeLauncher_R", "size": (0.22, 0.40, 0.22), "at": (1.30, 0.55, 0.46)},
            # Open bustle basket, 2.2 m wide and 0.4 m tall, overhanging the turret rear by 0.45 m, with stowage.
            {"name": "Rack_Floor", "size": (2.20, 0.47, 0.04), "at": (0.0, -2.03, 0.42), "paint": False},
            {"name": "Rack_Rear", "size": (2.20, 0.04, 0.40), "at": (0.0, -2.25, 0.62), "paint": False},
            {"name": "Rack_Side_L", "size": (0.04, 0.47, 0.40), "at": (-1.10, -2.03, 0.62), "paint": False},
            {"name": "Rack_Side_R", "size": (0.04, 0.47, 0.40), "at": (1.10, -2.03, 0.62), "paint": False},
            {"name": "Stowage_Bags", "size": (1.80, 0.36, 0.30), "at": (0.0, -2.05, 0.59)},
        ],
    },
    "gun": {
        "trunnion": (0.0, 2.07, 0.57),
        # Short gun collar where the needle nose meets the gun (no box mantlet on the M60A1/A3 turret).
        "mantlet_size": (0.45, 0.15, 0.45),
        "mantlet_at": (0.0, 0.0, 0.0),
        # Muzzle at hull y = -HALF + 9.309 = 5.836 -> turret-local 5.806 -> gun-local 3.736.
        "sections": [(0.0, 0.16), (0.30, 0.16), (0.31, 0.085), (2.05, 0.083), (2.10, 0.118), (2.62, 0.118), (2.67, 0.078), (3.72, 0.076), (3.736, 0.072)],
        "segments": 16,
    },
    "hull_parts": [
        # AVDS-1790 rear deck grille doors and the big rear exhaust grilles.
        {"name": "Grille_Deck", "size": (2.20, 1.60, 0.03), "at": (0.0, -2.55, 1.715), "paint": False},
        {"name": "Grille_Rear_L", "size": (0.90, 0.06, 0.50), "at": (-0.75, -HALF - 0.03, 1.30), "paint": False},
        {"name": "Grille_Rear_R", "size": (0.90, 0.06, 0.50), "at": (0.75, -HALF - 0.03, 1.30), "paint": False},
        {"name": "Headlight_Guards", "size": (2.60, 0.18, 0.14), "at": (0.0, HALF - 0.40, 1.66)},
    ],
    "sockets": {
        "Exhaust_0": (0.0, -HALF - 0.02, 1.30),
        "Optic_TTS": (0.70, 0.83, 1.0),
        "Hatch_Cmd": (0.42, -0.35, 1.30),
        "Hatch_Driver": (0.0, 2.75, 1.72),
    },
}
