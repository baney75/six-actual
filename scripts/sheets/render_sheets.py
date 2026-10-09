#!/usr/bin/env python3
"""Contact sheets for an asset stage (section M4): CS-1 ortho overlay with IoU, CS-2 turntable.

    (cd tools/py && uv run python ../../scripts/sheets/render_sheets.py <id> --stage blockout)

Renders the asset's .blend headless (scripts/sheets/blender_sheets.py), then composes:
  CS-1_ortho.png  side/front/top: reference mask in red, model in blue, overlap purple, IoU printed
  CS-2_turntable.png  8 views at 45 deg steps
  iou.json            per-view IoU and mask confidence
into build/assets_review/<id>/<stage>/. Masks: assets_src/refs/<id>/masks/<view>.png (+ .json);
a missing top mask is synthesised from the spec's plan dimensions and marked confidence "low".
Check parish-media status before running: EEVEE uses the GPU.
"""
from __future__ import annotations

import argparse
import json
import subprocess
import sys
from pathlib import Path

import cv2
import numpy as np

REPO = Path(__file__).resolve().parents[2]


def spec_number(spec: dict, name: str):
    for n in spec.get("numbers", []):
        if n["name"] == name:
            return n["value"]
    return None


def synth_top(spec: dict, px: int) -> np.ndarray:
    """Low-confidence plan mask from dimensions: hull rectangle plus the gun line (length with gun forward)."""
    L = spec_number(spec, "length_hull_m")
    W = spec_number(spec, "width_over_skirts_m") or spec_number(spec, "width_m")
    G = spec_number(spec, "length_gun_forward_m") or L
    img = np.zeros((int(W * px), int(G * px)), np.uint8)
    img[:, : int(L * px)] = 255
    mid = img.shape[0] // 2
    img[mid - int(0.1 * px): mid + int(0.1 * px), :] = 255
    return img


def tight(a: np.ndarray) -> np.ndarray:
    ys, xs = np.where(a > 127)
    return a[ys.min(): ys.max() + 1, xs.min(): xs.max() + 1]


def place(a: np.ndarray, h: int, w: int, x: int, y_bottom: int) -> np.ndarray:
    """Paste boolean `a` into an h x w canvas with its bottom-left corner at (x, h - y_bottom)."""
    out = np.zeros((h, w), bool)
    y1 = h - y_bottom
    y0 = y1 - a.shape[0]
    xa0, ya0 = max(0, -x), max(0, -y0)
    xa1, ya1 = min(a.shape[1], w - x), min(a.shape[0], h - y0)
    if xa1 > xa0 and ya1 > ya0:
        out[y0 + ya0: y0 + ya1, x + xa0: x + xa1] = a[ya0:ya1, xa0:xa1]
    return out


def iou_overlay(mask: np.ndarray, model: np.ndarray, anchor_right: bool, top_cut: bool = False,
                search_px: tuple[int, int] = (50, 10), bottom_cut_px: int = 0, front_cut: bool = False,
                centre_v: bool = False) -> tuple[float, np.ndarray, tuple[int, int]]:
    """Register the model silhouette on the reference mask and measure IoU.

    Start aligned on the ground line and the front (or centre, for front views), then search small offsets
    (+-0.5 m horizontally, +-0.1 m vertically at 100 px/m) for the best overlap: a reference photo's turret
    may be slightly traversed or its scale a little off, which should not count against the model's shape.
    With top_cut the reference holds no information above its own height, so the model is clipped there.
    bottom_cut_px excludes the band just above the ground line for both (the photo does not observe it).
    centre_v (plan views): there is no ground line; both are centred on the vehicle centreline instead, so a
    span difference splits evenly to both sides rather than all landing on one."""
    m = tight(mask) > 127
    s = tight(model) > 127
    if top_cut and s.shape[0] > m.shape[0]:
        s = s[s.shape[0] - m.shape[0]:, :]
    if front_cut:
        # The reference ends at its front edge (front = right in side views): align on the rear, clip the model.
        anchor_right = False
        s = s[:, : min(s.shape[1], m.shape[1])]
    pad = max(search_px) + 4
    h = max(m.shape[0], s.shape[0]) + 2 * pad
    w = max(m.shape[1], s.shape[1]) + 2 * pad
    if anchor_right:
        mx, sx = w - pad - m.shape[1], w - pad - s.shape[1]
    elif front_cut:
        mx, sx = pad, pad
    else:
        mx, sx = (w - m.shape[1]) // 2, (w - s.shape[1]) // 2
    hmax = max(m.shape[0], s.shape[0])
    ym = pad + ((hmax - m.shape[0]) // 2 if centre_v else 0)
    ys = pad + ((hmax - s.shape[0]) // 2 if centre_v else 0)
    cm = place(m, h, w, mx, ym)
    keep = np.ones((h, w), bool)
    if bottom_cut_px > 0:
        keep[h - pad - bottom_cut_px:, :] = False
    cm &= keep
    best = (-1.0, 0, 0)
    for dy in range(-search_px[1], search_px[1] + 1, 2):
        for dx in range(-search_px[0], search_px[0] + 1, 2):
            cs = place(s, h, w, sx + dx, ys + dy) & keep
            union = np.logical_or(cm, cs).sum()
            iou = np.logical_and(cm, cs).sum() / max(1, union)
            if iou > best[0]:
                best = (iou, dx, dy)
    iou, dx, dy = best
    cs = place(s, h, w, sx + dx, ys + dy) & keep
    vis = np.full((h, w, 3), 40, np.uint8)
    vis[cm & ~cs] = (60, 60, 230)
    vis[cs & ~cm] = (230, 120, 40)
    vis[cm & cs] = (200, 90, 200)
    return iou, vis, (dx, dy)


def label(img: np.ndarray, text: str) -> np.ndarray:
    bar = np.full((44, img.shape[1], 3), 24, np.uint8)
    cv2.putText(bar, text, (8, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.8, (235, 235, 235), 2, cv2.LINE_AA)
    return np.vstack([bar, img])


def kit_sheets(vid: str, stage: str, out: Path, raw: Path) -> int:
    """Building kits have street-photo references, not ortho masks (section M2 table): no CS-1 IoU. Sheets are
    the turntable (CS-2), elevations and plan at 40 px/m (CS-2b), and street-level plus RTS views (CS-2c)."""
    tiles = [cv2.resize(cv2.imread(str(raw / f"turntable_{k}.png")), (480, 300)) for k in range(8)]
    rows = [np.hstack(tiles[i * 4:(i + 1) * 4]) for i in range(2)]
    cv2.imwrite(str(out / "CS-2_turntable.png"), label(np.vstack(rows), f"CS-2 {vid} {stage}: 8 views at 45 deg"))
    closes = [cv2.imread(str(raw / f"closeup_{v}.png")) for v in ("front", "side", "top")]
    width = max(c.shape[1] for c in closes)
    closes = [np.pad(c, ((0, 10), (0, width - c.shape[1]), (0, 0)), constant_values=30) for c in closes]
    cv2.imwrite(str(out / "CS-2b_closeups.png"), label(np.vstack(closes), f"CS-2b {vid} {stage}: street elevation, side-street elevation, plan (40 px/m)"))
    street = [cv2.resize(cv2.imread(str(raw / f"street_{k}.png")), (960, 600)) for k in range(4)]
    rts = [cv2.resize(cv2.imread(str(raw / f"rts_{k}.png")), (960, 600)) for k in range(2)]
    grid = np.vstack([np.hstack(street[0:2]), np.hstack(street[2:4]), np.hstack(rts)])
    cv2.imwrite(str(out / "CS-2c_street.png"), label(grid, f"CS-2c {vid} {stage}: street level at 1.7 m eye height (rows 1-2), RTS camera ~150 m (row 3)"))
    (out / "iou.json").write_text(json.dumps({"n/a": "building kit: street-photo references, no ortho masks"}, indent=2) + "\n")
    print("KIT_SHEETS_OK")
    return 0


def squad_sheets(vid: str, stage: str, out: Path, raw: Path) -> int:
    """Infantry kits: photo references, no ortho masks. CS-2 turntable, CS-2b hero soldier front/side/back at
    300 px/m, CS-2c lineup at 120 px/m plus the RTS camera at 150 m (gameplay) and 60 m (closest zoom)."""
    tiles = [cv2.resize(cv2.imread(str(raw / f"turntable_{k}.png")), (480, 300)) for k in range(8)]
    rows = [np.hstack(tiles[i * 4:(i + 1) * 4]) for i in range(2)]
    cv2.imwrite(str(out / "CS-2_turntable.png"), label(np.vstack(rows), f"CS-2 {vid} {stage}: 8 views at 45 deg"))
    hero = [cv2.imread(str(raw / f"closeup_{v}.png")) for v in ("front", "side", "back")]
    h = max(c.shape[0] for c in hero)
    hero = [np.pad(c, ((0, h - c.shape[0]), (0, 10), (0, 0)), constant_values=30) for c in hero]
    row = np.hstack(hero)
    rto = [raw / f"closeup_{v}.png" for v in ("rto_side", "rto_back", "gr_side", "law_back")]
    if all(p.exists() for p in rto):
        r = [cv2.imread(str(p)) for p in rto]
        rh = max(c.shape[0] for c in r)
        r = np.hstack([np.pad(c, ((0, rh - c.shape[0]), (0, 10), (0, 0)), constant_values=30) for c in r])
        w = max(row.shape[1], r.shape[1])
        row = np.vstack([np.pad(row, ((0, 10), (0, w - row.shape[1]), (0, 0)), constant_values=30),
                         np.pad(r, ((0, 0), (0, w - r.shape[1]), (0, 0)), constant_values=30)])
    cv2.imwrite(str(out / "CS-2b_closeups.png"), label(row, f"CS-2b {vid} {stage}: row 1 soldier 00 front, side, back; row 2 RTO side, back, grenadier (M203) side, LAW rifleman back (300 px/m)"))
    lineup = cv2.imread(str(raw / "closeup_lineup.png"))
    rts = np.hstack([cv2.resize(cv2.imread(str(raw / f"rts_{k}.png")), (960, 600)) for k in range(2)])
    w = max(lineup.shape[1], rts.shape[1])
    lineup = np.pad(lineup, ((0, 10), (0, w - lineup.shape[1]), (0, 0)), constant_values=30)
    rts = np.pad(rts, ((0, 0), (0, w - rts.shape[1]), (0, 0)), constant_values=30)
    cv2.imwrite(str(out / "CS-2c_squad.png"), label(np.vstack([lineup, rts]), f"CS-2c {vid} {stage}: lineup (120 px/m); RTS camera at 150 m and 60 m"))
    (out / "iou.json").write_text(json.dumps({"n/a": "infantry kit: photo references, no ortho masks"}, indent=2) + "\n")
    print("SQUAD_SHEETS_OK")
    return 0


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("id")
    ap.add_argument("--stage", required=True)
    a = ap.parse_args()
    vid = a.id
    out = REPO / "build" / "assets_review" / vid / a.stage
    raw = out / "raw"
    blend = REPO / "assets_src" / "work" / vid / f"{vid}.blend"
    # Reference drawings drawn gear-up (mask json "gear_up": true) get silhouettes with the landing gear hidden.
    masks = REPO / "assets_src" / "refs" / vid / "masks"
    gear_up = any(json.loads(m.read_text()).get("gear_up") for m in masks.glob("*.json")) if masks.is_dir() else False
    spec = json.loads((REPO / "data" / "assets" / f"{vid}.json").read_text())
    kit = spec.get("class") == "building_kit"
    squad = spec.get("class") == "infantry_kit"
    r = subprocess.run([str(REPO / "tools" / "blender" / "blender"), "--background", str(blend), "--python",
                        str(REPO / "scripts" / "sheets" / "blender_sheets.py"), "--", str(raw)]
                       + (["--gear-up"] if gear_up else []) + (["--kit"] if kit else []) + (["--squad"] if squad else []),
                       capture_output=True, text=True)
    if "SHEETS_OK" not in r.stdout:
        print(r.stdout[-3000:], r.stderr[-3000:], file=sys.stderr)
        return 1
    if kit:
        return kit_sheets(vid, a.stage, out, raw)
    if squad:
        return squad_sheets(vid, a.stage, out, raw)
    px = 100
    results = {}
    panels = []
    for view in ("side", "front", "top"):
        model = cv2.imread(str(raw / f"silhouette_{view}.png"), cv2.IMREAD_GRAYSCALE)
        mpath = REPO / "assets_src" / "refs" / vid / "masks" / f"{view}.png"
        if mpath.exists():
            mask = cv2.imread(str(mpath), cv2.IMREAD_GRAYSCALE)
            meta = json.loads(mpath.with_suffix(".json").read_text())
            conf = meta.get("mask_confidence", "photo")
            top_cut = bool(meta.get("top_cut"))
            bottom_cut = int(round(float(meta.get("bottom_cut_m") or 0) * px))
            front_cut = bool(meta.get("front_cut"))
        elif view == "top":
            mask = synth_top(spec, px)
            conf = "low"
            top_cut = False
            bottom_cut = 0
            front_cut = False
        else:
            results[view] = {"iou": None, "confidence": "missing"}
            continue
        iou, vis, (dx, dy) = iou_overlay(mask, model, anchor_right=(view in ("side", "top")), top_cut=top_cut, bottom_cut_px=bottom_cut, front_cut=front_cut,
                                     centre_v=(view == "top"))
        results[view] = {"iou": round(float(iou), 4), "confidence": conf, "top_cut": top_cut, "registration_m": [dx / px, dy / px]}
        panels.append(label(vis, f"CS-1 {vid} {view}: IoU {iou:.3f} (mask {conf}, shift {dx / px:+.2f},{dy / px:+.2f} m)  red=ref blue=model"))
    width = max(p.shape[1] for p in panels)
    sheet = np.vstack([np.pad(p, ((0, 12), (0, width - p.shape[1]), (0, 0)), constant_values=16) for p in panels])
    cv2.imwrite(str(out / "CS-1_ortho.png"), sheet)
    tiles = [cv2.imread(str(raw / f"turntable_{k}.png")) for k in range(8)]
    tiles = [cv2.resize(t, (480, 300)) for t in tiles]
    rows = [np.hstack(tiles[i * 4:(i + 1) * 4]) for i in range(2)]
    cv2.imwrite(str(out / "CS-2_turntable.png"), label(np.vstack(rows), f"CS-2 {vid} {a.stage}: 8 views at 45 deg"))
    closes = [cv2.imread(str(raw / f"closeup_{v}.png")) for v in ("side", "top")]
    width = max(c.shape[1] for c in closes)
    closes = [np.pad(c, ((0, 10), (0, width - c.shape[1]), (0, 0)), constant_values=30) for c in closes]
    cv2.imwrite(str(out / "CS-2b_closeups.png"), label(np.vstack(closes), f"CS-2b {vid} {a.stage}: shaded ortho side and top (160 px/m)"))
    (out / "iou.json").write_text(json.dumps(results, indent=2) + "\n")
    print(json.dumps(results))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
