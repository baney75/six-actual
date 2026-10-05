#!/usr/bin/env python3
"""Ban unfinished markers and skipped tests in production and test code."""

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SKIP_PARTS = {"bin", "obj", ".godot", "tools"}
BANNED = ("NotImplementedException", "Debug.Assert(false)", "TODO", "FIXME")
SKIP_MARKERS = ("Skip =", "[Fact(Skip", "[Theory(Skip")


def main() -> int:
    errors: list[str] = []
    for folder in (ROOT / "sim", ROOT / "sim.Tests", ROOT / "analyzers", ROOT / "game"):
        if not folder.exists():
            continue
        for path in folder.rglob("*.cs"):
            if any(part in SKIP_PARTS for part in path.parts):
                continue
            text = path.read_text(encoding="utf-8")
            relative = path.relative_to(ROOT).as_posix()
            for marker in BANNED:
                if marker in text:
                    errors.append(f"{relative}: contains {marker}")
            for marker in SKIP_MARKERS:
                if marker in text:
                    errors.append(f"{relative}: skipped test ({marker})")
    if errors:
        for error in errors:
            print(error, file=sys.stderr)
        return 1
    print("code scan ok")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
