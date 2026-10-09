#!/usr/bin/env python3
"""G10 deck check (MEC-01): every shipped deck in data/decks.json is playable and consistent with the rules.

    python3 scripts/check_decks.py            check data/decks.json, after a self-test on broken copies

Per deck: unique id; side one of SIDES; units of the deck's era and nation; income is a profile in rules.economy.income_per_minute; every card names
a roster unit of the deck's side, a phase A/B/C, 1 <= count <= max_count (availability cap) and cost >= 10; at least one card is deployable in phase A.
Across decks: each side that ships decks ships one per income profile, so all four profiles are playable by every faction.
"""
from __future__ import annotations

import copy
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SIDES = ("NATO", "Warsaw Pact", "Allies", "Axis", "Russia", "China")


def problems(decks: dict, roster: list, rules: dict) -> list[str]:
    out = []
    units = {u["id"]: u for u in roster}
    profiles = set(rules["economy"]["income_per_minute"])
    seen = set()
    have = {s: set() for s in SIDES}
    factions: dict[tuple, set] = {}
    for d in decks["decks"]:
        did = d.get("id", "?")
        if did in seen:
            out.append(f"{did}: duplicate id")
        seen.add(did)
        if d.get("side") not in SIDES:
            out.append(f"{did}: bad side {d.get('side')}")
            continue
        if d.get("income") not in profiles:
            out.append(f"{did}: income {d.get('income')} not in {sorted(profiles)}")
        else:
            have[d["side"]].add(d["income"])
            factions.setdefault((d["side"], d.get("era", ""), d.get("nation", "")), set()).add(d["income"])
        if not any(c.get("phase") == "A" for c in d["cards"]):
            out.append(f"{did}: nothing deployable in phase A")
        for c in d["cards"]:
            u = units.get(c.get("unit"))
            if u is None:
                out.append(f"{did}: unknown unit {c.get('unit')}")
            elif u["side"] != d["side"]:
                out.append(f"{did}: {c['unit']} belongs to {u['side']}")
            elif d.get("nation") and u.get("nation") != d["nation"]:
                out.append(f"{did}: {c['unit']} is {u.get('nation')}, deck is {d['nation']}")
            elif d.get("era") and u.get("era") != d["era"]:
                out.append(f"{did}: {c['unit']} is {u.get('era')}, deck is {d['era']}")
            if c.get("phase") not in ("A", "B", "C"):
                out.append(f"{did}: {c.get('unit')} bad phase {c.get('phase')}")
            if not isinstance(c.get("count"), int) or c["count"] < 1:
                out.append(f"{did}: {c.get('unit')} count {c.get('count')}")
            elif c["count"] > c.get("max_count", 0):
                out.append(f"{did}: {c.get('unit')} count {c['count']} above its availability cap {c.get('max_count')}")
            if not isinstance(c.get("cost"), int) or c["cost"] < 10:
                out.append(f"{did}: {c.get('unit')} cost {c.get('cost')}")
    for s in SIDES:
        if not have[s] and not any(d.get("side") == s for d in decks["decks"]):
            continue
        missing = profiles - have[s]
        if missing:
            out.append(f"{s}: no deck for income profiles {sorted(missing)}")
    # ERA-01: every faction (side, era, nation) that ships decks ships all four income profiles.
    for (side, era, nation), got in sorted(factions.items()):
        missing = profiles - got
        if missing:
            out.append(f"{side}/{era}/{nation or 'any'}: no deck for income profiles {sorted(missing)}")
    return out


def self_test(decks: dict, roster: list, rules: dict) -> None:
    breaks = {
        "unknown unit": lambda d: d["decks"][0]["cards"][0].update(unit="nope"),
        "wrong side": lambda d: d["decks"][0]["cards"][0].update(unit=d["decks"][1]["cards"][0]["unit"]),
        "bad income": lambda d: d["decks"][0].update(income="lavish"),
        "missing profile": lambda d: d["decks"].pop(),
        "bad phase": lambda d: d["decks"][0]["cards"][0].update(phase="D"),
        "zero count": lambda d: d["decks"][0]["cards"][0].update(count=0),
        "wrong nation": lambda d: next(x for x in d["decks"] if x.get("nation") == "France")["cards"][0].update(unit=next(x for x in d["decks"] if x.get("nation") == "West Germany")["cards"][0]["unit"]),
        "faction missing profile": lambda d: d["decks"].remove(next(x for x in d["decks"] if x.get("nation") == "China")),
        "over cap": lambda d: d["decks"][0]["cards"][0].update(count=d["decks"][0]["cards"][0]["max_count"] + 1),
    }
    for name, brk in breaks.items():
        bad = copy.deepcopy(decks)
        brk(bad)
        if not problems(bad, roster, rules):
            raise SystemExit(f"check_decks self-test: '{name}' was not detected")


def main() -> int:
    decks = json.loads((ROOT / "data" / "decks.json").read_text())
    roster = json.loads((ROOT / "data" / "roster.json").read_text())["units"]
    rules = json.loads((ROOT / "data" / "rules.json").read_text())
    self_test(decks, roster, rules)
    errs = problems(decks, roster, rules)
    for e in errs:
        print("deck:", e, file=sys.stderr)
    print(f"check_decks: {len(decks['decks'])} decks, {len(errs)} problems")
    return 1 if errs else 0


if __name__ == "__main__":
    raise SystemExit(main())
