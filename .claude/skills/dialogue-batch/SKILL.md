---
name: dialogue-batch
description: Generate a batch of voiced lines locally with Qwen3-TTS on Parish1 from dialogue/lines.csv, with manifest caching, loudness normalization, and a local faster-whisper transcription round trip (W3 as amended by D020). Use for any voiced line. Never ElevenLabs.
---
# Dialogue batch (W3, local per D020)

1. Run `~/.local/bin/parish-media status`. Go only if `ac_online` is true, `active_jobs` and `compute_pids` are empty, and Donovan is not gaming. Otherwise stop and try later.
2. Select up to 50 rows from `dialogue/lines.csv` (id, speaker, era, faction, trigger, priority, text, variants, audio tags) whose text hash is not in `audio/manifest.json`.
3. Look up the speaker in `audio/voices.json`. Each recurring speaker (XO, the 8 commanders, pilots, a callout set per nation) has a fixed `design` instruct string: age, accent, timbre, delivery. Keep it byte-identical across batches so the voice stays consistent. For a new speaker, render 3 short candidates first, transcribe them, pick one, and save its instruct text and seed.
4. Generate one call per line, at most 1500 characters per call. Apply the delivery tag (urgent, under fire, calm) by appending it to the instruct text:
   `~/.local/bin/parish-media voice -- --mode design --instruct '<speaker instruct>, <delivery>' --text '<line>' --output $PWD/audio/generated/dialogue/<id>_<variant>.raw.wav`
5. Normalize to −16 LUFS (two-pass `loudnorm`), trim edge silence to 300 ms or less, and write `<id>_<variant>.wav`. Record the job receipt, instruct, seed and text hash in the manifest.
6. Transcribe: `(cd tools/asr && mise exec -- uv run python transcribe.py <files>)`. Normalize case and punctuation, apply `dialogue/aliases.csv` (XO → exo/ex-o and so on), and score word accuracy. Below 95%, regenerate with a new seed, at most 2 retries, then flag the line.
7. Write `build/audio_review/<batch>.html` with players for 20 random new lines, plus transcripts, durations and LUFS.
8. Confirm with `parish-media status` that the job is inactive and the GPU released. Report GPU minutes and failures.

The in-engine radio chain (band-pass, saturation, squelch, era-specific) is applied at runtime. Do not bake it in.
