#!/usr/bin/env python3
"""G9 audio gate (AUD-01, AUD-02): every dialogue trigger and variant has a file, and every file meets its targets.

    python3 scripts/check_audio.py     check audio/manifest.json against the sources and re-measure every file

Coverage: every row x variant of dialogue/lines.csv and every briefing in data/campaign.json is in the manifest and on
disk. Per file, re-measured with ffmpeg (ebur128 via loudnorm analysis, silencedetect, astats):
  dialogue  -16 LUFS +/- 1.5 (AUD-02), true peak <= -1 dBTP
  all       not silent (peak above -40 dBFS), not clipped (no samples at full scale), duration 0.4-60 s,
            no silence over 300 ms at the start or end
  speech    transcription word accuracy >= 0.95 (BRIEF W3), as recorded by scripts/voice_lines.py qa
The manifest's recorded loudness must match the file within 0.5 LU, so a stale manifest fails. A self-test checks the
rules on synthetic files (a silent file, a clipped file, a quiet file) before the real check.
"""
from __future__ import annotations

import csv
import hashlib
import json
import re
import subprocess
import sys
import tempfile

import numpy as np
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from voice_lines import edge_silence_ms, lines, loudness  # noqa: E402
from generate_music import LAYER, STYLE, flatness, seam  # noqa: E402

# faster-whisper invents these on instrumental audio; they are not vocals.
HALLUCINATIONS = ["thanks for watching", "thank you very much", "thank you", "see you next time", "music", "you"]

ROOT = Path(__file__).resolve().parent.parent


def stats(path: Path) -> dict:
    r = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-i", str(path), "-af", "astats=metadata=0:reset=0", "-f", "null", "-"], capture_output=True, text=True)
    peak = re.findall(r"Peak level dB: (-?[\d.inf]+)", r.stderr)
    clips = re.findall(r"Number of clipped samples: (\d+)", r.stderr)  # not printed by every ffmpeg; fall back to peak
    dur = float(subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", str(path)], capture_output=True, text=True).stdout.strip() or 0)
    p = float(peak[-1]) if peak and peak[-1] not in ("-inf", "inf") else -200.0
    return {"peak_db": p, "clipped": int(clips[-1]) if clips else (1 if p >= -0.01 else 0), "duration_s": dur}


def file_problems(path: Path, kind: str, recorded: dict | None) -> list[str]:
    out = []
    s = stats(path)
    if s["peak_db"] < -40:
        out.append(f"silent (peak {s['peak_db']:.1f} dBFS)")
    if s["clipped"]:
        out.append("clipped")
    if not 0.4 <= s["duration_s"] <= 60:
        out.append(f"duration {s['duration_s']:.2f} s outside 0.4-60")
    if s["peak_db"] >= -40:
        ld = loudness(path)
        lufs, tp = float(ld["input_i"]), float(ld["input_tp"])
        if kind == "dialogue" and abs(lufs + 16) > 1.5:
            out.append(f"{lufs:.1f} LUFS, need -16 +/- 1.5")
        if tp > -1.0:
            out.append(f"true peak {tp:.1f} dBTP, need <= -1")
        if recorded is not None and abs(recorded.get("lufs", 99) - lufs) > 0.5:
            out.append(f"manifest says {recorded.get('lufs')} LUFS, file measures {lufs:.1f} (stale manifest)")
        lead, trail = edge_silence_ms(path)
        if lead > 300 or trail > 300:
            out.append(f"edge silence {lead}/{trail} ms, max 300")
    if recorded is not None and kind == "dialogue" and recorded.get("word_accuracy", 0) < 0.95:
        out.append(f"word accuracy {recorded.get('word_accuracy')}, need >= 0.95")
    return out


def music_problems(path: Path, recorded: dict) -> list[str]:
    """Music stems (AUD-02, AUD-03, BRIEF D7): -18 LUFS +/- 2, <= -1 dBTP, not silent or clipped, 20-90 s, tonal
    (flatness < 0.5), no vocals (at most 4 transcribed words beyond the recogniser's stock hallucinations), and a
    seamless loop (no click: wrap jump <= 8x the typical step; spectra either side of the loop point correlate >= 0.9)."""
    out = []
    st = stats(path)
    if st["peak_db"] < -40:
        return ["silent"]
    if st["clipped"]:
        out.append("clipped")
    if not 20 <= st["duration_s"] <= 90:
        out.append(f"duration {st['duration_s']:.1f} s outside 20-90")
    ld = loudness(path)
    lufs, tp = float(ld["input_i"]), float(ld["input_tp"])
    if abs(lufs + 18) > 2:
        out.append(f"{lufs:.1f} LUFS, need -18 +/- 2")
    if tp > -1.0:
        out.append(f"true peak {tp:.1f} dBTP, need <= -1")
    if abs(recorded.get("lufs", 99) - lufs) > 0.5:
        out.append(f"manifest says {recorded.get('lufs')} LUFS, file measures {lufs:.1f} (stale manifest)")
    fl = flatness(path)
    if fl >= 0.5:
        out.append(f"spectral flatness {fl:.2f}: noise-like, need < 0.5")
    words = recorded.get("transcript", "").lower()
    for h in HALLUCINATIONS:
        words = words.replace(h, " ")
    if len(re.findall(r"[a-z']+", words)) > 4:
        out.append(f"possible vocals: transcript {recorded.get('transcript')!r}")
    sm = seam(path)
    if sm["seam_jump_ratio"] > 8:
        out.append(f"loop click: wrap jump {sm['seam_jump_ratio']}x the typical step")
    if sm["seam_spectral_corr"] < 0.9:
        out.append(f"loop seam correlation {sm['seam_spectral_corr']}, need >= 0.9")
    return out


def _mono(path: Path) -> np.ndarray:
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", str(path), "-ac", "1", "-ar", "48000", "-f", "f32le", "-"], capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype=np.float32)


def band_share(x: np.ndarray, lo: float, hi: float) -> float:
    spec = np.abs(np.fft.rfft(x)) ** 2
    f = np.fft.rfftfreq(len(x), 1 / 48000)
    return float(spec[(f >= lo) & (f < hi)].sum() / (spec.sum() + 1e-12))


def sfx_problems(files: list[dict]) -> list[str]:
    """SFX (AUD-02, BRIEF D7): peaks <= -1 dBTP, not silent or clipped, 0.03-10 s; far versions carry less than half
    the near version's share of energy above 4 kHz; large explosions put >= 20% of their energy below 100 Hz; no two
    variants nearly identical (normalised correlation < 0.95); engine loops seamless."""
    out = []
    audio = {}
    for f in files:
        path = ROOT / f["path"]
        if not path.exists():
            out.append(f"sfx {f['name']}: file missing")
            continue
        st = stats(path)
        if st["peak_db"] < -40:
            out.append(f"sfx {f['name']}: silent")
        if st["clipped"]:
            out.append(f"sfx {f['name']}: clipped")
        if not 0.03 <= st["duration_s"] <= 10:
            out.append(f"sfx {f['name']}: duration {st['duration_s']:.2f} s outside 0.03-10")
        tp = float(loudness(path)["input_tp"]) if st["duration_s"] >= 0.4 else st["peak_db"]
        if tp > -1.0:
            out.append(f"sfx {f['name']}: peak {tp:.1f} dB, need <= -1")
        audio[f["name"]] = _mono(path)
        if f.get("loop"):
            sm = seam(path)
            if sm["seam_jump_ratio"] > 8 or sm["seam_spectral_corr"] < 0.9:
                out.append(f"sfx {f['name']}: loop seam {sm}")
    for f in files:
        if f.get("band") == "far":
            near = f["name"].replace("_far_", "_near_")
            if near in audio and f["name"] in audio:
                hf_far, hf_near = band_share(audio[f["name"]], 4000, 24000), band_share(audio[near], 4000, 24000)
                if hf_far >= 0.5 * hf_near:
                    out.append(f"sfx {f['name']}: high-frequency share {hf_far:.3f} not under half of near ({hf_near:.3f})")
        if f.get("size") == "large" and f["name"] in audio:
            low = band_share(audio[f["name"]], 0, 100)
            if low < 0.2:
                out.append(f"sfx {f['name']}: {low:.2f} of energy below 100 Hz, need >= 0.2")
    groups: dict[str, list[str]] = {}
    for f in files:
        if "variant" in f:
            groups.setdefault(f["name"].rsplit("_v", 1)[0], []).append(f["name"])
    # BRIEF D7: at least 4 variants for frequently heard sounds (shots, bursts, explosions, footsteps per surface).
    for g, names in groups.items():
        if g.split("_")[0] in ("rifle", "mg", "explosion", "footstep") and len(names) < 4:
            out.append(f"sfx {g}: {len(names)} variants, frequent sounds need >= 4")
    for g, names in groups.items():
        for i in range(len(names)):
            for k in range(i + 1, len(names)):
                a, b = audio.get(names[i]), audio.get(names[k])
                if a is None or b is None:
                    continue
                n = min(len(a), len(b))
                c = float(np.dot(a[:n], b[:n]) / (np.linalg.norm(a[:n]) * np.linalg.norm(b[:n]) + 1e-12))
                if c >= 0.95:
                    out.append(f"sfx {names[i]} and {names[k]}: nearly identical (correlation {c:.3f})")
    return out


def self_test() -> None:
    with tempfile.TemporaryDirectory() as d:
        dd = Path(d)
        def gen(name: str, expr: str, dur: float = 2.0) -> Path:
            p = dd / name
            subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", f"aevalsrc={expr}:s=24000:d={dur}", str(p)], check=True)
            return p
        cases = {
            "silent": (gen("silent.wav", "0"), "silent"),
            "clipped": (gen("clip.wav", "1.5*sin(2*PI*440*t)"), "clipped"),
            "quiet": (gen("quiet.wav", "0.03*sin(2*PI*440*t)"), "LUFS"),
            "short": (gen("short.wav", "0.3*sin(2*PI*440*t)", 0.2), "duration"),
        }
        for name, (path, word) in cases.items():
            probs = file_problems(path, "dialogue", None)
            if not any(word in p for p in probs):
                raise SystemExit(f"check_audio self-test: '{name}' not detected ({probs})")
        # music rules: white noise is not music; a loop that ends on a square step clicks at the wrap
        noise = gen("noise.wav", "0.2*(random(0)*2-1)", 30.0)
        if not any("flatness" in p for p in music_problems(noise, {"lufs": -99})):
            raise SystemExit("check_audio self-test: noise passed the music tonality check")
        # sfx rules: identical variants, a "far" file as bright as its "near" file, a "large" explosion with no low end
        bright = gen("bright.wav", "0.5*(random(1)*2-1)", 1.0)
        rel = str(bright.relative_to(ROOT)) if bright.is_relative_to(ROOT) else None
        if rel is None:
            import shutil
            tmpdir = ROOT / "build" / "check_audio_selftest"
            tmpdir.mkdir(parents=True, exist_ok=True)
            shutil.copy(bright, tmpdir / "bright.wav")
            rel = str((tmpdir / "bright.wav").relative_to(ROOT))
        fake = [{"name": "t_near_v1", "band": "near", "variant": 1, "path": rel}, {"name": "t_far_v1", "band": "far", "variant": 1, "path": rel},
                {"name": "t_near_v2", "band": "near", "variant": 2, "path": rel}, {"name": "boom_large_v1", "size": "large", "variant": 1, "path": rel}]
        probs = sfx_problems(fake)
        for word in ("nearly identical", "high-frequency", "below 100 Hz"):
            if not any(word in q for q in probs):
                raise SystemExit(f"check_audio self-test: sfx rule '{word}' not detected ({probs})")
        click = gen("click.wav", "0.2*sin(2*PI*220*t)+0.5*gt(t\\,29.99)", 30.0)
        if not any("loop" in p for p in music_problems(click, {"lufs": -99})):
            raise SystemExit("check_audio self-test: a clicking loop passed the seam check")


def main() -> int:
    self_test()
    manifest_path = ROOT / "audio" / "manifest.json"
    if not manifest_path.exists():
        print("audio/manifest.json missing; run scripts/voice_lines.py qa")
        return 1
    manifest = {m["id"]: m for m in json.loads(manifest_path.read_text())["files"]}
    want = lines()
    problems = []
    rows = 0
    with open(ROOT / "dialogue" / "lines.csv", newline="") as f:
        rows = sum(int(r["variants"]) for r in csv.DictReader(f))
    for line in want:
        m = manifest.get(line["id"])
        if m is None:
            problems.append(f"{line['id']}: not in the manifest")
            continue
        if not line["path"].exists():
            problems.append(f"{line['id']}: file missing ({line['path'].relative_to(ROOT)})")
            continue
        if m.get("sha256") != hashlib.sha256(line["path"].read_bytes()).hexdigest():
            problems.append(f"{line['id']}: file differs from the manifest (sha256): re-run scripts/voice_lines.py qa")
        if m.get("text") != line["text"]:
            problems.append(f"{line['id']}: manifest text differs from the source (regenerate)")
        for p in file_problems(line["path"], "dialogue", m):
            problems.append(f"{line['id']}: {p}")
    music_path = ROOT / "audio" / "music_manifest.json"
    music = {m["id"]: m for m in json.loads(music_path.read_text())["files"]} if music_path.exists() else {}
    for era in STYLE:
        for layer in LAYER:
            mid = f"{era}_{layer}"
            m = music.get(mid)
            f = ROOT / "game" / "assets" / "audio" / "music" / f"{mid}.ogg"
            if m is None or not f.exists():
                problems.append(f"music {mid}: missing")
                continue
            if m.get("sha256") != hashlib.sha256(f.read_bytes()).hexdigest():
                problems.append(f"music {mid}: file differs from the manifest (sha256): re-run scripts/generate_music.py --recheck")
            for p in music_problems(f, m):
                problems.append(f"music {mid}: {p}")
    sfx_path = ROOT / "audio" / "sfx_manifest.json"
    sfx = json.loads(sfx_path.read_text())["files"] if sfx_path.exists() else []
    if not sfx:
        problems.append("audio/sfx_manifest.json missing or empty; run scripts/generate_sfx.py")
    problems += sfx_problems(sfx)
    # BRIEF D7: a design entry per sound family, a spectrogram sheet per family and an audition page listing every file.
    import audio_review
    review = audio_review.build()
    page = Path(review["page"]).read_text()
    for fam in sorted({f["family"] for f in sfx}):
        if not (ROOT / "audio" / "design" / f"{fam}.yaml").exists():
            problems.append(f"sfx family {fam}: no audio/design/{fam}.yaml")
        sheet = Path(review["sheets"].get(fam, ""))
        if not sheet.exists() or sheet.stat().st_size < 2000:
            problems.append(f"sfx family {fam}: spectrogram sheet missing or empty")
    for f in sfx:
        if f["path"] not in page:
            problems.append(f"audition page does not list {f['path']}")
    extra = sorted(set(manifest) - {l["id"] for l in want})
    if extra:
        problems.append(f"manifest lists files no source asks for: {extra[:5]}")
    for p in problems:
        print(f"FAIL {p}")
    print(f"check_audio: {len(want)} spoken files ({rows} dialogue rows x variants, {len(want) - rows} briefings), {len(music)} music stems, {len(sfx)} sfx, {len(problems)} problems")
    return 1 if problems else 0


if __name__ == "__main__":
    raise SystemExit(main())
