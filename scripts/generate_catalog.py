#!/usr/bin/env python3
"""Write data/catalog.json. The script is the source of truth for the seed catalog."""

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "data" / "catalog.json"

CATALOG = {
    "version": 1,
    "eras": [
        {"id": "ww2", "year_start": 1944, "year_end": 1945},
        {"id": "cold_war", "year_start": 1989, "year_end": 1989},
        {"id": "modern", "year_start": 2020, "year_end": 2029},
    ],
    "units": [],
}


def main() -> None:
    destination = Path(sys.argv[1]) if len(sys.argv) > 1 else OUT
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(CATALOG, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
