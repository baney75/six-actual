#!/usr/bin/env python3
"""Gate G7 asset validator (section M6), stage-aware.

    python3 scripts/validate_assets.py [id ...]     (default: every registry item past "refs")

Checks per asset, with what applies at its registry stage:
  blockout+  dimensions within +-3% of the spec's sourced numbers; ortho IoU against masks at the
             mask's confidence (photo: side 0.88 / front 0.82 / top 0.85; low: each -0.05 and the asset is
             flagged for human review); required nodes per class (section M7).
  detail+    triangle budget per class for LOD0 (section M7).
  full       LODs, UVs, textures, destroyed variant, ASSETS.md entry (not built yet: reported as pending).
Reads assets_src/exports/<id>/<id>_build.json and build/assets_review/<id>/<stage>/iou.json.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
STAGES = ["placeholder", "spec", "refs", "blockout", "detail", "lod", "uv", "materials", "rig", "full", "engine", "approved"]
IOU = {"side": 0.88, "front": 0.82, "top": 0.85}
LOW_DROP = 0.05
BUDGET = {"ground_vehicle": (15000, 30000), "aircraft": (20000, 40000), "helicopter": (15000, 30000), "infantry_kit": (5000, 9000)}
NODES = {
    "ground_vehicle": ["Root", "Hull", "Turret", "Gun", "Muzzle_0", "Wheel_L_01", "Wheel_R_01", "Track_L", "Track_R"],
    "aircraft": ["Root", "Fuselage", "Aileron_L", "Aileron_R", "Elevator_L", "Elevator_R", "Rudder", "Cockpit", "Canopy"],
}
KIT_STATS = {
    "storey_height_typical_m": "storey_height_m",
    "roof_pitch_typical_deg": "roof_pitch_median_deg",
    "building_footprint_short_side_median_m": "footprint_short_median_m",
    "building_footprint_long_side_median_m": "footprint_long_median_m",
}
# Spec number -> build.json measurement it must match.
DIMS = {
    "length_hull_m": "length_hull_m",
    "length_gun_forward_m": "length_gun_forward_m",
    "width_over_skirts_m": "width_m",
    "height_over_cal50_mg_m": "height_m",
    "height_turret_roof_m": "height_turret_roof_m",
    "length_m": "length_m",
    "wingspan_m": "wingspan_m",
    "height_m": "height_m",
    "rifle_m16a2_length_m": "rifle_m16a2_length_m",
}


def stage_of(item: dict) -> str:
    st = item["state"].replace("-ready-for-review", "")
    return st if st in STAGES else "refs"


def validate(item: dict) -> tuple[list[str], list[str]]:
    vid, cls = item["id"], item["class"]
    stage = stage_of(item)
    problems, notes = [], []
    build = ROOT / "assets_src" / "exports" / vid / f"{vid}_build.json"
    if not build.exists():
        return [f"{vid}: no build at {build.relative_to(ROOT)}"], notes
    b = json.loads(build.read_text())
    spec = json.loads((ROOT / "data" / "assets" / f"{vid}.json").read_text())
    for n in spec.get("numbers", []):
        key = DIMS.get(n["name"])
        if key is None or key not in b or n.get("unit") != "m":
            continue
        got, want = b[key], n["value"]
        err = abs(got - want) / want
        (problems if err > 0.03 else notes).append(f"{vid}: {n['name']} {got} vs {want} ({err:+.1%})")
    nodes = b.get("nodes", [])
    for node in NODES.get(cls, []):
        # Section M7 allows twin control surfaces as <name>_L/<name>_R (twin-fin rudders).
        if node not in nodes and not (f"{node}_L" in nodes and f"{node}_R" in nodes):
            problems.append(f"{vid}: missing node {node}")
    for n in spec.get("numbers", []):
        # Building kits: distribution checks against sourced medians, 10% (the 3 m sim chunk grid quantises sizes).
        key = KIT_STATS.get(n["name"])
        if key is None or b.get(key) is None:
            continue
        got, want = b[key], n["value"]
        err = abs(got - want) / want
        (problems if err > 0.10 else notes).append(f"{vid}: {n['name']} {got} vs {want} ({err:+.1%})")
    iou_path = ROOT / "build" / "assets_review" / vid / stage / "iou.json"
    if cls in ("building_kit", "infantry_kit"):
        if not iou_path.exists():
            problems.append(f"{vid}: no contact sheets for stage {stage}")
    elif iou_path.exists():
        for view, r in json.loads(iou_path.read_text()).items():
            if r.get("iou") is None:
                problems.append(f"{vid}: no {view} mask")
                continue
            need = IOU[view] - (LOW_DROP if r["confidence"] == "low" else 0)
            (problems if r["iou"] < need else notes).append(f"{vid}: IoU {view} {r['iou']:.3f} (need {need:.2f}, mask {r['confidence']})")
            if r["confidence"] == "low":
                notes.append(f"{vid}: {view} mask is low confidence -> human review required")
    else:
        problems.append(f"{vid}: no contact sheets for stage {stage} ({iou_path.relative_to(ROOT)})")
    if STAGES.index(stage) >= STAGES.index("detail") and cls in BUDGET:
        lo, hi = BUDGET[cls]
        t = b.get("triangles_lod0", 0)
        if not lo <= t <= hi:
            problems.append(f"{vid}: LOD0 {t} triangles outside {lo}-{hi}")
    if STAGES.index(stage) >= STAGES.index("full"):
        notes.append(f"{vid}: full-stage checks (LODs, UVs, textures, destroyed variant) not built yet")
    return problems, notes


def main() -> int:
    reg = json.loads((ROOT / "assets_src" / "registry.json").read_text())["assets"]
    want = set(sys.argv[1:])
    items = [a for a in reg if (a["id"] in want) if want] or [a for a in reg if STAGES.index(stage_of(a)) >= STAGES.index("blockout")]
    bad = 0
    for item in items:
        problems, notes = validate(item)
        for n in notes:
            print("  ok ", n)
        for p in problems:
            print("  FAIL", p)
        bad += len(problems)
    print(f"G7: {len(items)} assets checked, {bad} problems")
    return 1 if bad else 0


if __name__ == "__main__":
    raise SystemExit(main())
