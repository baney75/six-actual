#!/usr/bin/env python3
"""G10 ERA-03 map check (D075): data/maps.json against BRIEF ERA-03 and the Copernicus licence.

    python3 scripts/check_maps.py

Every era ships at least 4 maps of 8x8 km. Each heightfield decodes to n x n samples covering the map, its recorded
relief matches the data, and it has real relief (at least 20 m; a flat or constant field fails). The Copernicus
attribution and liability sentences are present verbatim, and every map names its source tile and that tile's
sha256. When the source tiles are cached in tools/cache/dem/, the file is regenerated and must match byte for byte,
and each tile's sha256 must match; without the cache that step is reported as skipped, not passed.
"""
from __future__ import annotations

import base64
import hashlib
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ATTRIBUTION = ("produced using Copernicus WorldDEM-30 © DLR e.V. 2010-2014 and © Airbus Defence and Space GmbH 2014-2018 "
               "provided under COPERNICUS by the European Union and ESA; all rights reserved")
LIABILITY = "The organisations in charge of the Copernicus programme by law or by delegation do not incur any liability for any use of the Copernicus WorldDEM-30"
MIN_RELIEF_DM = 200


def problems(data: dict) -> list[str]:
    out = []
    if data.get("attribution") != ATTRIBUTION:
        out.append("attribution is not the licence's notice for modified data")
    if data.get("liability") != LIABILITY:
        out.append("liability sentence missing or altered")
    maps = data.get("maps", [])
    ids = [m.get("id") for m in maps]
    if len(ids) != len(set(ids)):
        out.append("duplicate map ids")
    for era in ("ww2", "coldwar", "modern"):
        n = sum(1 for m in maps if m.get("era") == era)
        if n < 4:
            out.append(f"{era}: {n} maps, ERA-03 needs 4")
    for m in maps:
        mid = m.get("id")
        hf = m.get("heightfield", {})
        n, spacing = hf.get("n", 0), hf.get("spacing_m", 0)
        if m.get("size_m") != 8000:
            out.append(f"{mid}: size {m.get('size_m')} m, not 8000")
        if n < 2 or spacing <= 0 or (n - 1) * spacing < m.get("size_m", 0):
            out.append(f"{mid}: {n} samples at {spacing} m do not cover the map")
            continue
        raw = base64.b64decode(hf.get("dm_b64", ""))
        if len(raw) != n * n * 2:
            out.append(f"{mid}: heightfield has {len(raw)} bytes, expected {n * n * 2}")
            continue
        vals = [raw[i] | (raw[i + 1] << 8) for i in range(0, len(raw), 2)]
        if max(vals) != hf.get("relief_dm"):
            out.append(f"{mid}: recorded relief {hf.get('relief_dm')} dm, data has {max(vals)}")
        if min(vals) != 0:
            out.append(f"{mid}: heights are not relative to the map minimum")
        if max(vals) < MIN_RELIEF_DM:
            out.append(f"{mid}: relief {max(vals) / 10} m is not real ground (min {MIN_RELIEF_DM / 10} m)")
        src = m.get("source", {})
        if src.get("dataset") != "Copernicus DEM GLO-30" or not src.get("tile") or len(src.get("tile_sha256", "")) != 64:
            out.append(f"{mid}: source tile or its sha256 missing")
    return out


def self_test() -> None:
    good = {"attribution": ATTRIBUTION, "liability": LIABILITY, "maps": []}
    for era in ("ww2", "coldwar", "modern"):
        for k in range(4):
            vals = [0, 450, 900, 1200]
            good["maps"].append({"id": f"{era}{k}", "era": era, "size_m": 8000, "source": {"dataset": "Copernicus DEM GLO-30", "tile": "t", "tile_sha256": "0" * 64},
                                 "heightfield": {"n": 2, "spacing_m": 8000, "relief_dm": max(vals), "dm_b64": base64.b64encode(b"".join(v.to_bytes(2, "little") for v in vals)).decode()}})
    assert problems(good) == [], problems(good)
    flat = json.loads(json.dumps(good))
    flat["maps"][0]["heightfield"].update(relief_dm=0, dm_b64=base64.b64encode(bytes(8)).decode())
    assert any("not real ground" in p for p in problems(flat))
    short = json.loads(json.dumps(good))
    short["maps"] = short["maps"][1:]
    assert any("ERA-03 needs 4" in p for p in problems(short))
    noattr = dict(good, attribution="Copernicus")
    assert any("attribution" in p for p in problems(noattr))
    lie = json.loads(json.dumps(good))
    lie["maps"][2]["heightfield"]["relief_dm"] = 1
    assert any("recorded relief" in p for p in problems(lie))
    small = json.loads(json.dumps(good))
    small["maps"][3]["size_m"] = 4000
    assert any("not 8000" in p for p in problems(small))
    gap = json.loads(json.dumps(good))
    gap["maps"][4]["heightfield"]["spacing_m"] = 100
    assert any("do not cover" in p for p in problems(gap))
    dup = json.loads(json.dumps(good))
    dup["maps"][5]["id"] = dup["maps"][6]["id"]
    assert any("duplicate" in p for p in problems(dup))
    raised = json.loads(json.dumps(good))
    raised["maps"][7]["heightfield"]["dm_b64"] = base64.b64encode(b"".join(v.to_bytes(2, "little") for v in (100, 450, 900, 1200))).decode()
    assert any("relative to the map minimum" in p for p in problems(raised))
    nosrc = json.loads(json.dumps(good))
    nosrc["maps"][8]["source"]["tile_sha256"] = ""
    assert any("sha256 missing" in p for p in problems(nosrc))


def main() -> int:
    self_test()
    path = ROOT / "data" / "maps.json"
    data = json.loads(path.read_text())
    found = problems(data)
    game = ROOT / "game" / "data" / "sim" / "maps.json"
    if not game.exists() or game.read_bytes() != path.read_bytes():
        found.append("game/data/sim/maps.json is missing or stale; run scripts/sync_game_data.py")
    cache = ROOT / "tools" / "cache" / "dem"
    sys.path.insert(0, str(ROOT / "scripts"))
    import generate_maps  # noqa: E402

    tiles = [generate_maps.tile_path(lat, lon) for _, _, _, lat, lon, _ in generate_maps.MAPS]
    if all(t.exists() for t in tiles):
        for m in data["maps"]:
            mm = next(x for x in generate_maps.MAPS if x[0] == m["id"])
            t = generate_maps.tile_path(mm[3], mm[4])
            if hashlib.sha256(t.read_bytes()).hexdigest() != m["source"]["tile_sha256"]:
                found.append(f"{m['id']}: cached tile {t.name} differs from the recorded sha256")
        out = ROOT / "build" / "maps.generated.json"
        out.parent.mkdir(exist_ok=True)
        subprocess.run([sys.executable, str(ROOT / "scripts" / "generate_maps.py"), str(out)], check=True, capture_output=True)
        regen = "identical" if out.read_bytes() == path.read_bytes() else "DIFFERENT"
        if regen != "identical":
            found.append("data/maps.json does not match scripts/generate_maps.py on the cached tiles")
    else:
        regen = f"skipped ({sum(not t.exists() for t in tiles)} of {len(tiles)} tiles not cached in {cache.relative_to(ROOT)})"
    for p in found:
        print(f"FAIL maps: {p}")
    eras = {e: sum(1 for m in data["maps"] if m["era"] == e) for e in ("ww2", "coldwar", "modern")}
    print(f"maps: {len(data['maps'])} maps {eras}, min relief {min(m['heightfield']['relief_dm'] for m in data['maps']) / 10} m, regeneration {regen}")
    return 1 if found else 0


if __name__ == "__main__":
    raise SystemExit(main())
