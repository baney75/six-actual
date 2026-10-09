# State

Updated 2026-10-09 21:05Z. Phase: **1 (Cold War vertical slice)**, with the sim extended across all three eras. Nothing is committed; commits wait for Donovan.

## Verified (62)

SIM-01, 02, 03, 04, 05, 06, 07, 08, 09, 10, 11, 12, 13; CMD-01, 02, 03, 04, 05, 06, 08; FLY-01, 02, 03, 04, 07; SUP-01, 02, 03, 04, 05; ERA-01, 04; UX-01, 02, 03; MODE-01, 02; AUD-01, 02, 03; NET-02; GS-01, 02, 03, 05; QA-01, 02, 03; DET-08; MEC-01, 02, 03, 04, 05, 06, 07, 08, 09, 10, 11, 12, 13.

**Latest full gate set**, all green:
- G1–G4, G9–G14: `build/reports/20261009T205008Z-gates-G1-G2-G3-G4-G9-G10-G11-G12-G13-G14.md`
- G5: `build/reports/20261009T210319Z-gates-G5.md`
- G6: `build/reports/20261009T210439Z-gates-G6.md` (tick p95 3.19 ms; the limit was tightened to 5.0, D072)
- G13 runs 20 recorded input scripts. Gates fail on any failing command (D058).

## In the sim now

- **Battle (`sim/Battle/`).**
  - Terrain, LOS, pathing with formations and air, combat (facing, cover, veterancy, top attack), morale, supply.
  - Economy (phases, decks for 4 income profiles in each of 3 eras, SP, Field Requests), frontline.
  - Artillery (CEP, smoke, illumination, counter-battery, naval gunfire, precision and GPS rounds).
  - Off-map support: 87 options in 3 eras (air, engineering, EW, minefields, drones, tactical nuclear release).
  - Logistics: repair, dumps, supply and medevac helicopters.
  - Weather and time of day with thermal sights; multi-phase operations with persistent wrecks, craters and fortifications and an event deck.
  - Command: XO plus 8 seats, AI deployment and artillery doctrine.
  - Flight: Take Control and the flight model, loadouts, threat warnings.
- **Grand strategy (`sim/GrandStrategy/`).** 3 theaters; production, research, supply, armies, commanders; RTS or auto-resolved battles; diplomacy; nuclear escalation; AI; save/load.
- **Network (`sim/Net/`).** Deterministic lockstep: 8 clients, latency, loss, replays, reconnect.
- **Data.** 194 units and 56 decks across 3 eras and 14 factions (D054, D055, D057). Every number is sourced in `assets_src/research/*.json` or marked as a design value. Haiku agents cross-checked the new entries against their pages, and confirmed errors were fixed.

## Style board (Cold War) — blockout status

| Item | State | Evidence |
|---|---|---|
| M1A1 | escalated after 4 rejections | build/reports/m1a1-blockout-escalation.html |
| M60A3 | blockout APPROVED round 2 (4.00); front needs waiver | build/assets_review/m60a3/blockout/review_2.md |
| T-80BV | blockout APPROVED round 4 (4.00); side/front need waiver | build/assets_review/t80bv/blockout/review_4.md |
| A-10A | blockout APPROVED round 2 (4.00); front IoU needs waiver | build/assets_review/a10a/blockout/review_2.md |
| Su-25 | escalated after 4 rejections (2.56, 3.00, 3.44, 3.44) | build/assets_review/su25/blockout/review_1..4.md |
| Town block | blockout APPROVED round 2 (4.00) | build/assets_review/wgerman_town_block_1989/blockout/review_2.md |
| US rifle squad | escalated after 4 rejections (3.00, 3.00, 3.00, 3.44); helmet is the remaining blocker | build/assets_review/us_rifle_squad_1989/blockout/review_1..4.md |

Generators: ground_vehicle.py, aircraft.py, building_kit.py (on the sim chunk grid), infantry_kit.py (ANSUR 1988 body).

## In progress / next

1. **Next, unblocked:** little remains without Donovan. The open rows need the G8 visual gate and art (style-board sign-off), a Steamworks account (NET-01), a DEM licence choice (ERA-03), or GPU rendering runs on his desktop (PERF-01/02).
2. **Balance:** the Cold War GS skew (NATO wins about 90%). Faction soaks for the balanced decks are done (D057).
3. **Audio** (local, D020; AUD-01/02/03, G9), **DET-*** and **REN-*** (G8), art after the escalations.

## Blockers


- WAITING ON DONOVAN: M1A1, Su-25 and US rifle squad blockouts escalated after 4 rejections each; IoU waiver for views without distributable ortho-quality refs (A-10 front, Su-25 all views, T-80BV side/front).
- Commits: Donovan has not said "commit". Everything since Phase 0 is uncommitted.
- Style board and art bible sign-off (Donovan) gates mass asset production.

