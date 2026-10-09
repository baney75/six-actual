#!/usr/bin/env python3
"""Generate data/operation_events.json: the campaign operation event deck (MEC-07), Cold War 1989.

    python3 scripts/generate_operation_events.py [out.json]

Between the phases of a multi-phase operation the sim draws one card (battle RNG) and applies it. Effects:
  weather    the next phase's weather (rules.weather_* names)
  dp_side    deployment points to every player of a side
  resupply   every surviving unit of both sides rearmed and refuelled overnight
Weights are designed; each card records its reasoning.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

# id: (effect, params, weight, why)
CARDS = {
    "fog_bank": ("weather", {"weather": "fog"}, 2, "North German Plain autumn fog: grounds aircraft, shortens sight lines"),
    "rain_front": ("weather", {"weather": "rain"}, 2, "an Atlantic front moves through"),
    "clear_skies": ("weather", {"weather": "clear"}, 3, "high pressure: good flying weather"),
    "nightfall_overcast": ("weather", {"weather": "overcast"}, 2, "low cloud over the front"),
    "nato_reinforcements": ("dp_side", {"side": "NATO", "dp": 300}, 2, "a REFORGER battalion arrives from the POMCUS sites"),
    "pact_second_echelon": ("dp_side", {"side": "Warsaw Pact", "dp": 300}, 2, "the army's second echelon regiment is committed"),
    "night_resupply": ("resupply", {}, 3, "both sides rearm and refuel in the lull between attacks"),
}


def build() -> dict:
    return {"version": 1, "era": "coldwar", "cards": [
        {"id": cid, "effect": eff, "params": params, "weight": weight, "why": why}
        for cid, (eff, params, weight, why) in CARDS.items()]}


def main() -> int:
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "data" / "operation_events.json"
    out.write_text(json.dumps(build(), indent=2) + "\n", encoding="utf-8")
    print(f"wrote {out} ({len(CARDS)} cards)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
