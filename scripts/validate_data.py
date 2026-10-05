#!/usr/bin/env python3
"""Referential and range checks beyond JSON Schema."""

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main() -> int:
    catalog = json.loads((ROOT / "data" / "catalog.json").read_text(encoding="utf-8"))
    errors: list[str] = []
    era_ids: list[str] = []
    for era in catalog["eras"]:
        era_ids.append(era["id"])
        if era["year_end"] < era["year_start"]:
            errors.append(f"{era['id']}: year_end precedes year_start")
    if len(era_ids) != len(set(era_ids)):
        errors.append("duplicate era ids")

    unit_ids: list[str] = []
    known = set(era_ids)
    for unit in catalog["units"]:
        unit_ids.append(unit["id"])
        if unit["era"] not in known:
            errors.append(f"{unit['id']}: unknown era {unit['era']}")
        era = next(item for item in catalog["eras"] if item["id"] == unit["era"])
        if not era["year_start"] <= unit["year"] <= era["year_end"]:
            errors.append(f"{unit['id']}: year {unit['year']} is outside {unit['era']}")
    if len(unit_ids) != len(set(unit_ids)):
        errors.append("duplicate unit ids")

    roster = json.loads((ROOT / "data" / "units.json").read_text(encoding="utf-8"))
    seen = set()
    for unit in roster["units"]:
        if unit["id"] in seen:
            errors.append(f"duplicate roster id {unit['id']}")
        seen.add(unit["id"])
        if unit["era"] not in known:
            errors.append(f"{unit['id']}: unknown era {unit['era']}")
            continue
        era = next(item for item in catalog["eras"] if item["id"] == unit["era"])
        if not era["year_start"] <= unit["year"] <= era["year_end"]:
            errors.append(f"{unit['id']}: year {unit['year']} is outside {unit['era']}")
        if unit["armor_front"] < unit["armor_side"]:
            errors.append(f"{unit['id']}: side armor exceeds front armor")

    if errors:
        for error in errors:
            print(error, file=sys.stderr)
        return 1
    print(f"catalog ok: {len(era_ids)} eras, {len(unit_ids)} catalog units, {len(seen)} roster units")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
