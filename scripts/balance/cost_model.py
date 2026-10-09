#!/usr/bin/env python3
"""W1/W6 cost model and deck generator: data/decks.json from data/roster.json + data/weapons.json.

Starting cost is a transparent function of combat value, then rounded to 5. Balance passes adjust costs
(W6: costs before stats) by editing COST_OVERRIDES below, never by hand-editing data/decks.json.

  value = protection + firepower + mobility + specials
    protection = hp/2 + front armor (best of KE/CE)/8
    firepower  = per mount: penetration/10 (anti-armor) or damage*salvo*3/10 (soft), each times reach
                 (range/2 km, clamped 0.25-1); or blast/2*sqrt(salvo) (artillery); SAM/AAA add damage/2*reach
    mobility   = road speed m/s / 2
    specials   = recon stealth/10, supply/40, air 40
Usage: scripts/balance/cost_model.py [out]
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

# Card counts per deck by sim kind and role, and AI target mix weights. Designed, not historical.
COUNTS = {"tank": 8, "ifv": 10, "infantry": 12, "artillery": 8, "aa": 4, "air": 4, "supply": 6, "command": 2}
COST_OVERRIDES: dict[str, int] = {}

# W6 balance pass 1 (2026-10-08): 400-match soak had NATO winning 37% cross-faction (CI 31-44%, band 35-65%).
# Tried NATO x0.92: NATO fell to 34.5%. Tried +3/m/s above 15 m/s: NATO fell to 14% (it priced the HEMTT
# and Abrams up too). Both reverted; see DECISIONS D026.
# D057 faction calibration: per-nation multipliers set from the faction soak (PROGRESS 2026-10-09).
NATION_COST_SCALE: dict[tuple[str, str], float] = {
    ("West Germany", "coldwar"): 0.65, ("United Kingdom", "coldwar"): 0.7, ("France", "coldwar"): 0.7,
    ("East Germany", "coldwar"): 0.9, ("China", "modern"): 0.9, ("United States", "modern"): 0.8, ("Soviet Union", "ww2"): 1.15,
}
SIDE_COST_SCALE = {"NATO": 1.15, "Warsaw Pact": 1.0, "Allies": 1.0, "Axis": 1.0, "Russia": 1.0, "China": 1.0}

# MEC-01 shipped decks: one per income profile per side. Same unit pool and costs; card counts are scaled per sim
# kind to fit the profile's tempo (designed, not historical). The first deck of each side is the G5 soak deck and
# keeps the unscaled counts.
#   balanced    combined-arms baseline
#   juggernaut  late-heavy income: more tanks and artillery, fewer scouts
#   vanguard    early-heavy income: recon, infantry and IFVs for the opening, fewer tanks
#   maverick    mid-battle peak: air, AA and mobile infantry for a phase-B strike
MAX_COUNT_PCT = 150
ARTILLERY_CARD_CAP = 4

PROFILE_SCALE = {
    "balanced": {},
    "juggernaut": {"tank": 150, "artillery": 150, "ifv": 75, "infantry": 75},
    "vanguard": {"tank": 50, "ifv": 125, "infantry": 125, "artillery": 75},
    "maverick": {"air": 200, "aa": 150, "tank": 75, "artillery": 75},
}
DECKS = (
    ("NATO", "us_1989_armored", "balanced", "coldwar", "United States"),
    ("Warsaw Pact", "su_1989_motor_rifle", "balanced", "coldwar", "Soviet Union (GSFG)"),
    ("NATO", "us_1989_heavy_division", "juggernaut", "coldwar", "United States"),
    ("Warsaw Pact", "su_1989_tank_division", "juggernaut", "coldwar", "Soviet Union (GSFG)"),
    ("NATO", "us_1989_armored_cavalry", "vanguard", "coldwar", "United States"),
    ("Warsaw Pact", "su_1989_forward_detachment", "vanguard", "coldwar", "Soviet Union (GSFG)"),
    ("NATO", "us_1989_air_cavalry", "maverick", "coldwar", "United States"),
    ("Warsaw Pact", "su_1989_air_assault", "maverick", "coldwar", "Soviet Union (GSFG)"),
    # Cold War 1989 allies (ERA-01): Bundeswehr, BAOR, French Army; NVA, Polish People's Army.
    ("NATO", "frg_1989_panzergrenadier", "balanced", "coldwar", "West Germany"),
    ("NATO", "frg_1989_panzer_division", "juggernaut", "coldwar", "West Germany"),
    ("NATO", "frg_1989_panzeraufklaerer", "vanguard", "coldwar", "West Germany"),
    ("NATO", "frg_1989_heeresflieger", "maverick", "coldwar", "West Germany"),
    ("NATO", "uk_1989_mechanised_brigade", "balanced", "coldwar", "United Kingdom"),
    ("NATO", "uk_1989_armoured_division", "juggernaut", "coldwar", "United Kingdom"),
    ("NATO", "uk_1989_armoured_recce", "vanguard", "coldwar", "United Kingdom"),
    ("NATO", "uk_1989_raf_germany", "maverick", "coldwar", "United Kingdom"),
    ("NATO", "fr_1989_division_blindee", "balanced", "coldwar", "France"),
    ("NATO", "fr_1989_regiment_de_chars", "juggernaut", "coldwar", "France"),
    ("NATO", "fr_1989_cavalerie_legere", "vanguard", "coldwar", "France"),
    ("NATO", "fr_1989_alat", "maverick", "coldwar", "France"),
    ("Warsaw Pact", "nva_1989_mot_schuetzen", "balanced", "coldwar", "East Germany"),
    ("Warsaw Pact", "nva_1989_panzer_division", "juggernaut", "coldwar", "East Germany"),
    ("Warsaw Pact", "nva_1989_aufklaerung", "vanguard", "coldwar", "East Germany"),
    ("Warsaw Pact", "nva_1989_luftstreitkraefte", "maverick", "coldwar", "East Germany"),
    ("Warsaw Pact", "pl_1989_zmechanizowana", "balanced", "coldwar", "Poland"),
    ("Warsaw Pact", "pl_1989_pancerna", "juggernaut", "coldwar", "Poland"),
    ("Warsaw Pact", "pl_1989_rozpoznawcza", "vanguard", "coldwar", "Poland"),
    ("Warsaw Pact", "pl_1989_lotnictwo", "maverick", "coldwar", "Poland"),
    # WW2 1944 factions (ERA-01): US Army, British Army, Red Army vs Wehrmacht. Allied decks filter by nation.
    ("Allies", "us_1944_infantry_division", "balanced", "ww2"),
    ("Axis", "ger_1944_infanterie_division", "balanced", "ww2"),
    ("Allies", "us_1944_armored_division", "juggernaut", "ww2"),
    ("Axis", "ger_1944_panzer_division", "juggernaut", "ww2"),
    ("Allies", "us_1944_cavalry_group", "vanguard", "ww2"),
    ("Axis", "ger_1944_aufklaerung", "vanguard", "ww2"),
    ("Allies", "us_1944_ixth_tac_air", "maverick", "ww2"),
    ("Axis", "ger_1944_schlachtgeschwader", "maverick", "ww2"),
    ("Allies", "uk_1944_infantry_division", "balanced", "ww2", "United Kingdom"),
    ("Allies", "uk_1944_guards_armoured", "juggernaut", "ww2", "United Kingdom"),
    ("Allies", "uk_1944_recce_regiment", "vanguard", "ww2", "United Kingdom"),
    ("Allies", "uk_1944_2nd_tactical_air", "maverick", "ww2", "United Kingdom"),
    ("Allies", "su_1944_rifle_corps", "balanced", "ww2", "Soviet Union"),
    ("Allies", "su_1944_guards_tank_army", "juggernaut", "ww2", "Soviet Union"),
    ("Allies", "su_1944_mobile_group", "vanguard", "ww2", "Soviet Union"),
    ("Allies", "su_1944_shturmovik_corps", "maverick", "ww2", "Soviet Union"),
    # Modern 2026 (ERA-01): US Army vs Russian Ground Forces vs the PLA.
    ("NATO", "us_2026_abct", "balanced", "modern", "United States"),
    ("Russia", "ru_2026_motor_rifle", "balanced", "modern", "Russia"),
    ("NATO", "us_2026_armored_division", "juggernaut", "modern", "United States"),
    ("Russia", "ru_2026_tank_army", "juggernaut", "modern", "Russia"),
    ("NATO", "us_2026_stryker_bct", "vanguard", "modern", "United States"),
    ("Russia", "ru_2026_btg", "vanguard", "modern", "Russia"),
    ("NATO", "us_2026_combat_aviation", "maverick", "modern", "United States"),
    ("Russia", "ru_2026_army_aviation", "maverick", "modern", "Russia"),
    # China (ERA-01): PLA Ground Force combined-arms brigades.
    ("China", "pla_2026_combined_arms_brigade", "balanced", "modern", "China"),
    ("China", "pla_2026_heavy_brigade", "juggernaut", "modern", "China"),
    ("China", "pla_2026_reconnaissance", "vanguard", "modern", "China"),
    ("China", "pla_2026_army_aviation", "maverick", "modern", "China"),
)

# D057: modern unit values run ~35% above the 1989 roster on the same DP income, so a modern AI fielded ~17 units in a
# 30-minute battle. ERA_COST_SCALE brings each era's balanced decks near the 1989 totals.
ERA_COST_SCALE = {"coldwar": 1.0, "ww2": 1.0, "modern": 0.75}


def nation_scale(side: str, nation: str, era: str) -> float:
    """The side premium (W6 pass 1) was measured on US decks against the Soviets in 1989; it prices the US thermal
    sights and fire control the Soviets lacked. It applies only there: other armies and eras get their own
    NATION_COST_SCALE entry (D057), else 1.0."""
    if (nation, era) in NATION_COST_SCALE:
        return NATION_COST_SCALE[(nation, era)]
    return SIDE_COST_SCALE[side] if era == "coldwar" and nation in ("United States", "Soviet Union (GSFG)") else 1.0


def value(u: dict, weapons: dict) -> int:
    a = u["armor"]
    v = u["hp"] / 2 + max(a["front_ke"], a["front_ce"]) / 8
    for m in u["weapons"]:
        w = weapons[m["weapon"]]
        pen = max(p for _, p in w["pen"]) if w.get("pen") else 0
        # Reach (W6 pass 2, MEC-09): direct-fire value scales with range, floor 25% at 500 m, full from 2 km. A 300 m
        # AT4 is worth far less than a 3.75 km TOW with the same penetration, because it rarely gets to fire.
        reach = min(1.0, max(0.25, w.get("range_max_m", 0) / 2000))
        if w["type"] in ("gun_artillery", "mortar", "rocket_artillery"):
            v += w.get("blast_damage", 0) / 2 * w.get("salvo", 1) ** 0.5
        elif pen >= 100:
            v += pen / 10 * reach
        else:
            v += w.get("damage", 0) * w.get("salvo", 1) * 3 / 10 * reach
        if w["type"] in ("sam", "aaa"):
            # Air defence threatens 250-350 point aircraft (MEC-09: no AA card may destroy more than 1.5x its cost in
            # aircraft on average): an extra half its per-round damage, times reach.
            v += w.get("damage", 0) / 2 * reach
    v += u["speed_mm_s"] / 1000 / 2
    v += u["stealth"] / 10 + u["supply"] / 40 + (40 if u["sim_kind"] == "air" else 0)
    return int(round(v / 5) * 5)


def main() -> int:
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "data" / "decks.json"
    roster = json.loads((ROOT / "data" / "roster.json").read_text())["units"]
    weapons = {w["id"]: w for w in json.loads((ROOT / "data" / "weapons.json").read_text())["weapons"]}
    decks = []
    for entry in DECKS:
        side, deck_id, income, era = entry[:4]
        nation = entry[4] if len(entry) > 4 else ("United States" if (side, era) == ("Allies", "ww2") else None)
        scale = PROFILE_SCALE[income]
        cards = []
        for u in roster:
            if u["side"] != side or u["era"] != era or (nation is not None and u["nation"] != nation):
                continue
            cost = COST_OVERRIDES.get(u["id"], int(round(value(u, weapons) * nation_scale(side, u["nation"], era) * ERA_COST_SCALE[era] / 5) * 5))
            same_kind = sum(1 for x in roster if x["side"] == side and x["era"] == era and (nation is None or x["nation"] == nation) and x["sim_kind"] == u["sim_kind"])
            base = max(1, COUNTS[u["sim_kind"]] // max(1, same_kind))
            count = max(1, COUNTS[u["sim_kind"]] * scale.get(u["sim_kind"], 100) // 100 // max(1, same_kind))
            if u["sim_kind"] == "artillery":
                # D057: a side's only artillery card took the whole artillery budget (8 x 25-pdr), which the AI's
                # guns-first rule spent before any manoeuvre unit. No artillery card holds more than 4 guns.
                base, count = min(base, ARTILLERY_CARD_CAP), min(count, ARTILLERY_CARD_CAP)
            # Availability cap per card (MEC-09): 150% of the balanced deck's count, the most any shipped profile deck
            # takes. No deck may hold more of one card, so a single-class deck cannot spam the best unit.
            cards.append({"unit": u["id"], "count": count, "max_count": max(count, base * MAX_COUNT_PCT // 100), "phase": u["phase"], "cost": max(10, cost)})
        decks.append({"id": deck_id, "side": side, "era": era, "nation": nation or "", "income": income, "cards": cards})
    out.write_text(json.dumps({"version": 1, "decks": decks}, indent=2) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
