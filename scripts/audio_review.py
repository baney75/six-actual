#!/usr/bin/env python3
"""Audio review pack (BRIEF D7 checks, DET-08, D071): spectrogram sheets and an audition page.

    python3 scripts/audio_review.py

Writes build/audio_review/:
  <family>_spectrogram.png   per SFX family, the near, mid and far versions side by side (ffmpeg showspectrumpic)
  index.html                 players for every SFX file, every music stem and 20 sampled dialogue lines, so a person
                             can spot-check by ear
Returns the list of what it wrote, for scripts/check_audio.py.
"""
from __future__ import annotations

import html
import json
import random
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "build" / "audio_review"


def spectrogram(paths: list[Path], out: Path) -> None:
    inputs = []
    for p in paths:
        inputs += ["-i", str(p)]
    n = len(paths)
    chains = ";".join(f"[{i}:a]showspectrumpic=s=480x240:legend=0:scale=log[s{i}]" for i in range(n))
    stack = "".join(f"[s{i}]" for i in range(n)) + (f"hstack=inputs={n}[v]" if n > 1 else "copy[v]")
    subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", *inputs, "-filter_complex", f"{chains};{stack}", "-map", "[v]", "-frames:v", "1", str(out)], check=True)


def build() -> dict:
    OUT.mkdir(parents=True, exist_ok=True)
    sfx = json.loads((ROOT / "audio" / "sfx_manifest.json").read_text())["files"]
    families: dict[str, list[dict]] = {}
    for f in sfx:
        families.setdefault(f["family"], []).append(f)
    sheets = {}
    for fam, files in families.items():
        banded = [f for f in files if f.get("band") and f.get("variant") == 1]
        order = {"near": 0, "mid": 1, "far": 2}
        pick = sorted(banded, key=lambda f: order[f["band"]]) if banded else files[:3]
        out = OUT / f"{fam}_spectrogram.png"
        spectrogram([ROOT / f["path"] for f in pick], out)
        sheets[fam] = out
    rel = lambda p: "../../" + str(p)  # page lives in build/audio_review/
    rows = []
    for fam, files in families.items():
        rows.append(f"<h2>{html.escape(fam)}</h2><img src='{fam}_spectrogram.png' alt='{fam} spectrogram (near, mid, far)'>")
        for f in files:
            rows.append(f"<div>{html.escape(f['name'])} <audio controls preload='none' src='{rel(f['path'])}'></audio></div>")
    music = json.loads((ROOT / "audio" / "music_manifest.json").read_text())["files"]
    rows.append("<h2>music</h2>")
    for m in music:
        rows.append(f"<div>{html.escape(m['id'])} <audio controls preload='none' src='{rel(m['path'])}'></audio></div>")
    spoken = json.loads((ROOT / "audio" / "manifest.json").read_text())["files"]
    rows.append("<h2>dialogue (20 sampled lines)</h2>")
    for d in random.Random(7).sample(spoken, min(20, len(spoken))):
        rows.append(f"<div>{html.escape(d['id'])}: {html.escape(d['text'])} <audio controls preload='none' src='{rel(d['path'])}'></audio></div>")
    page = OUT / "index.html"
    page.write_text("<!doctype html><meta charset='utf-8'><title>Six Actual audio review</title>"
                    "<style>body{font-family:sans-serif;background:#111;color:#ddd;margin:16px}img{max-width:100%}audio{vertical-align:middle;height:28px}</style>"
                    "<h1>Audio review (generated; for spot-checks by ear)</h1>" + "\n".join(rows) + "\n")
    return {"sheets": {k: str(v) for k, v in sheets.items()}, "page": str(page), "sfx_listed": [f["path"] for f in sfx]}


if __name__ == "__main__":
    r = build()
    print(f"audio review: {len(r['sheets'])} spectrogram sheets, page {r['page']}")
