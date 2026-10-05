#!/usr/bin/env python3
"""Every shipped asset file must be named in ASSETS.md."""

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ROOTS = [ROOT / "game" / "assets", ROOT / "audio"]
ALLOW = {".gitkeep"}


def main() -> int:
    assets_md = (ROOT / "ASSETS.md").read_text(encoding="utf-8")
    missing: list[str] = []
    checked = 0
    for folder in ROOTS:
        if not folder.exists():
            continue
        for path in folder.rglob("*"):
            if not path.is_file() or path.name in ALLOW:
                continue
            checked += 1
            relative = path.relative_to(ROOT).as_posix()
            covered = relative in assets_md
            if not covered:
                for line in assets_md.splitlines():
                    if line.startswith("PREFIX ") and relative.startswith(line.split()[1]):
                        covered = True
                        break
            if not covered:
                missing.append(relative)
    if missing:
        print("assets missing an ASSETS.md entry:", file=sys.stderr)
        for item in missing:
            print(item, file=sys.stderr)
        return 1
    print(f"licenses ok: {checked} shipped files")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
