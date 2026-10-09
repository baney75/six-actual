# Roadmap

Contract v5 (`docs/BRIEF.md` §I). A phase is done only when `scripts/verify.sh --phase N` passes, the report is linked from `PROGRESS.md`, and `build/reports/phase-N.html` exists.

| Phase | Required IDs at `verified` | Extra exit criteria |
|---|---|---|
| 0 Foundations | SIM-01, SIM-02, SIM-03, QA-01, QA-02. G1 (Linux, Windows, macOS), G2, G3, G10, G11, G12, G14 operational. | Bootstrap reproducible on a fresh account. `.claude/` agents, hooks and skills in place and tested. API docs dumped. Tools pinned. |
| 1 Cold War slice | SIM-04–SIM-10, SIM-12; MEC-01–05, MEC-08; ART-01–03; REF-01; DET-01–05, DET-07, DET-08, DET-10, DET-11; CMD-01–06; FLY-01–03; SUP-01, SUP-02, SUP-05 (Cold War); AUD-01, AUD-02 (slice lines); UX-01 (subset); PERF-01 on the slice | 25 units a side with validated models. One 8×8 km map. Art bible and style board approved by Donovan. A human-playable 20-minute skirmish. Name collision check. |
| 2 Three eras | DET-06, DET-09, ERA-01, ERA-04, SIM-11, SUP-03, SUP-04, FLY-04–06, REN-01–03, AUD-03 | 30 units a faction validated. 2 maps an era. |
| 3 Campaigns | MODE-02, CMD-07, CMD-08, UX-03 | 3 operations an era, then 6. |
| 4 Multiplayer | NET-01, NET-02, FLY-07 | A real 2-machine LAN test by Donovan, recorded. |
| 5 Grand strategy | GS-01–GS-05 | |
| 6 Version 1.0 | Everything else, including ERA-02, ERA-03, MODE-01, UX-02, PERF-02, QA-03 | 60+ units a faction. 4 maps an era. `--full` green twice on different days. Release checklist. |

Current phase: **0** (reopened under v5 on 2026-10-08; see `docs/STATE.md`).

Phase 1 starts with the tooling that unblocks most of its rows: `sim/Headless --scenario` (G4), then the reference-pack board, then the §M generators and contact sheets.
