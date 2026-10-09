#!/usr/bin/env python3
"""Gate G6 (headless part): SIM-13 tick budget and cross-process determinism of the battle sim.

Runs `sim/Headless --bench <entities> <ticks>` twice in separate processes. Fails if p95 tick time exceeds
verify/thresholds.toml [perf].tick_p95_ms, if the battle never reached contact (no kills), or if the two
runs end with different world hashes. Rendered frame-time checks (PERF-01/02) are added with the Godot
benchmark scene.
"""
from __future__ import annotations

import re
import subprocess
import sys
import tomllib
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CMD = ["dotnet", "run", "--no-build", "--project", "sim/Headless/SixActual.Headless.csproj", "-c", "Release", "--", "--bench"]


def main() -> int:
    th = tomllib.loads((ROOT / "verify" / "thresholds.toml").read_text())["perf"]
    entities, ticks = 2000, 2400
    subprocess.run(["dotnet", "build", "sim/Headless/SixActual.Headless.csproj", "-c", "Release", "-warnaserror", "-v", "q"],
                   cwd=ROOT, check=True, capture_output=True)
    runs = []
    for _ in range(2):
        out = subprocess.run(CMD + [str(entities), str(ticks)], cwd=ROOT, capture_output=True, text=True, check=True).stdout
        print(out.strip())
        line = [l for l in out.splitlines() if l.startswith("bench ")][-1]
        runs.append(dict(re.findall(r"(\w+)=([\w.]+)", line)))
    bad = 0
    if runs[0]["hash"] != runs[1]["hash"]:
        print(f"G6: battle sim hashes differ across processes: {runs[0]['hash']} vs {runs[1]['hash']}", file=sys.stderr)
        bad += 1
    if int(runs[0]["kills"]) == 0:
        print("G6: benchmark never reached contact (0 kills); it does not measure combat", file=sys.stderr)
        bad += 1
    p95 = max(float(r["p95_ms"]) for r in runs)
    print(f"tick p95 {p95:.3f} ms (limit {th['tick_p95_ms']}) at {entities} entities")
    if p95 > th["tick_p95_ms"]:
        print("G6: SIM-13 tick budget exceeded", file=sys.stderr)
        bad += 1
    return 1 if bad else 0


if __name__ == "__main__":
    raise SystemExit(main())
