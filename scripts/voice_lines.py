#!/usr/bin/env python3
"""Local voice pipeline for every spoken line (D020, D066): campaign briefings and the radio dialogue matrix.

    python3 scripts/voice_lines.py generate [--only ID ...]   synthesise missing lines (Qwen3-TTS via parish-media)
    python3 scripts/voice_lines.py qa                         transcribe all, retry lines under 95% word accuracy
                                                              (new seed, at most 2 retries), write audio/manifest.json

Sources: data/campaign.json briefings (custom voices per era) and dialogue/lines.csv with audio/voices.json (designed
voice per speaker). Every file is loudness-normalised (two-pass ffmpeg loudnorm, -16 LUFS / -1.5 dBTP, AUD-02) and
gets a .wav.json sidecar. Word accuracy follows BRIEF W3 step 4 (>= 95%, at most 2 retries), with transcription by the
local faster-whisper (tools/asr) and spoken variants from dialogue/aliases.csv for names the recogniser spells
differently. The GPU guard in parish-media is respected: a busy GPU is waited out, never interrupted.
"""
from __future__ import annotations

import csv
import hashlib
import json
import re
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
GAME_AUDIO = ROOT / "game" / "assets" / "audio"
TOOL = Path.home() / ".local" / "bin" / "parish-media"
RAW = Path.home() / ".local" / "share" / "parish-media" / "outputs" / "six-actual-voice"
FLOOR = 0.95
BRIEFING_VOICES = {
    "coldwar": ("custom", "Ryan", "Calm, clipped US Army staff officer briefing a commander over a field radio; steady, serious, unhurried."),
    "ww2": ("custom", "Aiden", "Steady American Army staff officer of the 1940s briefing his commander in a command post; firm, measured, plain."),
    "modern": ("custom", "Ryan", "Crisp, professional modern US Army operations officer giving a short tactical briefing; focused and even."),
}


def lines() -> list[dict]:
    out = []
    camp = json.loads((ROOT / "data" / "campaign.json").read_text())
    for era, e in camp["eras"].items():
        mode, speaker, instruct = BRIEFING_VOICES[era]
        for n in e["operations"]:
            out.append({"id": n["id"], "kind": "briefing", "era": era, "text": n["briefing"], "mode": mode, "speaker": speaker,
                        "instruct": instruct, "seed": n["seed"], "path": GAME_AUDIO / "briefings" / era / f"{n['id']}.wav"})
    voices = json.loads((ROOT / "audio" / "voices.json").read_text())["speakers"]
    with open(ROOT / "dialogue" / "lines.csv", newline="") as f:
        for r in csv.DictReader(f):
            v = voices[r["speaker"]]
            for k in range(int(r["variants"])):
                vid = r["id"] if int(r["variants"]) == 1 else f"{r['id']}_v{k + 1}"
                out.append({"id": vid, "kind": "dialogue", "era": r["era"], "text": r["text"], "mode": v["mode"], "speaker": r["speaker"],
                            "instruct": v["instruction"], "seed": v["seed"] + k, "path": GAME_AUDIO / "dialogue" / r["era"] / f"{vid}.wav"})
    return out


def loudness(path: Path) -> dict:
    r = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-i", str(path), "-af", "loudnorm=I=-16:TP=-1.5:LRA=11:print_format=json", "-f", "null", "-"],
                       capture_output=True, text=True, check=True)
    m = re.search(r"\{[^{}]*\"input_i\"[^{}]*\}", r.stderr, re.S)
    return json.loads(m.group(0)) if m else {}


def synth(line: dict, seed: int) -> dict:
    RAW.mkdir(parents=True, exist_ok=True)
    raw = RAW / f"{line['id']}_s{seed}.wav"  # the tool never overwrites, so one file per seed
    args = [str(TOOL), "voice", "--", "--mode", line["mode"], "--instruct", line["instruct"], "--text", line["text"], "--seed", str(seed), "--output", str(raw)]
    if line["mode"] == "custom":
        args[5:5] = ["--speaker", line["speaker"]]  # after "--mode <mode>"
    for _ in range(8):
        r = subprocess.run(args, capture_output=True, text=True)
        if "GPU is in use" not in r.stdout + r.stderr:
            break
        time.sleep(20)
    receipt = json.loads(r.stdout.strip().splitlines()[-1]) if r.stdout.strip() else {}
    if r.returncode != 0 or receipt.get("status") != "success" or not raw.exists():
        raise SystemExit(f"FAIL {line['id']}: {r.stdout[-300:]} {r.stderr[-300:]}")
    first = loudness(raw)
    dst = line["path"]
    dst.parent.mkdir(parents=True, exist_ok=True)
    af = (f"loudnorm=I=-16:TP=-1.5:LRA=11:measured_I={first['input_i']}:measured_TP={first['input_tp']}:"
          f"measured_LRA={first['input_lra']}:measured_thresh={first['input_thresh']}:offset={first['target_offset']}:linear=true")
    subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", str(raw), "-af", af, "-ar", "24000", "-ac", "1", str(dst)], check=True)
    fit_loudness(dst)
    tool_meta = raw.with_suffix(".wav.json")
    meta = {"id": line["id"], "kind": line["kind"], "era": line["era"], "text": line["text"], "speaker": line["speaker"], "mode": line["mode"],
            "instruction": line["instruct"], "seed": seed, "job": receipt, "tool_metadata": json.loads(tool_meta.read_text()) if tool_meta.exists() else None,
            "processing": "ffmpeg loudnorm two-pass to -16 LUFS / -1.5 dBTP, 24 kHz mono", "prompt_hash": prompt_hash(line, seed)}
    meta = portable(meta)
    dst.with_suffix(".wav.json").write_text(json.dumps(meta, indent=2) + "\n")
    return meta


def fit_loudness(path: Path) -> None:
    """Short, peaky lines cannot reach -16 LUFS in linear mode under the -1.5 dBTP ceiling. If a file is more than
    1 LU off, limit the peaks first (alimiter at -6 dBFS), then normalise again."""
    for _ in range(3):
        ld = loudness(path)
        if abs(float(ld["input_i"]) + 16) <= 1.0:
            return
        tmp = path.with_suffix(".fit.wav")
        af = "alimiter=limit=0.5:level=disabled,loudnorm=I=-16:TP=-1.5:LRA=11"
        subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", str(path), "-af", af, "-ar", "24000", "-ac", "1", str(tmp)], check=True)
        tmp.replace(path)


def portable(obj):
    """Sidecars are tracked: home-directory paths become relative to the parish-media output root or the repo."""
    if isinstance(obj, dict):
        return {k: portable(v) for k, v in obj.items()}
    if isinstance(obj, list):
        return [portable(v) for v in obj]
    if isinstance(obj, str):
        return obj.replace(str(Path.home() / ".local" / "share" / "parish-media" / "outputs") + "/", "parish-media:outputs/").replace(str(ROOT) + "/", "")
    return obj


def prompt_hash(line: dict, seed: int) -> str:
    return hashlib.sha256(f"{line['mode']}|{line['speaker']}|{line['instruct']}|{line['text']}|{seed}".encode()).hexdigest()[:16]


def aliases() -> dict[str, list[str]]:
    out: dict[str, list[str]] = {}
    with open(ROOT / "dialogue" / "aliases.csv", newline="") as f:
        for r in csv.DictReader(f):
            out[r["canonical"].lower()] = [v.strip().lower() for v in r["spoken_variants"].split("|") if v.strip()]
    return out


NUMBERS = {"zero": "0", "one": "1", "two": "2", "three": "3", "four": "4", "five": "5", "six": "6", "seven": "7", "eight": "8", "nine": "9", "ten": "10",
           "first": "1st", "second": "2nd"}


def words(text: str) -> list[str]:
    t = text.lower().replace("oh-four-hundred", "0400").replace("-", " ").replace("'s", "s")
    return [NUMBERS.get(w, w) for w in re.findall(r"[a-z0-9]+", t)]


def stem(w: str) -> str:
    return w[:-1] if len(w) > 3 and w.endswith("s") else w


def recall(text: str, heard: str, al: dict[str, list[str]]) -> float:
    """Word accuracy of the script against the transcript, after normalising case, punctuation, number words and
    plural stems. A script word also counts when the transcript joins it with a neighbour ("under way" -> "underway")
    or splits it ("rearguard" -> "rear guard"), or when it says a listed spoken variant (dialogue/aliases.csv)."""
    heard_w = words(heard)
    got = {stem(w) for w in heard_w}
    joined = {stem(a + b) for a, b in zip(heard_w, heard_w[1:])}
    flat = " ".join(heard_w)
    want = words(text)
    ok = [False] * len(want)
    for i, w in enumerate(want):
        if stem(w) in got or stem(w) in joined or any(re.search(rf"\b{re.escape(v)}\b", flat) for v in al.get(w, [])):
            ok[i] = True
        if i + 1 < len(want) and stem(w + want[i + 1]) in got:
            ok[i] = ok[i + 1] = True
    return sum(ok) / max(1, len(want))


def transcribe(paths: list[Path]) -> dict[Path, str]:
    r = subprocess.run(["mise", "exec", "--", "uv", "run", "python", "transcribe.py", *map(str, paths)], cwd=ROOT / "tools" / "asr", capture_output=True, text=True, check=True)
    out = {}
    for row in r.stdout.splitlines():
        if "\t" in row:
            p, t = row.split("\t", 1)
            out[Path(p).resolve()] = t
    return out


def edge_silence_ms(path: Path) -> tuple[int, int]:
    """Leading and trailing silence (below -45 dB) in milliseconds."""
    r = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-i", str(path), "-af", "silencedetect=noise=-45dB:d=0.05", "-f", "null", "-"], capture_output=True, text=True)
    dur = float(subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", str(path)], capture_output=True, text=True).stdout.strip())
    starts = [float(x) for x in re.findall(r"silence_start: ([\d.]+)", r.stderr)]
    ends = [float(x) for x in re.findall(r"silence_end: ([\d.]+)", r.stderr)]
    lead = ends[0] if starts and starts[0] <= 0.001 and ends else 0.0
    trail = dur - starts[-1] if starts and (len(ends) < len(starts) or ends[-1] >= dur - 0.01) else 0.0
    return int(lead * 1000), int(trail * 1000)


def trim_edges(path: Path) -> None:
    """Trim leading/trailing silence to 100 ms (G9: no silence over 300 ms at either end), keep loudness."""
    tmp = path.with_suffix(".trim.wav")
    af = "silenceremove=start_periods=1:start_threshold=-45dB:start_silence=0.1,areverse,silenceremove=start_periods=1:start_threshold=-45dB:start_silence=0.1,areverse"
    subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", str(path), "-af", af, str(tmp)], check=True)
    tmp.replace(path)


def qa() -> int:
    al = aliases()
    all_lines = lines()
    by_path = {l["path"].resolve(): l for l in all_lines}
    missing = [l["id"] for l in all_lines if not l["path"].exists()]
    if missing:
        print(f"missing {len(missing)} lines: {missing[:10]}")
        return 1
    todo = [l["path"] for l in all_lines]
    for attempt in range(3):
        for p in todo:
            lead, trail = edge_silence_ms(p)
            if lead > 250 or trail > 250:
                trim_edges(p)
        heard = transcribe(todo)
        retry = []
        for p in todo:
            line = by_path[p.resolve()]
            meta = json.loads(p.with_suffix(".wav.json").read_text())
            rec = recall(line["text"], heard.get(p.resolve(), ""), al)
            meta["asr"] = {"tool": "tools/asr/transcribe.py (faster-whisper, CPU)", "transcript": heard.get(p.resolve(), ""), "word_accuracy": round(rec, 3), "attempt": attempt}
            p.with_suffix(".wav.json").write_text(json.dumps(meta, indent=2) + "\n")
            if rec < FLOOR and attempt < 2:
                retry.append(p)
        if not retry:
            break
        print(f"retrying {len(retry)} lines below {FLOOR}: {[by_path[p.resolve()]['id'] for p in retry]}")
        for p in retry:
            line = by_path[p.resolve()]
            synth(line, line["seed"] + 1000 * (attempt + 1))
        todo = retry
    manifest = []
    for l in all_lines:
        meta = json.loads(l["path"].with_suffix(".wav.json").read_text())
        ld = loudness(l["path"])
        lead, trail = edge_silence_ms(l["path"])
        dur = float(subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", str(l["path"])], capture_output=True, text=True).stdout.strip())
        manifest.append({"id": l["id"], "kind": l["kind"], "category": "dialogue", "era": l["era"], "speaker": l["speaker"], "text": l["text"],
                         "path": str(l["path"].relative_to(ROOT)), "lufs": float(ld["input_i"]), "true_peak_db": float(ld["input_tp"]),
                         "duration_s": round(dur, 3), "lead_silence_ms": lead, "trail_silence_ms": trail,
                         "word_accuracy": meta["asr"]["word_accuracy"], "seed": meta["seed"], "prompt_hash": meta.get("prompt_hash") or prompt_hash(l, meta["seed"]),
                         "tool": (meta.get("tool_metadata") or {}).get("model_identifier", ""),
                         "sha256": hashlib.sha256(l["path"].read_bytes()).hexdigest()})
    (ROOT / "audio" / "manifest.json").write_text(json.dumps({"version": 1, "files": manifest}, indent=1, ensure_ascii=False) + "\n")
    low = [m for m in manifest if m["word_accuracy"] < FLOOR]
    acc = [m["word_accuracy"] for m in manifest]
    print(f"voice qa: {len(manifest)} lines, word accuracy mean {sum(acc) / len(acc):.3f} min {min(acc):.3f}, {len(low)} below {FLOOR}")
    for m in low:
        print(f"LOW {m['id']}: {m['word_accuracy']}: {json.loads((ROOT / m['path']).with_suffix('.wav.json').read_text())['asr']['transcript']}")
    return 1 if low else 0


def main() -> int:
    cmd = sys.argv[1] if len(sys.argv) > 1 else ""
    if cmd == "generate":
        only = set(sys.argv[sys.argv.index("--only") + 1:]) if "--only" in sys.argv else None
        made = 0
        for line in lines():
            if (only and line["id"] not in only) or (line["path"].exists() and not only):
                continue
            meta = synth(line, line["seed"])
            made += 1
            print(f"ok {line['id']}: {(meta.get('tool_metadata') or {}).get('duration_seconds')} s", flush=True)
        print(f"generated {made}")
        return 0
    if cmd == "qa":
        return qa()
    if cmd == "normalize":
        for line in lines():
            if line["path"].exists():
                fit_loudness(line["path"])
        print("normalized")
        return 0
    print(__doc__)
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
