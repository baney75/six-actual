"""US Army rifle squad, 1989 (9 men, two fire teams) plus an attached platoon RTO, for infantry_kit.py.

Organisation: squad leader; per fire team a team leader (M16A2), automatic rifleman (M249), grenadier
(M16A2/M203) and rifleman (M16A2, M72 LAW). Kit per data/assets/us_rifle_squad_1989.json: M81 woodland BDU,
PASGT helmet (woodland cover, band, foliage) and vest, LC-2 webbing, ALICE pack; the RTO carries the AN/PRC-77
on an ALICE frame with the long whip. Equipment sizes: assets_src/research/us_infantry_1989_dims.json.
Soldier 00 (squad leader, ready) is the close-up subject. Wedge spacing compressed to 2.5 m for the sheets.
"""

SPEC = {
    "seed": 1989,
    "ground": (-9.0, -9.0, 9.0, 6.0),
    "soldiers": [
        {"idx": 0, "role": "SL", "pose": "ready", "weapon": "m16a2", "at": (0.0, 0.0, 0.0), "skin": "skin_a"},
        {"idx": 1, "role": "A_TL", "pose": "aim", "weapon": "m16a2", "at": (-2.5, -2.0, 0.0), "skin": "skin_c", "yaw": 10},
        {"idx": 2, "role": "A_AR", "pose": "prone", "weapon": "m249", "at": (-5.0, -3.0, 0.0), "skin": "skin_b", "yaw": 5, "pack": "none"},
        {"idx": 3, "role": "A_GR", "pose": "kneel", "weapon": "m16a2_m203", "at": (-6.5, -5.5, 0.0), "skin": "skin_a", "yaw": 15},
        {"idx": 4, "role": "A_R", "pose": "walk", "weapon": "m16a2", "at": (-4.0, -6.5, 0.0), "skin": "skin_c", "law": True},
        {"idx": 5, "role": "B_TL", "pose": "ready", "weapon": "m16a2", "at": (2.5, -2.0, 0.0), "skin": "skin_b", "yaw": -10},
        {"idx": 6, "role": "B_AR", "pose": "walk", "weapon": "m249", "at": (5.0, -3.5, 0.0), "skin": "skin_a", "yaw": -5},
        {"idx": 7, "role": "B_GR", "pose": "ready", "weapon": "m16a2_m203", "at": (6.5, -5.5, 0.0), "skin": "skin_c", "yaw": -15},
        {"idx": 8, "role": "B_R", "pose": "kneel", "weapon": "m16a2", "at": (4.0, -6.5, 0.0), "skin": "skin_b", "law": True, "yaw": -20},
        {"idx": 9, "role": "RTO", "pose": "walk", "weapon": "m16a2", "at": (1.2, -3.5, 0.0), "skin": "skin_a", "pack": "radio", "foliage": False},
    ],
}
