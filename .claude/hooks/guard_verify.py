#!/usr/bin/env python3
"""PreToolUse: block writes to verify/ unless .claude/verify-unlock exists.

The unlock file holds one line: the reason and the DECISIONS.md id. The
commit-msg git hook is the hard backstop (VERIFY: prefix, own commit).
"""
import json
import os
import re
import sys
from pathlib import Path

ROOT = Path(os.environ.get("CLAUDE_PROJECT_DIR") or Path(__file__).resolve().parents[2])
UNLOCK = ROOT / ".claude" / "verify-unlock"
WRITE_HINT = re.compile(r"(>|>>|\btee\b|\bsed\s+-i|\bmv\b|\bcp\b|\brm\b|\btruncate\b|\bln\b|write_text|open\([^)]*['\"][wa])")


def blocked(what: str) -> int:
    print(f"{what} touches verify/, which is protected. Write a DECISIONS.md entry, then create "
          f".claude/verify-unlock with the reason and decision id. Commit it alone with a 'VERIFY:' prefix. "
          f"Loosening a threshold or changing a golden hash also needs Donovan's approval.", file=sys.stderr)
    return 2


def main() -> int:
    data = json.load(sys.stdin)
    tool = data.get("tool_name", "")
    inp = data.get("tool_input") or {}
    if UNLOCK.exists() and UNLOCK.read_text().strip():
        if tool in ("Write", "Edit", "MultiEdit") and "verify/" in (inp.get("file_path") or ""):
            print(f"verify/ unlocked: {UNLOCK.read_text().strip()}", file=sys.stderr)
        return 0
    if tool in ("Write", "Edit", "MultiEdit", "NotebookEdit"):
        path = Path(inp.get("file_path") or inp.get("notebook_path") or "").resolve()
        try:
            rel = path.relative_to(ROOT).as_posix()
        except ValueError:
            return 0
        if rel.startswith("verify/"):
            return blocked(rel)
    elif tool == "Bash":
        cmd = inp.get("command") or ""
        for seg in re.split(r"[;&|\n]+", cmd):
            if re.search(r"(^|[\s'\"=/])verify/", seg) and WRITE_HINT.search(seg):
                return blocked("This command")
    return 0


if __name__ == "__main__":
    sys.exit(main())
