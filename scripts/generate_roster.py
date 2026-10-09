#!/usr/bin/env python3
"""Generate data/roster.json and data/weapons.json for the Phase 1 Cold War (1989) slice.

Inputs: assets_src/research/cw1989_roster_sources.json (sourced facts, W1 step 1).
This script is the tuning layer on top of those facts. Rules:
  * A researched number is used as-is and its sources travel with the unit.
  * A researched range ("360-380") becomes its midpoint and the field is listed in `estimates`.
  * A missing number comes from the DESIGN tables below; every such field is listed in `design`
    with a reason, so nothing unsourced is presented as sourced.
Accuracy, reload, damage and suppression are game tuning, not real-world data; they live in WEAPONS.

Usage: scripts/generate_roster.py [roster_out weapons_out]
"""
from __future__ import annotations

import json
import math
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
RESEARCH = ROOT / "assets_src" / "research" / "cw1989_roster_sources.json"
RESEARCH_WW2 = ROOT / "assets_src" / "research" / "ww2_1944_roster_sources.json"
sys.path.insert(0, str(Path(__file__).resolve().parent))
from roster_ww2 import CARRIERS_WW2, UNITS_WW2, WEAPON_YEARS_WW2, WEAPONS_WW2  # noqa: E402
from roster_modern import CARRIERS_MODERN, THERMAL_MODERN, UNITS_MODERN, WEAPON_YEARS_MODERN, WEAPONS_MODERN  # noqa: E402
from roster_cw_allies import CARRIERS_CWA, NATION_VARIANTS, THERMAL_CWA, UNITS_CWA, WEAPON_YEARS_CWA, WEAPONS_CWA  # noqa: E402

RESEARCH_CWA = ROOT / "assets_src" / "research" / "cw1989_allies_sources.json"
RESEARCH_MODERN = ROOT / "assets_src" / "research" / "modern_2026_roster_sources.json"

# --------------------------------------------------------------------------- weapons
# pen: (range m, mm RHA). KE tables are anchored on the researched 2000 m figure and lose ~3.5% per km
# (typical APFSDS velocity decay); CE weapons are flat. accuracy: (range m, % vs stationary MBT-size target).
FCS_GEN3 = [[0, 95], [1000, 90], [2000, 75], [3000, 50], [4000, 20]]  # laser RF + ballistic computer + stab
FCS_GEN2 = [[0, 92], [1000, 85], [2000, 65], [3000, 35], [3700, 20]]
ATGM_SACLOS = lambda lo, hi: [[lo, 88], [hi, 82]]  # noqa: E731  wire-guided SACLOS: flat, slight drop at max


def ke(p2000: int) -> list[list[int]]:
    return [[0, round(p2000 * 1.07)], [2000, p2000], [4000, round(p2000 * 0.93)]]


WEAPONS = {
    # Tank guns (src: research file; penetration marked estimate there)
    "m256_m829a1": dict(type="ke", range_max_m=4000, pen=ke(650), accuracy=FCS_GEN3, reload_ticks=120, damage=110, suppression=25),
    "m68_m833": dict(type="ke", range_max_m=3500, pen=ke(420), accuracy=FCS_GEN2, reload_ticks=140, damage=95, suppression=25),
    "2a46_3bm42": dict(type="ke", range_max_m=3700, pen=ke(520), accuracy=FCS_GEN2, reload_ticks=140, damage=105, suppression=25),
    "kobra_9m112": dict(type="atgm", range_min_m=100, range_max_m=4000, pen=[[0, 600]], accuracy=ATGM_SACLOS(100, 4000), reload_ticks=400, damage=110, suppression=30),
    # ATGMs
    "tow2": dict(type="atgm", range_min_m=65, range_max_m=3750, pen=[[0, 900]], accuracy=ATGM_SACLOS(65, 3750), reload_ticks=360, damage=120, suppression=35),
    "dragon2": dict(type="atgm", range_min_m=65, range_max_m=1000, pen=[[0, 610]], accuracy=[[65, 85], [1000, 70]], reload_ticks=400, damage=110, suppression=30),
    "konkurs_9m113": dict(type="atgm", range_min_m=75, range_max_m=4000, pen=[[0, 600]], accuracy=ATGM_SACLOS(75, 4000), reload_ticks=360, damage=110, suppression=30),
    "fagot_9m111m": dict(type="atgm", range_min_m=70, range_max_m=2500, pen=[[0, 600]], accuracy=ATGM_SACLOS(70, 2500), reload_ticks=360, damage=110, suppression=30),
    "shturm_9m114": dict(type="atgm", range_min_m=400, range_max_m=5000, pen=[[0, 560]], accuracy=ATGM_SACLOS(400, 5000), reload_ticks=200, damage=110, suppression=30, hits_air=True),
    "hellfire_agm114a": dict(type="atgm", range_min_m=500, range_max_m=8000, pen=[[0, 750]], accuracy=[[500, 90], [8000, 85]], reload_ticks=160, damage=120, suppression=35, design="pen: no sourced figure in research; 750 mm design value"),
    "maverick_agm65": dict(type="atgm", range_min_m=500, range_max_m=8000, pen=[[0, 1000]], accuracy=[[500, 85], [8000, 75]], reload_ticks=200, damage=130, suppression=40, design="pen design value; research has no figure"),
    "kh25_kh29": dict(type="atgm", range_min_m=500, range_max_m=8000, pen=[[0, 1000]], accuracy=[[500, 85], [8000, 70]], reload_ticks=200, damage=130, suppression=40, design="pen design value; research has no figure"),
    # Autocannon and HMG (design penetration where research is null)
    "m242_25mm": dict(type="autocannon", range_max_m=2500, pen=[[0, 66], [1000, 55], [2000, 40]], accuracy=[[0, 80], [1000, 60], [2000, 30]], reload_ticks=40, salvo=3, damage=25, suppression=25, blast_damage=10, blast_radius_m=4, design="M791 pen: research null; design table"),
    "2a42_30mm": dict(type="autocannon", range_max_m=2000, pen=[[0, 55], [1000, 45], [2000, 30]], accuracy=[[0, 75], [1000, 55], [2000, 25]], reload_ticks=40, salvo=3, damage=25, suppression=25, blast_damage=10, blast_radius_m=4, design="research gives 25 mm for an unspecified round; design table for 3UBR8"),
    "2a28_pg15v": dict(type="heat", range_max_m=1300, pen=[[0, 335]], accuracy=[[0, 70], [800, 40], [1300, 15]], reload_ticks=140, damage=90, suppression=25, design="PG-15V pen: research null; 335 mm commonly cited, unsourced here"),
    "m2hb_50": dict(type="mg", range_max_m=1800, pen=[[0, 25], [1000, 12]], accuracy=[[0, 45], [1000, 25], [1800, 10]], reload_ticks=30, salvo=3, damage=10, suppression=20, design="pen design"),
    "kpvt_145": dict(type="mg", range_max_m=2000, pen=[[0, 32], [1000, 20]], accuracy=[[0, 45], [1000, 25], [2000, 10]], reload_ticks=30, salvo=3, damage=12, suppression=22, design="pen design"),
    "mg_762": dict(type="mg", range_max_m=1100, pen=[[0, 8], [1000, 2]], accuracy=[[0, 40], [600, 20], [1100, 8]], reload_ticks=20, salvo=4, damage=10, suppression=18),
    "gau8_30mm": dict(type="autocannon", range_max_m=1800, pen=[[0, 69], [1200, 50]], accuracy=[[0, 70], [1200, 45], [1800, 25]], reload_ticks=60, salvo=5, damage=35, suppression=40, design="pen design (DU API, top attack)"),
    "gsh30_30mm": dict(type="autocannon", range_max_m=1600, pen=[[0, 50], [1200, 35]], accuracy=[[0, 65], [1200, 40]], reload_ticks=60, salvo=4, damage=30, suppression=35, design="pen design"),
    "m230_30mm": dict(type="autocannon", range_max_m=4000, pen=[[0, 25], [2000, 15]], accuracy=[[0, 60], [2000, 30], [4000, 10]], reload_ticks=40, salvo=4, damage=20, suppression=30, blast_damage=15, blast_radius_m=4, design="pen design (HEDP)"),
    "m61_20mm": dict(type="autocannon", range_max_m=1500, pen=[[0, 30], [1000, 20]], accuracy=[[0, 55], [1000, 30]], reload_ticks=60, salvo=4, damage=20, suppression=25, hits_air=True, design="pen design"),
    # Infantry weapons
    "rifle_section": dict(type="smallarms", range_max_m=550, pen=[[0, 4]], accuracy=[[0, 30], [300, 15], [550, 6]], reload_ticks=20, salvo=3, damage=10, suppression=12),
    "lmg_section": dict(type="mg", range_max_m=800, pen=[[0, 6]], accuracy=[[0, 35], [500, 18], [800, 8]], reload_ticks=20, salvo=3, damage=10, suppression=16),
    "law_m72": dict(type="heat", range_max_m=200, pen=[[0, 300]], accuracy=[[0, 60], [200, 35]], reload_ticks=200, damage=80, suppression=25),
    "at4_m136": dict(type="heat", range_max_m=300, pen=[[0, 450]], accuracy=[[0, 65], [300, 35]], reload_ticks=200, damage=90, suppression=25),
    "rpg7_pg7vl": dict(type="heat", range_max_m=330, pen=[[0, 500]], accuracy=[[0, 60], [330, 25]], reload_ticks=160, damage=90, suppression=25, design="PG-7VL pen: research null; 500 mm design value"),
    "rpg18": dict(type="heat", range_max_m=200, pen=[[0, 300]], accuracy=[[0, 55], [200, 30]], reload_ticks=200, damage=80, suppression=25),
    "gl_40mm": dict(type="he", range_max_m=350, pen=[[0, 0]], accuracy=[[0, 45], [350, 20]], reload_ticks=60, damage=10, blast_damage=20, blast_radius_m=5, suppression=20),
    # Air defence (engagement checked against air only)
    "m168_vulcan": dict(type="aaa", range_max_m=1600, pen=[[0, 25]], accuracy=[[0, 45], [1600, 15]], reload_ticks=40, salvo=4, damage=20, suppression=20, hits_air=True),
    "zsu23_amur": dict(type="aaa", range_max_m=2500, pen=[[0, 30]], accuracy=[[0, 50], [2500, 15]], reload_ticks=40, salvo=4, damage=22, suppression=25, hits_air=True),
    "chaparral": dict(type="sam", range_min_m=500, range_max_m=4800, pen=[[0, 50]], accuracy=[[500, 60], [4800, 40]], reload_ticks=300, damage=70, suppression=10, hits_air=True, hits_ground=False),
    "stinger": dict(type="sam", range_min_m=200, range_max_m=4800, pen=[[0, 50]], accuracy=[[200, 70], [4800, 45]], reload_ticks=300, damage=70, suppression=10, hits_air=True, hits_ground=False),
    "strela10": dict(type="sam", range_min_m=800, range_max_m=5000, pen=[[0, 50]], accuracy=[[800, 60], [5000, 35]], reload_ticks=300, damage=70, suppression=10, hits_air=True, hits_ground=False),
    "igla": dict(type="sam", range_min_m=500, range_max_m=5200, pen=[[0, 50]], accuracy=[[500, 65], [5200, 40]], reload_ticks=300, damage=70, suppression=10, hits_air=True, hits_ground=False),
    "aim9": dict(type="aam", range_min_m=500, range_max_m=8000, pen=[[0, 50]], accuracy=[[500, 75], [8000, 45]], reload_ticks=200, damage=80, suppression=10, hits_air=True, hits_ground=False),
    "aim7": dict(type="aam", range_min_m=1000, range_max_m=30000, pen=[[0, 50]], accuracy=[[1000, 65], [30000, 35]], reload_ticks=300, damage=80, suppression=10, hits_air=True, hits_ground=False),
    "r27_r73": dict(type="aam", range_min_m=500, range_max_m=20000, pen=[[0, 50]], accuracy=[[500, 75], [20000, 35]], reload_ticks=250, damage=80, suppression=10, hits_air=True, hits_ground=False),
    # Rockets and bombs used by aircraft against ground targets
    "rockets_70mm": dict(type="rocket", range_max_m=6000, pen=[[0, 100]], accuracy=[[0, 40], [3000, 25], [6000, 10]], reload_ticks=40, salvo=4, damage=40, blast_damage=40, blast_radius_m=10, suppression=40, design="area rockets; design"),
    "bombs_500lb": dict(type="he", range_max_m=1500, pen=[[0, 150]], accuracy=[[0, 60], [1500, 30]], reload_ticks=100, salvo=2, damage=150, blast_damage=150, blast_radius_m=40, suppression=80, design="Mk 82 class; design"),
    # FLY-06 loadout weapons.
    "cbu87": dict(type="cluster", range_max_m=1500, pen=[[0, 190]], accuracy=[[0, 70], [1500, 40]], reload_ticks=100, salvo=6, damage=60, blast_damage=45, blast_radius_m=15, suppression=60, design="CBU-87 CEM: BLU-97 bomblets (shaped charge ~190 mm, fragmentation); 6 clusters per pass; design"),
    "zab500": dict(type="incendiary", range_max_m=1500, pen=[[0, 20]], accuracy=[[0, 60], [1500, 30]], reload_ticks=150, salvo=2, damage=60, blast_damage=60, blast_radius_m=30, suppression=90, design="ZAB-500 incendiary: sets vehicles in the burst on fire (SIM-07 fire critical); design"),
    "kh58": dict(type="arm", range_min_m=1000, range_max_m=12000, pen=[[0, 150]], accuracy=[[1000, 85], [12000, 70]], reload_ticks=300, damage=200, blast_damage=40, blast_radius_m=15, suppression=40, design="Kh-58 anti-radiation missile: homes only on emitting radars (radar-directed AA); design"),
    # Indirect fire: used by fire missions (SUP-01), never in direct fire. tof_m_s = mean shell speed over the
# trajectory (time of flight = range / tof_m_s); cep_permille = CEP as thousandths of range (design: unguided
# tube ~0.5%, mortars ~0.8%, rockets ~1%; FM 6-40 dispersion order of magnitude, not a firing table).
    "how_155": dict(type="gun_artillery", range_min_m=2000, range_max_m=18100, pen=[[0, 0]], accuracy=[[0, 100]], reload_ticks=600, damage=0, blast_damage=200, blast_radius_m=30, suppression=80, tof_m_s=500, cep_permille=5),
    "how_152": dict(type="gun_artillery", range_min_m=2000, range_max_m=18500, pen=[[0, 0]], accuracy=[[0, 100]], reload_ticks=600, damage=0, blast_damage=190, blast_radius_m=30, suppression=80, tof_m_s=480, cep_permille=5),
    "how_122": dict(type="gun_artillery", range_min_m=2000, range_max_m=15300, pen=[[0, 0]], accuracy=[[0, 100]], reload_ticks=450, damage=0, blast_damage=130, blast_radius_m=22, suppression=60, tof_m_s=450, cep_permille=6),
    "mortar_107": dict(type="mortar", range_min_m=500, range_max_m=6840, pen=[[0, 0]], accuracy=[[0, 100]], reload_ticks=200, damage=0, blast_damage=90, blast_radius_m=18, suppression=55, tof_m_s=250, cep_permille=8),
    "mortar_120": dict(type="mortar", range_min_m=500, range_max_m=7180, pen=[[0, 0]], accuracy=[[0, 100]], reload_ticks=200, damage=0, blast_damage=110, blast_radius_m=20, suppression=60, tof_m_s=270, cep_permille=8),
    "naval_406": dict(type="gun_artillery", range_min_m=3000, range_max_m=38000, pen=[[0, 0]], accuracy=[[0, 100]], reload_ticks=600, damage=0, blast_damage=400, blast_radius_m=55, suppression=100, tof_m_s=700, cep_permille=4, design="Iowa-class 16 in/50 Mk 7 HC round (NGFS, 1989): blast and CEP design (0.4% of range)"),
    "naval_130": dict(type="gun_artillery", range_min_m=2000, range_max_m=24000, pen=[[0, 0]], accuracy=[[0, 100]], reload_ticks=60, damage=0, blast_damage=150, blast_radius_m=22, suppression=70, tof_m_s=600, cep_permille=6, design="Sovremenny-class AK-130 130 mm (NGFS): blast and CEP design (0.6% of range)"),
    "mlrs_m26": dict(type="rocket_artillery", range_min_m=10000, range_max_m=32000, pen=[[0, 0]], accuracy=[[0, 100]], reload_ticks=6000, salvo=12, damage=0, blast_damage=120, blast_radius_m=60, suppression=90, tof_m_s=900, cep_permille=10),
    "grad_9m22u": dict(type="rocket_artillery", range_min_m=4000, range_max_m=20380, pen=[[0, 0]], accuracy=[[0, 100]], reload_ticks=6000, salvo=40, damage=0, blast_damage=90, blast_radius_m=35, suppression=80, tof_m_s=700, cep_permille=12),
}

# ERA-04: year each weapon (or the round modelled) entered service, from public service histories (FAS, Jane's
# summaries on Wikipedia); "design" marks a generic or approximate year. Checked against the era by check_era.py.
WEAPON_YEARS = {
    "m256_m829a1": (1988, "M829A1 type classified 1988"), "m68_m833": (1983, "M833 APFSDS fielded early 1980s; design"),
    "2a46_3bm42": (1986, "3BM42 Mango adopted 1986"), "kobra_9m112": (1976, "9M112 Kobra with the T-64B, 1976"),
    "tow2": (1983, "BGM-71D TOW-2 fielded 1983"), "dragon2": (1985, "Dragon II warhead upgrade from 1985; design"),
    "konkurs_9m113": (1974, "9M113 Konkurs adopted 1974"), "fagot_9m111m": (1983, "9M111M Faktoriya 1983"),
    "shturm_9m114": (1976, "9M114 Shturm adopted 1976"), "hellfire_agm114a": (1984, "AGM-114A in service 1984"),
    "maverick_agm65": (1972, "AGM-65A 1972"), "kh25_kh29": (1980, "Kh-29 1980 (Kh-25 1975)"),
    "m242_25mm": (1981, "M242 Bushmaster with the M2 Bradley, 1981"), "2a42_30mm": (1980, "2A42 with the BMP-2, 1980"),
    "2a28_pg15v": (1966, "2A28 Grom with the BMP-1, 1966"), "m2hb_50": (1933, "M2HB, 1933"), "kpvt_145": (1955, "KPVT, 1955; design"),
    "mg_762": (1961, "PKT / M240-class coax, early 1960s; design"), "gau8_30mm": (1977, "GAU-8 with the A-10A, 1977"),
    "gsh30_30mm": (1981, "GSh-30-2 with the Su-25, 1981"), "m230_30mm": (1984, "M230 with the AH-64A, 1984"),
    "m61_20mm": (1959, "M61 Vulcan, 1959"), "rifle_section": (1983, "M16A2 adopted 1983; AK-74 1974"),
    "lmg_section": (1984, "M249 adopted 1984; RPK-74 1974"), "law_m72": (1963, "M72 LAW, 1963"), "at4_m136": (1987, "AT4 (M136) US service 1987"),
    "rpg7_pg7vl": (1977, "PG-7VL round 1977"), "rpg18": (1972, "RPG-18, 1972"), "gl_40mm": (1969, "M203, 1969; GP-25 1978"),
    "m168_vulcan": (1968, "M163 VADS, 1968"), "zsu23_amur": (1965, "ZSU-23-4, 1965"), "chaparral": (1969, "MIM-72 Chaparral, 1969"),
    "stinger": (1981, "FIM-92A Stinger, 1981"), "strela10": (1976, "9K35 Strela-10, 1976"), "igla": (1983, "9K38 Igla, 1983"),
    "aim9": (1978, "AIM-9L, 1978"), "aim7": (1976, "AIM-7F, 1976"), "r27_r73": (1984, "R-27 1983, R-73 1984"),
    "rockets_70mm": (1956, "2.75 in FFAR family, 1950s; design"), "bombs_500lb": (1954, "Mk 82, 1954"),
    "how_155": (1963, "M109 155 mm, 1963"), "how_152": (1971, "2S3 Akatsiya, 1971"), "how_122": (1972, "2S1 Gvozdika, 1972"),
    "mortar_107": (1951, "M30 4.2 in mortar, 1951"), "mortar_120": (1981, "2B11 120 mm mortar (2S12), 1981"),
    "cbu87": (1986, "CBU-87 CEM in service 1986"), "zab500": (1950, "ZAB-500 incendiary bomb family, 1950s; design"),
    "kh58": (1982, "Kh-58 (AS-11 Kilter) in service 1982"),
    "naval_406": (1943, "16 in/50 Mk 7 (Iowa class), reactivated 1982-1988 for naval gunfire support"), "naval_130": (1985, "AK-130 on Sovremenny-class destroyers, 1985"),
    "mlrs_m26": (1983, "M270 MLRS with M26 rockets, 1983"), "grad_9m22u": (1963, "BM-21 Grad, 1963"),
}

# --------------------------------------------------------------------------- units
# Per research id: sim kind, move class, mounts (weapon id, ammo), and design values for anything the research
# leaves null or vague. Armor: (front_ke, front_ce, side_ke, side_ce, rear, top). "A" = computed from research.
SIM_KIND = {
    "tank": "tank", "ifv": "ifv", "apc": "ifv", "recon": "ifv", "atgm_vehicle": "ifv", "command": "command",
    "infantry": "infantry", "at_team": "infantry", "manpads": "infantry", "mortar": "artillery", "spg": "artillery",
    "mlrs": "artillery", "spaag": "aa", "sam": "aa", "supply": "supply", "recovery": "supply",
    "attack_helo": "air", "recon_helo": "air", "transport_helo": "air", "attack_jet": "air", "multirole_jet": "air",
    "fighter": "air", "strike_jet": "air",
    "td": "tank", "towed_gun": "artillery", "mortar_team": "artillery",
}

UNITS = {
    # id: (mounts, design dict). Design keys: armor tuple, members, speed_kmh, stealth, optics_m, size_pct,
    # supply, front_weight, phase, why.
    "m1a1_abrams": ([("m256_m829a1", 40), ("m2hb_50", 900), ("mg_762", 10400)], dict(armor="A", top=30, why="top armor design")),
    "m60a3_tts": ([("m68_m833", 63), ("m2hb_50", 900), ("mg_762", 5950)], dict(armor=(250, 250, 55, 55, 33, 25), why="front armor: research null; ~109 mm glacis at 65 deg = ~250 mm LOS steel")),
    "m2a2_bradley": ([("m242_25mm", 900), ("tow2", 7), ("mg_762", 2200)], dict(armor=(40, 80, 30, 40, 25, 15), why="research gives protection classes, not mm; design")),
    "m3a2_bradley_cfv": ([("m242_25mm", 1500), ("tow2", 12), ("mg_762", 2200)], dict(armor=(40, 80, 30, 40, 25, 15), stealth=80, optics_m=3000, front_weight=0, why="recon: design armor as M2A2; recon optics/stealth")),
    "m113a2": ([("m2hb_50", 2000)], dict(armor=(15, 15, 12, 12, 12, 10), why="38-44 mm aluminium ~ 15 mm RHA; design")),
    "m901_itv": ([("tow2", 12), ("mg_762", 1000)], dict(armor=(15, 15, 12, 12, 12, 10), stealth=40, front_weight=0, why="M113 hull; missile stowage null in research, 12 design")),
    "us_mech_rifle_squad": ([("rifle_section", 1200), ("lmg_section", 1000), ("gl_40mm", 36), ("at4_m136", 2)], dict(members=9, speed_kmh=5, stealth=120, optics_m=1500, size_pct=25, why="squad of 9 (6 dismount + 3 crew-served/leaders); FM 7-7J; design ammo")),
    "m47_dragon_team": ([("dragon2", 6)], dict(members=2, speed_kmh=4, stealth=180, optics_m=2000, size_pct=15, front_weight=0, why="2-man team; design")),
    "m220_tow_team": ([("tow2", 6)], dict(members=4, speed_kmh=3, stealth=160, optics_m=3000, size_pct=15, front_weight=0, why="4-man team (research crew 4); thermal AN/TAS-4; design ammo")),
    "m577a2_cp": ([("mg_762", 600)], dict(armor=(15, 15, 12, 12, 12, 10), why="M113 family; design")),
    "us_scout_section": ([("rifle_section", 800), ("lmg_section", 600), ("at4_m136", 2)], dict(members=4, speed_kmh=5, stealth=250, optics_m=2800, size_pct=15, front_weight=0, sim_kind="infantry", why="recon infantry; design")),
    "m106a2_mortar": ([("mortar_107", 88), ("m2hb_50", 600)], dict(armor=(15, 15, 12, 12, 12, 10), speed_kmh=64, why="M113 family; road speed as M113A2 (research null)")),
    "m109a3_sph": ([("how_155", 32), ("m2hb_50", 500)], dict(armor=(15, 15, 12, 12, 12, 10), why="aluminium hull; design")),
    "m270_mlrs": ([("mlrs_m26", 12)], dict(armor=(12, 12, 10, 10, 10, 8), why="light armored cab; design")),
    "m163_vads": ([("m168_vulcan", 2230)], dict(armor=(15, 15, 12, 12, 12, 10), why="M113 chassis; design")),
    "m48_chaparral": ([("chaparral", 12)], dict(armor=(10, 10, 8, 8, 8, 6), speed_kmh=61, why="M730 carrier; research weight looks wrong (6.6 t), speed design")),
    "stinger_team": ([("stinger", 6)], dict(members=2, speed_kmh=4, stealth=180, optics_m=4000, size_pct=15, front_weight=0, why="2-man team; design")),
    "m977_hemtt": ([], dict(armor=(5, 5, 4, 4, 4, 3), supply=2000, why="supply truck; design armor and capacity")),
    "m88a1_arv": ([("m2hb_50", 1300)], dict(armor=(50, 50, 30, 30, 20, 15), supply=0, repair=1, why="recovery vehicle: repairs friendly vehicles (SUP-03); design armor")),
    "ah64a_apache": ([("m230_30mm", 1200), ("hellfire_agm114a", 16), ("rockets_70mm", 38)], dict(hp=90, armor=(12, 12, 12, 12, 10, 10), speed_kmh=293, optics_m=6000, size_pct=70, why="speed: FAS 293 km/h (research null in summary); design armor")),
    "oh58d_kiowa_warrior": ([("m2hb_50", 500)], dict(hp=40, armor=(5, 5, 5, 5, 5, 5), speed_kmh=240, optics_m=7000, stealth=60, size_pct=50, front_weight=0, why="1989: scout (mast sight); armed KW ~1991, so only a .50 pod; design")),
    "uh60a_black_hawk": ([("mg_762", 2000)], dict(hp=70, armor=(8, 8, 8, 8, 6, 6), speed_kmh=280, optics_m=3000, size_pct=80, supply=300, medevac=1, why="design; supply 300: ~1.2 t internal or sling ammunition lift, design; medevac: UH-60A air ambulance role (SUP-03)")),
    "a10a_thunderbolt": ([("gau8_30mm", 1174), ("maverick_agm65", 4), ("bombs_500lb", 6)], dict(hp=120, armor=(15, 15, 15, 15, 12, 15), speed_kmh=560, optics_m=6000, size_pct=65, why="titanium tub; cruise speed design")),
    "f16c_block30": ([("m61_20mm", 500), ("aim9", 2), ("bombs_500lb", 6)], dict(hp=60, armor=(5, 5, 5, 5, 5, 5), speed_kmh=900, optics_m=8000, size_pct=55, why="design")),
    "f15c_eagle": ([("m61_20mm", 940), ("aim7", 4), ("aim9", 4)], dict(hp=65, armor=(5, 5, 5, 5, 5, 5), speed_kmh=900, optics_m=12000, size_pct=60, why="design")),
    "t80bv": ([("2a46_3bm42", 38), ("kobra_9m112", 4), ("mg_762", 1250)], dict(armor="A", era_ce=200, era_side_ce=100, top=30, why="Kontakt-1 adds 200 mm vs CE frontally, 100 on the skirts (design)")),
    "t64bv": ([("2a46_3bm42", 36), ("kobra_9m112", 4), ("mg_762", 1250)], dict(armor="A", era_ce=200, era_side_ce=100, side=70, top=30, weight_t=42, why="side armor and weight null in research; 70 mm and 42 t design; ERA as T-80BV")),
    "bmp2": ([("2a42_30mm", 500), ("konkurs_9m113", 5), ("mg_762", 2000)], dict(armor=(35, 35, 15, 15, 15, 8), why="front design (proof vs 12.7 mm); side 15 vs research 9 mm steel + spaced; design")),
    "bmp1p": ([("2a28_pg15v", 40), ("fagot_9m111m", 4), ("mg_762", 2000)], dict(armor=(33, 33, 17, 17, 16, 8), why="research side 16-18, rear 16; front design")),
    "btr80": ([("kpvt_145", 500), ("mg_762", 2000)], dict(armor=(10, 10, 8, 8, 7, 5), why="research side 7-9; design")),
    "brdm2": ([("kpvt_145", 500), ("mg_762", 2000)], dict(armor=(10, 10, 7, 7, 7, 5), stealth=100, optics_m=2600, front_weight=0, why="recon; research side/rear 7")),
    "9p148_konkurs": ([("konkurs_9m113", 15)], dict(armor=(10, 10, 7, 7, 7, 5), stealth=60, front_weight=0, why="BRDM-2 hull; missile stowage null, 15 design")),
    "sov_motor_rifle_squad": ([("rifle_section", 1200), ("lmg_section", 1200), ("rpg7_pg7vl", 5), ("rpg18", 2)], dict(members=8, speed_kmh=5, stealth=120, optics_m=1500, size_pct=25, why="7 dismounts (research) + leader; design ammo")),
    "9k111_fagot_team": ([("fagot_9m111m", 6)], dict(members=3, speed_kmh=4, stealth=180, optics_m=2200, size_pct=15, front_weight=0, why="3-man team; design")),
    "bmp1ksh": ([("mg_762", 1000)], dict(armor=(33, 33, 17, 17, 16, 8), why="BMP-1 hull; design")),
    "sov_razvedka_section": ([("rifle_section", 800), ("lmg_section", 600), ("rpg7_pg7vl", 3)], dict(members=6, speed_kmh=5, stealth=250, optics_m=2800, size_pct=15, front_weight=0, sim_kind="infantry", why="recon infantry; design")),
    "2s12_sani": ([("mortar_120", 60)], dict(armor=(5, 5, 4, 4, 4, 3), speed_kmh=80, weight_t=6, why="mortar on GAZ-66 truck; research weight 0.21 t is the mortar alone; 6 t design")),
    "2s1_gvozdika": ([("how_122", 40)], dict(armor=(20, 20, 15, 15, 15, 10), why="design")),
    "2s3_akatsiya": ([("how_152", 46), ("mg_762", 1500)], dict(armor=(30, 30, 15, 15, 15, 10), why="research side 15")),
    "bm21_grad": ([("grad_9m22u", 40)], dict(armor=(5, 5, 4, 4, 4, 3), why="Ural truck; design")),
    "zsu23_4_shilka": ([("zsu23_amur", 2000)], dict(armor=(15, 15, 10, 10, 10, 8), why="design")),
    "9k35_strela10": ([("strela10", 8), ("mg_762", 1000)], dict(armor=(10, 10, 7, 7, 7, 5), why="MT-LB chassis; design")),
    "9k38_igla_team": ([("igla", 6)], dict(members=2, speed_kmh=4, stealth=180, optics_m=4000, size_pct=15, front_weight=0, why="2-man team; design")),
    "ural4320": ([], dict(armor=(5, 5, 4, 4, 4, 3), supply=1600, why="supply truck; design armor and capacity")),
    "brem1": ([("mg_762", 800)], dict(armor=(300, 300, 70, 70, 40, 20), supply=0, repair=1, why="T-72 hull recovery vehicle: repairs friendly vehicles (SUP-03); design")),
    "mi24v_hind": ([("m2hb_50", 1470), ("shturm_9m114", 4), ("rockets_70mm", 64)], dict(hp=110, armor=(15, 15, 12, 12, 10, 10), speed_kmh=320, optics_m=5000, size_pct=80, why="Yak-B modelled as HMG; S-8 as rocket class; ru speed 320 used")),
    "mi8mt_hip": ([("mg_762", 1000), ("rockets_70mm", 64)], dict(hp=80, armor=(8, 8, 8, 8, 6, 6), speed_kmh=250, optics_m=3000, size_pct=90, supply=400, medevac=1, why="design; supply 400: 4 t cargo, design; medevac: Mi-8MB/MT casualty evacuation role (SUP-03)")),
    "su25_frogfoot": ([("gsh30_30mm", 250), ("kh25_kh29", 2), ("bombs_500lb", 6)], dict(hp=110, armor=(12, 12, 12, 12, 10, 12), speed_kmh=750, optics_m=5000, size_pct=60, why="armoured cockpit; design")),
    "mig29_9_12": ([("gsh30_30mm", 150), ("r27_r73", 6)], dict(hp=60, armor=(5, 5, 5, 5, 5, 5), speed_kmh=900, optics_m=10000, size_pct=60, why="design")),
    "su24m_fencer": ([("m61_20mm", 500), ("kh25_kh29", 2), ("bombs_500lb", 8)], dict(hp=70, armor=(5, 5, 5, 5, 5, 5), speed_kmh=900, optics_m=6000, size_pct=70, why="GSh-6-23 modelled as the 20 mm class; design")),
}

KIND_DEFAULTS = {  # stealth, optics_m, size_pct, phase
    "tank": (0, 2400, 100, "C"), "ifv": (20, 2200, 85, "B"), "apc": (20, 1800, 80, "A"), "recon": (80, 2800, 80, "A"),
    "atgm_vehicle": (40, 2600, 75, "B"), "command": (20, 1800, 85, "A"), "infantry": (120, 1500, 25, "A"),
    "at_team": (180, 2200, 15, "A"), "manpads": (180, 4000, 15, "A"), "mortar": (20, 1500, 90, "A"),
    "spg": (0, 1500, 100, "B"), "mlrs": (0, 1500, 100, "C"), "spaag": (20, 3000, 90, "B"), "sam": (20, 3000, 90, "B"),
    "supply": (20, 1200, 90, "A"), "recovery": (20, 1500, 100, "B"), "attack_helo": (0, 5000, 70, "B"),
    "recon_helo": (60, 7000, 50, "A"), "transport_helo": (0, 3000, 85, "A"), "attack_jet": (0, 6000, 65, "B"),
    "multirole_jet": (0, 8000, 55, "B"), "fighter": (0, 10000, 60, "B"), "strike_jet": (0, 6000, 70, "C"),
    "td": (40, 2000, 90, "B"), "towed_gun": (60, 1500, 70, "A"), "mortar_team": (120, 1200, 25, "A"),
}

DEFAULT_RANGE_KM = {"tracked": 450, "wheeled": 600, "infantry": 0}

# Infantry team -> (carrier unit id, why). FM 71-2 (US mech infantry, 1988) and FM 100-2-3 (Soviet MRR organisation).
CARRIERS = {
    "us_mech_rifle_squad": ("m2a2_bradley", "M2 Bradley IFV, the mech infantry squad's vehicle (FM 71-2)"),
    "m47_dragon_team": ("m113a2", "Dragon gunners rode the company's M113/M2 carriers"),
    "m220_tow_team": ("m113a2", "battalion antiarmor company TOW sections on M113/M901 carriers"),
    "us_scout_section": ("m3a2_bradley_cfv", "M3 CFV, the cavalry scout section's vehicle"),
    "stinger_team": ("m113a2", "Stinger teams attached to maneuver battalions rode M113s"),
    "sov_motor_rifle_squad": ("bmp2", "BMP-2, the motor rifle squad's vehicle in a BMP regiment (FM 100-2-3)"),
    "9k111_fagot_team": ("bmp2", "Fagot ATGM teams of the BMP battalion's antitank platoon"),
    "sov_razvedka_section": ("brdm2", "BRDM-2 of the regimental reconnaissance company"),
    "9k38_igla_team": ("btr80", "Igla teams of the battalion air defence platoon rode BTRs"),
}

# FLY-06 loadout variants: separate cards of the same airframe with another weapon set. id: (base, name suffix,
# mounts, why).
VARIANTS = {
    "a10a_cbu87": ("a10a_thunderbolt", "CBU-87", [("gau8_30mm", 1174), ("cbu87", 6), ("aim9", 2)], "anti-armour cluster loadout: CBU-87 CEM and AIM-9 self-defence"),
    "su25_zab500": ("su25_frogfoot", "ZAB-500", [("gsh30_30mm", 250), ("zab500", 4), ("rockets_70mm", 40)], "incendiary loadout: ZAB-500 and S-8 rockets"),
    "su24m_kh58": ("su24m_fencer", "Kh-58 SEAD", [("m61_20mm", 500), ("kh58", 2), ("bombs_500lb", 4)], "SEAD loadout: two Kh-58 anti-radiation missiles"),
}

THERMAL = {"m1a1_abrams", "m60a3_tts", "m2a2_bradley", "m3a2_bradley_cfv", "m901_itv", "m220_tow_team", "ah64a_apache", "oh58d_kiowa_warrior", "a10a_thunderbolt"}


def num(x):
    """Researched value -> (number or None, is_estimate). Ranges become midpoints."""
    if isinstance(x, dict):
        est = bool(x.get("estimate"))
        x = x.get("value")
    else:
        est = False
    if x is None or isinstance(x, bool):
        return None, est
    if isinstance(x, (int, float)):
        return x, est
    m = re.findall(r"\d+(?:\.\d+)?", str(x))
    if not m:
        return None, est
    vals = [float(v) for v in m[:2]]
    return sum(vals) / len(vals), True if len(vals) > 1 or est else est


FLIGHT = ROOT / "assets_src" / "research" / "flight_envelopes_1989.json"


def build():
    research = {u["id"]: u for u in json.loads(RESEARCH.read_text())["units"]}
    missing = set(UNITS) - set(research)
    if missing:
        raise SystemExit(f"units missing from research: {sorted(missing)}")
    weapons = []
    for era, table, years in (("coldwar", WEAPONS, WEAPON_YEARS), ("coldwar", WEAPONS_CWA, WEAPON_YEARS_CWA), ("ww2", WEAPONS_WW2, WEAPON_YEARS_WW2), ("modern", WEAPONS_MODERN, WEAPON_YEARS_MODERN)):
        for wid, w in table.items():
            entry = {"id": wid, **{k: v for k, v in w.items() if k != "design"}}
            entry.setdefault("range_min_m", 0)
            entry["era"] = era
            entry["year"], entry["year_note"] = years[wid]
            if "design" in w:
                entry["design"] = w["design"]
            weapons.append(entry)
    roster = []
    research_ww2 = {u["id"]: u for u in json.loads(RESEARCH_WW2.read_text())["units"]}
    missing = set(UNITS_WW2) - set(research_ww2)
    if missing:
        raise SystemExit(f"WW2 units missing from research: {sorted(missing)}")
    eras = [("coldwar", 1989, uid, research[uid], m, d) for uid, (m, d) in UNITS.items()]
    research_cwa = {u["id"]: u for u in json.loads(RESEARCH_CWA.read_text())["units"]}
    missing = set(UNITS_CWA) - set(research_cwa)
    if missing:
        raise SystemExit(f"Cold War allied units missing from research: {sorted(missing)}")
    eras += [("coldwar", 1989, uid, research_cwa[uid], m, d) for uid, (m, d) in UNITS_CWA.items()]
    eras += [("ww2", 1944, uid, research_ww2[uid], m, d) for uid, (m, d) in UNITS_WW2.items()]
    research_modern = {u["id"]: u for u in json.loads(RESEARCH_MODERN.read_text())["units"]}
    missing = set(UNITS_MODERN) - set(research_modern)
    if missing:
        raise SystemExit(f"modern units missing from research: {sorted(missing)}")
    eras += [("modern", 2026, uid, research_modern[uid], m, d) for uid, (m, d) in UNITS_MODERN.items()]
    for era, default_year, uid, r, mounts, design in eras:
        kind = r["kind"]
        stealth, optics, size, phase = KIND_DEFAULTS[kind]
        est, des = [], []
        mob = r.get("mobility") or {}
        dims = r.get("dimensions") or {}
        speed, e = num(mob.get("max_road_speed_kmh"))
        if speed is None:
            speed = design.get("speed_kmh")
            des.append("speed")
        elif e:
            est.append("speed")
        weight, e = num(dims.get("combat_weight_t"))
        members = design.get("members", 1)
        sim_kind = design.get("sim_kind", SIM_KIND[kind])
        if sim_kind == "infantry":
            hp = members * 10
            move_class = "infantry"
        else:
            if "weight_t" in design:
                weight = design["weight_t"]
                des.append("weight")
            if "hp" in design:
                hp = design["hp"]
                des.append("hp")
            else:
                if weight is None or weight < 1:
                    raise SystemExit(f"{uid}: no weight for hp; add a design weight_t or hp")
                hp = round(40 + 2 * weight)
            prop = (mob.get("propulsion") or "").lower()
            move_class = "air" if sim_kind == "air" else ("wheeled" if "wheel" in prop or kind in ("supply",) or uid in ("2s12_sani", "bm21_grad") else "tracked")
        if "move_class" in design:
            move_class = design["move_class"]
            des.append("move_class")
        armor = design.get("armor")
        a = r.get("armor") or {}
        if armor == "A":
            tf_ke, e1 = num(a.get("turret_front_ke"))
            hf_ke, e2 = num(a.get("hull_front_ke"))
            tf_ce, _ = num(a.get("turret_front_ce"))
            hf_ce, _ = num(a.get("hull_front_ce"))
            side, _ = num(a.get("side_mm"))
            rear, _ = num(a.get("rear_mm"))
            front_ke = round(0.6 * tf_ke + 0.4 * hf_ke)
            front_ce = round(0.6 * tf_ce + 0.4 * hf_ce) + design.get("era_ce", 0)
            side = design.get("side", side if side is not None else 70)
            armor = (front_ke, front_ce, round(side), round(side) + design.get("era_side_ce", 0), round(rear) if rear else 40, design.get("top", 30))
            est.append("armor (research estimates; front = 60% turret + 40% hull)")
            if "era_ce" in design:
                des.append("ERA bonus vs CE")
        elif armor == "W":
            # WW2 plate armour: effective thickness = mm / cos(slope from vertical); KE and CE alike for steel.
            def eff(mm_key, slope_key):
                mm, _ = num(a.get(mm_key))
                sl, _ = num(a.get(slope_key))
                return (mm or 0) / math.cos(math.radians(sl or 0))
            hull, turret = eff("hull_front_mm", "hull_front_slope_deg"), eff("turret_front_mm", "turret_front_slope_deg")
            front = round(0.6 * turret + 0.4 * hull)
            side = round(num(a.get("side_mm"))[0] or 0)
            armor = (front, front, side, side, round(num(a.get("rear_mm"))[0] or 0), round(num(a.get("top_mm"))[0] or 0))
            est.append("armor (researched plate and slope; front = 60% turret + 40% hull, effective = mm / cos slope)")
        elif armor is None:
            armor = (0, 0, 0, 0, 0, 0)
        else:
            des.append("armor")
        # Fuel is tenths of a kilometre of range: every vehicle burns 10 per km, so a full tank = range_km * 10.
        rng, _ = num(mob.get("range_km"))
        if sim_kind == "infantry" or move_class == "infantry":
            fuel, fuel_per_km = 0, 0
        elif sim_kind == "air":
            fuel, fuel_per_km = 5000, 10  # design: 500 km of loiter/transit, Bingo at 25%
            des.append("fuel (aircraft abstracted to 500 km)")
        else:
            if rng is None:
                rng = DEFAULT_RANGE_KM[move_class]
                des.append("range")
            fuel, fuel_per_km = round(rng * 10), 10
        for key in ("stealth", "optics_m", "size_pct"):
            if key in design:
                des.append(key)
        role = r.get("one_line_role")
        roster.append({
            "id": uid,
            "name": r["name"],
            "side": r["side"],
            "nation": r.get("nation", ""),
            "era": era,
            "year": r.get("year_in_service") or default_year,
            "kind": kind,
            "sim_kind": sim_kind,
            "move_class": move_class,
            "speed_mm_s": round((speed or 5) * 1000 / 3.6),
            "hp": hp,
            "members": members,
            "crew": num(r.get("crew"))[0],
            "armor": dict(zip(["front_ke", "front_ce", "side_ke", "side_ce", "rear", "top"], [int(v) for v in armor])),
            "optics_m": design.get("optics_m", optics),
            "thermal": 1 if uid in THERMAL or uid in THERMAL_MODERN or uid in THERMAL_CWA else 0,
            "stealth": design.get("stealth", stealth),
            "size_pct": design.get("size_pct", size),
            "fuel": int(fuel or 0),
            "fuel_per_km": int(fuel_per_km),
            "supply": design.get("supply", 0),
            "repair": design.get("repair", 0),
            "medevac": design.get("medevac", 0),
            "front_weight": design.get("front_weight", -1),
            "phase": design.get("phase", phase),
            "use_it_for": (role.get("value") if isinstance(role, dict) else role) or "",
            "weapons": [{"weapon": w, "ammo": n} for w, n in mounts],
            "estimates": est,
            "design": des,
            "design_note": design.get("why", ""),
            "sources": r.get("sources", []),
        })
    # FLY-03 flight envelopes (assets_src/research/flight_envelopes_1989.json): sea-level max speed, stall (or the
    # labelled landing/approach stand-in), climb rate and G limit, converted to the sim's integer units.
    flight = json.loads(FLIGHT.read_text())["aircraft"]
    for u in roster:
        f = flight.get(u["id"])
        if f is None:
            continue
        u["flight"] = {
            "vmax_mm_s": round(f["vmax_sl_kmh"]["value"] * 1000 / 3.6),
            "vstall_mm_s": round(f["vstall_kmh"]["value"] * 1000 / 3.6),
            "climb_mm_s": round(f["climb_m_s"]["value"] * 1000),
            "g_limit_x10": round(f["g_limit"]["value"] * 10),
            "sources": sorted({src for k in ("vmax_sl_kmh", "vstall_kmh", "climb_m_s", "g_limit") for src in f[k].get("sources", [])}),
        }
    # FLY-06 loadout variants.
    by_id0 = {u["id"]: u for u in roster}
    for vid, (base, suffix, mounts, why) in VARIANTS.items():
        v = json.loads(json.dumps(by_id0[base]))
        v["id"] = vid
        v["name"] = f"{v['name']} ({suffix})"
        v["weapons"] = [{"weapon": w, "ammo": n} for w, n in mounts]
        v["variant_of"] = base
        v["design"] = sorted(set(v["design"]) | {"loadout"})
        v["design_note"] = (v["design_note"] + "; " if v["design_note"] else "") + "loadout: " + why
        roster.append(v)
    # ERA-01 shared equipment: the same vehicle fielded by another army, listed under that army's nation.
    for vid, (base, nation, why) in NATION_VARIANTS.items():
        v = json.loads(json.dumps(by_id0[base]))
        v["id"] = vid
        v["nation"] = nation
        v["variant_of"] = base
        v["design_note"] = (v["design_note"] + "; " if v["design_note"] else "") + "nation variant: " + why
        roster.append(v)
    # MEC-09 mounted movement: mechanised and motorised infantry ride their carrier between fights. The sim does not
    # model the carrier as a separate unit; a carried team moves at its carrier's road speed and move class until it
    # fires, is hit or is suppressed (rules.movement.dismount_ticks).
    by_id = {u["id"]: u for u in roster}
    for u in roster:
        carrier = CARRIERS.get(u["id"]) or CARRIERS_CWA.get(u["id"]) or CARRIERS_MODERN.get(u["id"]) or CARRIERS_WW2.get(u["id"])
        u["carrier"] = carrier[0] if carrier else ""
        u["mounted_speed_mm_s"] = by_id[carrier[0]]["speed_mm_s"] if carrier else 0
        u["mounted_move_class"] = by_id[carrier[0]]["move_class"] if carrier else ""
        if carrier:
            u["design"].append("carrier")
            u["design_note"] = (u["design_note"] + "; " if u["design_note"] else "") + "carrier: " + carrier[1]
    return {"version": 1, "eras": ["coldwar", "ww2", "modern"], "units": roster}, {"version": 1, "weapons": weapons}


def main() -> int:
    out_r = Path(sys.argv[1]) if len(sys.argv) > 2 else ROOT / "data" / "roster.json"
    out_w = Path(sys.argv[2]) if len(sys.argv) > 2 else ROOT / "data" / "weapons.json"
    roster, weapons = build()
    ids = {w["id"] for w in weapons["weapons"]}
    for u in roster["units"]:
        for m in u["weapons"]:
            if m["weapon"] not in ids:
                raise SystemExit(f"{u['id']} mounts unknown weapon {m['weapon']}")
    out_r.write_text(json.dumps(roster, indent=2) + "\n", encoding="utf-8")
    out_w.write_text(json.dumps(weapons, indent=2) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
