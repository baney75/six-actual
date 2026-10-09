#!/usr/bin/env python3
"""Trace an orthographic silhouette mask from a DISTRIBUTABLE reference photo (section M2).

GrabCut inside a hint box, keep the largest component, remove thin parts (antennas, whips) with a
morphological opening, crop to the silhouette and scale so its horizontal extent equals --length-m.
Writes <out>.png (white = vehicle, 100 px per metre by default), <out>.json (view, source, scale, measured
height) and <out>.debug.png (overlay on the photo) so the trace can be checked by eye before use.

Never run this on look-only material: masks are verification data derived from the reference (R1).
Run with: (cd tools/py && uv run python ../../scripts/refs/trace_ortho.py ...)
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

import cv2
import numpy as np

REPO = Path(__file__).resolve().parents[2]


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("image")
    ap.add_argument("--box", nargs=4, type=int, required=True, metavar=("X0", "Y0", "X1", "Y1"))
    ap.add_argument("--length-m", type=float, required=True, help="real horizontal extent of the silhouette")
    ap.add_argument("--view", choices=["side", "front", "top"], required=True)
    ap.add_argument("--out", required=True, help="output path without extension")
    ap.add_argument("--px-per-m", type=int, default=100)
    ap.add_argument("--confidence", choices=["photo", "low"], default="",
                    help="override: 'low' for cluttered photos (dust, stowage, foreshortening); record why with --why")
    ap.add_argument("--why", default="", help="reason recorded in the mask metadata")
    ap.add_argument("--front-cut", action="store_true",
                    help="the box's front edge cuts the vehicle (gun out of frame, attachment): align on the rear and clip the model there")
    ap.add_argument("--bottom-cut-m", type=float, default=0.0,
                    help="the photo does not observe the vehicle below this height (dust, shadow): IoU ignores that band")
    ap.add_argument("--top-cut", action="store_true",
                    help="the box top deliberately cuts through the vehicle (crew, antennas): IoU ignores the model above the mask")
    ap.add_argument("--height-m", type=float, default=0.0,
                    help="scale height independently to this real value (corrects perspective compression); marks confidence low")
    ap.add_argument("--flip", action="store_true", help="mirror so the vehicle faces right (+Y in the model)")
    ap.add_argument("--open-m", type=float, default=0.10, help="remove features thinner than this (metres)")
    ap.add_argument("--iters", type=int, default=8)
    ap.add_argument("--lineart", action="store_true",
                    help="the source is a line drawing (3-view): fill closed outlines instead of GrabCut")
    ap.add_argument("--rotate", type=int, choices=[0, 90, -90, 180], default=0,
                    help="rotate the traced silhouette (e.g. a nose-up plan view to nose-right)")
    ap.add_argument("--fg", nargs=4, type=int, action="append", default=[], metavar=("X0", "Y0", "X1", "Y1"),
                    help="known vehicle inside the box (e.g. a thin gun barrel against clutter); repeatable")
    ap.add_argument("--bg", nargs=4, type=int, action="append", default=[], metavar=("X0", "Y0", "X1", "Y1"),
                    help="known background inside the box (e.g. the gap under the belly); repeatable")
    ap.add_argument("--erase", nargs=4, type=int, action="append", default=[], metavar=("X0", "Y0", "X1", "Y1"),
                    help="source pixels removed from the silhouette (drawn stores, crew, props); repeatable, recorded")
    ap.add_argument("--erase-why", default="", help="reason for --erase, recorded in the mask metadata")
    ap.add_argument("--gear-up", action="store_true", help="the reference is drawn gear-up; sheets hide the model's gear")
    a = ap.parse_args()
    if a.erase and not a.erase_why:
        raise SystemExit("--erase needs --erase-why")

    img = cv2.imread(a.image)
    if img is None:
        raise SystemExit(f"cannot read {a.image}")
    x0, y0, x1, y1 = a.box
    if a.lineart:
        # Ink = dark pixels; everything the box border can reach without crossing ink is outside.
        gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)[y0:y1, x0:x1]
        ink = np.where(gray < 160, 255, 0).astype(np.uint8)
        ink = cv2.dilate(ink, np.ones((2, 2), np.uint8))
        outside = ink.copy()
        flood = np.zeros((outside.shape[0] + 2, outside.shape[1] + 2), np.uint8)
        for seed in [(0, 0), (outside.shape[1] - 1, 0), (0, outside.shape[0] - 1), (outside.shape[1] - 1, outside.shape[0] - 1)]:
            if outside[seed[1], seed[0]] == 0:
                cv2.floodFill(outside, flood, seed, 128)
        inside = np.where(outside != 128, 255, 0).astype(np.uint8)
        fg = np.zeros(img.shape[:2], np.uint8)
        fg[y0:y1, x0:x1] = inside
    else:
        mask = np.full(img.shape[:2], cv2.GC_BGD, np.uint8)
        mask[y0:y1, x0:x1] = cv2.GC_PR_FGD
        for bx0, by0, bx1, by1 in a.bg:
            mask[by0:by1, bx0:bx1] = cv2.GC_BGD
        for fx0, fy0, fx1, fy1 in a.fg:
            mask[fy0:fy1, fx0:fx1] = cv2.GC_FGD
        bgd = np.zeros((1, 65), np.float64)
        fgd = np.zeros((1, 65), np.float64)
        cv2.grabCut(img, mask, None, bgd, fgd, a.iters, cv2.GC_INIT_WITH_MASK)
        fg = np.where((mask == cv2.GC_FGD) | (mask == cv2.GC_PR_FGD), 255, 0).astype(np.uint8)

    for ex0, ey0, ex1, ey1 in a.erase:
        fg[ey0:ey1, ex0:ex1] = 0
    n, labels, stats, _ = cv2.connectedComponentsWithStats(fg)
    if n < 2:
        raise SystemExit("no foreground found")
    biggest = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
    fg = np.where(labels == biggest, 255, 0).astype(np.uint8)

    xs = np.where(fg.any(axis=0))[0]
    ys = np.where(fg.any(axis=1))[0]
    px_per_m_photo = (xs[-1] - xs[0] + 1) / a.length_m
    k = max(1, int(round(a.open_m * px_per_m_photo)))
    fg = cv2.morphologyEx(fg, cv2.MORPH_OPEN, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (k, k)))
    fg = cv2.morphologyEx(fg, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (k, k)))
    # A silhouette has no holes: fill everything the outside background cannot reach.
    padded = cv2.copyMakeBorder(fg, 1, 1, 1, 1, cv2.BORDER_CONSTANT, value=0)
    reach = padded.copy()
    flood = np.zeros((reach.shape[0] + 2, reach.shape[1] + 2), np.uint8)
    cv2.floodFill(reach, flood, (0, 0), 255)
    fg = (padded | cv2.bitwise_not(reach))[1:-1, 1:-1]
    xs = np.where(fg.any(axis=0))[0]
    ys = np.where(fg.any(axis=1))[0]
    crop = fg[ys[0]:ys[-1] + 1, xs[0]:xs[-1] + 1]
    if a.flip:
        crop = crop[:, ::-1]
    if a.rotate == 90:
        crop = cv2.rotate(crop, cv2.ROTATE_90_CLOCKWISE)
    elif a.rotate == -90:
        crop = cv2.rotate(crop, cv2.ROTATE_90_COUNTERCLOCKWISE)
    elif a.rotate == 180:
        crop = cv2.rotate(crop, cv2.ROTATE_180)
    scale = a.length_m * a.px_per_m / crop.shape[1]
    out_h = int(round(a.height_m * a.px_per_m)) if a.height_m > 0 else int(round(crop.shape[0] * scale))
    out = cv2.resize(crop, (int(round(crop.shape[1] * scale)), out_h), interpolation=cv2.INTER_AREA)
    out = np.where(out > 127, 255, 0).astype(np.uint8)
    base = Path(a.out)
    base.parent.mkdir(parents=True, exist_ok=True)
    cv2.imwrite(str(base.with_suffix(".png")), out)

    overlay = img.copy()
    tint = np.zeros_like(img)
    tint[:, :, 2] = fg
    overlay = cv2.addWeighted(overlay, 1.0, tint, 0.5, 0)
    cv2.rectangle(overlay, (x0, y0), (x1, y1), (0, 255, 255), 2)
    cv2.imwrite(str(base.with_suffix(".debug.png")), overlay)

    meta = {
        "view": a.view,
        "source": str(Path(a.image).resolve().relative_to(REPO)),
        "box": a.box,
        "bg": a.bg,
        "fg": a.fg,
        "length_m": a.length_m,
        "height_m": round(out.shape[0] / a.px_per_m, 3),
        "px_per_m": a.px_per_m,
        "flipped": a.flip,
        "open_m": a.open_m,
        "mask_confidence": a.confidence or ("low" if a.height_m > 0 else ("drawing" if a.lineart else "photo")),
        "confidence_why": a.why,
        "height_scaled_to_m": a.height_m or None,
        "top_cut": a.top_cut,
        "bottom_cut_m": a.bottom_cut_m,
        "front_cut": a.front_cut,
        "erase": a.erase,
        "erase_why": a.erase_why or None,
        "gear_up": a.gear_up,
        "note": ("ink trace of a distributable orthographic drawing" if a.lineart
                 else "GrabCut trace of a distributable photo; perspective not corrected (telephoto side/front views only)"),
    }
    base.with_suffix(".json").write_text(json.dumps(meta, indent=2) + "\n")
    print(json.dumps(meta))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
