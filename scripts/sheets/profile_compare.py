#!/usr/bin/env python3
"""Print mask vs model silhouette top/bottom edge heights (metres) along a view, aligned like CS-1.

    (cd tools/py && uv run python ../../scripts/sheets/profile_compare.py <id> <stage> <view> [step_m])
A modelling aid: shows where the model's outline departs from the traced reference.
"""
import sys
from pathlib import Path

import cv2
import numpy as np

REPO = Path(__file__).resolve().parents[2]
vid, stage, view = sys.argv[1:4]
step = float(sys.argv[4]) if len(sys.argv) > 4 else 0.5
px = 100


def tight(a):
    ys, xs = np.where(a > 127)
    return a[ys.min(): ys.max() + 1, xs.min(): xs.max() + 1] > 127


import json as _json
mask = tight(cv2.imread(str(REPO / "assets_src/refs" / vid / "masks" / f"{view}.png"), 0))
model = tight(cv2.imread(str(REPO / "build/assets_review" / vid / stage / "raw" / f"silhouette_{view}.png"), 0))
_meta = REPO / "assets_src/refs" / vid / "masks" / f"{view}.json"
FROM_REAR = _meta.exists() and _json.loads(_meta.read_text()).get("front_cut")
if FROM_REAR:
    # Rear-aligned comparison (front = right): keep the model's rear part, the width of the mask.
    model = model[:, : mask.shape[1]]
    print("(front_cut mask: positions measured from the REAR)")
w = max(mask.shape[1], model.shape[1])


def edges(a, x):
    col = a[:, x] if 0 <= x < a.shape[1] else np.zeros(a.shape[0], bool)
    ys = np.where(col)[0]
    if len(ys) == 0:
        return None, None
    return (a.shape[0] - ys.min()) / px, (a.shape[0] - ys.max() - 1) / px


print(f"{view}: position measured from the {'front (right)' if view != 'front' else 'centre'} in m; top/bottom heights in m")
print(" pos   mask_top model_top  diff   mask_bot model_bot")
for k in range(int(w / (step * px)) + 1):
    if view == "front":
        off = int((k * step - w / px / 2) * px)
        xm = mask.shape[1] // 2 + off
        xs = model.shape[1] // 2 + off
        pos = k * step - w / px / 2
    elif FROM_REAR:
        xm = int(k * step * px)
        xs = int(k * step * px)
        pos = k * step
    else:
        xm = mask.shape[1] - 1 - int(k * step * px)
        xs = model.shape[1] - 1 - int(k * step * px)
        pos = k * step
    mt, mb = edges(mask, xm)
    st, sb = edges(model, xs)
    f = lambda v: "  -  " if v is None else f"{v:5.2f}"
    d = "     " if mt is None or st is None else f"{st - mt:+5.2f}"
    print(f"{pos:5.1f}  {f(mt)}   {f(st)}   {d}   {f(mb)}   {f(sb)}")
