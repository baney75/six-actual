---
name: phase-gate
description: Run and package a phase gate — verify --phase N, polish checklist, tagged commit, three exports, capture video, phase-N.html report. Use when a phase's required IDs are all verified.
---
# Phase gate (§I, W7)

1. Confirm every ID the phase needs in `ROADMAP.md` is `verified` in `REQUIREMENTS.md`, with a report path.
2. Run `scripts/verify.sh --phase N`. Read the whole report. Then spawn `qa-verifier` on the same report for an independent read.
3. Make sure `docs/polish-checklist.md` has every item for this phase checked, with links (G15, from Phase 1 on).
4. Exports come from G1: `build/linux/SixActual.x86_64`, `build/windows/SixActual.exe`, `build/macos/SixActual.zip`.
5. Capture a 2–4 minute video of the phase's headline features with `--write-movie` (GPU: check `parish-media status`), then convert it with ffmpeg to mp4.
6. Write `build/reports/phase-N.html`: self-contained, with embedded screenshots and contact sheets, the verify summary, open issues, the next phase plan, and every item marked prepared, verified, built or delivered.
7. Add a PROGRESS.md entry linking the report, and rewrite docs/STATE.md.
8. Ask Donovan before you commit or tag (`phase-N`).
