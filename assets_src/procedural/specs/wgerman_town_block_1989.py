"""West German town block, 1989 (Hessian village core, after Ober-Erlenbach) for building_kit.py.

Block frame: x along the main street, y into the block (street line y = 0), side street along x = 0.
Footprints sit on the 3 m sim chunk grid and bracket the sourced OSM medians (short 9.3 m, long 12.9 m):
9 x 12 m and 12 x 9 m houses, an 18 x 9 m barn. Roof pitches follow the sourced 35-60 deg range.
Composition from the reference pack:
- Corner Gaststaette (1960s-80s infill, 3 storeys, shop front, sign): us_army_m60_tank_in_german_village
- Eave-to-street Fachwerk house with barn door: ober_erlenbach_ober_erlenbacher_strasse_12
- Hofreite gateway (Hoftor) into a cobbled courtyard: section F signature feature list
- Gable-to-street Fachwerk house, steep roof: ober_erlenbach_ober_erlenbacher_strasse_1/3
- Postwar rendered house with shutters: ober_erlenbach_am_alten_rathaus
- Stone tithe barn with slate mansard and dormers: ober_erlenbach_bornstrasse_18_zehntscheune
"""

SPEC = {
    "buildings": [
        {"name": "Gasthaus", "at": (0.0, 0.0), "nx": 4, "ny": 3, "floors": 3, "style": "infill",
         "roof": {"type": "gable", "pitch": 35, "ridge": "x"}, "door": [1], "shop": True, "party": ["right"],
         "chimney": [2], "gable_windows": True, "balconies": [[1, 1], [1, 2], [2, 1], [2, 2]],
         "street": ["left"]},
        {"name": "FachwerkEave", "at": (12.0, 0.0), "nx": 4, "ny": 3, "floors": 2, "style": "fachwerk",
         "roof": {"type": "gable", "pitch": 52, "ridge": "x"}, "door": [1], "gate": [0], "party": ["left"],
         "chimney": [2], "gable_windows": True, "jetty": 0.35, "dormers": [1, 2]},
        {"name": "FachwerkGable", "at": (28.5, 0.0), "nx": 3, "ny": 5, "floors": 3, "style": "fachwerk",
         "roof": {"type": "gable", "pitch": 55, "ridge": "y"}, "door": [1], "party": ["right"],
         "chimney": [2], "gable_windows": True, "jetty": 0.35, "gable_finish": "fachwerk"},
        {"name": "Gruenderzeit", "at": (37.5, 0.0), "nx": 4, "ny": 3, "floors": 2, "style": "plaster",
         "roof": {"type": "mansard", "lower": 70, "pitch": 30, "ridge": "x"}, "door": [2], "party": ["left"],
         "shutters": True, "chimney": [1], "dormers": [1, 2], "slate_roof": True},
        {"name": "Zehntscheune", "at": (12.0, 24.0), "nx": 6, "ny": 3, "floors": 1, "style": "stone",
         "roof": {"type": "mansard", "lower": 70, "pitch": 32, "ridge": "x"}, "gate": [2], "dormers": [1, 2, 3, 4]},
        # Side street: postwar rendered house with its gable to the side street (front faces -x), slate-hung gable.
        {"name": "Seitenhaus", "at": (0.0, 25.0), "rot": -90, "nx": 4, "ny": 3, "floors": 2, "style": "plaster",
         "roof": {"type": "gable", "pitch": 48, "ridge": "y"}, "door": [1], "shutters": True, "gable_windows": True,
         "gable_finish": "slate"},
        # Rear lot: stable along the east wall, a timber shed, a flat-roofed 1970s garage.
        {"name": "Stall", "at": (43.5, 18.0), "nx": 2, "ny": 5, "floors": 1, "style": "stone",
         "roof": {"type": "gable", "pitch": 45, "ridge": "y"}, "party": ["right"]},
        {"name": "Schuppen", "at": (31.5, 30.0), "nx": 3, "ny": 2, "floors": 1, "style": "fachwerk",
         "roof": {"type": "gable", "pitch": 45, "ridge": "x"}, "party": ["back"], "fachwerk_gf": True},
        {"name": "Garage", "at": (2.0, 28.0), "nx": 2, "ny": 2, "floors": 1, "style": "infill",
         "roof": {"type": "flat"}, "gate": [0, 1]},
    ],
    "gateways": [
        {"name": "Hoftor", "at": (24.0, 0.0), "width": 4.5, "arch": 3.2, "spring": 2.3, "height": 4.2},
    ],
    "walls": [
        {"from": (0.0, 36.0), "to": (49.5, 36.0)},
        {"from": (0.0, 25.0), "to": (0.0, 36.0)},
        {"from": (49.5, 9.0), "to": (49.5, 36.0)},
        {"from": (0.0, 9.0), "to": (0.0, 13.0), "h": 1.4},
        {"from": (30.0, 24.0), "to": (30.0, 36.0), "h": 2.2},
    ],
    "signs": [
        # Gasthaus sign on the corner, bracketed off the main facade at first-floor level.
        {"at": (1.2, 0.0, 3.2)},
    ],
    "ground": [
        {"rect": (-9.0, -9.0, 58.0, -2.0), "mat": "asphalt", "z": -0.12},
        {"rect": (-9.0, -2.0, -2.0, 40.0), "mat": "asphalt", "z": -0.12},
        {"rect": (-2.0, -2.0, 58.0, 0.0), "mat": "pavement", "z": 0.0},
        {"rect": (-2.0, 0.0, 0.0, 40.0), "mat": "pavement", "z": 0.0},
        {"rect": (12.0, 9.0, 37.5, 24.0), "mat": "cobble", "z": 0.0},
        {"rect": (0.0, 0.0, 58.0, 40.0), "mat": "grass", "z": -0.02},
    ],
}
