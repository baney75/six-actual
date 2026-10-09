#!/usr/bin/env python3
"""G10 era check (ERA-04): no anachronisms in unit, weapon and support availability.

    python3 scripts/check_era.py        check data/, after a self-test on broken copies

Every roster unit and weapon carries an era and an in-service year; each must be no later than the era's last year
(ERAS). A unit may only mount weapons of its own era that entered service no later than the era year. Decks,
off-map support options and Field Requests task forces may only use units and weapons of their own era; a support
option's weapon must be in service by the era year.
"""
from __future__ import annotations

import copy
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ERAS = {"ww2": 1945, "coldwar": 1989, "modern": 2026}


def problems(roster: dict, weapons: dict, decks: dict, support: dict, field: dict) -> list[str]:
    out = []
    units = {u["id"]: u for u in roster["units"]}
    weaps = {w["id"]: w for w in weapons["weapons"]}
    for w in weaps.values():
        era = w.get("era")
        if era not in ERAS:
            out.append(f"weapon {w['id']}: unknown era {era}")
        elif not isinstance(w.get("year"), int) or w["year"] > ERAS[era]:
            out.append(f"weapon {w['id']}: year {w.get('year')} after {era} ({ERAS[era]})")
    for u in units.values():
        era = u.get("era")
        if era not in ERAS:
            out.append(f"unit {u['id']}: unknown era {era}")
            continue
        if u["year"] > ERAS[era]:
            out.append(f"unit {u['id']}: year {u['year']} after {era} ({ERAS[era]})")
        for m in u["weapons"]:
            w = weaps.get(m["weapon"])
            if w is None:
                out.append(f"unit {u['id']}: unknown weapon {m['weapon']}")
            elif w.get("era") != era or w.get("year", 9999) > ERAS[era]:
                out.append(f"unit {u['id']}: weapon {w['id']} ({w.get('era')} {w.get('year')}) not available in {era}")
    for d in decks["decks"]:
        eras = {units[c["unit"]]["era"] for c in d["cards"] if c["unit"] in units}
        if len(eras) > 1:
            out.append(f"deck {d['id']}: mixes eras {sorted(eras)}")
    for o in support["options"]:
        era = o.get("era")
        if era not in ERAS:
            out.append(f"support {o['id']}: unknown era {era}")
            continue
        uid = o["params"].get("unit")
        if uid is not None and (uid not in units or units[uid]["era"] != era or units[uid]["side"] != o["side"]):
            out.append(f"support {o['id']}: landing unit {uid} is not a {era} {o['side']} unit")
        wid = o["params"].get("weapon")
        if wid is not None:
            w = weaps.get(wid)
            if w is None or w.get("era") != era or w.get("year", 9999) > ERAS[era]:
                out.append(f"support {o['id']}: weapon {wid} not available in {era}")
    for t in field["task_forces"]:
        for e in t["units"]:
            u = units.get(e["unit"])
            if u is None or u["era"] != t["era"]:
                out.append(f"task force {t['id']}: {e['unit']} is not a {t['era']} unit")
    return out


def load() -> tuple:
    j = lambda n: json.loads((ROOT / "data" / n).read_text())
    return j("roster.json"), j("weapons.json"), j("decks.json"), j("support.json"), j("field_requests.json")


def self_test(data: tuple) -> None:
    def weapon_late(d):
        d[1]["weapons"][0]["year"] = 1995

    def unit_late(d):
        d[0]["units"][0]["year"] = 1993

    def mount_other_era(d):
        d[1]["weapons"][0]["era"] = "modern"

    def support_era(d):
        d[3]["options"][0]["era"] = "medieval"

    def landing_unit(d):
        landing = next(o for o in d[3]["options"] if "unit" in o["params"])
        landing["params"]["unit"] = "nope"

    def field_era(d):
        d[4]["task_forces"][0]["era"] = "ww2"

    for name, brk in {"weapon after era": weapon_late, "unit after era": unit_late, "mount from another era": mount_other_era,
                      "support unknown era": support_era, "landing unit unknown": landing_unit, "task force era mismatch": field_era}.items():
        bad = copy.deepcopy(data)
        brk(bad)
        if not problems(*bad):
            raise SystemExit(f"check_era self-test: '{name}' was not detected")


def main() -> int:
    data = load()
    self_test(data)
    errs = problems(*data)
    for e in errs:
        print("era:", e, file=sys.stderr)
    print(f"check_era: {len(data[0]['units'])} units, {len(data[1]['weapons'])} weapons, {len(data[3]['options'])} support options, {len(errs)} problems")
    return 1 if errs else 0


if __name__ == "__main__":
    raise SystemExit(main())
