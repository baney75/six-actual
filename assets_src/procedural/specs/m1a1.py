"""M1A1 Abrams (1989) for assets_src/procedural/ground_vehicle.py.

Blender axes: +Y = front, +Z = up, metres, ground at z = 0. Sourced dimensions (data/assets/m1a1.json):
hull length 7.917, gun forward 9.83, width over skirts 3.66, turret roof 2.44, track width 0.635,
ground contact 4.575, ground clearance 0.432, 7 road wheels and 2 return rollers per side.
Shapes (glacis line, turret plan, skirt line) are read from the reference pack photos, chiefly
abrams_in_formation_12b8.jpg (side), da_sc_98_04644_08a8.jpg (front) and the three-quarter views.
"""

L = 7.917
HALF = L / 2

SPEC = {
    "width_m": 3.66,
    "paint": (0.23, 0.26, 0.17),
    "track": {"width_m": 0.635, "thickness_m": 0.08},
    "skirt": {
        "thickness_m": 0.07,
        "inset_m": 0.03,
        # Side line: full-length skirt, the front (hinged) section's lower edge rising toward the nose.
        # Front (hinged) section drops lower than the main skirt toward the nose (side photo: ~0.6 m at the front).
        # Main skirt ends where the hinged front section (a separate hull part) begins.
        "profile": [(-HALF + 0.20, 0.66), (HALF - 1.38, 0.64), (HALF - 1.38, 1.40), (-HALF + 0.12, 1.40)],
    },
    "hull": {
        # Between the tracks: belly at the 0.432 m clearance, steep lower glacis, very shallow upper glacis.
        "lower_profile": [(-HALF, 0.76), (-HALF + 0.28, 0.432), (HALF - 0.42, 0.45), (HALF, 1.04), (2.05, 1.42), (-HALF, 1.42)],
        # Over the tracks (fenders/sponsons), from above the track top run to the deck.
        "upper_profile": [(-HALF, 1.10), (HALF - 0.30, 1.10), (HALF, 1.04), (2.05, 1.42), (-HALF, 1.42)],
    },
    "running_gear": {
        "wheel_segments": 16,
        "road_wheels": [(y, 0.40, 0.32) for y in (2.08, 1.34, 0.60, -0.14, -0.88, -1.62, -2.36)],
        "return_rollers": [(1.20, 0.97, 0.10), (-1.25, 0.97, 0.10)],
        # Side photo: the track meets the ground well forward and well aft; idler low, sprocket low and far back.
        "idler": (3.15, 0.50, 0.31),
        "sprocket": (-3.38, 0.56, 0.33),
    },
    "turret": {
        "ring_center": (0.0, 0.35, 1.44),
        # Plan rings (x right, y forward) at the turret base and roof: wedge front, slightly tapered sides,
        # narrower bustle. Counter-clockwise seen from above.
        # Plan from the head-on and overhead photos: about 74% of hull width (~2.7 m), wedge cheeks angled
        # sharply in toward the gun, an undercut where the turret overhangs the hull (base ring inset), and a
        # roof sloping up from ~2.09 m at the front faces to 2.40 m at the rear.
        "rings": [
            # Base ring stepped well back at the front: the turret front overhangs the hull with a visible gap.
            (0.00, [(0.0, 1.10), (-0.42, 1.06), (-1.20, 0.55), (-1.22, -1.60), (-1.02, -2.62), (1.02, -2.62), (1.22, -1.60), (1.20, 0.55), (0.42, 1.06)]),
            (0.14, [(0.0, 1.95), (-0.45, 1.90), (-1.36, 1.00), (-1.34, -1.62), (-1.12, -2.78), (1.12, -2.78), (1.34, -1.62), (1.36, 1.00), (0.45, 1.90)]),
            # Near-vertical front faces either side of the mantlet, then the roof slopes up toward the rear.
            (0.96, [(0.0, 1.86, 0.58), (-0.44, 1.82, 0.60), (-1.30, 0.96, 0.70), (-1.26, -1.55, 0.96), (-1.06, -2.70, 0.96), (1.06, -2.70, 0.96), (1.26, -1.55, 0.96), (1.30, 0.96, 0.70), (0.44, 1.82, 0.60)]),
        ],
        "parts": [
            {"name": "Cupola_Cmd", "size": (0.74, 0.74, 0.26), "at": (0.55, -0.15, 0.0), "rest": True},
            {"name": "Hatch_Loader_Ring", "size": (0.66, 0.66, 0.10), "at": (-0.60, -0.05, 0.0), "rest": True},
            {"name": "Optic_GPS_Doghouse", "size": (0.44, 0.60, 0.32), "at": (0.82, 0.70, 0.0), "rest": True},
            # Six-barrel M250 smoke grenade launchers, one block on each front cheek.
            # M250 launchers high on the cheek tops, tight to the turret walls, visible from above.
            {"name": "SmokeLauncher_L", "size": (0.22, 0.42, 0.22), "at": (-1.30, 0.86, 0.62)},
            {"name": "SmokeLauncher_R", "size": (0.22, 0.42, 0.22), "at": (1.30, 0.86, 0.62)},
            # M2 on the commander's cupola mount: top of the gun at the sourced 2.885 m.
            # M2 on a pintle above the cupola: the pintle rests on the roof, the gun's top at the sourced 2.885 m.
            {"name": "Mg_Cal50_Mount", "size": (0.14, 0.18, 0.62), "at": (0.55, 0.10, 0.0), "rest": True},
            {"name": "Mg_Cal50", "size": (0.09, 1.20, 0.10), "at": (0.55, 0.35, 1.395), "paint": False},
            {"name": "Mg_M240_Mount", "size": (0.08, 0.10, 0.22), "at": (-0.60, 0.25, 0.0), "rest": True},
            {"name": "Mg_M240_Loader", "size": (0.07, 0.95, 0.08), "at": (-0.60, 0.40, 1.04), "paint": False},
            # Bustle rack: an open frame at turret-side level (below the roof) wrapping the turret rear and
            # overhanging the engine deck; stowage sits inside it.
            # Bustle rack ~1.1 m deep behind the turret, ending ~0.45 m short of the hull rear (formation and
            # Australian side photos); open rails, stowage inside.
            {"name": "Rack_Rail_L", "size": (0.05, 1.30, 0.05), "at": (-1.24, -3.20, 0.74)},
            {"name": "Rack_Rail_R", "size": (0.05, 1.30, 0.05), "at": (1.24, -3.20, 0.74)},
            {"name": "Rack_Rear", "size": (2.54, 0.05, 0.40), "at": (0.0, -3.84, 0.56)},
            {"name": "Rack_Floor", "size": (2.48, 1.20, 0.04), "at": (0.0, -3.24, 0.36)},
            {"name": "Stowage_Bedroll_L", "size": (0.95, 0.70, 0.36), "at": (-0.70, -3.35, 0.56)},
            {"name": "Stowage_Bedroll_R", "size": (0.95, 0.70, 0.30), "at": (0.75, -3.40, 0.53)},
            {"name": "Stowage_Box", "size": (0.55, 0.45, 0.30), "at": (0.05, -3.10, 0.53)},
        ],
    },
    "gun": {
        "trunnion": (0.0, 1.55, 0.45),
        # Mantlet recessed in its slot between the cheeks, its top below the cheek/roof line.
        "mantlet_size": (0.56, 0.40, 0.34),
        "mantlet_at": (0.0, 0.05, 0.0),
        # Muzzle at hull y = -HALF + 9.83 = 5.872 -> turret-local 5.522 -> gun-local 3.97.
        "sections": [(0.0, 0.17), (0.45, 0.17), (0.46, 0.09), (2.15, 0.088), (2.20, 0.118), (2.80, 0.118), (2.86, 0.083), (3.95, 0.080), (3.972, 0.076)],
        "segments": 16,
    },
    "hull_parts": [
        # AGT1500 louvered rear grille and the rear deck grilles (overhead and rear photos).
        {"name": "Grille_Rear", "size": (2.30, 0.06, 0.48), "at": (0.0, -HALF - 0.03, 1.04), "paint": False},
        {"name": "Grille_Deck_L", "size": (0.95, 1.30, 0.03), "at": (-0.62, -3.05, 1.435), "paint": False},
        {"name": "Grille_Deck_R", "size": (0.95, 1.30, 0.03), "at": (0.62, -3.05, 1.435), "paint": False},
        # Hinged front skirt section: its own plate, set slightly proud of the main skirt line.
        # Hinged front skirt: its lower edge angles up toward the sloped nose.
        # Hinged front skirt: its own plate standing 3 cm proud of the main skirt; its angled lower edge
        # reaches lower and further forward than the main skirt line; its leading edge slants back to the fender.
        {"name": "Skirt_Front_L", "x": (-1.83, -1.79), "profile": [(HALF - 1.38, 0.60), (HALF - 0.45, 0.55), (HALF - 0.04, 0.80), (HALF - 0.32, 1.38), (HALF - 1.38, 1.40)]},
        {"name": "Skirt_Front_R", "x": (1.79, 1.83), "profile": [(HALF - 1.38, 0.60), (HALF - 0.45, 0.55), (HALF - 0.04, 0.80), (HALF - 0.32, 1.38), (HALF - 1.38, 1.40)]},
    ],
    "sockets": {
        "Exhaust_0": (0.0, -HALF - 0.02, 1.18),
        "Optic_GPS": (0.98, 1.27, 1.30),
        "Hatch_Cmd": (0.62, -0.05, 1.24),
        "Hatch_Loader": (-0.68, 0.05, 1.07),
        "Hatch_Driver": (0.0, 2.65, 1.30),
    },
}
