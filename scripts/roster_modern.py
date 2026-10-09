"""Modern 2026 tables for scripts/generate_roster.py (ERA-01 slice: US Army vs Russian Ground Forces).

Research: assets_src/research/modern_2026_roster_sources.json. Weapon figures cite the Wikipedia articles fetched
2026-10-09; "design" marks values the sources do not give. `top_attack` weapons strike the target's top armour
(Javelin, loitering munitions; SUP-01 / ERA-01).
"""
from __future__ import annotations

FCS_MODERN = [[0, 96], [1000, 92], [2000, 82], [3000, 60], [4000, 30]]


def ke(p2000: int) -> list[list[int]]:
    return [[0, round(p2000 * 1.07)], [2000, p2000], [4000, round(p2000 * 0.93)]]


WEAPONS_MODERN = {
    "m256_m829a4": dict(type="ke", range_max_m=4000, pen=ke(700), accuracy=FCS_MODERN, reload_ticks=110, damage=120, suppression=25,
                        design="M829A3 680-720 mm (Wikipedia M829, Lanz-Odermatt); A4 not public, taken as A3: design"),
    "2a46m5_svinets": dict(type="ke", range_max_m=4000, pen=ke(600), accuracy=FCS_MODERN, reload_ticks=130, damage=115, suppression=25,
                           design="3BM60 Svinets-2: 300 mm/60 deg at 2 km (Wikipedia 2A46) -> 600 mm geometric line of sight: design conversion"),
    "refleks_9m119m": dict(type="atgm", range_min_m=100, range_max_m=5000, pen=[[0, 750]], accuracy=[[100, 85], [5000, 78]], reload_ticks=400, damage=110, suppression=30,
                           design="9M119M Refleks tandem HEAT, 6 km max (Wikipedia T-90); pen design"),
    "m242_m919": dict(type="autocannon", range_max_m=2500, pen=[[0, 100], [1000, 80], [2000, 60]], accuracy=[[0, 85], [1000, 65], [2000, 35]], reload_ticks=40, salvo=3, damage=25, suppression=25, blast_damage=10, blast_radius_m=4,
                      design="25 mm M919 APFSDS-T; pen design"),
    "tow2b": dict(type="atgm", range_min_m=65, range_max_m=3750, pen=[[0, 900]], accuracy=[[65, 90], [3750, 85]], reload_ticks=360, damage=120, suppression=35, top_attack=True,
                  design="TOW-2B flies over and fires down (top attack); pen design"),
    "2a72_30mm": dict(type="autocannon", range_max_m=2000, pen=[[0, 60], [1000, 48], [2000, 32]], accuracy=[[0, 78], [1000, 58], [2000, 28]], reload_ticks=40, salvo=3, damage=25, suppression=25, blast_damage=10, blast_radius_m=4,
                      design="30 mm 2A72 (BMP-3, BTR-82A); pen design"),
    "2a70_bastion": dict(type="atgm", range_min_m=100, range_max_m=5500, pen=[[0, 600]], accuracy=[[100, 85], [5500, 75]], reload_ticks=400, damage=110, suppression=30,
                         design="9M117 Bastion gun-launched ATGM (8 carried, Wikipedia BMP-3); pen design"),
    "m2hb_modern": dict(type="mg", range_max_m=1800, pen=[[0, 25], [1000, 12]], accuracy=[[0, 50], [1000, 28], [1800, 12]], reload_ticks=30, salvo=3, damage=10, suppression=20, design="M2HB in a remote weapon station; design"),
    "kord_127": dict(type="mg", range_max_m=1800, pen=[[0, 25], [1000, 12]], accuracy=[[0, 50], [1000, 28], [1800, 12]], reload_ticks=30, salvo=3, damage=10, suppression=20, design="12.7 mm Kord (T-90M RWS); design"),
    "m284_155_he": dict(type="gun_artillery", range_min_m=2000, range_max_m=21000, pen=[[0, 0]], accuracy=[[0, 100]], reload_ticks=600, damage=0, blast_damage=200, blast_radius_m=30, suppression=80, tof_m_s=500, cep_permille=5,
                        design="M284 L/39, HE to 21 km (Wikipedia M109); blast/CEP design"),
    "2a64_152_he": dict(type="gun_artillery", range_min_m=2000, range_max_m=24700, pen=[[0, 0]], accuracy=[[0, 100]], reload_ticks=120, damage=0, blast_damage=190, blast_radius_m=30, suppression=80, tof_m_s=500, cep_permille=5,
                        design="2A64 L/47, HE to 24.7 km, 10 rpm (Wikipedia 2S19 Msta); blast/CEP design"),
    "gmlrs_m31": dict(type="rocket_artillery", range_min_m=15000, range_max_m=92000, pen=[[0, 0]], accuracy=[[0, 100]], reload_ticks=6000, salvo=6, damage=0, blast_damage=250, blast_radius_m=40, suppression=90, tof_m_s=900, cep_permille=0,
                      design="GMLRS M31: 15-92 km, 91 kg unitary warhead, GPS (~1 m) (Wikipedia M142 HIMARS); blast design"),
    "tornado_s_300": dict(type="rocket_artillery", range_min_m=20000, range_max_m=120000, pen=[[0, 0]], accuracy=[[0, 100]], reload_ticks=6000, salvo=12, damage=0, blast_damage=250, blast_radius_m=45, suppression=90, tof_m_s=900, cep_permille=1,
                          design="9M542 300 mm, GLONASS-guided, 120 km (Wikipedia Tornado MLRS); blast/CEP design"),
    "pantsir_57e6": dict(type="sam", range_min_m=1000, range_max_m=18000, pen=[[0, 50]], accuracy=[[1000, 80], [18000, 55]], reload_ticks=200, damage=90, suppression=10, hits_air=True, hits_ground=False,
                         design="57E6: 18 km, 12 carried (Wikipedia Pantsir); accuracy design"),
    "pantsir_2a38m": dict(type="aaa", range_max_m=4000, pen=[[0, 40]], accuracy=[[0, 60], [4000, 20]], reload_ticks=20, salvo=6, damage=22, suppression=25, hits_air=True,
                          design="2 x 2A38M 30 mm, 4 km, radar-directed (Wikipedia Pantsir); design"),
    "tor_9m338": dict(type="sam", range_min_m=1000, range_max_m=16000, pen=[[0, 50]], accuracy=[[1000, 82], [16000, 55]], reload_ticks=150, damage=90, suppression=10, hits_air=True, hits_ground=False,
                      design="9M338: 16 km, 16 carried (Wikipedia Tor); accuracy design"),
    "stinger_modern": dict(type="sam", range_min_m=200, range_max_m=4800, pen=[[0, 50]], accuracy=[[200, 75], [4800, 50]], reload_ticks=300, damage=70, suppression=10, hits_air=True, hits_ground=False,
                           design="FIM-92 Stinger (M-SHORAD: 4) (CRS IF12397); accuracy design"),
    "xm914_30mm": dict(type="aaa", range_max_m=3000, pen=[[0, 60], [1500, 40]], accuracy=[[0, 65], [3000, 25]], reload_ticks=30, salvo=4, damage=22, suppression=25, hits_air=True,
                       design="XM914 30 mm with the multi-mission radar (CRS IF12397); design"),
    "hellfire_jagm": dict(type="atgm", range_min_m=500, range_max_m=8000, pen=[[0, 900]], accuracy=[[500, 92], [8000, 88]], reload_ticks=160, damage=120, suppression=35,
                          design="Longbow Hellfire / JAGM; pen design"),
    "m230_30mm_e": dict(type="autocannon", range_max_m=3000, pen=[[0, 25], [1500, 15]], accuracy=[[0, 60], [1500, 30], [3000, 10]], reload_ticks=40, salvo=4, damage=20, suppression=30, blast_damage=15, blast_radius_m=4,
                        design="M230 30 mm (AH-64E); design"),
    "vikhr": dict(type="atgm", range_min_m=500, range_max_m=8000, pen=[[0, 900]], accuracy=[[500, 88], [8000, 82]], reload_ticks=200, damage=120, suppression=35,
                  design="Vikhr laser-guided, 8 km, 12 carried (Wikipedia Ka-50/52); pen design"),
    "2a42_30mm_heli": dict(type="autocannon", range_max_m=2500, pen=[[0, 55], [1500, 35]], accuracy=[[0, 60], [1500, 30]], reload_ticks=40, salvo=4, damage=25, suppression=30,
                           design="2A42 30 mm (460 rounds, Ka-52); design"),
    "gau22_25mm": dict(type="autocannon", range_max_m=2000, pen=[[0, 50], [1000, 35]], accuracy=[[0, 60], [1000, 35]], reload_ticks=40, salvo=4, damage=20, suppression=30, hits_air=True,
                       design="GAU-22/A 25 mm (182 rounds) (Wikipedia F-35); design"),
    "jdam_gbu31": dict(type="he", range_max_m=15000, pen=[[0, 300]], accuracy=[[0, 95], [15000, 90]], reload_ticks=200, salvo=1, damage=300, blast_damage=300, blast_radius_m=45, suppression=100,
                       design="GBU-31 JDAM, GPS-guided 2,000 lb; stand-off and blast design"),
    "aim120": dict(type="aam", range_min_m=1000, range_max_m=40000, pen=[[0, 50]], accuracy=[[1000, 75], [40000, 40]], reload_ticks=300, damage=90, suppression=10, hits_air=True, hits_ground=False,
                   design="AIM-120 AMRAAM; accuracy design"),
    "gsh301_30mm": dict(type="autocannon", range_max_m=1800, pen=[[0, 45], [1000, 30]], accuracy=[[0, 55], [1000, 30]], reload_ticks=40, salvo=3, damage=25, suppression=30,
                        design="GSh-30-1 (180 rounds, Su-34); design"),
    "fab500_umpk": dict(type="he", range_max_m=40000, pen=[[0, 200]], accuracy=[[0, 85], [40000, 60]], reload_ticks=300, salvo=1, damage=300, blast_damage=300, blast_radius_m=50, suppression=100,
                        design="FAB-500 with UMPK glide kit (Wikipedia Su-34): stand-off range and accuracy design"),
    "us_rifles_modern": dict(type="smallarms", range_max_m=550, pen=[[0, 5]], accuracy=[[0, 35], [300, 18], [550, 7]], reload_ticks=20, salvo=3, damage=10, suppression=12, design="M4/M7 section fire with optics; design"),
    "m249_saw": dict(type="mg", range_max_m=800, pen=[[0, 6]], accuracy=[[0, 38], [500, 19], [800, 8]], reload_ticks=20, salvo=3, damage=10, suppression=16, design="M249 (2 per squad); design"),
    "m320_40mm": dict(type="he", range_max_m=400, pen=[[0, 0]], accuracy=[[0, 50], [400, 22]], reload_ticks=60, damage=10, blast_damage=22, blast_radius_m=5, suppression=20, design="M320 (2 per squad); design"),
    "rus_rifles_modern": dict(type="smallarms", range_max_m=550, pen=[[0, 5]], accuracy=[[0, 32], [300, 16], [550, 6]], reload_ticks=20, salvo=3, damage=10, suppression=12, design="AK-12/AK-74M section fire; design"),
    "pkm_modern": dict(type="mg", range_max_m=1000, pen=[[0, 8]], accuracy=[[0, 40], [600, 20], [1000, 8]], reload_ticks=20, salvo=4, damage=10, suppression=18, design="PKM / Pecheneg; design"),
    "at4_modern": dict(type="heat", range_max_m=300, pen=[[0, 500]], accuracy=[[0, 60], [300, 25]], reload_ticks=200, damage=90, suppression=25, design="M136 AT4 (HEAT, 300 m vs point targets), squad-issued; pen design"),
    "rpg7_tandem": dict(type="heat", range_max_m=300, pen=[[0, 650]], accuracy=[[0, 60], [300, 25]], reload_ticks=160, damage=90, suppression=25, design="RPG-7 with PG-7VR tandem (grenadier); design"),
    "javelin_fgm148": dict(type="atgm", range_min_m=65, range_max_m=4000, pen=[[0, 760]], accuracy=[[65, 92], [4000, 90]], reload_ticks=400, damage=130, suppression=35, top_attack=True,
                           design="FGM-148: 4 km, >760 mm, fire-and-forget top attack (Wikipedia Javelin)"),
    "kornet_9m133": dict(type="atgm", range_min_m=100, range_max_m=5500, pen=[[0, 1200]], accuracy=[[100, 90], [5500, 85]], reload_ticks=360, damage=130, suppression=35,
                         design="9M133 Kornet-E: 5.5 km, 1,200 mm behind ERA, SACLOS (Wikipedia 9M133 Kornet)"),
}

WEAPON_YEARS_MODERN = {
    "m256_m829a4": (2016, "M829A4 fielded mid-2010s; design"), "2a46m5_svinets": (2020, "Svinets-2 with the T-90M, 2020; design"),
    "refleks_9m119m": (2000, "9M119M, 2000s; design"), "m242_m919": (1990, "M919, 1990"), "tow2b": (1992, "TOW-2B, 1992"),
    "2a72_30mm": (1987, "2A72 with the BMP-3, 1987"), "2a70_bastion": (1987, "2A70 with the BMP-3, 1987"),
    "m2hb_modern": (1933, "M2HB, 1933"), "kord_127": (1998, "Kord, 1998"), "m284_155_he": (2015, "M109A7, 2015"),
    "2a64_152_he": (1989, "2A64, 1989"), "gmlrs_m31": (2005, "GMLRS with HIMARS, 2005"), "tornado_s_300": (2016, "Tornado-S, 2016"),
    "pantsir_57e6": (2012, "Pantsir-S1, 2012"), "pantsir_2a38m": (2012, "Pantsir-S1, 2012"), "tor_9m338": (2016, "9M338 with Tor-M2; design"),
    "stinger_modern": (1981, "FIM-92, 1981"), "xm914_30mm": (2021, "M-SHORAD Inc 1, April 2021"), "hellfire_jagm": (2013, "Longbow Hellfire / JAGM; design"),
    "m230_30mm_e": (2013, "AH-64E IOC 2013"), "vikhr": (2011, "Ka-52 service 2011"), "2a42_30mm_heli": (2011, "Ka-52 service 2011"),
    "gau22_25mm": (2016, "F-35A IOC 2016"), "jdam_gbu31": (1997, "JDAM, 1997"), "aim120": (1991, "AIM-120, 1991"),
    "gsh301_30mm": (2014, "Su-34 service 2014"), "fab500_umpk": (2023, "UMPK glide kits in use from 2023; design"),
    "us_rifles_modern": (2020, "current US rifles; design"), "m249_saw": (1984, "M249, 1984"), "m320_40mm": (2009, "M320, 2009"),
    "rus_rifles_modern": (2018, "AK-12, 2018"), "pkm_modern": (1969, "PKM, 1969"), "rpg7_tandem": (1988, "PG-7VR, 1988"), "at4_modern": (1987, "M136 AT4 in US service, 1987"),
    "javelin_fgm148": (1996, "Javelin in service since 1996"), "kornet_9m133": (1998, "Kornet in service 1998"),
}

UNITS_MODERN = {
    "m1a2_sepv3": ([("m256_m829a4", 40), ("m2hb_modern", 900)], dict(armor="A", side=80, top=40, why="SEPv2-group protection estimates")),
    "t90m": ([("2a46m5_svinets", 43), ("refleks_9m119m", 4), ("kord_127", 300)], dict(armor="A", side=80, top=40, why="Relikt-inclusive estimates; Refleks count design")),
    "t72b3m": ([("2a46m5_svinets", 40), ("kord_127", 300)], dict(armor="A", side=70, top=35, why="protection design (no source figure)")),
    "m2a4_bradley": ([("m242_m919", 900), ("tow2b", 7)], dict(armor=(60, 120, 40, 60, 30, 20), why="armor design (spaced laminate + ERA option)")),
    "bmp3": ([("2a70_bastion", 8), ("2a72_30mm", 500)], dict(armor=(45, 45, 20, 20, 15, 10), why="hull front 22 mm steel + 60 mm aluminium ~ 45 mm RHA: design")),
    "m1126_stryker": ([("m2hb_modern", 2000)], dict(armor=(25, 25, 20, 20, 15, 10), why="all-round 14.5 mm protection with ceramic add-on: design")),
    "btr82a": ([("2a72_30mm", 300), ("pkm_modern", 2000)], dict(armor=(12, 12, 10, 10, 8, 7), why="10 mm hull, 7 mm turret (Wikipedia BTR-80)")),
    "m109a7": ([("m284_155_he", 39), ("m2hb_modern", 500)], dict(armor=(30, 30, 20, 20, 15, 15), why="rounds and armor design")),
    "2s19m2": ([("2a64_152_he", 50), ("kord_127", 300)], dict(armor=(30, 30, 20, 20, 15, 15), why="unit of fire 50 rounds (Wikipedia 2S19); armor design")),
    "m142_himars": ([("gmlrs_m31", 6)], dict(armor=(10, 10, 8, 8, 6, 6), why="armored cab: design")),
    "tornado_s": ([("tornado_s_300", 12)], dict(armor=(8, 8, 6, 6, 5, 5), hp=70, why="truck chassis; hp design")),
    "pantsir_s1": ([("pantsir_57e6", 12), ("pantsir_2a38m", 1400)], dict(armor=(10, 10, 8, 8, 6, 6), hp=70, speed_kmh=90, why="KAMAZ-6560: mass, speed, gun rounds design")),
    "tor_m2": ([("tor_9m338", 16)], dict(armor=(15, 15, 10, 10, 8, 8), why="armor design")),
    "m_shorad_inc1": ([("stinger_modern", 4), ("xm914_30mm", 600), ("hellfire_jagm", 2)], dict(armor=(25, 25, 20, 20, 15, 10), hp=70, speed_kmh=97, why="Stryker A1 hull: hp, speed (M1126 figure) design")),
    "ah64e_apache": ([("m230_30mm_e", 1200), ("hellfire_jagm", 8), ("hydra70_modern", 38)], dict(hp=80, armor=(10, 10, 10, 10, 8, 10), speed_kmh=270, why="loadout and speed design (E is 32 km/h faster than the D)")),
    "ka52_alligator": ([("2a42_30mm_heli", 460), ("vikhr", 12), ("s8_rockets", 40)], dict(hp=85, armor=(12, 12, 10, 10, 8, 10), why="armour design")),
    "f35a": ([("gau22_25mm", 182), ("jdam_gbu31", 2), ("aim120", 2)], dict(hp=70, armor=(5, 5, 5, 5, 5, 5), stealth=300, optics_m=12000, size_pct=50, why="low observable: stealth 300 design; internal bay JDAM + AMRAAM")),
    "su34": ([("gsh301_30mm", 180), ("fab500_umpk", 4)], dict(hp=80, armor=(10, 10, 8, 8, 6, 8), optics_m=8000, size_pct=75, why="armoured cockpit; hp design")),
    "us_infantry_squad_2026": ([("us_rifles_modern", 1200), ("m249_saw", 1200), ("m320_40mm", 24), ("at4_modern", 2)], dict(members=9, speed_kmh=5, stealth=130, optics_m=1800, size_pct=25, why="9-man squad with night optics: optics design; 2 AT4 per squad, design")),
    "us_javelin_team": ([("javelin_fgm148", 6)], dict(members=2, speed_kmh=4, stealth=190, optics_m=3000, size_pct=15, front_weight=0, why="CLU thermal sight: optics design; 6 missiles design")),
    "rus_motor_rifle_squad_2026": ([("rus_rifles_modern", 1200), ("pkm_modern", 1000), ("rpg7_tandem", 4)], dict(members=8, speed_kmh=5, stealth=120, optics_m=1500, size_pct=25, why="8-man section; RPG grenadier")),
    "rus_kornet_team": ([("kornet_9m133", 6)], dict(members=2, speed_kmh=4, stealth=180, optics_m=3000, size_pct=15, front_weight=0, why="2-man crew; 6 missiles design")),
    "hemtt_a4": ([], dict(supply=2500, hp=80, armor=(0, 0, 0, 0, 0, 0), why="10 short ton payload: supply design")),
    "kamaz_5350": ([], dict(supply=1800, hp=60, armor=(0, 0, 0, 0, 0, 0), speed_kmh=90, why="6 t payload: supply design; speed design")),
}

EXTRA_WEAPONS_MODERN = {
    "hydra70_modern": dict(type="rocket", range_max_m=6000, pen=[[0, 100]], accuracy=[[0, 45], [3000, 25], [6000, 10]], reload_ticks=40, salvo=4, damage=40, blast_damage=40, blast_radius_m=10, suppression=40, design="Hydra 70 (AH-64E); design"),
    "s8_rockets": dict(type="rocket", range_max_m=4000, pen=[[0, 400]], accuracy=[[0, 45], [2000, 25], [4000, 10]], reload_ticks=40, salvo=4, damage=45, blast_damage=40, blast_radius_m=10, suppression=40, design="S-8 80 mm (Ka-52); design"),
}
EXTRA_WEAPONS_MODERN.update({
    "zpt98_125": dict(type="ke", range_max_m=4000, pen=ke(650), accuracy=FCS_MODERN, reload_ticks=130, damage=115, suppression=25, design="125 mm ZPT-98 APFSDS: pen design"),
    "gp125_atgm": dict(type="atgm", range_min_m=100, range_max_m=5000, pen=[[0, 750]], accuracy=[[100, 85], [5000, 78]], reload_ticks=400, damage=110, suppression=30, design="GP125 gun-launched ATGM, 5 km (Wikipedia Type 99); pen design"),
    "hj73c": dict(type="atgm", range_min_m=500, range_max_m=3000, pen=[[0, 600]], accuracy=[[500, 80], [3000, 75]], reload_ticks=400, damage=100, suppression=30, design="HJ-73C (ZBL-08): pen design"),
    "plz05_155": dict(type="gun_artillery", range_min_m=2000, range_max_m=39000, pen=[[0, 0]], accuracy=[[0, 100]], reload_ticks=120, damage=0, blast_damage=200, blast_radius_m=30, suppression=80, tof_m_s=600, cep_permille=5,
                      design="155 mm L/52: 39 km ERFB-BB, 8-10 rpm (Wikipedia PLZ-05); blast/CEP design"),
    "hq17_missile": dict(type="sam", range_min_m=1500, range_max_m=15000, pen=[[0, 50]], accuracy=[[1500, 80], [15000, 55]], reload_ticks=150, damage=90, suppression=10, hits_air=True, hits_ground=False,
                         design="HQ-17: 1.5-15 km, 8 missiles (Wikipedia HQ-17); accuracy design"),
    "px10a_23mm": dict(type="autocannon", range_max_m=2000, pen=[[0, 35], [1500, 20]], accuracy=[[0, 60], [1500, 30]], reload_ticks=40, salvo=4, damage=20, suppression=30, design="23 mm PX-10A (Z-10); design"),
    "akd10": dict(type="atgm", range_min_m=500, range_max_m=8000, pen=[[0, 900]], accuracy=[[500, 88], [8000, 82]], reload_ticks=200, damage=120, suppression=35, design="AKD-10 laser-guided (up to 16, Wikipedia Z-10): range/pen design"),
    "ls500j": dict(type="he", range_max_m=15000, pen=[[0, 200]], accuracy=[[0, 92], [15000, 85]], reload_ticks=200, salvo=1, damage=250, blast_damage=250, blast_radius_m=40, suppression=100, design="LS-500J laser-guided bomb (J-16): design"),
    "pla_rifles": dict(type="smallarms", range_max_m=550, pen=[[0, 5]], accuracy=[[0, 34], [300, 17], [550, 7]], reload_ticks=20, salvo=3, damage=10, suppression=12, design="QBZ-191 section fire; design"),
    "qjb201": dict(type="mg", range_max_m=800, pen=[[0, 6]], accuracy=[[0, 38], [500, 19], [800, 8]], reload_ticks=20, salvo=3, damage=10, suppression=16, design="QJB-201 LMG; design"),
    "pf98": dict(type="heat", range_max_m=400, pen=[[0, 650]], accuracy=[[0, 60], [400, 25]], reload_ticks=200, damage=95, suppression=25, design="PF-98 120 mm: design"),
    "hj12": dict(type="atgm", range_min_m=100, range_max_m=4000, pen=[[0, 1100]], accuracy=[[100, 92], [4000, 90]], reload_ticks=400, damage=130, suppression=35, top_attack=True,
                 design="HJ-12: 4 km, ~1,100 mm behind ERA, fire-and-forget top attack (Wikipedia HJ-12)"),
})
WEAPONS_MODERN.update(EXTRA_WEAPONS_MODERN)
WEAPON_YEARS_MODERN.update({"hydra70_modern": (1990, "Hydra 70; design"), "s8_rockets": (1984, "S-8 rockets; design"),
    "zpt98_125": (2011, "Type 99A; design"), "gp125_atgm": (2011, "Type 99A; design"), "hj73c": (2008, "ZBL-08, 2008"), "plz05_155": (2008, "PLZ-05, 2008"),
    "hq17_missile": (2013, "HQ-17, 2013"), "px10a_23mm": (2009, "Z-10, 2009"), "akd10": (2009, "Z-10, 2009; design"), "ls500j": (2015, "J-16, 2015"),
    "pla_rifles": (2019, "QBZ-191, 2019"), "qjb201": (2019, "QJB-201; design"), "pf98": (2000, "PF-98; design"), "hj12": (2020, "HJ-12 in service 2020")})
UNITS_MODERN.update({
    "type99a": ([("zpt98_125", 42), ("gp125_atgm", 4), ("m2hb_modern", 300)], dict(armor="A", side=80, top=40, why="CE >1000 mm (Wikipedia Type 99); KE design")),
    "zbd04a": ([("2a70_bastion", 8), ("2a72_30mm", 500)], dict(armor=(45, 45, 25, 25, 15, 10), why="100 mm gun-launcher and 30 mm (same class as BMP-3); front proof vs 30 mm: design")),
    "zbl08": ([("2a72_30mm", 400), ("hj73c", 2)], dict(armor=(40, 40, 20, 20, 15, 10), why="front proof vs 25 mm at 1 km (Wikipedia ZBL-08): design figures; ZPT-99 30 mm = 2A72 class")),
    "plz05": ([("plz05_155", 30), ("m2hb_modern", 500)], dict(armor=(30, 30, 20, 20, 15, 15), why="rounds and armor design")),
    "hq17": ([("hq17_missile", 8)], dict(armor=(15, 15, 10, 10, 8, 8), hp=90, why="Tor-class chassis: hp design")),
    "z10": ([("px10a_23mm", 300), ("akd10", 8)], dict(hp=75, armor=(10, 10, 10, 10, 8, 10), why="loadout design")),
    "j16": ([("gsh301_30mm", 150), ("ls500j", 4)], dict(hp=80, armor=(8, 8, 8, 8, 6, 8), optics_m=10000, size_pct=70, why="hp design")),
    "pla_infantry_squad": ([("pla_rifles", 1200), ("qjb201", 1000), ("pf98", 2)], dict(members=7, speed_kmh=5, stealth=125, optics_m=1700, size_pct=25, why="7 dismounts")),
    "eq2102": ([], dict(armor=(5, 5, 4, 4, 4, 3), supply=1600, why="supply truck; design armor and capacity")),
    "pla_hj12_team": ([("hj12", 6)], dict(members=2, speed_kmh=4, stealth=190, optics_m=3000, size_pct=15, front_weight=0, why="2-man team; 6 missiles design")),
})
THERMAL_MODERN_EXTRA = {"type99a", "zbd04a", "zbl08", "z10", "j16", "pla_hj12_team"}

THERMAL_MODERN_BASE = {"m1a2_sepv3", "t90m", "t72b3m", "m2a4_bradley", "bmp3", "m1126_stryker", "btr82a", "m_shorad_inc1", "ah64e_apache", "ka52_alligator",
                  "f35a", "su34", "us_infantry_squad_2026", "us_javelin_team", "rus_kornet_team"}
THERMAL_MODERN = THERMAL_MODERN_BASE | THERMAL_MODERN_EXTRA
# MEC-09 mounted movement for modern squads (D057), as the Cold War decks.
CARRIERS_MODERN = {
    "pla_infantry_squad": ("zbl08", "ZBL-08 carries 7 dismounts"),
    "us_infantry_squad_2026": ("m1126_stryker", "Stryker ICV carries 9 dismounts (research)"),
    "rus_motor_rifle_squad_2026": ("btr82a", "BTR-82A carries 7 dismounts"),
}
