#!/usr/bin/env python3
"""Copy the sim's data files into the Godot project (game/data/sim/), which ships them in the export.

    python3 scripts/sync_game_data.py           copy
    python3 scripts/sync_game_data.py --check   G10: fail if game/data/sim/ differs from data/

The game copies them from res:// to user:// at startup, because the sim's loaders use System.IO, which cannot read
inside a .pck (D060). It also writes game/data/sim/credits.json, the credits screen (UX-01), generated from ASSETS.md:
every PREFIX line (tool and license of a file family) and every reference row (author and license).
"""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
FILES = ("roster.json", "weapons.json", "units.json", "decks.json", "rules.json", "support.json",
         "field_requests.json", "operation_events.json", "theaters.json", "campaign.json")
DEST = ROOT / "game" / "data" / "sim"
# Files from outside data/ that the game reads at runtime (radio dialogue, CMD-07): source -> name in game/data/sim.
# dialogue/lines.csv ships as JSON: Godot imports .csv files as translation tables, so a raw CSV never reaches the build.
EXTRA = {"dialogue/lines.csv": "dialogue_lines.json", "audio/voices.json": "voices.json"}


def credits() -> bytes:
    own, tools, refs = [], [], []
    for line in (ROOT / "ASSETS.md").read_text(encoding="utf-8").splitlines():
        if line.startswith("PREFIX "):
            _, prefix, kind, *rest = line.split(" ", 3)
            (own if kind == "own-work" else tools).append(f"{prefix}: {kind} ({rest[0] if rest else ''})".replace(" ()", ""))
        elif line.count(" | ") >= 3 and not line.startswith("|"):
            cells = [c.strip() for c in line.split(" | ")]
            refs.append(f"{cells[2]} ({cells[3]}): {cells[1]}")
    sections = [
        {"title": "Six Actual", "lines": ["Every mesh, material and animation is scripted own work (D021). Speech and music are generated locally (D020)."]},
        {"title": "Own work", "lines": own},
        {"title": "Generated content and tools", "lines": tools},
        {"title": "Reference material (look and proportion only)", "lines": sorted(set(refs))},
    ]
    return (json.dumps({"source": "ASSETS.md", "sections": sections}, indent=1, ensure_ascii=False) + "\n").encode()


def main() -> int:
    check = "--check" in sys.argv[1:]
    stale = []
    for name in FILES:
        src = (ROOT / "data" / name).read_bytes()
        dst = DEST / name
        if dst.exists() and dst.read_bytes() == src:
            continue
        if check:
            stale.append(name)
        else:
            DEST.mkdir(parents=True, exist_ok=True)
            dst.write_bytes(src)
    for src_rel, name in EXTRA.items():
        src = (ROOT / src_rel).read_bytes()
        if src_rel.endswith(".csv"):
            with open(ROOT / src_rel, newline="") as f:
                src = (json.dumps({"source": src_rel, "lines": list(csv.DictReader(f))}, indent=1, ensure_ascii=False) + "\n").encode()
        dst = DEST / name
        if not (dst.exists() and dst.read_bytes() == src):
            if check:
                stale.append(name)
            else:
                DEST.mkdir(parents=True, exist_ok=True)
                dst.write_bytes(src)
    cred = DEST / "credits.json"
    if not (cred.exists() and cred.read_bytes() == credits()):
        if check:
            stale.append("credits.json")
        else:
            DEST.mkdir(parents=True, exist_ok=True)
            cred.write_bytes(credits())
    extra = sorted(p.name for p in DEST.glob("*.json") if p.name not in FILES and p.name not in ("credits.json", *EXTRA.values())) if DEST.exists() else []
    if check and (stale or extra):
        print(f"game data out of date: {stale or ''} {('unexpected ' + str(extra)) if extra else ''}; run scripts/sync_game_data.py")
        return 1
    print(f"sync_game_data: {len(FILES)} files {'checked' if check else 'in game/data/sim'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
