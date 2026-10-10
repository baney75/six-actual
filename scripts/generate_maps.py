#!/usr/bin/env python3
"""ERA-03 battle maps (D075): 4 real 8x8 km areas per era, heightfields from the Copernicus DEM GLO-30.

    python3 scripts/generate_maps.py [out.json]        default data/maps.json

Source tiles are the public Copernicus GLO-30 COGs (s3://copernicus-dem-30m, licence and attribution in LEGAL.md),
cached in tools/cache/dem/ (gitignored, about 30 MB each). The script downloads a missing tile with curl, reads it with
tifffile through the tools/dem uv project, crops the 8x8 km square around each map centre (north up: sim y grows
northward, so player 0 deploys from the south edge), applies a 3x3 box blur (the DSM carries tree canopy and roofs),
samples a 251x251 grid at 32 m and stores decimetres above the map's lowest point as little-endian uint16, base64.
Vegetation, roads and villages stay procedural from the battle seed (sim/Battle/Skirmish.cs); only the ground is real.
"""
from __future__ import annotations

import base64
import hashlib
import json
import math
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CACHE = ROOT / "tools" / "cache" / "dem"
BUCKET = "https://copernicus-dem-30m.s3.amazonaws.com"
SIZE_M = 8000
SPACING_M = 32
N = SIZE_M // SPACING_M + 1

# id, name, era, centre latitude, centre longitude, why the ground matters
MAPS = [
    ("hurtgen", "Huertgen Forest (Vossenack ridge)", "ww2", 50.69, 6.38, "Steep wooded ridges and the Kall valley; November 1944."),
    ("oosterbeek", "Oosterbeek (Arnhem)", "ww2", 51.96, 5.83, "Low Rhine flood plain under the Veluwe edge; September 1944."),
    ("seelow", "Seelow Heights", "ww2", 52.53, 14.38, "The Oderbruch flats below a 50 m escarpment; April 1945."),
    ("noville", "Noville (Bastogne)", "ww2", 50.04, 5.72, "Open Ardennes plateau north of Bastogne; December 1944."),
    ("fulda", "Fulda Gap (Huenfeld)", "coldwar", 50.67, 9.77, "The corridor between the Vogelsberg and the Rhoen; V Corps sector."),
    ("hof", "Hof Gap", "coldwar", 50.32, 11.92, "Rolling Franconian upland on the inner-German border; VII Corps north flank."),
    ("eichsfeld", "Eichsfeld (Duderstadt)", "coldwar", 51.51, 10.26, "Basin and wooded hills on the Goettingen approach; I German Corps."),
    ("lueneburg", "Lueneburg Heath", "coldwar", 53.10, 10.30, "North German Plain heath and low moraine; NORTHAG."),
    ("suwalki", "Suwalki Gap", "modern", 54.25, 22.85, "Moraine hills and lakes on the Poland-Lithuania border corridor."),
    ("narva", "Narva", "modern", 59.37, 28.15, "Flat river-bank country around the border city and its reservoir."),
    ("tapa", "Tapa", "modern", 59.25, 25.90, "Northern Estonian plain around the garrison town; forest and bog."),
    ("daugavpils", "Daugavpils", "modern", 55.87, 26.53, "The Daugava bend in Latgale's lake upland."),
]


def tile_name(lat: float, lon: float) -> str:
    return f"Copernicus_DSM_COG_10_N{math.floor(lat):02d}_00_E{math.floor(lon):03d}_00_DEM"


def tile_path(lat: float, lon: float) -> Path:
    return CACHE / f"cop_N{math.floor(lat):02d}_E{math.floor(lon):03d}.tif"


def fetch(lat: float, lon: float) -> Path:
    p = tile_path(lat, lon)
    if not p.exists():
        CACHE.mkdir(parents=True, exist_ok=True)
        name = tile_name(lat, lon)
        subprocess.run(["curl", "-sfL", "--retry", "3", "-o", str(p), f"{BUCKET}/{name}/{name}.tif"], check=True)
    return p


# Runs inside tools/dem (tifffile + imagecodecs + numpy). Reads JSON jobs on stdin, writes JSON results on stdout.
WORKER = r'''
import base64, json, sys
import numpy as np, tifffile
out = []
for job in json.load(sys.stdin):
    with tifffile.TiffFile(job["tile"]) as t:
        page = t.pages[0]
        dem = page.asarray().astype(np.float64)
        sx, sy, _ = page.tags[33550].value
        _, _, _, lon0, lat0, _ = page.tags[33922].value
    pad = np.pad(dem, 1, mode="edge")
    blur = sum(pad[1 + dy:pad.shape[0] - 1 + dy, 1 + dx:pad.shape[1] - 1 + dx] for dy in (-1, 0, 1) for dx in (-1, 0, 1)) / 9.0
    n, spacing, size = job["n"], job["spacing_m"], job["size_m"]
    lat_c, lon_c = job["lat"], job["lon"]
    m_lat = 111320.0
    m_lon = 111320.0 * np.cos(np.radians(lat_c))
    off = np.arange(n) * spacing - size / 2.0
    lats = lat_c + off / m_lat            # row j: y = j * spacing, south to north
    lons = lon_c + off / m_lon
    fy = (lat0 - lats) / sy               # tile rows run north to south
    fx = (lons - lon0) / sx
    if fy.min() < 0 or fy.max() > dem.shape[0] - 1 or fx.min() < 0 or fx.max() > dem.shape[1] - 1:
        raise SystemExit(f"{job['id']}: crop leaves the tile")
    gy, gx = np.meshgrid(fy, fx, indexing="ij")
    y0 = np.floor(gy).astype(int); x0 = np.floor(gx).astype(int)
    y1 = np.minimum(y0 + 1, dem.shape[0] - 1); x1 = np.minimum(x0 + 1, dem.shape[1] - 1)
    ty = gy - y0; tx = gx - x0
    h = (blur[y0, x0] * (1 - ty) * (1 - tx) + blur[y0, x1] * (1 - ty) * tx + blur[y1, x0] * ty * (1 - tx) + blur[y1, x1] * ty * tx)
    low = float(h.min())
    dm = np.rint((h - low) * 10.0).astype("<u2")
    out.append({"id": job["id"], "min_m": round(low, 1), "max_m": round(float(h.max()), 1),
                "relief_dm": int(dm.max()), "dm_b64": base64.b64encode(dm.tobytes()).decode()})
json.dump(out, sys.stdout)
'''


def build() -> dict:
    jobs = []
    tiles = {}
    for mid, _, _, lat, lon, _ in MAPS:
        p = fetch(lat, lon)
        tiles[p.name] = hashlib.sha256(p.read_bytes()).hexdigest()
        jobs.append({"id": mid, "tile": str(p), "lat": lat, "lon": lon, "n": N, "spacing_m": SPACING_M, "size_m": SIZE_M})
    r = subprocess.run(["mise", "exec", "--", "uv", "run", "--quiet", "python", "-c", WORKER], cwd=ROOT / "tools" / "dem",
                       input=json.dumps(jobs), capture_output=True, text=True)
    if r.returncode != 0:
        raise SystemExit(f"DEM worker failed:\n{r.stderr[-2000:]}")
    results = {x["id"]: x for x in json.loads(r.stdout)}
    maps = []
    for mid, name, era, lat, lon, why in MAPS:
        res = results[mid]
        maps.append({
            "id": mid, "name": name, "era": era, "centre": {"lat": lat, "lon": lon}, "about": why,
            "size_m": SIZE_M, "source": {"dataset": "Copernicus DEM GLO-30", "tile": tile_name(lat, lon), "tile_sha256": tiles[tile_path(lat, lon).name]},
            "elevation_m": {"min": res["min_m"], "max": res["max_m"]},
            "heightfield": {"n": N, "spacing_m": SPACING_M, "relief_dm": res["relief_dm"], "encoding": "uint16 little-endian decimetres above min, row-major, row 0 = south edge", "dm_b64": res["dm_b64"]},
        })
    return {
        "version": 1,
        "attribution": "produced using Copernicus WorldDEM-30 \u00a9 DLR e.V. 2010-2014 and \u00a9 Airbus Defence and Space GmbH 2014-2018 provided under COPERNICUS by the European Union and ESA; all rights reserved",
        "liability": "The organisations in charge of the Copernicus programme by law or by delegation do not incur any liability for any use of the Copernicus WorldDEM-30",
        "maps": maps,
    }


def main() -> int:
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "data" / "maps.json"
    data = build()
    out.write_text(json.dumps(data, indent=1) + "\n")
    for m in data["maps"]:
        print(f"{m['era']:8} {m['id']:11} relief {m['heightfield']['relief_dm'] / 10:6.1f} m  ({m['elevation_m']['min']}-{m['elevation_m']['max']} m)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
