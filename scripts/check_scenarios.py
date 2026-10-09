#!/usr/bin/env python3
"""Gate G4: run verify/scenarios, then every mutant in verify/mutants.toml must break its scenarios.

Each mutant runs only the scenarios it names in must_fail: the full suite has already passed unmutated, and the
question per mutant is whether those scenarios notice the change.
"""
from __future__ import annotations

import json
import re
import shutil
import subprocess
import sys
import tempfile
import tomllib
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
HEADLESS = ["dotnet", "run", "--no-build", "--project", "sim/Headless/SixActual.Headless.csproj", "-c", "Release", "--", "--scenario"]


def run(extra: list[str], targets: list[str] | None = None) -> tuple[int, str]:
    r = subprocess.run(HEADLESS + (targets or ["verify/scenarios"]) + extra, cwd=ROOT, capture_output=True, text=True)
    return r.returncode, r.stdout + r.stderr


def scenario_files() -> dict[str, str]:
    """Scenario id -> file, so a mutant runs only the scenarios it must break (the suite already passed unmutated)."""
    out = {}
    for f in sorted((ROOT / "verify" / "scenarios").glob("*.json")):
        out[json.loads(f.read_text())["id"]] = str(f.relative_to(ROOT))
    return out


def failed_ids(out: str) -> set[str]:
    return {m.group(1) for m in re.finditer(r"^FAIL (\S+) ", out, re.M)}


def main() -> int:
    subprocess.run(["dotnet", "build", "sim/Headless/SixActual.Headless.csproj", "-c", "Release", "-warnaserror", "-v", "q"],
                   cwd=ROOT, check=True, capture_output=True)
    code, out = run([])
    print(out.strip())
    if code != 0:
        print("G4: scenario suite failed", file=sys.stderr)
        return 1
    base = json.loads((ROOT / "data" / "rules.json").read_text())
    mutants = tomllib.loads((ROOT / "verify" / "mutants.toml").read_text()).get("mutant", [])
    bad = 0
    for m in mutants:
        rules = json.loads(json.dumps(base))
        files = {}
        tmp = Path(tempfile.mkdtemp(prefix="mutant-"))
        (tmp / "data").mkdir()
        for name in ("roster.json", "weapons.json", "units.json", "support.json", "decks.json"):
            if (ROOT / "data" / name).exists():
                shutil.copy(ROOT / "data" / name, tmp / "data" / name)
        try:
            for dotted, value in m["set"].items():
                if dotted.startswith(("roster:", "weapons:", "support:", "decks:")):
                    # <file>:<item id>.<field path> for roster (units), weapons, support (options) and decks
                    kind, rest = dotted.split(":", 1)
                    path = tmp / "data" / f"{kind}.json"
                    doc = files.setdefault(path, json.loads(path.read_text()))
                    item_id, *fields = rest.split(".")
                    items = doc[{"roster": "units", "weapons": "weapons", "support": "options", "decks": "decks"}[kind]]
                    node = next((x for x in items if x["id"] == item_id), None)
                    if node is None:
                        print(f"mutant {m['name']}: no {kind} entry {item_id}", file=sys.stderr)
                        return 1
                else:
                    node, fields = rules, dotted.split(".")
                *parents, leaf = fields
                for k in parents:
                    node = node[k]
                if leaf not in node:
                    print(f"mutant {m['name']}: no field {dotted}", file=sys.stderr)
                    return 1
                node[leaf] = value
            for path, doc in files.items():
                path.write_text(json.dumps(doc))
            rules_path = tmp / "rules.json"
            rules_path.write_text(json.dumps(rules))
            ids = scenario_files()
            unknown = sorted(set(m["must_fail"]) - set(ids))
            if unknown:
                print(f"mutant {m['name']}: unknown scenarios {unknown}", file=sys.stderr)
                return 1
            _, mout = run(["--rules", str(rules_path), "--data-root", str(tmp)], [ids[i] for i in m["must_fail"]])
        finally:
            shutil.rmtree(tmp)
        missing = set(m["must_fail"]) - failed_ids(mout)
        status = "ok" if not missing else f"SURVIVED by {sorted(missing)}"
        print(f"mutant {m['name']}: {status}")
        bad += 1 if missing else 0
    return 1 if bad else 0


if __name__ == "__main__":
    raise SystemExit(main())
