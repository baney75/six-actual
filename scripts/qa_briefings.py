#!/usr/bin/env python3
"""Transcribe every briefing voice line (tools/asr, faster-whisper on the CPU) and score word recall against its text.

    python3 scripts/qa_briefings.py         writes transcript and recall into each .wav.json; exit 1 below the floor

Recall = share of the script's words found in the transcript (case and punctuation ignored, spelled-out times
normalised). Place names the recogniser does not know (Fulda, Huenfeld, Vogelsberg, Suwalki) lower recall without the
speech being wrong, so the floor is 0.85, and any line below it is listed for a human listen.
"""
from __future__ import annotations

import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
AUDIO = ROOT / "game" / "assets" / "audio" / "briefings"
FLOOR = 0.85


def words(text: str) -> list[str]:
    t = text.lower().replace("oh-four-hundred", "0400").replace("-", " ")
    return re.findall(r"[a-z0-9']+", t)


def main() -> int:
    wavs = sorted(AUDIO.glob("*/*.wav"))
    r = subprocess.run(["mise", "exec", "--", "uv", "run", "python", "transcribe.py", *map(str, wavs)], cwd=ROOT / "tools" / "asr", capture_output=True, text=True, check=True)
    heard = {}
    for line in r.stdout.splitlines():
        if "\t" in line:
            path, text = line.split("\t", 1)
            heard[Path(path).resolve()] = text
    low = []
    for wav in wavs:
        meta_path = wav.with_suffix(".wav.json")
        meta = json.loads(meta_path.read_text())
        said = heard.get(wav.resolve(), "")
        want = words(meta["text"])
        got = set(words(said))
        recall = sum(1 for w in want if w in got) / max(1, len(want))
        meta["asr"] = {"tool": "tools/asr/transcribe.py (faster-whisper, CPU)", "transcript": said, "word_recall": round(recall, 3)}
        meta_path.write_text(json.dumps(meta, indent=2) + "\n")
        if recall < FLOOR:
            low.append((wav.relative_to(ROOT), recall, said))
    for path, rec, said in low:
        print(f"LOW {path}: recall {rec:.2f}: {said}")
    recalls = [json.loads(w.with_suffix('.wav.json').read_text())["asr"]["word_recall"] for w in wavs]
    print(f"qa_briefings: {len(wavs)} lines, mean recall {sum(recalls) / max(1, len(recalls)):.3f}, min {min(recalls, default=0):.3f}, {len(low)} below {FLOOR}")
    return 1 if low else 0


if __name__ == "__main__":
    raise SystemExit(main())
