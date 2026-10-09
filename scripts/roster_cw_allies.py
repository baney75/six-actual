"""Cold War 1989 allied factions for scripts/generate_roster.py (ERA-01): West Germany, United Kingdom, France,
East Germany, Poland.

Research: assets_src/research/cw1989_allies_sources.json. Penetration figures for the 1980s NATO tank rounds and
MILAN/HOT are not in the fetched sources: they are design values (marked). NATION_VARIANTS copy a sourced unit of
another nation (equipment the faction really fielded) under the faction's nation.
"""
from __future__ import annotations

FCS_GEN2 = [[0, 92], [1000, 85], [2000, 65], [3000, 35], [3700, 20]]
FCS_GEN3 = [[0, 95], [1000, 90], [2000, 75], [3000, 50], [4000, 20]]


def ke(p2000: int) -> list[list[int]]:
    return [[0, round(p2000 * 1.07)], [2000, p2000], [4000, round(p2000 * 0.93)]]


WEAPONS_CWA = {
    "rh120_dm33": dict(type="ke", range_max_m=4000, pen=ke(480), accuracy=FCS_GEN3, reload_ticks=120, damage=110, suppression=25, design="Rh-120 with DM33 APFSDS: pen design"),
    "l7a3_105": dict(type="ke", range_max_m=3500, pen=ke(390), accuracy=FCS_GEN2, reload_ticks=130, damage=95, suppression=25, design="105 mm L7A3 with DM23/DM33 APFSDS: pen design"),
    "l11a5_120": dict(type="ke", range_max_m=4000, pen=ke(450), accuracy=FCS_GEN3, reload_ticks=130, damage=110, suppression=25, design="L11A5 120 mm rifled with L26 APFSDS: pen design"),
    "cn105_f1": dict(type="ke", range_max_m=3500, pen=ke(400), accuracy=FCS_GEN2, reload_ticks=130, damage=95, suppression=25, design="CN-105-F1 with OFL 105 F1 APFSDS (B2, 1982): pen design"),
    "f2_105_meca": dict(type="ke", range_max_m=3000, pen=ke(330), accuracy=FCS_GEN2, reload_ticks=130, damage=90, suppression=25, design="105 mm F2 medium-pressure APFSDS: pen design"),
    "2a46_3bm22": dict(type="ke", range_max_m=3500, pen=ke(400), accuracy=FCS_GEN2, reload_ticks=140, damage=100, suppression=25, design="2A46 with 3BM22 (export-grade APFSDS): pen design"),
    "rh202_20mm": dict(type="autocannon", range_max_m=2000, pen=[[0, 45], [1000, 32], [2000, 20]], accuracy=[[0, 75], [1000, 55], [2000, 25]], reload_ticks=40, salvo=3, damage=20, suppression=22, design="20 mm Rh 202 (Marder): pen design"),
    "m693_20mm": dict(type="autocannon", range_max_m=2000, pen=[[0, 45], [1000, 32], [2000, 20]], accuracy=[[0, 75], [1000, 55], [2000, 25]], reload_ticks=40, salvo=3, damage=20, suppression=22, design="20 mm M693 (AMX-10P, 800 rounds): pen design"),
    "rarden_30mm": dict(type="autocannon", range_max_m=1500, pen=[[0, 50], [1000, 40], [1500, 32]], accuracy=[[0, 80], [1000, 62], [1500, 40]], reload_ticks=50, salvo=3, damage=25, suppression=25, design="30 mm L21 RARDEN, 1,500 m effective (Wikipedia Warrior): pen design"),
    "milan2": dict(type="atgm", range_min_m=200, range_max_m=2000, pen=[[0, 790]], accuracy=[[200, 88], [2000, 82]], reload_ticks=360, damage=120, suppression=35, design="MILAN 2 (1984): 2 km, wire SACLOS (Wikipedia MILAN); pen design"),
    "hot2": dict(type="atgm", range_min_m=75, range_max_m=4000, pen=[[0, 900]], accuracy=[[75, 88], [4000, 82]], reload_ticks=300, damage=120, suppression=35, design="HOT 2 (Gazelle): range and pen design"),
    "malyutka_9m14": dict(type="atgm", range_min_m=500, range_max_m=3000, pen=[[0, 400]], accuracy=[[500, 70], [3000, 65]], reload_ticks=400, damage=100, suppression=30, design="9M14 Malyutka (MCLOS): pen design"),
    "oerlikon_35mm": dict(type="aaa", range_max_m=5500, pen=[[0, 70], [2000, 50]], accuracy=[[0, 60], [5500, 15]], reload_ticks=20, salvo=4, damage=25, suppression=25, hits_air=True,
                          design="2 x 35 mm Oerlikon KDA, 5.5 km, radar-directed (Wikipedia Gepard); per-burst design"),
    "rapier_missile": dict(type="sam", range_min_m=400, range_max_m=8200, pen=[[0, 50]], accuracy=[[400, 75], [8200, 50]], reload_ticks=300, damage=80, suppression=10, hits_air=True, hits_ground=False,
                           design="Rapier FSB: 0.4-8.2 km, SACLOS (Wikipedia Rapier); accuracy design"),
    "roland2": dict(type="sam", range_min_m=500, range_max_m=8000, pen=[[0, 50]], accuracy=[[500, 75], [8000, 50]], reload_ticks=200, damage=80, suppression=10, hits_air=True, hits_ground=False,
                    design="Roland 2: 8 km, 5.5 km ceiling (Wikipedia Roland); accuracy design"),
    "gct_155": dict(type="gun_artillery", range_min_m=2000, range_max_m=23000, pen=[[0, 0]], accuracy=[[0, 100]], reload_ticks=150, damage=0, blast_damage=200, blast_radius_m=30, suppression=80, tof_m_s=500, cep_permille=5,
                    design="GCT 155 mm: 23 km, 8 rpm (Wikipedia GCT 155mm); blast/CEP design"),
    "aden_25mm": dict(type="autocannon", range_max_m=1500, pen=[[0, 45], [1000, 30]], accuracy=[[0, 60], [1000, 35]], reload_ticks=40, salvo=4, damage=22, suppression=30, design="2 x 25 mm ADEN (Harrier GR5); design"),
    "bk27_27mm": dict(type="autocannon", range_max_m=1500, pen=[[0, 45], [1000, 30]], accuracy=[[0, 60], [1000, 35]], reload_ticks=40, salvo=4, damage=24, suppression=30, design="27 mm BK-27 pod, 150 rounds (Wikipedia Alpha Jet); design"),
    "nr30_30mm": dict(type="autocannon", range_max_m=1500, pen=[[0, 45], [1000, 30]], accuracy=[[0, 55], [1000, 30]], reload_ticks=40, salvo=4, damage=25, suppression=30, design="2 x 30 mm NR-30 (Su-22M4); design"),
}

WEAPON_YEARS_CWA = {
    "rh120_dm33": (1987, "DM33, 1987; design"), "l7a3_105": (1965, "L7A3 on the Leopard 1"), "l11a5_120": (1983, "Challenger 1, 1983"),
    "cn105_f1": (1982, "OFL 105 F1 with the AMX-30B2, 1982"), "f2_105_meca": (1979, "AMX-10 RC, 1979"), "2a46_3bm22": (1976, "3BM22, 1976; design"),
    "rh202_20mm": (1971, "Marder 1, 1971"), "m693_20mm": (1973, "AMX-10P, 1973"), "rarden_30mm": (1974, "RARDEN with Scimitar, 1974"),
    "milan2": (1984, "MILAN 2, 1984"), "hot2": (1985, "HOT 2, 1985; design"), "malyutka_9m14": (1963, "9M14, 1963"),
    "oerlikon_35mm": (1976, "Gepard, 1976"), "rapier_missile": (1979, "Rapier with Blindfire, 1979"), "roland2": (1981, "Roland 2 in French service, 1981"),
    "gct_155": (1977, "GCT, 1977"), "aden_25mm": (1987, "Harrier GR5, 1987"), "nr30_30mm": (1984, "Su-22M4, 1984"), "bk27_27mm": (1979, "Alpha Jet A, 1979"),
}

UNITS_CWA = {
    "leopard2a4": ([("rh120_dm33", 42), ("mg_762", 4750)], dict(armor="A", side=80, top=30, why="protection estimates from the Leopard 2 article")),
    "leopard1a5": ([("l7a3_105", 55), ("mg_762", 5500)], dict(armor=(150, 150, 35, 35, 25, 20), why="10-70 mm steel (Wikipedia Leopard 1): front 70 mm sloped turret/hull -> ~150 mm LOS, design")),
    "marder1a3": ([("rh202_20mm", 1250), ("milan2", 4), ("mg_762", 5000)], dict(armor=(45, 45, 25, 25, 20, 15), why="proof vs 20-25 mm APDS (Wikipedia Marder): design figures")),
    "gepard": ([("oerlikon_35mm", 640)], dict(armor=(70, 70, 30, 30, 20, 15), why="Leopard 1 hull: design")),
    "frg_pzgren_squad": ([("rifle_section", 1000), ("lmg_section", 1500), ("law_m72", 2)], dict(members=6, speed_kmh=5, stealth=120, optics_m=1500, size_pct=25, why="G3/MG3 section; Panzerfaust 44 modelled with the LAW, design")),
    "frg_milan_team": ([("milan2", 6)], dict(members=2, speed_kmh=4, stealth=180, optics_m=2500, size_pct=15, front_weight=0, why="2-man team; 6 missiles design")),
    "challenger1": ([("l11a5_120", 64), ("mg_762", 4000)], dict(armor="A", side=80, top=30, why="Chobham: design estimates (source gives none)")),
    "warrior_fv510": ([("rarden_30mm", 234), ("mg_762", 2000)], dict(armor=(40, 40, 25, 25, 20, 15), why="aluminium + appliqué, small-arms proof all round (Wikipedia Warrior): design")),
    "scimitar_fv107": ([("rarden_30mm", 165), ("mg_762", 3000)], dict(armor=(20, 20, 12, 12, 10, 10), why="aluminium armour: design")),
    "rapier_fsb": ([("rapier_missile", 4)], dict(hp=50, armor=(0, 0, 0, 0, 0, 0), speed_kmh=30, move_class="wheeled", why="towed by a Land Rover: speed design")),
    "uk_infantry_section_1989": ([("rifle_section", 1000), ("lmg_section", 1000), ("at4_m136", 2)], dict(members=7, speed_kmh=5, stealth=120, optics_m=1500, size_pct=25, why="SA80/LSW section; LAW 80 modelled with the AT4, design")),
    "uk_milan_team": ([("milan2", 6)], dict(members=2, speed_kmh=4, stealth=180, optics_m=2500, size_pct=15, front_weight=0, why="2-man team")),
    "harrier_gr5": ([("aden_25mm", 200), ("bombs_500lb", 6)], dict(hp=60, armor=(5, 5, 5, 5, 5, 5), optics_m=6000, size_pct=55, why="hp design")),
    "amx30b2": ([("cn105_f1", 50), ("mg_762", 2000)], dict(armor=(134, 134, 35, 35, 30, 20), why="glacis 80 mm at 68 deg (213 LOS), turret 80.8 mm (Wikipedia AMX-30): 60% turret + 40% hull")),
    "amx10p": ([("m693_20mm", 800), ("mg_762", 2000)], dict(armor=(30, 30, 20, 20, 15, 10), why="aluminium: design")),
    "amx10rc": ([("f2_105_meca", 38), ("mg_762", 4000)], dict(armor=(30, 30, 15, 15, 12, 10), why="front proof vs 23 mm API at 300 m (Wikipedia AMX-10 RC): design figure")),
    "vab": ([("mg_762", 2000)], dict(armor=(12, 12, 10, 10, 8, 6), why="proof vs 7.62 mm (Wikipedia VAB): design")),
    "roland_amx30": ([("roland2", 10)], dict(armor=(80, 80, 35, 35, 25, 20), why="AMX-30 chassis: design")),
    "auf1": ([("gct_155", 42), ("mg_762", 2000)], dict(armor=(30, 30, 20, 20, 15, 15), why="rounds and armor design")),
    "fr_infantry_squad_1989": ([("rifle_section", 1000), ("lmg_section", 1000), ("at4_m136", 2)], dict(members=8, speed_kmh=5, stealth=120, optics_m=1500, size_pct=25, why="FAMAS/AA52 group; LRAC 89 modelled with the AT4, design")),
    "fr_milan_team": ([("milan2", 6)], dict(members=2, speed_kmh=4, stealth=180, optics_m=2500, size_pct=15, front_weight=0, why="2-man team")),
    "gazelle_hot": ([("hot2", 4)], dict(hp=50, armor=(4, 4, 4, 4, 4, 4), optics_m=6000, size_pct=50, why="hp design")),
    "t72m1_nva": ([("2a46_3bm22", 44), ("mg_762", 2000)], dict(armor="A", side=80, top=30, why="protection design (sources give none)")),
    "bmp1_nva": ([("2a28_pg15v", 40), ("malyutka_9m14", 4), ("mg_762", 2000)], dict(armor=(33, 33, 17, 17, 16, 8), why="6-33 mm steel (Wikipedia BMP-1)")),
    "spw70": ([("kpvt_145", 500), ("mg_762", 2000)], dict(armor=(10, 10, 7, 7, 7, 5), why="9 mm front, 7 mm sides (Wikipedia BTR-70)")),
    "nva_mot_rifle_squad": ([("rifle_section", 1200), ("lmg_section", 1200), ("rpg7_pg7vl", 4)], dict(members=8, speed_kmh=5, stealth=120, optics_m=1500, size_pct=25, why="8 dismounts; RPG-7")),
    "w50_truck": ([], dict(supply=2000, hp=55, armor=(0, 0, 0, 0, 0, 0), why="5.3 t payload: supply design")),
    "t72m1_pl": ([("2a46_3bm22", 44), ("mg_762", 2000)], dict(armor="A", side=80, top=30, why="protection design")),
    "bwp1": ([("2a28_pg15v", 40), ("malyutka_9m14", 4), ("mg_762", 2000)], dict(armor=(33, 33, 17, 17, 16, 8), why="6-33 mm steel (Wikipedia BMP-1)")),
    "skot_ot64": ([("kpvt_145", 500), ("malyutka_9m14", 4)], dict(armor=(13, 13, 10, 10, 8, 6), why="6-13 mm (Wikipedia OT-64)")),
    "pl_infantry_squad": ([("rifle_section", 1200), ("lmg_section", 1200), ("rpg7_pg7vl", 4)], dict(members=8, speed_kmh=5, stealth=120, optics_m=1500, size_pct=25, why="8 dismounts; RPG-7")),
    "su22m4": ([("nr30_30mm", 160), ("kh25_kh29", 2), ("bombs_500lb", 6)], dict(hp=70, armor=(5, 5, 5, 5, 5, 5), optics_m=6000, size_pct=70, why="hp design")),
    "man_kat1": ([], dict(armor=(5, 5, 4, 4, 4, 3), supply=2000, why="supply truck; design armor and capacity")),
    "bedford_mk": ([], dict(armor=(5, 5, 4, 4, 4, 3), supply=1600, why="supply truck; design armor and capacity")),
    "renault_trm10000": ([], dict(armor=(5, 5, 4, 4, 4, 3), supply=2400, why="10 t truck; design armor and capacity")),
    "star266": ([], dict(armor=(5, 5, 4, 4, 4, 3), supply=1400, why="supply truck; design armor and capacity")),
    "pah1": ([("hot2", 6)], dict(hp=45, armor=(4, 4, 4, 4, 4, 4), optics_m=6000, size_pct=50, why="6 HOT (Wikipedia Bo 105); hp design")),
    "alpha_jet_a": ([("bk27_27mm", 150), ("bombs_500lb", 4)], dict(hp=55, armor=(5, 5, 5, 5, 5, 5), optics_m=6000, size_pct=50, why="hp and bomb load design")),
}

CARRIERS_CWA = {
    "frg_pzgren_squad": ("marder1a3", "Marder 1 carries 6 dismounts"),
    "uk_infantry_section_1989": ("warrior_fv510", "Warrior section"),
    "fr_infantry_squad_1989": ("vab", "VAB section"),
    "nva_mot_rifle_squad": ("bmp1_nva", "BMP-1 squad"),
    "pl_infantry_squad": ("bwp1", "BWP-1 squad"),
}

# id: (base unit id, nation, why) - the same equipment fielded by another army.
NATION_VARIANTS = {
    "m109a3g": ("m109a3_sph", "West Germany", "M109A3G of the Bundeswehr artillery"),
    "m113_bw": ("m113a2", "West Germany", "Bundeswehr M113 (Panzermörser and carriers)"),
    "m109_uk": ("m109a3_sph", "United Kingdom", "British Army M109A2/A3 regiments in BAOR"),
    "2s1_nva": ("2s1_gvozdika", "East Germany", "NVA 2S1 Gvozdika"),
    "zsu23_4_nva": ("zsu23_4_shilka", "East Germany", "NVA ZSU-23-4"),
    "mi24_nva": ("mi24v_hind", "East Germany", "NVA Mi-24"),
    "m577_bw": ("m577a2_cp", "West Germany", "Bundeswehr M577 command post carrier"),
    "mig29_nva": ("mig29_9_12", "East Germany", "NVA MiG-29, delivered from 1988"),
    "su22m4_nva": ("su22m4", "East Germany", "NVA Su-22M4 (JBG 77, 1984)"),
    "mi8_nva": ("mi8mt_hip", "East Germany", "NVA Mi-8"),
    "mig29_pl": ("mig29_9_12", "Poland", "Polish MiG-29, delivered 1989"),
    "mi8_pl": ("mi8mt_hip", "Poland", "Polish Mi-8"),
    "ural4320_pl": ("ural4320", "Poland", "Polish Army Ural-4320"),
    "2s1_pl": ("2s1_gvozdika", "Poland", "Polish 2S1 Goździk"),
    "zsu23_4_pl": ("zsu23_4_shilka", "Poland", "Polish ZSU-23-4"),
    "mi24_pl": ("mi24v_hind", "Poland", "Polish Mi-24"),
}

# Thermal sights fielded by 1989: Leopard 2A4 (WBG-X), Challenger 1 (TOGS), MILAN posts with the MIRA thermal sight.
THERMAL_CWA = {"leopard2a4", "challenger1", "frg_milan_team", "uk_milan_team", "fr_milan_team"}
