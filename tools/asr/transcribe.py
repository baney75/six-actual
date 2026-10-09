"""Local transcription for the dialogue round trip (W3). CPU int8, so it never takes the GPU job lock.

Usage: uv run python transcribe.py <file.wav> [...]   -> prints "<file>\t<transcript>" per file
"""
import subprocess
import sys

import numpy as np
from faster_whisper import WhisperModel


def load(path: str) -> np.ndarray:
    raw = subprocess.run(["ffmpeg", "-nostdin", "-v", "error", "-i", path, "-f", "s16le", "-ac", "1", "-ar", "16000", "-"],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.int16).astype(np.float32) / 32768.0


model = WhisperModel("small.en", device="cpu", compute_type="int8", download_root="../cache/whisper")
for f in sys.argv[1:]:
    segs, _ = model.transcribe(load(f), beam_size=5, language="en")
    print(f"{f}\t{' '.join(s.text.strip() for s in segs)}")
