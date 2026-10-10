#!/usr/bin/env python3
"""PERF-02 soak and PERF-01 resolution captures on the exported Linux build (D074), against verify/thresholds.toml [perf].

    python3 scripts/check_perf.py soak [--seconds 1200] [--res 2560x1440]     PERF-02: exit 1 on a frame over
                                                                              frame_max_ms or RSS growth after
                                                                              5 min over memory_growth_max
    python3 scripts/check_perf.py resolutions [--seconds 120]                 PERF-01 numbers at 1080p/1440p/4K
    python3 scripts/check_perf.py evaluate <capture.json> --log <capture.log> re-check an existing soak capture

PERF-02 in the BRIEF is measured in the G6 benchmark scene (the PERF-01 scene). Until that scene exists, a pass here
is preliminary evidence on the skirmish scene, not verification, and G6 does not run this script.
The worst frame and the 100 ms count cover the whole run, warm-up included; percentiles skip the first 10 s.

The game's `--perf` mode (game/PerfCapture.cs) runs a rendered 5v5 AI battle at real time with vsync off and an
off-map barrage every 30 s. The 3D world renders offscreen at exactly the requested size (a tiling window manager
ignores window sizes). Memory is process RSS. It needs a display, AC power and an idle GPU: the script reads
`~/.local/bin/parish-media status` first and refuses otherwise. It never stops another process.
"""
from __future__ import annotations

import datetime as dt
import json
import os
import platform
import subprocess
import sys
import tomllib
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
BINARY = ROOT / "build" / "linux" / "SixActual.x86_64"
OUT = ROOT / "build" / "perf"
REPORTS = ROOT / "build" / "reports"


def limits() -> dict:
    return tomllib.loads((ROOT / "verify" / "thresholds.toml").read_text())["perf"]


def gpu_ready() -> str | None:
    r = subprocess.run([str(Path.home() / ".local" / "bin" / "parish-media"), "status"], capture_output=True, text=True)
    try:
        s = json.loads(r.stdout)
    except json.JSONDecodeError:
        return f"parish-media status unreadable: {r.stdout[-200:]} {r.stderr[-200:]}"
    if not s.get("ac_online"):
        return "not on AC power"
    if s.get("active_jobs") or s.get("compute_pids"):
        return f"GPU busy: jobs {s.get('active_jobs')} pids {s.get('compute_pids')}"
    return None


def specs() -> dict:
    gpu = subprocess.run(["nvidia-smi", "--query-gpu=name,driver_version,memory.total", "--format=csv,noheader"], capture_output=True, text=True).stdout.strip()
    cpu = next((ln.split(":", 1)[1].strip() for ln in Path("/proc/cpuinfo").read_text().splitlines() if ln.startswith("model name")), "?")
    mem = next((int(ln.split()[1]) // 1048576 for ln in Path("/proc/meminfo").read_text().splitlines() if ln.startswith("MemTotal")), 0)
    return {"gpu": gpu, "cpu": cpu, "ram_gb": mem, "kernel": platform.release()}


def capture(seconds: int, res: str, name: str) -> dict:
    if not BINARY.exists():
        raise SystemExit(f"missing {BINARY}; run the G1 export first")
    why = gpu_ready()
    if why:
        raise SystemExit(f"refusing to run: {why}")
    OUT.mkdir(parents=True, exist_ok=True)
    out = OUT / f"{name}.json"
    log = OUT / f"{name}.log"
    out.unlink(missing_ok=True)
    env = {**os.environ, "DISPLAY": os.environ.get("DISPLAY", ":0"), "WAYLAND_DISPLAY": os.environ.get("WAYLAND_DISPLAY", "wayland-1")}
    with log.open("w") as f:
        r = subprocess.run([str(BINARY), "--", "--perf", str(seconds), str(out), "--res", res], stdout=f, stderr=subprocess.STDOUT, env=env, timeout=seconds + 300)
    errors = [ln for ln in log.read_text(errors="replace").splitlines() if ln.startswith("ERROR")]
    if r.returncode != 0 or not out.exists():
        raise SystemExit(f"capture failed (exit {r.returncode}); see {log}")
    d = json.loads(out.read_text())
    d["engine_errors"] = errors
    d["capture"] = str(out.relative_to(ROOT))
    return d


def judge_soak(d: dict, lim: dict) -> list[str]:
    fails = []
    if d["seconds"] < 1200 - 1:
        fails.append(f"soak lasted {d['seconds']:.0f} s, under 20 minutes")
    if d["frames_over_100ms"] > 0 or d["max_ms"] > lim["frame_max_ms"]:
        fails.append(f"max frame {d['max_ms']:.1f} ms, {d['frames_over_100ms']} frames at or over {lim['frame_max_ms']} ms")
    if d["memory_growth_pct_after_5min"] / 100.0 > lim["memory_growth_max"]:
        fails.append(f"RSS grew {d['memory_growth_pct_after_5min']:.2f}% after 5 min (limit {lim['memory_growth_max'] * 100:.0f}%)")
    if d.get("engine_errors"):
        fails.append(f"engine errors: {d['engine_errors'][:3]}")
    return fails


def report(title: str, rows: list[str], fails: list[str]) -> Path:
    REPORTS.mkdir(parents=True, exist_ok=True)
    path = REPORTS / f"{dt.datetime.now(dt.UTC).strftime('%Y%m%dT%H%M%SZ')}-perf.md"
    sp = specs()
    body = [f"# {title}", "", f"Result: **{'FAIL' if fails else 'PASS'}**", "",
            f"Machine: {sp['gpu']}; {sp['cpu']}; {sp['ram_gb']} GB RAM; Linux {sp['kernel']}", "", *rows, ""]
    body += [f"- FAIL: {f}" for f in fails]
    path.write_text("\n".join(body) + "\n")
    return path


def soak_rows(d: dict, lim: dict) -> list[str]:
    return [f"- Capture: `{d['capture']}`, {d['seconds']:.0f} s, 3D at {d['render_size_3d']}, {d['frames']} frames, sim ticks {d['sim_ticks']}",
            f"- Frame time (after 10 s): p50 {d['p50_ms']} ms, p95 {d['p95_ms']} ms, p99 {d['p99_ms']} ms",
            f"- Worst frame over the whole run, warm-up included: {d['max_ms']:.1f} ms (limit {lim['frame_max_ms']} ms); frames at or over 100 ms: {d['frames_over_100ms']}",
            f"- Spikes over 33 ms: {len(d['spikes_over_33ms'])} {d['spikes_over_33ms']}",
            f"- RSS: {d['rss_mb_at_5min']:.1f} MB at 5 min, {d['rss_mb_end']:.1f} MB at end, growth {d['memory_growth_pct_after_5min']:.2f}% (limit {lim['memory_growth_max'] * 100:.0f}%); max {d['rss_mb_max']:.1f} MB",
            f"- GPU render p95 {d['gpu_render_ms_p95']} ms; video memory {d['video_mem_mb_end']:.1f} MB; GC gen0/gen2 {d['gc_gen0']}/{d['gc_gen2']}"]


def main() -> int:
    args = sys.argv[1:]
    if not args:
        print(__doc__)
        return 2

    def opt(name: str, default: str) -> str:
        return args[args.index(name) + 1] if name in args else default

    lim = limits()
    if args[0] in ("soak", "evaluate"):
        if args[0] == "soak":
            d = capture(int(opt("--seconds", "1200")), opt("--res", "2560x1440"), "perf02_soak")
        else:
            src = Path(args[1])
            log = Path(opt("--log", str(src.with_suffix(".log"))))
            if not log.exists():
                raise SystemExit(f"no engine log at {log}; pass --log so engine errors are checked")
            d = json.loads(src.read_text())
            d.setdefault("capture", str(src.resolve().relative_to(ROOT)) if src.resolve().is_relative_to(ROOT) else str(src))
            d["engine_errors"] = [ln for ln in log.read_text(errors="replace").splitlines() if ln.startswith("ERROR")]
        fails = judge_soak(d, lim)
        path = report("PERF-02 soak (preliminary: skirmish scene, not the G6 benchmark scene)", soak_rows(d, lim), fails)
    elif args[0] == "resolutions":
        rows, fails = [], []
        for res in ("1920x1080", "2560x1440", "3840x2160"):
            d = capture(int(opt("--seconds", "120")), res, f"perf01_{res}")
            rows.append(f"- {res}: p50 {d['p50_ms']} ms, p95 {d['p95_ms']} ms, p99 {d['p99_ms']} ms, max {d['max_ms']:.1f} ms; GPU render p95 {d['gpu_render_ms_p95']} ms; "
                        f"video memory {d['video_mem_mb_end']:.1f} MB; {d['units_at_end']} units ({d['capture']})")
            if d["p95_ms"] > lim["frame_p95_ms"]:
                fails.append(f"{res} p95 {d['p95_ms']} ms over {lim['frame_p95_ms']} ms")
        rows.append("- Preliminary: placeholder meshes, no full VFX and about 200 units, not the 2,000-entity PERF-01 scene.")
        path = report("PERF-01 resolution captures (preliminary)", rows, fails)
    else:
        print(__doc__)
        return 2
    print(path.read_text())
    print(f"report {path.relative_to(ROOT)}")
    return 1 if fails else 0


if __name__ == "__main__":
    raise SystemExit(main())
