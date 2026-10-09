# Detail rubric (VFX, destruction, animation, audio, environment, UI sheets — contract §D)

Used by `art-reviewer` for sheets under `build/detail_review/` and `build/audio_review/`. Score 1–5. Every score cites a sheet file and frame or region, and the reference it is compared to.

| Criterion | Weight | 5 means |
|---|---|---|
| Reference fidelity | 25% | Timing, shape, color and motion match the reference pack notes (flash ≤ 2 frames, smoke rise, debris arcs, gait, sound character) |
| Layer completeness | 20% | Every layer the §D section lists is present and distinct (e.g. flash, fireball, shock ring, ejecta, sparks, smoke, scorch for D1; transient, body, low end, tail for D7) |
| Variant distinctness | 15% | Surface, type, scale and distance variants differ in shape and timing, not just tint or gain |
| RTS readability | 15% | Reads clearly at 120–300 m and does not hide units or UI |
| Persistence and consequence | 10% | Leaves what the section requires (craters, rubble, wrecks, smoke for 60 s+, trampled grass) and it matches the sim state |
| Technical quality | 15% | No popping, sorting errors, foot slide, clipping, audible loop seams or clicks; within the G6 or G9 budget |

**Pass:** weighted score ≥ 4.0 and no criterion below 3 (G15). Sheets must be newer than the content they show.
