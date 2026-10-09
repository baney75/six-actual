#!/usr/bin/env python3
"""Procedural camouflage textures for the 1989 style board (Claude-authored, D021: no photo textures).

    (cd tools/py && uv run python ../../scripts/textures/camo.py [scheme ...])

Each scheme is a tileable pattern: band-limited periodic noise (white noise filtered in the frequency domain, so
it wraps seamlessly), domain-warped for organic edges, then thresholded by quantile so every colour covers its
target fraction. Colours come from assets_src/research/palettes_1989.json (sourced sRGB); a scheme whose colours
are not sourced yet is written with grey placeholders and marked so in the sidecar json. Output:
assets_src/textures/camo_<scheme>.png (+ .json with colours, coverage, physical tile size in metres).
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

REPO = Path(__file__).resolve().parents[2]
OUT = REPO / "assets_src" / "textures"
PALETTES = REPO / "assets_src" / "research" / "palettes_1989.json"


def periodic_noise(n: int, scale_px: float, rng: np.random.Generator, aniso: float = 1.0) -> np.ndarray:
    """Tileable noise with features about scale_px across (Gaussian low-pass of white noise), normalised 0..1."""
    white = rng.standard_normal((n, n))
    fy = np.fft.fftfreq(n)[:, None]
    fx = np.fft.fftfreq(n)[None, :]
    sigma = 1.0 / scale_px
    filt = np.exp(-((fx * aniso) ** 2 + (fy / aniso) ** 2) / (2 * sigma * sigma))
    f = np.real(np.fft.ifft2(np.fft.fft2(white) * filt))
    return (f - f.min()) / (f.max() - f.min())


def warp(field: np.ndarray, amount_px: float, scale_px: float, rng: np.random.Generator) -> np.ndarray:
    n = field.shape[0]
    dx = (periodic_noise(n, scale_px, rng) - 0.5) * 2 * amount_px
    dy = (periodic_noise(n, scale_px, rng) - 0.5) * 2 * amount_px
    yy, xx = np.mgrid[0:n, 0:n]
    return field[(yy + dy).astype(int) % n, (xx + dx).astype(int) % n]


def layer(n, scale, rng, warp_amt=0.6, aniso=1.0):
    f = periodic_noise(n, scale, rng, aniso)
    f = 0.75 * f + 0.25 * periodic_noise(n, scale / 3, rng)
    return warp(f, scale * warp_amt, scale * 0.8, rng)


def mask_fraction(field: np.ndarray, frac: float) -> np.ndarray:
    return field > np.quantile(field, 1.0 - frac)


# scheme: tile size (m), resolution, layers [(colour index, coverage, feature scale as fraction of tile, aniso)]
SCHEMES = {
    "m81_woodland": {"palette": "us_m81_woodland_bdu", "picks": ["Light Green 354", "Brown 356", "Dark Green 355", "Black 357"], "tile_m": 0.9, "n": 1024,
                     "layers": [(1, 0.38, 0.10, 1.8), (2, 0.22, 0.07, 1.6), (3, 0.10, 0.035, 3.5)]},
    "nato3_carc": {"palette": "us_army_nato_3color_carc", "picks": ["CARC Green 383", "CARC Brown 383", "CARC Black"], "tile_m": 6.0, "n": 1024,
                   "layers": [(1, 0.38, 0.16, 1.4), (2, 0.20, 0.10, 1.4)]},
    "a10_european1": {"palette": "usaf_a10a_european_i", "picks": ["Dark Green (Euro I)", "Medium Green (Euro I)", "Dark Gunship Gray"], "tile_m": 9.0, "n": 1024,
                      "layers": [(1, 0.40, 0.17, 1.6), (2, 0.25, 0.12, 1.6)]},
    "su25_1989": {"palette": "su25_1980s", "picks": ["Sand", "Brown", "Green / Light Green"], "tile_m": 8.0, "n": 1024,
                  "layers": [(1, 0.35, 0.15, 1.5), (2, 0.25, 0.11, 1.5)]},
    "soviet_4bo": {"palette": "soviet_ground_vehicles_gsfg_1989", "picks": ["4BO protective green"], "tile_m": 4.0, "n": 512, "layers": [], "fade": 0.06},
}


def palette_for(name: str, picks: list[str]):
    """Sourced sRGB colours for a scheme, picked by colour-name prefix (first = base), or None if any is unsourced."""
    if not PALETTES.exists():
        return None
    sch = json.loads(PALETTES.read_text()).get("schemes", {}).get(name)
    if not sch:
        return None
    out = []
    for want in picks:
        hit = next((c for c in sch.get("colors", []) if c["name"].startswith(want)), None)
        if hit is None or not hit.get("hex"):
            return None
        h = hit["hex"]
        out.append(tuple(int(h[i:i + 2], 16) for i in (1, 3, 5)))
    return out


def render(name: str, spec: dict, seed: int = 1989) -> dict:
    rng = np.random.default_rng(seed + sum(map(ord, name)))
    n = spec["n"]
    ncol = 1 + len(spec["layers"])
    cols = palette_for(spec["palette"], spec["picks"])
    placeholder = cols is None
    if placeholder:
        cols = [(int(110 + 40 * i), int(110 + 40 * i), int(110 + 40 * i)) for i in range(ncol)]
    idx = np.zeros((n, n), np.uint8)
    coverage = {}
    for ci, frac, scale, aniso in spec["layers"]:
        f = layer(n, n * scale, rng, aniso=aniso)
        idx[mask_fraction(f, frac)] = ci  # later layers paint over earlier ones
    img = np.array(cols, np.float32)[idx]
    if spec.get("fade"):
        img *= (1.0 + (periodic_noise(n, n * 0.2, rng)[..., None] - 0.5) * 2 * spec["fade"])
    for ci in range(ncol):
        coverage[str(ci)] = round(float((idx == ci).mean()), 3)
    OUT.mkdir(parents=True, exist_ok=True)
    Image.fromarray(np.clip(img, 0, 255).astype(np.uint8)).save(OUT / f"camo_{name}.png")
    meta = {"scheme": name, "tile_m": spec["tile_m"], "px": n, "colors_srgb": cols, "coverage": coverage,
            "placeholder_colors": placeholder, "palette_source": str(PALETTES.relative_to(REPO)), "seed": seed}
    (OUT / f"camo_{name}.json").write_text(json.dumps(meta, indent=2) + "\n")
    return meta


def main() -> int:
    names = sys.argv[1:] or list(SCHEMES)
    for nm in names:
        m = render(nm, SCHEMES[nm])
        print(nm, "placeholder" if m["placeholder_colors"] else "sourced", m["coverage"])
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
