#!/usr/bin/env python3
"""PostToolUse: format touched C# and validate touched data JSON.

Exit 2 sends the failure back to Claude so it fixes the file it just wrote.
"""
import json
import os
import subprocess
import sys
from pathlib import Path

ROOT = Path(os.environ.get("CLAUDE_PROJECT_DIR") or Path(__file__).resolve().parents[2])


def run(cmd: list[str]) -> subprocess.CompletedProcess:
    env = dict(os.environ)
    env["PATH"] = f"{Path.home()}/.local/share/mise/shims:{env.get('PATH', '')}"
    return subprocess.run(cmd, cwd=ROOT, capture_output=True, text=True, env=env, timeout=170)


def main() -> int:
    data = json.load(sys.stdin)
    path = (data.get("tool_input") or {}).get("file_path") or ""
    if not path:
        return 0
    p = Path(path).resolve()
    try:
        rel = p.relative_to(ROOT).as_posix()
    except ValueError:
        return 0
    if rel.endswith(".cs"):
        project = "game/SixActual.sln" if rel.startswith("game/") else "SixActual.slnx"
        r = run(["dotnet", "format", project, "--include", rel, "--severity", "warn"])
        if r.returncode != 0:
            print(f"dotnet format failed on {rel}:\n{r.stdout[-2000:]}{r.stderr[-2000:]}", file=sys.stderr)
            return 2
    elif rel.startswith("data/") and rel.endswith(".json"):
        checks = [["python3", "scripts/validate_data.py"]]
        schema = ROOT / "data" / "schema" / (Path(rel).stem + ".schema.json")
        if schema.exists():
            checks.append(["mise", "exec", "--", "uvx", "--from", "check-jsonschema", "check-jsonschema",
                           "--schemafile", schema.relative_to(ROOT).as_posix(), rel])
        for cmd in checks:
            r = run(cmd)
            if r.returncode != 0:
                print(f"{' '.join(cmd)} failed after editing {rel}:\n{r.stdout[-2000:]}{r.stderr[-2000:]}",
                      file=sys.stderr)
                return 2
        if rel in ("data/units.json", "data/catalog.json"):
            print(f"{rel} is generated. Edit scripts/generate_*.py instead; G10 fails on a hand edit.",
                  file=sys.stderr)
            return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
