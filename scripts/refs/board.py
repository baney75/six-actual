#!/usr/bin/env python3
"""Reference board for one item (section R4): build/refs/<id>.html.

A thumbnail grid of every reference with its slot tags, a licence badge (green distributable, grey
look-only) and source link; the spec's sourced numbers; and the coverage checklist with ticks.
Distributable images are embedded; look-only images appear only as local file links, so the board
never carries look-only pixels if shared (R1, R4).
"""
from __future__ import annotations

import base64
import html
import io
import json
import sys
import tomllib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def thumb(path: Path) -> str:
    try:
        from PIL import Image
        im = Image.open(path)
        im.thumbnail((360, 260))
        buf = io.BytesIO()
        im.convert("RGB").save(buf, "JPEG", quality=80)
        return "data:image/jpeg;base64," + base64.b64encode(buf.getvalue()).decode()
    except Exception:
        return ""


def main() -> int:
    vid = sys.argv[1]
    reg = {a["id"]: a for a in json.loads((ROOT / "assets_src" / "registry.json").read_text())["assets"]}
    cls = reg[vid]["class"]
    refs = json.loads((ROOT / "assets_src" / "refs" / vid / "refs.json").read_text())["refs"]
    spec = json.loads((ROOT / "data" / "assets" / f"{vid}.json").read_text())
    need = tomllib.loads((ROOT / "verify" / "ref_coverage.toml").read_text())["class"][cls]
    counts: dict[str, int] = {}
    tiles = []
    for r in refs:
        for s in r["slots"]:
            counts[s] = counts.get(s, 0) + 1
        p = ROOT / r["file"]
        tags = ", ".join(r["slots"]) or "-"
        if r["kind"] == "distributable" and p.suffix.lower() in (".jpg", ".jpeg", ".png", ".webp"):
            img = f'<img src="{thumb(p)}" alt="{html.escape(p.name)}">'
            badge = '<span class="b d">distributable</span>'
        elif r["kind"] == "distributable":
            img = f'<div class="ph">{html.escape(p.name)}</div>'
            badge = '<span class="b d">distributable</span>'
        else:
            img = f'<div class="ph"><a href="file://{html.escape(str(p))}">look-only: open locally</a></div>'
            badge = '<span class="b l">look-only</span>'
        tiles.append(f'<figure>{img}<figcaption>{badge} <b>{html.escape(tags)}</b><br>{html.escape(r.get("license", ""))} · <a href="{html.escape(r.get("url", ""))}">source</a></figcaption></figure>')
    rows = "".join(f"<tr><td>{html.escape(n['name'])}</td><td>{n['value']} {html.escape(n.get('unit', ''))}</td><td>{html.escape(n['source'])}</td></tr>" for n in spec.get("numbers", []))
    checks = "".join(f"<li>{'✓' if counts.get(s, 0) >= m else '✗'} {html.escape(s)}: {counts.get(s, 0)}/{m}</li>" for s, m in need.items())
    feats = "".join(f"<li>{html.escape(f)}</li>" for f in spec.get("signature_features", []))
    doc = f"""<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Refs {html.escape(vid)}</title><style>
:root{{--bg:#f4f3ef;--fg:#1b1d1a;--card:#fff;--line:#d8d5cc;--d:#2f6b3a;--l:#777}}
@media (prefers-color-scheme:dark){{:root{{--bg:#141614;--fg:#e6e7e2;--card:#1d201c;--line:#333;--d:#7fc28a;--l:#aaa}}}}
body{{margin:0;background:var(--bg);color:var(--fg);font:14px/1.5 system-ui,sans-serif}} main{{max-width:1200px;margin:auto;padding:20px 16px}}
.grid{{display:grid;grid-template-columns:repeat(auto-fill,minmax(260px,1fr));gap:12px}}
figure{{margin:0;background:var(--card);border:1px solid var(--line);border-radius:8px;overflow:hidden}}
figure img{{width:100%;display:block}} figcaption{{padding:6px 8px;font-size:12px}} .ph{{padding:40px 8px;text-align:center}}
.b{{font-size:11px;font-weight:700;padding:1px 6px;border-radius:99px;border:1px solid currentColor}} .d{{color:var(--d)}} .l{{color:var(--l)}}
table{{border-collapse:collapse;width:100%}} td{{border-top:1px solid var(--line);padding:4px 6px;vertical-align:top;font-size:12px}}
</style></head><body><main><h1>Reference board: {html.escape(vid)} ({html.escape(cls)})</h1>
<h2>Signature features</h2><ul>{feats}</ul><h2>Sourced numbers</h2><table>{rows}</table>
<h2>Coverage (verify/ref_coverage.toml)</h2><ul>{checks}</ul><h2>References ({len(refs)})</h2><div class="grid">{''.join(tiles)}</div></main></body></html>"""
    out = ROOT / "build" / "refs" / f"{vid}.html"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(doc, encoding="utf-8")
    print(out)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
