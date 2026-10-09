#!/usr/bin/env python3
"""Write build/reports/phase-0.html (self-contained). Inputs are passed on the command line."""
import base64, html, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
report, fresh_status, fresh_detail, shot = sys.argv[1:5]
img = base64.b64encode(Path(shot).read_bytes()).decode()

items = [
    ("SIM-01 fixed tick, no float in state", "verified", report + " — G1 warnaserror, G2 analyzer test"),
    ("SIM-02 bit-identical determinism", "verified", report + " — G3, 3 seeds × threads 1/8 × 2 processes, matches golden"),
    ("SIM-03 replay reproduces hash", "verified", report + " — G3 replay=ok"),
    ("QA-01 analyzers, format, banned patterns", "verified", report + " — G12"),
    ("QA-02 ≥80% sim line coverage", "verified", report + " — 0.947 (921/973)"),
    ("G1 Linux / Windows / macOS exports", "built", "build/linux/SixActual.x86_64 · build/windows/SixActual.exe · build/macos/SixActual.zip (universal, ad-hoc signed, untested on a Mac)"),
    ("G14 reference coverage + self-test", "verified", report + " — 7 synthetic cases fail/pass as designed; 0 leaks in 398 files"),
    (".claude/ hooks (Stop, Pre, Post) + commit-msg", "verified", "each triggered with block and pass inputs; live hook blocked a headless session's edit to verify/ (PROGRESS 2026-10-08)"),
    ("9 subagents, 12 skills", "verified", "session init event lists all of them as loaded"),
    ("MCP servers (godot-agent-loop 3.0.1, mcp-for-blender 2.1.9)", "prepared", ".mcp.json pinned; not exercised"),
    ("Blender 5.2.2 LTS pin", "verified", "SHA-256 matched; tools/blender/blender --version prints 5.2.2"),
    ("Bootstrap on a fresh account", fresh_status, fresh_detail),
    ("Local voice route (Qwen3-TTS on Parish1)", "verified", "14.2 s render, GPU released; local transcript 'Exo has the battle, Commander. Good hunting.' (XO spelled phonetically); −19.1 LUFS before normalization; not listened to by ear"),
]
badge = {"verified": "ok", "built": "ok", "prepared": "warn", "FAILED": "bad", "not run": "warn"}
rows = "\n".join(
    f'<tr><td>{html.escape(a)}</td><td><span class="b {badge.get(s.split()[0], "warn")}">{html.escape(s)}</span></td><td>{html.escape(e)}</td></tr>'
    for a, s, e in items)

doc = f"""<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Six Actual Phase 0</title><style>
:root{{--bg:#f6f5f1;--fg:#1d1f1c;--mut:#5f645c;--card:#fff;--line:#dcd9d0;--ok:#2f6b3a;--warn:#8a6100;--bad:#a12a2a;--acc:#4a5a2c}}
@media (prefers-color-scheme:dark){{:root{{--bg:#151714;--fg:#e7e8e3;--mut:#a3a89c;--card:#1e211d;--line:#33372f;--ok:#7fc28a;--warn:#e0b45a;--bad:#ef8a8a;--acc:#b6c58a}}}}
body{{margin:0;background:var(--bg);color:var(--fg);font:15px/1.55 system-ui,sans-serif}}
main{{max-width:980px;margin:0 auto;padding:28px 16px 60px}}
h1{{font-size:26px;margin:0 0 4px}} h2{{font-size:18px;margin:32px 0 10px;color:var(--acc)}}
.sub{{color:var(--mut);margin:0 0 20px}}
.card{{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:16px 18px;margin:12px 0}}
.verdict{{font-size:17px}} table{{width:100%;border-collapse:collapse;font-size:14px}}
td,th{{text-align:left;padding:8px 6px;border-top:1px solid var(--line);vertical-align:top}} th{{color:var(--mut);font-weight:600}}
.tw{{overflow-x:auto}} .b{{font-size:12px;font-weight:700;padding:2px 7px;border-radius:99px;border:1px solid currentColor;white-space:nowrap}}
.ok{{color:var(--ok)}} .warn{{color:var(--warn)}} .bad{{color:var(--bad)}}
img{{max-width:100%;border-radius:8px;border:1px solid var(--line)}} code{{font-size:13px}} li{{margin:4px 0}}
</style></head><body><main>
<h1>Six Actual — Phase 0 under contract v5</h1>
<p class="sub">2026-10-08 · Parish1 (RTX 4080 Laptop, Omarchy, Hyprland 0.56.2) · report <code>{html.escape(report)}</code></p>
<div class="card verdict"><b>Verdict:</b> <code>scripts/verify.sh --phase 0</code> passed: G1 G2 G3 G10 G11 G12 G14, 0 failures.
Phase 0 closes when the fresh-account bootstrap item below is green and the work is committed. Commits wait for your go-ahead.</div>

<h2>What changed today</h2>
<ul>
<li>The contract in <code>docs/BRIEF.md</code> is now v5. <code>REQUIREMENTS.md</code> is generated from it with 88 IDs. SIM-05, SIM-07 and SIM-08 dropped back to in-progress, because their only evidence is unit tests and v5 needs G4 scenarios.</li>
<li>Blender is pinned to the 5.2.2 LTS tarball. The macOS universal export was added. G14 reference coverage is wired into <code>--quick</code>.</li>
<li>Added <code>CLAUDE.md</code>, 9 subagents, 12 skills, hooks that block a stop on red, format and validate on edit, and guard <code>verify/</code>, plus a <code>commit-msg</code> rule for <code>VERIFY:</code> commits.</li>
<li>Decisions D012–D019 are in <code>DECISIONS.md</code>.</li>
</ul>

<h2>Status by item</h2>
<div class="card tw"><table><tr><th>Item</th><th>State</th><th>Evidence</th></tr>
{rows}</table></div>

<h2>Current look (placeholder)</h2>
<div class="card"><img alt="Fulda skirmish at tick 30: flat green plane, low-poly gray tanks with blue sphere markers" src="data:image/jpeg;base64,{img}">
<p class="sub">Fulda skirmish frame from 2026-10-05, not re-shot today: a flat green plane, low-poly placeholder tanks, blue spheres as unit markers, and a debug HUD. None of the 103 meshes has been through the §M pipeline. The screen does not yet label them as placeholders, which the contract requires. That is open issue 2.</p></div>

<h2>Open issues</h2>
<ol>
<li><code>vulkaninfo</code> is missing, so the §B1 profile is incomplete. Fix: run the package line that <code>scripts/bootstrap.sh</code> prints (it now adds <code>cage</code>, <code>openal</code>, <code>noto-fonts</code>, <code>wl-clipboard</code> and <code>libxkbcommon-x11</code>).</li>
<li>Placeholder meshes and markers are not labeled as placeholders on screen.</li>
<li><code>free -h</code> reports 31 GiB today, against 62 GiB in the Oct 4 log. Worth a look if you didn't change anything.</li>
<li>The MCP servers are registered but not exercised. The CLI path covers every step.</li>
</ol>

<h2>Decisions needed from you</h2>
<ol>
<li><b>Commit go-ahead.</b> Planned as two commits: <code>VERIFY: add §R3 reference coverage checklist</code> (<code>verify/ref_coverage.toml</code> + <code>DECISIONS.md</code>), then <code>SIM-01 SIM-02 SIM-03 QA-01 QA-02: adopt contract v5 Phase 0 tooling</code>.</li>
<li><b>Fresh-account check.</b> Accept the clean-HOME proxy run, or create a second user account and run <code>scripts/bootstrap.sh</code> there.</li>
<li><b>Settled today:</b> no ElevenLabs and no AI-3D services. Audio runs locally on Parish1 (Qwen3-TTS, ACE-Step, faster-whisper), and Claude authors every model, material and animation (D020, D021). No credit budget is needed.</li>
</ol>

<h2>Next (Phase 1, Cold War slice)</h2>
<ol><li><code>sim/Headless --scenario</code> and the G4 scenario schema. This unblocks SIM-04–10 and gets SIM-05/07/08 back to verified properly.</li>
<li>The reference board and the first ref pack (T-72B / M1A1 for the style board).</li>
<li>The §M procedural generators, the G7 validator, and contact sheets CS-1–CS-5.</li></ol>
</main></body></html>"""
out = ROOT / "build" / "reports" / "phase-0.html"
out.write_text(doc, encoding="utf-8")
print(out)
