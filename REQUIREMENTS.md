# Requirements

Status is `todo`, `in-progress`, `verified`, or `waived (reason, approval)`. A `verified` row names the check that passed. IDs match `docs/BRIEF.md` section F and stay stable.

| ID | Requirement | Status | Evidence |
|---|---|---|---|
| SIM-01 | Fixed tick, fixed-point state, no binary floating point in sim state | verified | `build/reports/20261005T020355Z-phase0.md` (G1, G2) |
| SIM-02 | Bit-identical state across runs, processes, and thread counts | verified | same report, G3, `verify/golden/determinism.txt` |
| SIM-03 | Replay reproduces the final state hash | verified | same report, G3 replay line |
| SIM-04 | Terrain LOS, optics, stealth | in-progress | Hill blocks a shot in `BattleTests`. Not a G4 scenario yet. |
| SIM-05 | Fog of war | verified | `SystemsTests.Fog_hides_an_enemy_until_it_is_spotted` and the fog-weather case. Own units stay visible. |
| SIM-06 | Armor facing and penetration | in-progress | Front holds, side does not, in `BattleTests`. |
| SIM-07 | Criticals | verified | `SystemsTests.Overmatch_sets_a_critical_flag` |
| SIM-08 | Suppression and morale | verified | `SystemsTests.Suppression_breaks_morale_and_the_unit_bails` |
| SIM-09 | Ammo, fuel, resupply | in-progress | A truck refills a tank in `BattleTests`. |
| SIM-10 | Structural destruction | in-progress | `Collapse.Step` drops an unsupported column in one tick. Not yet tied to the battle map. |
| SIM-11 | Weather and time of day | in-progress | Fog cuts detection versus clear in `SystemsTests`. Air and accuracy modifiers are not in yet. |
| SIM-12 | Pathfinding | todo | |
| SIM-13 | 2,000 entities, tick p95 ≤ 8 ms | todo | |
| CMD-01 | XO plus eight sub-commanders | in-progress | Nine seat names exist. They do not yet own separate orders. |
| CMD-02 | Assignment and reassignment | todo | |
| CMD-03 | Order set with queue and deadline | todo | |
| CMD-04 | Intent changes behavior | in-progress | High aggression advances, low aggression holds, in `BattleTests`. |
| CMD-05 | Support requests | todo | |
| CMD-06 | Utility AI or HTN, with behavior metrics | todo | |
| CMD-07 | Names, portraits, voices, traits, reports | todo | |
| CMD-08 | XP, wounds, replacement persist | todo | |
| FLY-01 | Take Control, XO assumes command | in-progress | Take-control sets the XO flag within one tick in `BattleTests`. |
| FLY-02 | Return on bingo, winchester, destruction, landing, or key | in-progress | Bingo, empty ammo, and the release order are unit-tested. Destruction and landing are not. |
| FLY-03 | Arcade-sim flight model from data | in-progress | Climbing burns more fuel than level flight in `SystemsTests`. Stall and G limits are not in yet. |
| FLY-04 | Mouse-aim, gamepad, HOTAS | todo | |
| FLY-05 | Era HUDs | todo | |
| FLY-06 | Era weapons and ground/air threats | todo | |
| FLY-07 | Flight in lockstep multiplayer | todo | |
| SUP-01 | Artillery and fires | in-progress | A battery damages the target only after the delay in `SystemsTests`. The rest of the fires menu is not in. |
| SUP-02 | Air support | todo | |
| SUP-03 | Logistics and engineering | todo | |
| SUP-04 | Electronic warfare | todo | |
| SUP-05 | Data-driven, era-gated, delayed support | todo | |
| ERA-01 | Three eras and their factions | todo | Eras exist in `data/catalog.json`; factions and units do not |
| ERA-02 | 60 units per faction | todo | |
| ERA-03 | Four 8 km maps per era | todo | |
| ERA-04 | No anachronisms | todo | |
| MODE-01 | Skirmish 1v1 to 5v5 | todo | |
| MODE-02 | Campaigns | in-progress | Six operations per era, XP, and an early end after three losses. No voiced briefings. |
| NET-01 | Lockstep, lobbies, replays, reconnect | todo | |
| NET-02 | 4v4 soak with loss and latency | todo | |
| GS-01 | Grand strategy theater | in-progress | Eight Central Europe provinces resolve to one owner in `CampaignTests`. |
| GS-02 | Battle or auto-resolve | todo | |
| GS-03 | Light diplomacy | in-progress | A ceasefire stops provinces changing hands. Alliances and lend-lease are not in. |
| GS-04 | Nuclear escalation | in-progress | High tension spends the one nuclear weapon and cuts enemy industry. No blast scene. |
| GS-05 | Grand strategy soak | todo | |
| REN-01 | War-zone VFX | todo | |
| REN-02 | Readable at RTS height | todo | |
| REN-03 | Lighting and atmosphere | todo | |
| AUD-01 | Dialogue files for every trigger | todo | |
| AUD-02 | Loudness standards | todo | |
| AUD-03 | Spatial audio and music layers | todo | |
| UX-01 | Menu flow | todo | |
| UX-02 | Accessibility | todo | |
| UX-03 | Tutorials | todo | |
| PERF-01 | 1440p benchmark | todo | |
| PERF-02 | Soak frame time and memory | todo | |
| QA-01 | No warnings, format clean, no unfinished markers | verified | same report, G12 |
| QA-02 | 80% line coverage on sim | verified | `build/reports/20261005T023633Z-phase0.md`, sim line coverage 0.934 |

The Phase 0 tick advances positions from orders at 20 Hz. Combat resolution is not in that tick yet. See `DECISIONS.md`.
