#!/usr/bin/env python3
"""Generate the battle music stems locally (AUD-03 dynamic layers, AUD-02 loudness, D020, D067).

    python3 scripts/generate_music.py [--force]

Per era, three instrumental layers (calm, tension, combat) from ACE-Step 1.5 through ~/.local/bin/parish-media, 60 s
each. Each is normalised to -18 LUFS / -1.5 dBTP (AUD-02 music target), made into a seamless loop (the last 3 s
cross-faded into the first 3 s, then cut at the cross-fade so the end flows into the start), and encoded as Ogg Vorbis
in game/assets/audio/music/<era>_<layer>.ogg with a .ogg.json sidecar. Checks recorded in the sidecar: no vocals (the
local transcriber hears only "music" or nothing) and tonal content (median spectral flatness below 0.5; white noise is ~1, dense percussion ~0.4), and a seamless loop seam.
audio/music_manifest.json lists every stem for G9.
"""
from __future__ import annotations

import hashlib
import json
import re
import subprocess
import sys
import time
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from voice_lines import loudness, portable  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "game" / "assets" / "audio" / "music"
TOOL = Path.home() / ".local" / "bin" / "parish-media"
RAW = Path.home() / ".local" / "share" / "parish-media" / "outputs" / "six-actual-music"
STYLE = {
    "ww2": "1940s orchestral war film score, brass and strings, military snare",
    "coldwar": "1980s Cold War military score, low strings, analog synth pads, snare",
    "modern": "modern military thriller score, hybrid orchestra, deep synth pulses, taut percussion",
}
LAYER = {
    "calm": ("calm, sparse and watchful, soft sustained notes, 70 BPM", 0),
    "tension": ("tense and building, ostinato, low pulse, 90 BPM", 1),
    "combat": ("intense battle music, driving percussion, full ensemble, 120 BPM", 2),
}
SECONDS = 60
XFADE = 3


def flatness(path: Path) -> float:
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", str(path), "-ac", "1", "-ar", "22050", "-f", "f32le", "-"], capture_output=True, check=True).stdout
    x = np.frombuffer(raw, dtype=np.float32)
    vals = []
    for i in range(0, len(x) - 2048, 2048):
        s = np.abs(np.fft.rfft(x[i:i + 2048] * np.hanning(2048))) + 1e-9
        vals.append(float(np.exp(np.mean(np.log(s))) / np.mean(s)))
    return float(np.median(vals)) if vals else 1.0


def heard(path: Path) -> str:
    r = subprocess.run(["mise", "exec", "--", "uv", "run", "python", "transcribe.py", str(path)], cwd=ROOT / "tools" / "asr", capture_output=True, text=True, check=True)
    for row in r.stdout.splitlines():
        if "\t" in row:
            return row.split("\t", 1)[1].strip()
    return ""


def seam(path: Path) -> dict:
    """Loop seam: the jump from the last sample to the first against the typical sample step (a click is a jump many
    times larger), and the correlation of band energies (30 log-spaced bands) in the 50 ms either side of the loop point (BRIEF D7: >= 0.9)."""
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", str(path), "-ac", "1", "-ar", "48000", "-f", "f32le", "-"], capture_output=True, check=True).stdout
    x = np.frombuffer(raw, dtype=np.float32)
    step = float(np.median(np.abs(np.diff(x[-4800:])))) + 1e-6
    jump = abs(float(x[0] - x[-1])) / step
    n = 2400  # 50 ms either side of the loop point
    # Energy in 30 log-spaced bands (60 Hz-20 kHz), not raw FFT bins: two windows of the same noise bed have equal band
    # energies but uncorrelated bins, so raw-bin correlation would flag a seamless noise loop.
    f = np.fft.rfftfreq(n, 1 / 48000)
    edges = np.geomspace(60, 20000, 31)
    def bands(seg: np.ndarray) -> np.ndarray:
        p = np.abs(np.fft.rfft(seg * np.hanning(n))) ** 2
        return np.log10(np.array([p[(f >= lo) & (f < hi)].sum() for lo, hi in zip(edges, edges[1:])]) + 1e-12)
    corr = float(np.corrcoef(bands(x[-n:]), bands(x[:n]))[0, 1])
    return {"seam_jump_ratio": round(jump, 2), "seam_spectral_corr": round(corr, 3)}


_EDGES = np.geomspace(60, 10000, 31)


def _spec(x: np.ndarray, i: int, n: int) -> np.ndarray:
    """Log energy in 30 log-spaced bands, the same measure seam() checks (at the 22.05 kHz analysis rate)."""
    p = np.abs(np.fft.rfft(x[i:i + n] * np.hanning(n))) ** 2
    f = np.fft.rfftfreq(n, 1 / 22050)
    return np.log10(np.array([p[(f >= lo) & (f < hi)].sum() for lo, hi in zip(_EDGES, _EDGES[1:])]) + 1e-12)


def loop_points(path: Path) -> tuple[float, float]:
    """Loop start a: the moment in 3-12 s where the spectrum is most stable across 50 ms either side (no note onset at
    the seam). Loop end b: the moment in the last 4-16 s whose preceding 50 ms best matches the 50 ms before a, so the
    cross-fade from b back into a joins like with like."""
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", str(path), "-ac", "1", "-ar", "22050", "-f", "f32le", "-"], capture_output=True, check=True).stdout
    x = np.frombuffer(raw, dtype=np.float32)
    sr, n, hop = 22050, 1102, 220
    dur = len(x) / sr
    best_a, ca = XFADE + 0.1, -2.0
    for i in range(int((XFADE + 0.1) * sr), int(12 * sr), hop):
        c = float(np.corrcoef(_spec(x, i - n, n), _spec(x, i, n))[0, 1])
        if c > ca:
            best_a, ca = i / sr, c
    ref = _spec(x, int(best_a * sr) - n, n)
    best_b, cb = dur - XFADE - 1, -2.0
    for i in range(int((dur - 16) * sr), int((dur - XFADE - 1) * sr), hop):
        c = float(np.corrcoef(_spec(x, i - n, n), ref)[0, 1])
        if c > cb:
            best_b, cb = i / sr, c
    return best_a, best_b


def make_loop(src: Path, dst: Path) -> None:
    """Normalise, choose loop points, then loop = take[a : b] + cross-fade(take[b : b+X] -> take[a-X : a]): the file
    ends on the audio just before a and starts on the audio just after it, a continuous, stable moment of the take."""
    norm = dst.with_suffix(".norm.wav")
    first = loudness(src)
    af = (f"loudnorm=I=-18:TP=-1.5:LRA=11:measured_I={first['input_i']}:measured_TP={first['input_tp']}:"
          f"measured_LRA={first['input_lra']}:measured_thresh={first['input_thresh']}:offset={first['target_offset']}:linear=true")
    subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", str(src), "-af", af, "-ar", "48000", str(norm)], check=True)
    a, b = loop_points(norm)
    fc = (f"[0]atrim={a}:{b},asetpts=PTS-STARTPTS[body];[0]atrim={b}:{b + XFADE},asetpts=PTS-STARTPTS[tail];"
          f"[0]atrim={a - XFADE}:{a},asetpts=PTS-STARTPTS[head];[tail][head]acrossfade=d={XFADE}:c1=tri:c2=tri[seam];[body][seam]concat=n=2:v=0:a=1[out]")
    subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", str(norm), "-filter_complex", fc, "-map", "[out]", "-c:a", "libvorbis", "-q:a", "5", str(dst)], check=True)
    norm.unlink()


def main() -> int:
    force = "--force" in sys.argv[1:]
    recheck = "--recheck" in sys.argv[1:]
    RAW.mkdir(parents=True, exist_ok=True)
    OUT.mkdir(parents=True, exist_ok=True)
    manifest = []
    for e_i, (era, style) in enumerate(STYLE.items()):
        for layer, (mood, l_i) in LAYER.items():
            dst = OUT / f"{era}_{layer}.ogg"
            seed = 200 + (10 * e_i) + l_i
            prompt = f"{style}, {mood}, instrumental, no vocals, loopable"
            if force or not dst.exists():
                raw = RAW / f"{era}_{layer}_s{seed}.wav"
                if not raw.exists():
                    for _ in range(8):
                        r = subprocess.run([str(TOOL), "music", "--timeout", "1800", "--", "--prompt", prompt, "--duration", str(SECONDS), "--seed", str(seed), "--output", str(raw)],
                                           capture_output=True, text=True)
                        if "GPU is in use" not in r.stdout + r.stderr:
                            break
                        time.sleep(20)
                    receipt = json.loads(r.stdout.strip().splitlines()[-1]) if r.stdout.strip() else {}
                    if receipt.get("status") != "success" or not raw.exists():
                        print(f"FAIL {era}/{layer}: {r.stdout[-300:]} {r.stderr[-300:]}")
                        return 1
                else:
                    receipt = {"status": "reused raw take"}
                make_loop(raw, dst)
                ld = loudness(dst)
                words = heard(dst)
                meta = {"id": f"{era}_{layer}", "era": era, "layer": layer, "prompt": prompt, "seed": seed, "duration_requested_s": SECONDS,
                        "model": "ACE-Step 1.5 (MIT), local on Parish1", "job": receipt, "lufs": float(ld["input_i"]), "true_peak_db": float(ld["input_tp"]),
                        "flatness": round(flatness(dst), 3), "transcript": words, "loop": f"loop points chosen for a stable seam; the end cross-fades into the {XFADE} s before the loop start",
                        "processing": "ffmpeg loudnorm two-pass to -18 LUFS / -1.5 dBTP, loop, Vorbis q5"}
                meta = portable(meta)
                dst.with_suffix(".ogg.json").write_text(json.dumps(meta, indent=2) + "\n")
                print(f"ok {era}/{layer}: {meta['lufs']} LUFS, flatness {meta['flatness']}, heard {words!r}", flush=True)
            meta = json.loads(dst.with_suffix(".ogg.json").read_text())
            if recheck or "seam_spectral_corr" not in meta:
                ld = loudness(dst)
                meta.update({"lufs": float(ld["input_i"]), "true_peak_db": float(ld["input_tp"]), "flatness": round(flatness(dst), 3), "transcript": heard(dst), **seam(dst)})
                dst.with_suffix(".ogg.json").write_text(json.dumps(meta, indent=2) + "\n")
                print(f"rechecked {era}/{layer}: {meta['lufs']} LUFS, flatness {meta['flatness']}, heard {meta['transcript']!r}, seam {meta['seam_jump_ratio']}x / {meta['seam_spectral_corr']}")
            manifest.append({**{k: meta[k] for k in ("id", "era", "layer", "prompt", "seed", "lufs", "true_peak_db", "flatness", "transcript", "seam_jump_ratio", "seam_spectral_corr")},
                             "category": "music", "path": str(dst.relative_to(ROOT)), "sha256": hashlib.sha256(dst.read_bytes()).hexdigest()})
    (ROOT / "audio" / "music_manifest.json").write_text(json.dumps({"version": 1, "files": manifest}, indent=1) + "\n")
    print(f"music stems: {len(manifest)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
