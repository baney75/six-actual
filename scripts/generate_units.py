#!/usr/bin/env python3
"""Write data/units.json and game/data/units.json. Stats are game-scale estimates."""

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

# id, era, faction, year, kind, length_mm, width_mm, height_mm
ROSTER = [
    ("us_rifle", "ww2", "usa", 1944, "infantry", 1800, 500, 1800),
    ("uk_rifle", "ww2", "uk", 1944, "infantry", 1800, 500, 1800),
    ("ger_rifle", "ww2", "germany", 1944, "infantry", 1800, 500, 1800),
    ("su_rifle", "ww2", "ussr", 1944, "infantry", 1800, 500, 1800),
    ("mg42", "ww2", "germany", 1944, "infantry", 1800, 800, 1600),
    ("bren", "ww2", "uk", 1944, "infantry", 1800, 800, 1600),
    ("m4_sherman", "ww2", "usa", 1944, "tank", 5900, 2620, 2740),
    ("cromwell", "ww2", "uk", 1944, "tank", 6350, 2900, 2490),
    ("pz4", "ww2", "germany", 1944, "tank", 7020, 2880, 2680),
    ("panther", "ww2", "germany", 1944, "tank", 8660, 3420, 2990),
    ("tiger", "ww2", "germany", 1944, "tank", 8450, 3705, 3000),
    ("t34_85", "ww2", "ussr", 1944, "tank", 8100, 3000, 2700),
    ("is2", "ww2", "ussr", 1944, "tank", 9900, 3090, 2730),
    ("m7_priest", "ww2", "usa", 1944, "artillery", 6020, 2870, 2540),
    ("wespe", "ww2", "germany", 1944, "artillery", 4810, 2280, 2300),
    ("nebelwerfer", "ww2", "germany", 1944, "artillery", 5400, 1600, 1800),
    ("katyusha", "ww2", "ussr", 1944, "artillery", 6000, 2200, 2500),
    ("p47", "ww2", "usa", 1944, "air", 11000, 12400, 4400),
    ("p51", "ww2", "usa", 1944, "air", 9800, 11200, 4100),
    ("b26", "ww2", "usa", 1944, "air", 17600, 21600, 6400),
    ("b17", "ww2", "usa", 1944, "air", 22600, 31700, 5800),
    ("typhoon", "ww2", "uk", 1944, "air", 9700, 12700, 4600),
    ("spitfire", "ww2", "uk", 1944, "air", 9300, 11300, 3800),
    ("lancaster", "ww2", "uk", 1944, "air", 21200, 31100, 6200),
    ("fw190", "ww2", "germany", 1944, "air", 9000, 10500, 4000),
    ("bf109", "ww2", "germany", 1944, "air", 9000, 9900, 3400),
    ("ju87", "ww2", "germany", 1944, "air", 11000, 13800, 4000),
    ("il2", "ww2", "ussr", 1944, "air", 11400, 14600, 4200),
    ("supply_us_ww2", "ww2", "usa", 1944, "supply", 6000, 2200, 2400),
    ("us_rifle_cw", "cold_war", "usa", 1989, "infantry", 1800, 600, 1800),
    ("wg_rifle", "cold_war", "west_germany", 1989, "infantry", 1800, 600, 1800),
    ("uk_rifle_cw", "cold_war", "uk", 1989, "infantry", 1800, 600, 1800),
    ("fr_rifle", "cold_war", "france", 1989, "infantry", 1800, 600, 1800),
    ("su_rifle_cw", "cold_war", "ussr", 1989, "infantry", 1800, 600, 1800),
    ("ddr_rifle", "cold_war", "east_germany", 1989, "infantry", 1800, 600, 1800),
    ("pol_rifle", "cold_war", "poland", 1989, "infantry", 1800, 600, 1800),
    ("m1_abrams", "cold_war", "usa", 1989, "tank", 7920, 3650, 2440),
    ("leopard2", "cold_war", "west_germany", 1989, "tank", 7720, 3700, 2790),
    ("challenger", "cold_war", "uk", 1989, "tank", 11500, 3520, 2500),
    ("amx30", "cold_war", "france", 1989, "tank", 9480, 3100, 2290),
    ("t80", "cold_war", "ussr", 1989, "tank", 7400, 3600, 2200),
    ("t72", "cold_war", "ussr", 1989, "tank", 6860, 3460, 2190),
    ("t72_ddr", "cold_war", "east_germany", 1989, "tank", 6860, 3460, 2190),
    ("t72_pol", "cold_war", "poland", 1989, "tank", 6860, 3460, 2190),
    ("m2_bradley", "cold_war", "usa", 1989, "ifv", 6450, 3200, 2980),
    ("m113", "cold_war", "usa", 1989, "ifv", 4860, 2690, 2500),
    ("marder", "cold_war", "west_germany", 1989, "ifv", 6790, 3240, 2900),
    ("bmp2", "cold_war", "ussr", 1989, "ifv", 6730, 3150, 2450),
    ("tow_team", "cold_war", "usa", 1989, "infantry", 1800, 800, 1600),
    ("milan", "cold_war", "west_germany", 1989, "infantry", 1800, 800, 1600),
    ("konkurs", "cold_war", "ussr", 1989, "infantry", 1800, 800, 1600),
    ("stinger_team", "cold_war", "usa", 1989, "aa", 1800, 800, 1700),
    ("blowpipe", "cold_war", "uk", 1989, "aa", 1800, 800, 1700),
    ("igla", "cold_war", "ussr", 1989, "aa", 1800, 800, 1700),
    ("m109", "cold_war", "usa", 1989, "artillery", 6610, 3150, 3280),
    ("mlrs", "cold_war", "usa", 1989, "artillery", 6900, 2970, 2600),
    ("s2s3", "cold_war", "ussr", 1989, "artillery", 7760, 3250, 3050),
    ("bm21", "cold_war", "ussr", 1989, "artillery", 7350, 2400, 3090),
    ("zsu23", "cold_war", "ussr", 1989, "aa", 6540, 2950, 2480),
    ("gepard", "cold_war", "west_germany", 1989, "aa", 7700, 3250, 3100),
    ("sa8", "cold_war", "ussr", 1989, "aa", 9150, 2800, 4200),
    ("hawk", "cold_war", "usa", 1989, "aa", 5000, 2000, 2500),
    ("a10", "cold_war", "usa", 1989, "air", 16260, 17530, 4470),
    ("f16", "cold_war", "usa", 1989, "air", 15030, 9450, 5000),
    ("f15", "cold_war", "usa", 1989, "air", 19430, 13050, 5860),
    ("tornado", "cold_war", "uk", 1989, "air", 16800, 13900, 5900),
    ("jaguar", "cold_war", "uk", 1989, "air", 16700, 8700, 4900),
    ("su25", "cold_war", "ussr", 1989, "air", 15360, 14360, 4800),
    ("mig29", "cold_war", "ussr", 1989, "air", 17320, 11360, 4730),
    ("mig23", "cold_war", "ussr", 1989, "air", 16700, 14000, 4800),
    ("su24", "cold_war", "ussr", 1989, "air", 24590, 17500, 6200),
    ("supply_us_cw", "cold_war", "usa", 1989, "supply", 7000, 2500, 2800),
    ("supply_su_cw", "cold_war", "ussr", 1989, "supply", 7000, 2500, 2800),
    ("us_rifle_m", "modern", "usa", 2024, "infantry", 1800, 600, 1800),
    ("ru_rifle", "modern", "russia", 2024, "infantry", 1800, 600, 1800),
    ("cn_rifle", "modern", "china", 2024, "infantry", 1800, 600, 1800),
    ("m1a2", "modern", "usa", 2024, "tank", 7920, 3650, 2440),
    ("leo2a7", "modern", "west_germany", 2024, "tank", 7730, 3700, 2800),
    ("t90m", "modern", "russia", 2024, "tank", 6860, 3780, 2230),
    ("t14", "modern", "russia", 2024, "tank", 10900, 3900, 3300),
    ("type99", "modern", "china", 2024, "tank", 11000, 3400, 2300),
    ("stryker", "modern", "usa", 2024, "ifv", 6950, 2720, 2640),
    ("bmp3", "modern", "russia", 2024, "ifv", 7140, 3230, 2450),
    ("zbd04", "modern", "china", 2024, "ifv", 7200, 3200, 2500),
    ("javelin", "modern", "usa", 2024, "infantry", 1800, 800, 1600),
    ("kornet", "modern", "russia", 2024, "infantry", 1800, 800, 1600),
    ("fpv_ru", "modern", "russia", 2024, "infantry", 1800, 800, 1600),
    ("fpv_cn", "modern", "china", 2024, "infantry", 1800, 800, 1600),
    ("himars", "modern", "usa", 2024, "artillery", 7000, 2400, 3200),
    ("tornados", "modern", "russia", 2024, "artillery", 12400, 3000, 3000),
    ("pcl191", "modern", "china", 2024, "artillery", 11000, 3000, 3200),
    ("patriot", "modern", "usa", 2024, "aa", 10000, 2500, 3500),
    ("s400", "modern", "russia", 2024, "aa", 12000, 3000, 4000),
    ("pantsir", "modern", "russia", 2024, "aa", 8200, 3300, 4000),
    ("f35", "modern", "usa", 2024, "air", 15670, 10700, 4300),
    ("f16v", "modern", "usa", 2024, "air", 15030, 9450, 5000),
    ("a10c", "modern", "usa", 2024, "air", 16260, 17530, 4470),
    ("mq9", "modern", "usa", 2024, "air", 11000, 20000, 3600),
    ("su34", "modern", "russia", 2024, "air", 23570, 14700, 6100),
    ("su35", "modern", "russia", 2024, "air", 21900, 15400, 5900),
    ("j16", "modern", "china", 2024, "air", 22000, 15000, 5900),
    ("j20", "modern", "china", 2024, "air", 20800, 13000, 4600),
    ("supply_us_m", "modern", "usa", 2024, "supply", 8000, 2500, 3000),
]

STATS = {
    ("infantry", "ww2"): dict(speed_mm_s=4000, hp=30, armor_front=2, armor_side=1, armor_rear=1, armor_top=1, pen_mm=40, pen_drop_per_km=10, range_m=400, reload_ticks=20, optics_m=600, stealth=40, ammo=80, fuel=0),
    ("infantry", "cold_war"): dict(speed_mm_s=4500, hp=30, armor_front=2, armor_side=1, armor_rear=1, armor_top=1, pen_mm=500, pen_drop_per_km=20, range_m=1500, reload_ticks=40, optics_m=900, stealth=40, ammo=12, fuel=0),
    ("infantry", "modern"): dict(speed_mm_s=4500, hp=30, armor_front=2, armor_side=1, armor_rear=1, armor_top=1, pen_mm=800, pen_drop_per_km=10, range_m=2000, reload_ticks=60, optics_m=1200, stealth=45, ammo=8, fuel=0),
    ("tank", "ww2"): dict(speed_mm_s=10000, hp=80, armor_front=90, armor_side=40, armor_rear=30, armor_top=20, pen_mm=120, pen_drop_per_km=30, range_m=1200, reload_ticks=40, optics_m=800, stealth=0, ammo=40, fuel=400),
    ("tank", "cold_war"): dict(speed_mm_s=18000, hp=120, armor_front=500, armor_side=80, armor_rear=40, armor_top=30, pen_mm=550, pen_drop_per_km=40, range_m=2200, reload_ticks=50, optics_m=1800, stealth=0, ammo=40, fuel=500),
    ("tank", "modern"): dict(speed_mm_s=19000, hp=140, armor_front=700, armor_side=100, armor_rear=50, armor_top=40, pen_mm=750, pen_drop_per_km=30, range_m=2500, reload_ticks=50, optics_m=2200, stealth=0, ammo=40, fuel=500),
    ("ifv", "cold_war"): dict(speed_mm_s=16000, hp=60, armor_front=30, armor_side=15, armor_rear=10, armor_top=8, pen_mm=200, pen_drop_per_km=20, range_m=1600, reload_ticks=30, optics_m=1400, stealth=5, ammo=30, fuel=400),
    ("ifv", "modern"): dict(speed_mm_s=18000, hp=70, armor_front=40, armor_side=20, armor_rear=12, armor_top=10, pen_mm=250, pen_drop_per_km=20, range_m=1800, reload_ticks=30, optics_m=1600, stealth=5, ammo=30, fuel=400),
    ("artillery", "ww2"): dict(speed_mm_s=8000, hp=40, armor_front=15, armor_side=10, armor_rear=8, armor_top=6, pen_mm=80, pen_drop_per_km=5, range_m=4000, reload_ticks=80, optics_m=500, stealth=0, ammo=30, fuel=300),
    ("artillery", "cold_war"): dict(speed_mm_s=14000, hp=50, armor_front=20, armor_side=12, armor_rear=10, armor_top=8, pen_mm=100, pen_drop_per_km=5, range_m=8000, reload_ticks=100, optics_m=700, stealth=0, ammo=30, fuel=400),
    ("artillery", "modern"): dict(speed_mm_s=16000, hp=50, armor_front=20, armor_side=12, armor_rear=10, armor_top=8, pen_mm=120, pen_drop_per_km=2, range_m=12000, reload_ticks=120, optics_m=900, stealth=0, ammo=20, fuel=400),
    ("aa", "cold_war"): dict(speed_mm_s=12000, hp=40, armor_front=15, armor_side=8, armor_rear=8, armor_top=6, pen_mm=40, pen_drop_per_km=5, range_m=3000, reload_ticks=8, optics_m=2500, stealth=0, ammo=200, fuel=300),
    ("aa", "modern"): dict(speed_mm_s=14000, hp=50, armor_front=20, armor_side=10, armor_rear=8, armor_top=8, pen_mm=60, pen_drop_per_km=4, range_m=8000, reload_ticks=20, optics_m=6000, stealth=0, ammo=80, fuel=300),
    ("aa", "ww2"): dict(speed_mm_s=4000, hp=25, armor_front=2, armor_side=1, armor_rear=1, armor_top=1, pen_mm=30, pen_drop_per_km=8, range_m=1500, reload_ticks=10, optics_m=1200, stealth=10, ammo=100, fuel=0),
    ("air", "ww2"): dict(speed_mm_s=140000, hp=40, armor_front=8, armor_side=4, armor_rear=4, armor_top=4, pen_mm=30, pen_drop_per_km=5, range_m=800, reload_ticks=4, optics_m=2000, stealth=0, ammo=400, fuel=800),
    ("air", "cold_war"): dict(speed_mm_s=250000, hp=50, armor_front=6, armor_side=4, armor_rear=4, armor_top=4, pen_mm=80, pen_drop_per_km=2, range_m=3000, reload_ticks=6, optics_m=4000, stealth=0, ammo=200, fuel=1000),
    ("air", "modern"): dict(speed_mm_s=280000, hp=55, armor_front=6, armor_side=4, armor_rear=4, armor_top=4, pen_mm=100, pen_drop_per_km=1, range_m=5000, reload_ticks=8, optics_m=6000, stealth=10, ammo=120, fuel=1000),
    ("supply", "ww2"): dict(speed_mm_s=12000, hp=30, armor_front=4, armor_side=2, armor_rear=2, armor_top=2, pen_mm=0, pen_drop_per_km=0, range_m=0, reload_ticks=0, optics_m=400, stealth=0, ammo=0, fuel=400),
    ("supply", "cold_war"): dict(speed_mm_s=16000, hp=35, armor_front=6, armor_side=3, armor_rear=3, armor_top=3, pen_mm=0, pen_drop_per_km=0, range_m=0, reload_ticks=0, optics_m=500, stealth=0, ammo=0, fuel=500),
    ("supply", "modern"): dict(speed_mm_s=18000, hp=35, armor_front=6, armor_side=3, armor_rear=3, armor_top=3, pen_mm=0, pen_drop_per_km=0, range_m=0, reload_ticks=0, optics_m=600, stealth=0, ammo=0, fuel=500),
}

# Named guns that should not use the generic infantry missile profile.
OVERRIDES = {
    "us_rifle": dict(pen_mm=8, range_m=300, ammo=120, reload_ticks=8),
    "uk_rifle": dict(pen_mm=8, range_m=300, ammo=120, reload_ticks=8),
    "ger_rifle": dict(pen_mm=8, range_m=300, ammo=120, reload_ticks=8),
    "su_rifle": dict(pen_mm=8, range_m=300, ammo=120, reload_ticks=8),
    "us_rifle_cw": dict(pen_mm=12, range_m=400, ammo=150, reload_ticks=6),
    "wg_rifle": dict(pen_mm=12, range_m=400, ammo=150, reload_ticks=6),
    "uk_rifle_cw": dict(pen_mm=12, range_m=400, ammo=150, reload_ticks=6),
    "fr_rifle": dict(pen_mm=12, range_m=400, ammo=150, reload_ticks=6),
    "su_rifle_cw": dict(pen_mm=12, range_m=400, ammo=150, reload_ticks=6),
    "ddr_rifle": dict(pen_mm=12, range_m=400, ammo=150, reload_ticks=6),
    "pol_rifle": dict(pen_mm=12, range_m=400, ammo=150, reload_ticks=6),
    "us_rifle_m": dict(pen_mm=14, range_m=500, ammo=150, reload_ticks=6),
    "ru_rifle": dict(pen_mm=14, range_m=500, ammo=150, reload_ticks=6),
    "cn_rifle": dict(pen_mm=14, range_m=500, ammo=150, reload_ticks=6),
    "mg42": dict(pen_mm=10, range_m=700, ammo=300, reload_ticks=4),
    "bren": dict(pen_mm=10, range_m=600, ammo=200, reload_ticks=6),
    "tiger": dict(armor_front=120, pen_mm=160),
    "panther": dict(armor_front=110, pen_mm=150),
    "pz4": dict(armor_front=80, pen_mm=110),
    "m4_sherman": dict(armor_front=70, pen_mm=100),
    "fpv_ru": dict(pen_mm=400, range_m=3000, ammo=4, reload_ticks=80),
    "fpv_cn": dict(pen_mm=400, range_m=3000, ammo=4, reload_ticks=80),
    "mq9": dict(speed_mm_s=100000, ammo=4, reload_ticks=40, range_m=4000),
    "b17": dict(speed_mm_s=110000, ammo=80, range_m=600),
    "lancaster": dict(speed_mm_s=100000, ammo=80, range_m=600),
    "stinger_team": dict(pen_mm=40, range_m=4000, ammo=6, reload_ticks=80),
    "igla": dict(pen_mm=40, range_m=4000, ammo=6, reload_ticks=80),
    "blowpipe": dict(pen_mm=30, range_m=3000, ammo=6, reload_ticks=90),
}


def build() -> dict:
    units = []
    seen = set()
    for unit_id, era, faction, year, kind, length, width, height in ROSTER:
        if unit_id in seen:
            raise SystemExit(f"duplicate {unit_id}")
        seen.add(unit_id)
        stats = dict(STATS[(kind, era)])
        stats.update(OVERRIDES.get(unit_id, {}))
        units.append({
            "id": unit_id,
            "era": era,
            "faction": faction,
            "year": year,
            "kind": kind,
            "length_mm": length,
            "width_mm": width,
            "height_mm": height,
            **stats,
        })
    return {"version": 1, "units": units}


def main() -> None:
    payload = json.dumps(build(), indent=2) + "\n"
    destinations = [ROOT / "data" / "units.json", ROOT / "game" / "data" / "units.json"]
    if len(sys.argv) > 1:
        destinations = [Path(sys.argv[1])]
    for destination in destinations:
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_text(payload, encoding="utf-8")


if __name__ == "__main__":
    main()
