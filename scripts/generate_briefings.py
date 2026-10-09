#!/usr/bin/env python3
"""Generate the campaign briefing voice lines locally on Parish1 (MODE-02, D020, D065).

    python3 scripts/generate_briefings.py [--only ID ...] [--force]

For every operation in data/campaign.json without audio: Qwen3-TTS (custom voice, one speaker per era) through
~/.local/bin/parish-media, then ffmpeg loudness normalisation to -16 LUFS integrated / -1.5 dBTP (AUD-02 dialogue
target), written to game/assets/audio/briefings/<era>/<id>.wav with a .wav.json sidecar (text, speaker, instruction,
seed, the tool's receipt and the measured loudness). Run `~/.local/bin/parish-media status` first; the tool refuses a
busy GPU or battery power by itself.
"""
from __future__ import annotations

import json
import re
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "game" / "assets" / "audio" / "briefings"
TOOL = Path.home() / ".local" / "bin" / "parish-media"
RAW = Path.home() / ".local" / "share" / "parish-media" / "outputs" / "six-actual-briefings"
VOICES = {
    "coldwar": ("Ryan", "Calm, clipped US Army staff officer briefing a commander over a field radio; steady, serious, unhurried."),
    "ww2": ("Aiden", "Steady American Army staff officer of the 1940s briefing his commander in a command post; firm, measured, plain."),
    "modern": ("Ryan", "Crisp, professional modern US Army operations officer giving a short tactical briefing; focused and even."),
}


def loudness(path: Path) -> dict:
    r = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-i", str(path), "-af", "loudnorm=I=-16:TP=-1.5:LRA=11:print_format=json", "-f", "null", "-"],
                       capture_output=True, text=True, check=True)
    m = re.search(r"\{[^{}]*\"input_i\"[^{}]*\}", r.stderr, re.S)
    return json.loads(m.group(0)) if m else {}


def main() -> int:
    args = sys.argv[1:]
    force = "--force" in args
    only = set(args[args.index("--only") + 1:]) if "--only" in args else None
    camp = json.loads((ROOT / "data" / "campaign.json").read_text())
    RAW.mkdir(parents=True, exist_ok=True)
    made = 0
    for era, e in camp["eras"].items():
        speaker, instruct = VOICES[era]
        for n in e["operations"]:
            if only and n["id"] not in only:
                continue
            dst = OUT / era / f"{n['id']}.wav"
            if dst.exists() and not force:
                continue
            raw = RAW / f"{n['id']}.wav"
            for attempt in range(6):
                r = subprocess.run([str(TOOL), "voice", "--", "--mode", "custom", "--speaker", speaker, "--instruct", instruct,
                                    "--text", n["briefing"], "--seed", str(n["seed"]), "--output", str(raw)], capture_output=True, text=True)
                # The tool refuses a busy GPU (another job, a game). Wait and ask again; never touch the other work.
                if "GPU is in use" not in r.stdout + r.stderr:
                    break
                time.sleep(20)
            receipt = json.loads(r.stdout.strip().splitlines()[-1]) if r.stdout.strip() else {}
            if r.returncode != 0 or receipt.get("status") != "success" or not raw.exists():
                print(f"FAIL {era}/{n['id']}: {r.stdout[-400:]} {r.stderr[-400:]}")
                return 1
            first = loudness(raw)
            dst.parent.mkdir(parents=True, exist_ok=True)
            af = (f"loudnorm=I=-16:TP=-1.5:LRA=11:measured_I={first['input_i']}:measured_TP={first['input_tp']}:"
                  f"measured_LRA={first['input_lra']}:measured_thresh={first['input_thresh']}:offset={first['target_offset']}:linear=true")
            subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", str(raw), "-af", af, "-ar", "24000", "-ac", "1", str(dst)], check=True)
            after = loudness(dst)
            tool_meta = raw.with_suffix(".wav.json")
            meta = {
                "operation": n["id"], "era": era, "text": n["briefing"], "speaker": speaker, "mode": "custom", "instruction": instruct,
                "seed": n["seed"], "job": receipt, "tool_metadata": json.loads(tool_meta.read_text()) if tool_meta.exists() else None,
                "loudness_before": {k: first.get(k) for k in ("input_i", "input_tp")},
                "loudness_after": {k: after.get(k) for k in ("input_i", "input_tp")},
                "processing": "ffmpeg loudnorm two-pass to -16 LUFS / -1.5 dBTP, 24 kHz mono",
            }
            dst.with_suffix(".wav.json").write_text(json.dumps(meta, indent=2) + "\n")
            made += 1
            print(f"ok {era}/{n['id']}: {after.get('input_i')} LUFS, {meta['tool_metadata'] and meta['tool_metadata'].get('duration_seconds')} s")
    print(f"briefings generated: {made}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
