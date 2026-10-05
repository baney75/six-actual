#!/usr/bin/env python3
"""Fail when sim line coverage is under verify/thresholds.toml."""

import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def threshold() -> float:
    for line in (ROOT / "verify" / "thresholds.toml").read_text(encoding="utf-8").splitlines():
        stripped = line.strip()
        if stripped.startswith("sim_line_min"):
            return float(stripped.split("=", 1)[1].strip())
    raise SystemExit("sim_line_min missing from thresholds")


def main() -> int:
    reports = sorted((ROOT / "build" / "coverage").rglob("coverage.cobertura.xml"))
    if not reports:
        print("no coverage report", file=sys.stderr)
        return 1
    root = ET.parse(reports[-1]).getroot()
    hits = 0
    total = 0
    for cls in root.findall(".//class"):
        filename = cls.get("filename", "").replace("\\", "/")
        if "/sim/" not in f"/{filename}" and not filename.startswith("sim/"):
            continue
        if "/Headless/" in filename or "/sim.Tests/" in filename or "/analyzers/" in filename:
            continue
        for line in cls.findall("./lines/line"):
            total += 1
            if int(line.get("hits", "0")) > 0:
                hits += 1
    if total == 0:
        print("coverage report had no sim lines", file=sys.stderr)
        return 1
    rate = hits / total
    minimum = threshold()
    print(f"sim line coverage {rate:.3f} ({hits}/{total}), minimum {minimum:.3f}")
    return 0 if rate + 1e-9 >= minimum else 1


if __name__ == "__main__":
    raise SystemExit(main())
