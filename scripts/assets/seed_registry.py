#!/usr/bin/env python3
"""Generate assets_src/registry.json from data/units.json.

New units enter as `placeholder` with route `legacy-box-gate` (the Phase 0
meshes from scripts/build_models.py, which have no reference pack or review).
Existing entries keep their state and route. `--check` fails if the file on
disk differs from what this script would write.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REGISTRY = ROOT / "assets_src" / "registry.json"
KIND_TO_CLASS = {
    "tank": "ground_vehicle", "ifv": "ground_vehicle", "artillery": "ground_vehicle",
    "aa": "ground_vehicle", "supply": "ground_vehicle", "air": "aircraft",
    "infantry": "infantry_kit",
}


def build() -> str:
    units = json.loads((ROOT / "data" / "units.json").read_text(encoding="utf-8"))["units"]
    old = {}
    if REGISTRY.exists():
        old = {a["id"]: a for a in json.loads(REGISTRY.read_text(encoding="utf-8"))["assets"]}
    assets = []
    for u in sorted(units, key=lambda u: u["id"]):
        prev = old.get(u["id"], {})
        entry = {
            "id": u["id"],
            "class": KIND_TO_CLASS[u["kind"]],
            "state": prev.get("state", "placeholder"),
            "route": prev.get("route", "legacy-box-gate"),
        }
        if "rejections" in prev:
            entry["rejections"] = prev["rejections"]
        assets.append(entry)
    for aid, a in sorted(old.items()):
        if aid not in {u["id"] for u in units}:
            assets.append(a)
    assets.sort(key=lambda a: a["id"])
    return json.dumps({"version": 1, "assets": assets}, indent=2) + "\n"


if __name__ == "__main__":
    text = build()
    if "--check" in sys.argv:
        same = REGISTRY.exists() and REGISTRY.read_text(encoding="utf-8") == text
        print("registry current" if same else "assets_src/registry.json is stale; run scripts/assets/seed_registry.py")
        sys.exit(0 if same else 1)
    REGISTRY.write_text(text, encoding="utf-8")
    print(f"wrote {REGISTRY.relative_to(ROOT)}")
