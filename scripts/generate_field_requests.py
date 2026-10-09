#!/usr/bin/env python3
"""Generate data/field_requests.json: the Field Requests task-force pools (MEC-06), Cold War 1989.

    python3 scripts/generate_field_requests.py [out.json]

At each phase change (A->B, B->C) a player with Field Requests on is offered 3 of their side's task forces for the
new phase and picks one; its units arrive at the player's entry point in addition to the deck, free of deployment
points. Each pool holds 4 task forces of different character so the 3 offered always differ. Task forces are sized
to a value budget per phase (card costs from data/decks.json, the side's balanced deck) so the choice is about
composition, not size: each task force is within 15% of its side and phase budget (BUDGET). Organisations are company/battery-sized attachments
modelled on FM 71-2 (US task force cross-attachment) and FM 100-2-3 (Soviet regimental reinforcement); unit counts
are designed.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
# Value budgets per side and phase: NATO prices carry the x1.15 side cost scale (D040).
BUDGET = {"NATO": {"B": 480, "C": 700}, "Warsaw Pact": {"B": 400, "C": 600}}
TOLERANCE_PCT = 15

# (side, phase, id, name, [(unit, count)], why)
POOLS = [
    ("NATO", "B", "nato_b_mech_team", "Mech infantry team", [("m2a2_bradley", 1), ("us_mech_rifle_squad", 2), ("m47_dragon_team", 1)], "a Bradley platoon element cross-attached with its dismounts"),
    ("NATO", "B", "nato_b_antiarmor", "Antiarmor section", [("m901_itv", 2), ("m220_tow_team", 1)], "battalion antiarmor company ITV section plus a ground TOW"),
    ("NATO", "B", "nato_b_fires", "Fires and observation", [("m109a3_sph", 2), ("us_scout_section", 1)], "a DS howitzer section with forward observers"),
    ("NATO", "B", "nato_b_air_defense", "Air defense and scouts", [("m163_vads", 1), ("m48_chaparral", 1), ("stinger_team", 1), ("m3a2_bradley_cfv", 1)], "divisional ADA attachment screened by a cavalry track"),
    ("NATO", "C", "nato_c_tank_platoon", "Tank platoon", [("m1a1_abrams", 2), ("m60a3_tts", 1)], "an armor platoon (three tanks) cross-attached to the task force"),
    ("NATO", "C", "nato_c_armored_cav", "Armored cavalry troop element", [("m3a2_bradley_cfv", 2), ("m1a1_abrams", 1)], "cavalry scout section with a tank"),
    ("NATO", "C", "nato_c_attack_helicopters", "Attack helicopter team", [("ah64a_apache", 2), ("us_scout_section", 1)], "an AH-64 team on call to the brigade, with ground scouts to cue it"),
    ("NATO", "C", "nato_c_rocket_and_cas", "Close air support", [("a10a_thunderbolt", 1), ("m109a3_sph", 1), ("us_scout_section", 1)], "an A-10 sortie allocated to the brigade, with a howitzer and its observers"),
    ("Warsaw Pact", "B", "pact_b_motor_rifle", "Motor rifle platoon", [("bmp2", 1), ("sov_motor_rifle_squad", 2), ("9k111_fagot_team", 1)], "a BMP-2 platoon element with its dismounts and a Fagot team"),
    ("Warsaw Pact", "B", "pact_b_antitank", "Antitank platoon", [("9p148_konkurs", 2), ("9k111_fagot_team", 2)], "the regiment's antitank battery reinforcement"),
    ("Warsaw Pact", "B", "pact_b_artillery", "Artillery battery element", [("2s1_gvozdika", 3), ("sov_razvedka_section", 1)], "a 2S1 section with reconnaissance"),
    ("Warsaw Pact", "B", "pact_b_air_defence", "Air defence and recon", [("zsu23_4_shilka", 1), ("9k35_strela10", 1), ("9k38_igla_team", 1), ("brdm2", 1)], "the regimental air defence battery attachment"),
    ("Warsaw Pact", "C", "pact_c_tank_platoon", "Tank platoon", [("t80bv", 1), ("t64bv", 1)], "a tank platoon element attached to the motor rifle battalion"),
    ("Warsaw Pact", "C", "pact_c_forward_detachment", "Forward detachment", [("t80bv", 1), ("bmp2", 2)], "a forward detachment's lead tank and BMPs"),
    ("Warsaw Pact", "C", "pact_c_attack_helicopters", "Attack helicopter flight", [("mi24v_hind", 3)], "a Mi-24 flight from the army aviation regiment"),
    ("Warsaw Pact", "C", "pact_c_frontal_aviation", "Frontal aviation sortie", [("su25_frogfoot", 1), ("2s3_akatsiya", 1), ("brdm2", 1)], "an Su-25 sortie with a 152 mm gun in support and a BRDM to spot for it"),
]


def build() -> dict:
    decks = json.loads((ROOT / "data" / "decks.json").read_text())["decks"]
    cost = {}
    for d in decks:
        if d["income"] == "balanced":
            for c in d["cards"]:
                cost[c["unit"]] = c["cost"]
    out = []
    for side, phase, tid, name, units, why in POOLS:
        value = sum(cost[u] * n for u, n in units)
        budget = BUDGET[side][phase]
        lo, hi = budget * (100 - TOLERANCE_PCT) // 100, budget * (100 + TOLERANCE_PCT) // 100
        if not lo <= value <= hi:
            raise SystemExit(f"{tid}: value {value} outside {lo}-{hi} for phase {phase}")
        out.append({"id": tid, "era": "coldwar", "side": side, "phase": phase, "name": name, "value": value,
                    "units": [{"unit": u, "count": n, "cost": cost[u]} for u, n in units], "why": why})
    return {"version": 1, "offers_per_phase": 3, "task_forces": out}


def main() -> int:
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "data" / "field_requests.json"
    roster = {u["id"]: u for u in json.loads((ROOT / "data" / "roster.json").read_text())["units"]}
    data = build()
    for t in data["task_forces"]:
        for u in t["units"]:
            r = roster.get(u["unit"])
            if r is None or r["side"] != t["side"]:
                raise SystemExit(f"{t['id']}: {u['unit']} is not a {t['side']} roster unit")
    for side in ("NATO", "Warsaw Pact"):
        for phase in BUDGET[side]:
            n = sum(1 for t in data["task_forces"] if t["side"] == side and t["phase"] == phase)
            if n < data["offers_per_phase"] + 1:
                raise SystemExit(f"{side} phase {phase}: {n} task forces, need at least {data['offers_per_phase'] + 1}")
    out.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    print(f"wrote {out} ({len(data['task_forces'])} task forces)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
