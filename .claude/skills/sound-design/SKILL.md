---
name: sound-design
description: Design and produce one sound family per section D7 — design YAML, procedural synthesis layers plus CC0 recordings, near/mid/far bands, ≥4 variants, loops, G9 and spectrogram checks. Music stems via local ACE-Step. Never ElevenLabs (D020).
---
# Sound design (§D7, local per D020)

1. **Ref pack** (`ref-pack`, class `sound`): near, mid and far listening references (look-only, never sampled) and a character note.
2. Write `audio/design/<id>.yaml`: purpose, refs, layers (transient, body, mechanics, low end, tail, debris), distance bands, variants, loop points, priority, max concurrent, and the synthesis recipe per layer.
3. Make the layers:
   - **Synthesis** in `scripts/audio/synth/` (numpy): noise bursts with shaped envelopes for transients; filtered noise and resonant bodies for booms; modal and FM partials for metal clanks and mechanics; sub-bass sine sweeps for low end; convolution with generated impulse responses (open field, forest, town, interior, valley) for tails.
   - **CC0 recordings**: Freesound with the CC0 filter, logged in ASSETS.md with URL, author and date.
   - Seed every random source so a variant is reproducible.
4. Edit with sox and ffmpeg scripts in `scripts/audio/` (trim, fade, normalize, seamless loop crossfades). Mix the layers into variants with the Python mixer.
5. Make near (< 50 m), mid (50–500 m) and far (> 500 m) versions. Far versions lose transients and highs and gain low rumble. Make 4 or more variants for frequent sounds.
6. G9 checks (`scripts/check_audio.py`):
   - loudness and peak
   - loop seam correlation ≥ 0.9 with no click
   - far versions have less energy above 4 kHz than near versions
   - big explosions have enough energy below 100 Hz
   - no near-duplicate variants
7. Write a spectrogram sheet to `build/audio_review/spectro/<id>.png` (open it) and add the family to the audition page.

**Music:** three stems per era theme (calm, tension, combat) from ACE-Step 1.5. Check `parish-media status`, then start with a 20–30 s instrumental:
`~/.local/bin/parish-media music --timeout 1800 -- --prompt '<instruments, mood, BPM, structure>' --duration 30 --seed <n> --output <abs path>`
Keep the model and seed metadata, then normalize to −18 LUFS. Keep the stems in the same key and BPM so the music director can crossfade them.

*The synth library, mixer and `scripts/check_audio.py` are not built yet (AUD-02, DET-08).*
