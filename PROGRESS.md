# Progress

## 2026-10-04 — Project started

Machine, from `fastfetch` and `lspci` on this Omarchy box. `vulkaninfo` is not installed, so no Vulkan device report yet. `scripts/bootstrap.sh` lists `vulkan-tools` for the user to install.

- PC: Legion Pro 7 16IRX9H (83DE)
- CPU: Intel Core i9-14900HX
- GPU: NVIDIA GeForce RTX 4080 Max-Q / Mobile (AD104M), plus Intel Raptor Lake-S UHD Graphics
- Display: 2560x1600 built-in, 240 Hz
- RAM: 62.12 GiB
- OS: Omarchy 4.0.4-1, kernel 7.2.5-3-omarchy, Hyprland 0.56.2 (Wayland)

Seeded the design docs and a Phase 0 sim.

## 2026-10-05 — Phase 0 gates passed

`scripts/verify.sh --phase 0` passed. Report: `build/reports/20261005T020355Z-phase0.md`. Failures: 0.

Verified: SIM-01, SIM-02, SIM-03, QA-01, QA-02. Sim line coverage 0.956 (the report's G2 line). Godot 4.7.2 import and Linux/Windows export completed with no `ERROR:` lines. The Linux export printed `Six Actual foundation 842055ead8b768f51de8003687c2a2ef3a8516d9109df9962b8f714849323669` under `--headless --quit-after 30`.

Not claimed: a second user account has not run bootstrap. There is no battle, no art, and no audio.

## 2026-10-05 — Fulda skirmish and the roster meshes

`scripts/verify.sh --phase 0` passed again. Report: `build/reports/20261005T023633Z-phase0.md`. Sim line coverage 0.934 (825/883). 40 tests passed.

Blender 5.2.1 wrote a GLB for each of the 103 roster entries. The Fulda skirmish runs: NATO 11 against Pact 11, with move orders and the take-control flag. The tick-30 frame is `docs/shots/fulda-tick30.png`. Armor facing, a hill blocking sight, supply, bingo fuel, and the aggression slider have unit tests. Those requirements are not marked verified. Campaigns, multiplayer, grand strategy, and voiced radio are not in the build.

## 2026-10-05 — Campaign, theater, fog, morale, criticals

`scripts/verify.sh --phase 0` passed. Report: `build/reports/20261005T030547Z-phase0.md`. 54 tests. Fog of war, criticals, and morale bail are covered. The menu can start the Cold War campaign and the 1989 province map. A ceasefire holds the front. Tension can spend the nuclear weapon. Chunks fall when the piece under them is gone. Climbing burns extra fuel. Artillery waits out its delay. The rest of the brief is still open.

## 2026-10-05 — Box silhouettes are rejected

The model script now refuses a mesh that fills its side or top bounding rectangle the way a box does. 183 models passed. Tanks have a glacis, turret, barrel, and road wheels. The Fulda frame was shot again after the rebuild. Aircraft tails still sit slightly off the fuselage. Phase 0 was not re-run after this mesh change.

Performance targets in the brief are for an RTX 3060-class GPU. This machine is faster. Numbers still have to be measured here, and the 3060 target remains the bar.

## 2026-10-08 — Contract v5 adopted, Phase 0 re-gated

Machine (Parish1): Legion Pro 7, i9-14900HX, RTX 4080 Laptop GPU, NVIDIA driver 610.57.04 (proprietary), 54 °C idle. `free -h` shows 31 GiB total today; the 2026-10-04 entry says 62 GiB, and this entry records what was measured now. `/home` has 836 GB free. Hyprland 0.56.2. Claude Code 2.1.293. `vulkaninfo` is still missing until Donovan runs the bootstrap package line. `parish-media status`: AC on, GPU idle.

Session-start `--quick` under the v3 gates was green: `build/reports/20261008T181528Z-quick.md`.

Changes:
- `docs/BRIEF.md` is v5. `REQUIREMENTS.md` is generated from it: 88 IDs, including MEC, ART, REF and DET. SIM-05, SIM-07 and SIM-08 dropped to in-progress (unit tests only; v5 wants G4). D012.
- Blender 5.2.2 LTS is pinned and unpacked to `tools/blender/`, SHA-256 verified (D013).
- macOS universal export added to G1 (D014).
- G14 added to `--quick` and Phase 0, with a self-test. `assets_src/registry.json` lists the 103 meshes as placeholders (D015).
- `CLAUDE.md`, 9 subagents, 12 skills, Stop/Pre/PostToolUse hooks, and the `commit-msg` git hook (D016, D019). Rubrics and the polish checklist are in `docs/`. MCP servers are pinned in `.mcp.json` but not exercised (D017). cua-driver route (D018).

Hook tests, each triggered:
- **guard:** 6 cases, blocks Edit, `sed -i` and redirects into `verify/`; allows reads and the unlock.
- **post-edit:** reformatted a misindented `sim/Order.cs` back to the original; rejected a schema-breaking `catalog.json` and a hand edit of the generated file.
- **Stop:** green run exits 0, cached run 0.016 s, a hand-edited `units.json` exits 2 (G10 FAIL), a `KNOWN-RED:` line exits 0.
- **commit-msg** (scratch repo): rejects no prefix, a missing DECISIONS.md, and mixed product code; accepts its own `VERIFY:` commit.
- A headless `claude -p` session in the repo had its Edit to `verify/thresholds.toml` blocked by the live hook, and the file stayed unchanged. The session init event listed all 9 agents and all 12 skills as loaded.

`scripts/verify.sh --phase 0` passed: `build/reports/20261008T183033Z-phase0.md`. Gates G1 G2 G3 G10 G11 G12 G14, 0 failures. 54 tests, coverage 0.947, golden hashes unchanged, Linux, Windows and macOS exports built.

Not done: bootstrap has not been run on a fresh user account (a Phase 0 exit item; it needs a second account, which Donovan has to create). Nothing is committed: the `verify/ref_coverage.toml` addition needs its own `VERIFY:` commit, and commits wait for Donovan.

Fresh-account proxy: a copy of the tracked tree, with `HOME` set to an empty directory and `PATH=/usr/local/bin:/usr/bin:/bin`. `scripts/bootstrap.sh` exited 0. It downloaded and checksummed the Godot 4.7.2 zip, the 1.11 GB templates and Blender 5.2.2, and installed the .NET SDKs through mise. Then `--quick` passed G1 G2 G3 G10 G14: `build/reports/20261008T183800Z-freshhome-quick.md`. This is a proxy, not a second user account. Summary page: `build/reports/phase-0.html`.

## 2026-10-08 — Donovan: model everything in-house, local audio only

Recorded as D020 (no ElevenLabs; Qwen3-TTS, ACE-Step and faster-whisper run locally) and D021 (Claude authors every mesh, material, camo pattern and animation; no AI mesh generators or downloaded assets). Updated to match: CLAUDE.md, the audio-producer, modeler and fx-animator agents, and the dialogue-batch, sound-design, asset-pipeline, anim-clip and new-unit skills. Licenses are in LEGAL.md: Qwen3-TTS Apache-2.0, ACE-Step MIT, FLUX.2 klein Apache-2.0.

Smoke test:
- `parish-media voice` in design mode rendered "XO has the battle, Commander. Good hunting." in 14.2 s, and the GPU was released afterwards.
- Measured: 4.16 s, −19.1 LUFS (needs normalizing to −16), peak −3.1 dBFS.
- Local faster-whisper transcript: "Exo has the battle, Commander. Good hunting." The voice said "XO"; Whisper spells the letters as a word. `dialogue/aliases.csv` maps it before scoring.
- I have not listened to it. The judgment rests on the transcript and the measurements.

G11 flagged the unlogged smoke WAV until ASSETS.md got the generated-audio entry. `scripts/verify.sh --phase 0` then passed: `build/reports/20261008T184638Z-phase0.md`.

## 2026-10-08 — Packages installed; Vulkan profile

Donovan ran `sudo pacman -S --needed git-lfs uv sox p7zip vulkan-tools mesa-utils cage`. `pacman -T` confirms all present.

`vulkaninfo --summary` lists two devices:
- GPU0: Intel Graphics (RPL-S), Mesa 26.2.2, Vulkan 1.4.354
- GPU1: NVIDIA GeForce RTX 4080 Laptop GPU, driver 610.57.04, Vulkan 1.4.341

Godot must be confirmed on the NVIDIA device before any G6 capture. Check the `Vulkan ... - Using Device` line in its log, and pass `--gpu-index 1` if it picks the iGPU.

## 2026-10-08 — New battle sim; G4 scenarios with mutation checks; SIM-04 and SIM-10 verified

`sim/Battle/` is the new data-driven sim (D022): rules from `data/rules.json`; heightfield terrain with forest, hedge, road, water and rubble layers; line of sight and per-player detection; a 16 m nav grid with deterministic A* per movement class; and chunk-grid buildings with support, collapse, crush and rubble.

G4 runs 18 scenarios in `verify/scenarios/` through `sim/Headless --scenario`, then 13 rule mutants from `verify/mutants.toml`. Each mutant must break the scenarios it names, and all 13 do. A check with `ci_lower_pct` uses an exact integer 95% Wilson bound. No floating point goes into sim/, and the source scan test enforces that.

The scenarios and mutants found real problems. Fixed:
- **Slope:** the nav slope estimate read 40% as 30% (wrong sample span), so trucks drove over a 40% ridge.
- **Speed:** units lost about 5% of their speed at every waypoint (leftover movement dropped).

They also caught four weak scenarios of my own. I corrected each against hand calculations:
- a dead-ground target too close to the fold
- a cross-group spotting leak
- a forest band that could be driven round
- a concrete "survives" blast aimed 200 m away from the building, so it passed vacuously

Evidence: `build/reports/20261008T211739Z-gates-G1-G2-G3-G4-G10-G12-G14.md`, all 7 gates passing, sim coverage above 0.9.

Verified: SIM-04 and SIM-10. Still in-progress:
- SIM-05: the view still uses the old BattleSim.
- SIM-12: formations, air pathing, 8 km performance and the G5 stuck metric are missing.

The roster stats are placeholders (the M1 and T-80 rows are identical). Sourcing them under W1 is open.

## 2026-10-08 — Sourced roster; combat, morale, supply, frontline, economy verified

The roster agent returned 50 Cold War units with per-field sources (`assets_src/research/cw1989_roster_sources.json`). `scripts/generate_roster.py` (D024) produces `data/roster.json` and `data/weapons.json` (47 weapons). Every value that is not sourced is labelled `design` with a reason. The battle sim now runs on this roster, and all scenarios were re-derived for real speeds and optics.

New systems: combat (facing, KE/CE, criticals, HE blast), morale (ladder, HQ, surrender), supply (ammo, fuel, trucks, aircraft turnaround), economy (phases, income, cards, SP), frontline (influence, territory, encirclement), and ROE.

The scenarios and mutants found more real bugs. Fixed:
- **Fuel truncation:** fuel never burned.
- **Seeds:** RNG seed correlation from unmixed small seeds.
- **Income rounding:** DP accrual lost 1–4%.
- **Kill credit:** burn-outs and bail-outs credited no attacker.
- **Flip margin:** a lone squad could never take ground.
- **Air attacks:** fire from aircraft ignored top armour.

Performance on the real roster first missed the budget (p95 11.6 ms). After the targeting, building-walk and square-root fixes it is p95 4.1 ms at 2,000 entities (D023 addendum).

Evidence: `build/reports/20261008T215612Z-gates-G1-G2-G3-G4-G6-G10-G12-G14.md`. 38 scenarios, every mutant killed, all 8 gates pass.

Verified today: SIM-04, SIM-06, SIM-07, SIM-08, SIM-09, SIM-10, SIM-13 and MEC-02, plus the Phase 0 set.

## 2026-10-08 — Command structure, fire missions, soak harness

- **Fire missions (SUP-01, Cold War subset):** setup, time of flight, exact-median CEP dispersion, smoke, illumination, counter-battery fixes.
- **Command structure:** 9 seats per player, task queues (attack, defend, hold, recon, screen), HTN-style planner, intents, support requests with approval, logistics dispatch, artillery servicing requests and counter-battery, XO standing intent with AI deployment, objectives and conquest victory.
- **Soak:** `sim/Headless --soak` and `scripts/analyze_soak.py` (G5). 400 matches in 80 s, deterministic per seed.

Bugs found and fixed:
- **Fuel:** fuel never burned (truncation).
- **Seeds:** RNG seed correlation.
- **Withdrawals:** withdrawn units re-withdrew every plan tick.
- **Detracked vehicles:** they kept their goals, so they showed as stuck.
- **Time cap:** an off-by-one in the soak cap.
- **Map generator:** overlapping villages crashed 2 of 400 matches.

The balance passes failed and were reverted (D026). NATO cross-faction is 37%, at the edge of the band.

Evidence: `build/reports/20261008T222943Z-gates-G1-G2-G3-G4-G5-G6-G10-G12-G14.md`, 9 gates passing. Newly verified: CMD-06, QA-03, MEC-10. Style-board reference packs are done and pass G14 coverage (D025). Privacy incident logged in D025.

## 2026-10-08 — Art pipeline started; M1A1 blockout escalated

Built:
- `scripts/refs/trace_ortho.py`: GrabCut and line-art mask tracing, with background and foreground hints and recorded top, bottom and front cuts (D027).
- `assets_src/procedural/ground_vehicle.py`: the parametric tank generator.
- `scripts/sheets/render_sheets.py`: CS-1 with registered IoU, CS-2 turntable, CS-2b close-ups.
- `scripts/validate_assets.py` (G7, blockout stage) and `scripts/refs/board.py`.

Blockouts:
- **M1A1:** dimensions within 0.2% of sourced. Fresh-context reviews rejected it 4 times (2.56, 3.00, 3.44, 3.00), so it is escalated to Donovan: `build/reports/m1a1-blockout-escalation.html`.
- **M60A3:** side IoU 0.884 on a clean photo mask (passes 0.88). It has no front mask and is not reviewed yet.
- **T-80BV:** dimensions match. It has no clean distributable side or front photo, so the IoU waiver is proposed in the same report.

A-10A masks are traced from its distributable 3-view.

Not done: the other 4 style-board items, materials and camo, and the art bible.

## 2026-10-08 (night) — Aircraft blockouts

Built `assets_src/procedural/aircraft.py`, the parametric fixed-wing generator. It builds:
- a lofted elliptical fuselage from stations
- lens-section surfaces, with vertical fins thick in X
- separately hinged control surfaces
- round or oval nacelles
- pylons, gear, and the M7 empties

The pipeline additions (store erasing, the gear-up flag, the twin-rudder node rule) and the drawing-inconsistency finding are recorded in D028.

**A-10A:**
- Built: 16.26 × 17.52 × 4.46 m, against sourced 16.26 × 17.53 × 4.47.
- Layout comes from the side-mask profile and the plan mask.
- IoU: side 0.935, top 0.945, front 0.638. The front fails because of the drawing (D028).
- Review: rejected in round 1 (2.56); approved in round 2 (4.00, exactly the pass line), in `build/assets_review/a10a/blockout/review_2.md`.
- Its three fixes carry into the detail stage: a proper GAU-8 cluster, a bubble canopy with a windscreen, and the tail underside and inboard pylons.
- Registry state: `blockout`. Front IoU waits on the waiver, so G7 shows it as a FAIL.

**Su-25:**
- Built: 15.53 × 14.34 × 4.79 m, against sourced 15.53 × 14.36 × 4.8.
- IoU: top 0.857, which passes. Front 0.728 on a low-confidence normalised mask, against a 0.77 threshold. There is no orthographic side reference.
- In fresh-context review.

**Su-25 review rounds:**
- Round 1 rejected at 2.56; round 2 at 3.00; round 3 at 3.44.
- The round-1 "blunt nose" was partly a tracing artifact. The trace box cut the drawing's twin nose probes, and the flood fill closed the gap between them. The mask was retraced with the probes.
- Fixes applied across the rounds: profile and canopy, gear stations, nacelles, wing chord trimmed to 33.7 m², sit height raised 0.18 m.
- Round 4 is in review. A 4th rejection escalates to Donovan (section A8).

**West German town block, 1989:**
- `building_kit.py` and the block spec are built: 6 buildings, a Hoftor and courtyard walls, 150 wall chunks, 21k triangles.
- The six buildings: an infill Gaststätte, two half-timbered houses, a postwar rendered house, a side-street house, and a stone tithe barn with a slate mansard.
- G7 kit checks pass: storey 2.8 against 2.75 m, median pitch 48° against 45°, footprints 9 × 12 against 9.3 × 12.9 m.
- The storey height moved to 2.8 m (D029).
- In fresh-context review.

**US rifle squad 1989:**
- `infantry_kit.py` builds the base soldier from ANSUR I (1988) US Army male means. The raw data (n = 1,774) is public domain and recorded in LEGAL.md.
- Body segments are placed by forward kinematics, with analytic two-bone IK for arms to the weapon grips and legs to the foot targets.
- Equipment: PASGT helmet with band and foliage, PASGT vest, LC-2 belt with pouches, canteens, E-tool and suspenders, ALICE pack with bedroll, M16A2 (1.007 m, exact), M16A2/M203, M249, M72A2 LAW, and PRC-77 on a frame with a 3 m whip. Equipment sizes are in `assets_src/research/us_infantry_1989_dims.json`.
- The squad: 9 men plus an attached RTO, in five poses.
- New sheet mode (`--squad`): hero close-ups at 300 px/m, a lineup, and the RTS camera at 150 m and 60 m.
- In review, round 1.
- The town block was approved in round 2 (4.00).
- verify --quick is green after regenerating the registry in generator format.

**Materials and camo:**
- Palettes: `assets_src/research/palettes_1989.json`, 31 colours, 19 sourced and 12 null (D030).
- Textures: `scripts/textures/camo.py` generates tileable camo textures in `assets_src/textures/camo_*.png`.
- Renderer: `scripts/sheets/lookdev.py` box-projects them at physical scale onto the blockouts and renders beauty shots to `build/style_board/*.png`, the first style-board preview.
- The Su-25 renders in placeholder grey until its colours are sourced.

**Review rounds this block:**
- M60A3: round 1 rejected (3.00); the needle-nose turret, drum cupola and bustle basket are fixed.
- T-80BV: round 1 rejected (2.56); the seated ERA, turbine grilles, snorkel, sloped nose with mudguards and round drums are fixed.
- Squad: round 2 rejected (3.00); the helmet shell, trousers, straps and radio are fixed. Four tooling bugs were found and fixed: RTS-tile visibility, straps buried inside the vest, the radio battery box, and the CS-2b scale (D030).
- All three are back in review.

**M60A3:**
- Blockout approved in round 2 (4.00, no margin), in `build/assets_review/m60a3/blockout/review_2.md`.
- Side IoU is 0.889.
- Front has no reference, so it joins the waiver list.
- Detail-stage fixes: one full-width rear exhaust grille, an undercut turret chin and fuller upper nose, the cupola and loader's hatch 0.35 m forward, and the bore evacuator moved aft.

**US rifle squad:**
- Escalated after 4 rejections (3.00, 3.00, 3.00, 3.44), per section A8.
- Round 4 confirmed that body, rifle, radio, pack and LAW measure to source, and found all five signature features present.
- The remaining blocker is the PASGT helmet profile: a level brim, the step at the temples, and a horizontal rear edge at the nape, with no hood effect.
- The reviewer expects a helmet-only fix to reach 4.0.
- Fixed earlier in the review rounds: ground contact for kneeling, walking and prone poses, M203 and LAW visibility, and the RTO radio and frame.

**T-80BV:**
- Blockout approved in round 4 (scores 2.56, 3.11, 3.56, 4.00), in `build/assets_review/t80bv/blockout/review_4.md`.
- Detail-stage fixes: seat the drums on the rear plate with straps, close the sprocket gap by about 0.30 m, and strengthen the turret Kontakt-1 belt and roof V with visible gaps.
- Style board: A-10A, M60A3, T-80BV and the town block are approved at blockout. M1A1, Su-25 and the rifle squad are escalated to Donovan.

## 2026-10-09 — Air commander tasks (CMD-03 verified)

- **New orders.** The air seats now have three orders: CAP (a racetrack orbit), CAS standby with CAS on request, and interdiction (strike spotted movement in a box, supply first).
- **Screen** got its own planner. Posts hold a line with return-fire ROE and fall back 600 m when engaged.
- **CAS behaviour.** Army attacks request CAS from a CAS-tasked air seat, under the same approval flow as fires. One sortie per request, after which the aircraft returns to base. CAS waits while spotted enemy air defence covers the target, and the air seat asks artillery for SEAD.
- **Bugs fixed:**
  - Return-fire ROE let units fire freely for the first 200 ticks of every match.
  - Scenario aircraft without an altitude spawned on the ground, where combat treated them as ground targets.
  - A hovering aircraft burned no fuel and could loiter forever. It now returns after its sortie.
- **Scenarios.** 5 new G4 scenarios and 4 mutants under D031: `cmd_cas`, `cmd_cas_approval`, `cmd_cap`, `cmd_interdict`, `cmd_screen`.
- **Verified:** G2, G4 (52 scenarios, every mutant killed) and G5 in `build/reports/20261009T014502Z-gates-G2-G4-G5.md`; quick gates in `20261009T014900Z-quick.md`; G6 in `20261009T015012Z-gates-G6.md`.
- **CMD-03 is now verified.**
- **Open (W6).** Automatic CAS from every AI attack swung cross-faction balance to NATO 98%, and the mechanism is not yet understood (D031). AI matches therefore fly CAS only when tasked, and G5 is back in band at NATO 0.365.

## 2026-10-09 — CMD-04 and CMD-05 verified

- **CMD-05:** approve, deny and reprioritise are proven by `cmd_request_decisions` (D032). Scenario decisions take a priority.
- **CMD-04:** priority targets are implemented as a seat intent with a 3× target weight. Statistical A/B scenarios over 50 seeds cover tolerance and priority, with non-overlapping Wilson intervals (D033).
- **Verified:** `build/reports/20261009T015223Z-gates-G2-G4.md` and `20261009T015844Z-gates-G2-G4.md`.
- **Status:** 19 requirements verified. CMD-01 and CMD-02 remain open on UI (G13) and the XO.

## 2026-10-09 — Flight (FLY-02, FLY-03 verified; FLY-01 sim side)

- **Take Control and return.** `sim/Battle/Flight.cs` brings Take Control to the battle sim, with the XO taking command within 1 tick. Control returns on Bingo, Winchester, ejection, landing, or manual release, and the AI pilot resumes.
- **Flight model.** An integer arcade-sim flight model derived from sourced envelopes (`assets_src/research/flight_envelopes_1989.json`, compiled by the ref-hunter) covers speed, climb, the G-limited turn and stall.
- **Scenarios.** 9 new G4 scenarios and 3 envelope mutants (D034).
- **Bug fixed:** an `int` overflow zeroed the turn rate.
- **Verified:** G2, G4 (64 scenarios), G10 (`build/reports/20261009T021313Z-gates-G2-G4-G10.md`, `20261009T022129Z-gates-G4.md`), and G5 and G6 (`20261009T022742Z-gates-G5-G6.md`; balance unchanged at NATO 0.365).
- **Status:** 21 requirements verified.

## 2026-10-09 — MEC-04 verified (cover, wrecks and craters, veterancy, retreat)

- **Built:**
  - directional cover tiers in hit chance
  - wrecks and craters as cover
  - veterancy earned by kills
  - the retreat order, with reinforcement that keeps veterancy (D035)
- **Field note: soak crash.**
  - **Symptom:** G5 crashed 391 of 400 matches with "Collection was modified".
  - **Cause:** I put the per-tick wreck list in a static field, and the soak runs matches in parallel.
  - **Fix:** the cache lives on `WorldState`.
  - **Check that proves it:** G5 shows 0 crashes in 400. The new ratchet test `Sim_sources_declare_no_static_mutable_collections` fails on any static mutable collection in `sim/`.
- **Verified:** G2, G4 (66 scenarios, every mutant killed) and G5 (cross-faction NATO 0.350, exactly at the band edge) in `build/reports/20261009T024708Z-gates-G2-G4-G5.md`; G6 p95 4.6 ms in `20261009T024548Z-gates-G5-G6.md`.
- **Status:** 22 requirements verified.

## 2026-10-09 — MEC-05 verified (battle planner)

- **Built:** spearhead and garrison planner orders, front and offensive-arrow aliases, and preparation with its micromanagement penalty and hit-chance bonus (D036).
- **Verified:** all gates in `build/reports/20261009T030415Z-gates-G2-G4-G5-G6-G10.md`. G4 runs 69 scenarios with every mutant killed. G5 cross-faction NATO is now 0.400, away from the band edge.
- **Status:** 23 requirements verified.

## 2026-10-09 03:40Z: MEC-03 verified; SUP-02 and SUP-05 Cold War slice (D037)

- **Verified:** MEC-03.
  - `build/reports/20261009T032437Z-gates-G2-G4-G10.md`: 75 scenarios and all mutants killed.
  - `build/reports/20261009T033601Z-gates-G5-G6.md`: soak balance band and p95 still pass.
- **Added:**
  - an off-map support catalogue (`generate_support.py`, 20 options), `Support.cs` and a spot-assist SP path within a side;
  - six scenarios and four mutants;
  - mutant runner support for `support:` keys.
- **In progress:** SUP-02 and SUP-05. The Cold War slice is in; the other eras and the air options that spawn units are open.
- **Field note.** Symptom: charges acted like a cooldown. Cause: a recharge-timer refusal (reason 4) ran even when charges remained. Fix: each spent charge recharges on its own timer. Check: `sup_offmap_economy`, where a second call with a charge left succeeds.
- **Field note.** Symptom: a 90° strike heading ran north. Cause: the sim's heading convention is 0 = east, counter-clockwise. Fix: the data uses 0 for NATO and 180 for Pact. Check: `sup_air_strikes` hits along the expected line.

## 2026-10-09 03:50Z: MEC-01 verified (D038)

- **Report:** `build/reports/20261009T034213Z-gates-G2-G4-G10.md`. The G5 soak was re-run after the soak deck lookup changed.
- **Decks:** 8 shipped, one per income profile per side, with a G10 validator and its self-test.

## 2026-10-09 04:10Z: MEC-08 verified (D039)

- **Report:** `build/reports/20261009T035618Z-gates-G2-G4-G5-G6-G10.md`.
- **New:** the aggregate scenario check `hit_rate_parity_pct`; Fired events now record the shots actually taken; the rule `direct_control_hit_pct = 0`.
- **Field note.** Symptom: a +15% hit-chance buff survived the ±10% parity check. Cause: close-range shots clamp at 100%, so the effective buff is about 8%. Fix: the mutants use ±25%, and D039 records the detection floor. Check: both mutants are killed in G4.

## 2026-10-09 05:05Z: MEC-09 work (D040). G5 KNOWN-RED.

- **Passing:**
  - G2, G4, G6 and G10: `build/reports/20261009T043753Z-gates-G2-G4-G6-G10.md`.
  - G5 MEC-09 checks: AA ratio worst 0.93; single-class tank 0.00, IFV 0.41, infantry 0.075.
- **G5 red:** `build/reports/20261009T044849Z-gates-G5.md`.
  - CMD-06 fire support before assault: 0.673, need 0.70. It was 0.70+ before D040.
  - mirror_nato: player 0 wins 0.37, band 0.40–0.60.
- **Field note: the soak AI never deployed phase-B or phase-C cards.**
  - Symptom: no tanks, A-10s or BMP-2s in any soak match.
  - Cause: the AI bought the cheapest affordable card the moment it could, so DP never reached 265.
  - Fix: save for the most-needed card.
  - Check: per-unit soak tallies show M1A1s and T-80BVs deployed.
- **Field note: guns never moved.**
  - Symptom: guns died at spawn, and a battery that was relocating never fired.
  - Causes: counter-battery fixes on a static battery; `Fires.Step` skips any unit that moved this tick.
  - Fix: shoot and scoot, and a fire order halts the battery.
  - Check: assaults with no gun alive fell from 86 to 29 per 24 matches.
- **Field note: the AI tuning is tightly coupled.**
  - Symptom: one-line AI rule changes swing cross-faction NATO between 0.44 and 0.78, and IFV-only between 0.41 and 0.84.
  - Lesson: measure each change with the full G5 battery (640 matches), never 12-match samples.

## 2026-10-09 06:40Z: G5 back to one red check (D040 final)

- **Report:** `build/reports/20261009T050637Z-gates-G2-G4-G5-G6-G10.md`. G2, G4, G6 and G10 pass.
- **G5:** every check passes except MEC-09 AA efficiency, where no AA was deployed in 640 matches. The check now requires a real sample, so it can't pass vacuously.
- **CMD-06:** re-verified at 0.755.
- **Field note: the AA check passed vacuously.**
  - Symptom: "worst - 0.00" with an empty AA list.
  - Cause: the AI never bought AA, because no enemy aircraft were ever visible.
  - Fix: the analyzer requires at least 8,000 points of AA deployed.
  - Check: the AA line now fails with "AA value deployed 0".

## 2026-10-09 07:15Z: MEC-09 verified; all gates green

- **Report:** `build/reports/20261009T052635Z-gates-G2-G4-G5-G6-G10.md`: G2, G4, G5, G6 and G10 pass.
- **MEC-09:** the AA ratio (worst 0.47) and the single-class decks both pass. The AA sample comes partly from the new 80-match maverick air-war run.
- **CMD-06:** re-verified at 0.752.
- KNOWN-RED cleared.

## 2026-10-09 07:30Z: MEC-06 Field Requests, sim half (D041)

- **Report:** `build/reports/20261009T054551Z-gates-G2-G4-G10.md`: 2 scenarios, 2 mutants killed, G10 drift check added.
- **Still open:** the G13 offer panel in the Godot view.

## 2026-10-09 08:05Z: ERA-04 verified; MEC-06 sim half; gates green

- **ERA-04:** `build/reports/20261009T055803Z-gates-G2-G10.md`.
  - New `scripts/check_era.py` in G10.
  - Weapons carry `era`, `year` and `year_note`.
- **G5 still passes** with Field Requests in the build: `build/reports/20261009T055650Z-gates-G5-G6.log` (all checks pass).
- **G6 passes:** `build/reports/20261009T060052Z-gates-G6.md`.
- **Field note: overlapping verify runs.**
  - Symptom: a G5,G6 run stopped after G5 with no G6 line.
  - Cause: a second `verify.sh` (G2,G10) started while it was running and shared the build output.
  - Fix: re-ran G6 alone. Never start two verify runs at once.
  - Check: `build/reports/20261009T060052Z-gates-G6.md` passes.

## 2026-10-09 08:25Z: SUP-03 part 1 (D042)

- **Reports:** `build/reports/20261009T060900Z-gates-G2-G4-G10.md`, `build/reports/20261009T062038Z-gates-G5.md`, `build/reports/20261009T062209Z-gates-G6.md`. All pass.
- **Added:** field repair, obstacles, trenches, minefields and scatterable mines.
- **Field note: scenario teams.**
  - Symptom: a BTR struck its own side's minefield.
  - Cause: scenario players never set `Team`, so everyone was team 0.
  - Fix: players default to team `player & 1`.
  - Check: `sup_engineering` (own-lane safety) passes, and all 81 existing scenarios still pass.

## 2026-10-09 08:45Z: SUP-03 verified (D042, D043)

- **Reports:**
  - `build/reports/20261009T062755Z-gates-G2-G4-G10.md`
  - `build/reports/20261009T064020Z-gates-G5.md`
  - `build/reports/20261009T064153Z-gates-G6.md`
- **Added:** engineer support (bridge, fortify, ditch, breach), ammunition dumps, supply and medevac helicopters.
- **Fixed:** an infantry unit's `hp_pct` now sets its member count.

## 2026-10-09 09:05Z: SUP-04 Cold War EW (D044)

- **Reports:**
  - `build/reports/20261009T064722Z-gates-G2-G4-G10.md`
  - `build/reports/20261009T070017Z-gates-G5.md`
  - `build/reports/20261009T070147Z-gates-G6.md`
  - All pass.
- **Added:** radar jamming, comms jamming and decoys.
- **Open:** GPS degradation, which needs the modern era.

## 2026-10-09 09:20Z: SIM-11 verified (D045)

- **Reports:** `build/reports/20261009T071538Z-gates-G4.md`; G2, G10, G5 and G6 also pass.
- **Added:** weather and darkness modifiers to accuracy, fog grounds aircraft, and thermal sights now count.
- **Field note: thermal model change.**
  - Symptom: `los_fog_weather` failed once thermal sights worked.
  - Cause: its observer was a thermal M1A1.
  - Fix: swapped in a non-thermal observer and added a thermal assertion.
  - Check: G4 passes with both.

## 2026-10-09 09:35Z: SIM-12 verified (D046)

- **Report:** `build/reports/20261009T073047Z-gates-G4.md`, plus the G5 stuck metric and G6 8 km pathing.

## 2026-10-09 10:05Z: MEC-07 and MEC-12 verified (D047)

- **Reports:** `build/reports/20261009T074518Z-gates-G2-G4-G10.md`; G5 and G6 also pass.
- **Added:** multi-phase operations on a persistent world, time of day, an event deck, and outcome-driven map state.

## 2026-10-09 10:40Z: Grand strategy (D048)

- **Verified:** GS-02, GS-03, GS-05, MEC-13 and CMD-08.
- **In progress:** GS-01 and GS-04, both with their sim parts in.
- **Reports:**
  - `build/reports/20261009T081611Z-gates-G2-G4-G10.md`
  - `build/reports/20261009T083255Z-gates-G5.md`
  - `build/reports/20261009T083429Z-gates-G6.md`
- **Known issue:** NATO wins about 90% of Cold War grand-strategy runs.

## 2026-10-09 11:05Z: GS-04 tactical card; GS-01 supply (D049)

- **Report:** `build/reports/20261009T084258Z-gates-G2-G4-G10.md`; G5 and G6 also pass.
- **Status:** GS-01 and GS-04 are complete on the sim side. They wait on G13 and G8, which need the Godot view.

## 2026-10-09 11:35Z: NET-02 and FLY-07 verified; NET-01 lockstep core (D050)

- **Report:** `build/reports/20261009T091909Z-gates-G2-G4-G10.md`. 94 mutants killed.
- **Speedup:** G4 runs mutants against their target scenarios only, so the whole run, network scenarios included, takes about 10 minutes.

## 2026-10-09 12:00Z: FLY-06 sim half (D051)

- **Report:** `build/reports/20261009T093725Z-gates-G2-G4-G10.md`; G5 and G6 pass with the variant cards in the decks.
- **Field note: Take Control and the XO.**
  - Symptom: AA died before it could fire in a threat-warning test.
  - Cause: Take Control activates the XO, whose seat ROE overrides a scenario's hold-fire.
  - Fix: the scenario keeps the other aircraft out of reach.
  - Check: `fly_threat_warnings` passes.

## 2026-10-09 12:30Z: SUP-01 naval gunfire and precision rounds (D052)

- **Report:** `build/reports/20261009T095447Z-gates-G2-G4-G10.md`; G5 and G6 pass.

## 2026-10-09 13:00Z: SUP-02 Cold War complete except WW2 gliders (D053)

- **Report:** `build/reports/20261009T101334Z-gates-G2-G4-G10.md`; G5 and G6 pass.
- **Next blocker for SUP-01/02/05, ERA-01, FLY-06 and the other eras:** WW2 and modern roster research, with sources.

## 2026-10-09 13:55Z: WW2 1944 slice (D054)

- **Report:** `build/reports/20261009T104107Z-gates-G2-G4-G10.md`; G5 and G6 pass.
- **Added:** 32 sourced WW2 units (US and German), 30 weapons with sourced penetration tables, 8 WW2 decks and 8 WW2 support options.
- **Field note: WW2 penetration conventions.**
  - Symptom: WW2 penetration figures mix 30° and normal-impact conventions.
  - Fix: a per-range conversion from the KwK 43 article, which tabulates both.
  - Check: `ww2_armor_and_support` — the Sherman fails against the Panther's front and kills from the side.

## 2026-10-09 14:40Z: Modern 2026 slice (D055); SUP-04 verified

- **Report:** `build/reports/20261009T110529Z-gates-G2-G4-G10.md`; G5 and G6 pass.
- **Added:** 24 sourced modern units, top-attack missiles, loitering munitions, FPV strikes, and GPS jamming against satellite-guided rounds.

## 2026-10-09 14:55Z: SUP-02 and SUP-05 verified (D056)

- **Report:** `build/reports/20261009T112335Z-gates-G2-G4-G10.md`.

## 2026-10-09 13:30Z: Remaining ERA-01 factions (D057); gate hole fixed (D058)

- **Added:**
  - WW2 British and Red Army units, with their own recon (Daimler, T-70, BA-64B);
  - Cold War West Germany, UK, France, East Germany and Poland (38 sourced units and 16 nation variants of shared kit);
  - China (11 sourced units).
  - 56 decks. Each names its era and nation, and every faction ships all four income profiles (G10 `check_decks`).
- **Faction soak.** 80 matches per pair on fresh seeds. Eleven of twelve pairs fall in 35–65%. The Red Army 1944 deck is 20/80 against the Wehrmacht; it's open, and D057 has the analysis. Numbers are in D057.
- **Cost model.**
  - The US 1989 thermal premium no longer applies to other NATO armies or the modern era.
  - Modern costs ×0.75, plus per-nation scales set from the soak.
  - No artillery card holds more than 4 guns.
  - G5 decks are unchanged; checked by diffing the JSON.
- **Field note: G2 passed with 17 failing tests.**
  - Symptom: `verify.sh` printed "G2: pass" over a `dotnet test` failure.
  - Cause: gates ran inside `if "$@"`, which disables errexit, so only the last command (the coverage check) counted.
  - Fix: `( set -e; gate )` as a plain statement (D058).
  - Check: a scratch script whose gate fails mid-function now returns 1. The 17 failures are fixed: GS scenarios get their own test theory, net scenarios stay in G4 with a short lockstep unit test, and a comment containing "double" was reworded.
- **Field note: AI armor duels in G4 scenarios.**
  - Symptom: a surviving mutant.
  - Cause: a tank that fires at anything in range engages the AI armies or the next duel pair, and then shows its side or never spots its intended target.
  - Fix: keep duel pairs 4+ km apart and AI spawns at the far map edge.
  - Check: `challenger_thin_front` now fails `era_factions_coldwar` on 3/3 seeds.

## 2026-10-09 13:40Z: ERA-01 verified

- **Reports:**
  - G2/G4/G10: `build/reports/20261009T132337Z-gates-G2-G4-G10.md`
  - G5: `build/reports/20261009T133606Z-gates-G5.md`
  - G6: `build/reports/20261009T133744Z-gates-G6.md`
- **Source audit.** Three Haiku agents checked every sourced number added this block against its cited page: about 120 fields matched. Each flagged item was re-checked by hand:
  - Cromwell IV speed: 64 → 51 km/h (later final drive).
  - Churchill VII speed: 20.4 → 21.7 km/h (specifications box).
  - EQ2102 crew: 2 → 1.
  - Seven values not on the page were re-marked as design estimates.
  - Two flags were false positives: the Leopard 2 road range, and the Warrior's speed, which its own page gives as both 74 and 75 km/h.

## 2026-10-09 14:10Z: Red Army tank riders (D059); every faction pair in band

- **Reports:**
  - G2/G4/G10: `build/reports/20261009T135132Z-gates-G2-G4-G10.md`
  - G5: `build/reports/20261009T140425Z-gates-G5.md`
  - G6: `build/reports/20261009T140603Z-gates-G6.md`
- **Change.** Soviet 1944 rifle squads ride the T-34-85, and the Soviet WW2 cost scale is 1.15. Result against the Wehrmacht: 75/160 over two fresh seed sets.

## 2026-10-09 15:15Z: G13 built; the game runs the battle sim (D060, D061)

- **Reports:**
  - Full set: `build/reports/20261009T145517Z-gates-G1-G2-G3-G4-G10-G11-G12-G13-G14.md`
  - G5: `20261009T150851Z-gates-G5.md`
  - G6: `20261009T151029Z-gates-G6.md`
  - G12/G13: `20261009T151224Z-gates-G12-G13.md`
- **Verified:** FLY-01, FLY-04, MEC-06, MEC-11 (placeholder glyph icons), UX-01, UX-02.
- **Progress:** MODE-01, MODE-02 and NET-01 gained G13 evidence and stay in progress.
- **Visuals:** placeholder boxes, labeled in-game.
- **Field note: Godot export exits 0 when the .NET publish fails.**
  - Symptom: the exported game ran old code.
  - Cause: a shell without mise had no `dotnet`, so the publish failed, but the export still wrote the .pck and left the previous DLL in place.
  - Fix: G13 exports in `verify.sh` (mise active) and fails on any `ERROR:` in the export log.
  - Check: a deliberate compile error made G13 fail with "Godot export for G13 reported errors".

## 2026-10-09 15:25Z: SIM-05, CMD-01, CMD-02 verified in G13 (D062)

- **Report:** `build/reports/20261009T152423Z-gates-G12-G13.md` (11 scripts).
- **Field note: headless Godot is 64×64.**
  - Symptom: a recorded click on a unit pressed the HUD's Take Control button.
  - Cause: the headless window is 64×64 and ignores `--resolution`.
  - Fix: under `--ux`, viewport stretch at 1280×720, with input mapped through the stretch transform.
  - Check: `ux_11` selects the unit and moves it.

## 2026-10-09 15:50Z: GS-01 verified (D063)

- **Reports:**
  - Full set: `build/reports/20261009T153315Z-gates-G1-G2-G3-G4-G10-G11-G12-G13-G14.md`
  - G5: `20261009T154634Z-gates-G5.md`
  - G6: `20261009T154812Z-gates-G6.md`
- **Change.** The strategy screen runs the GS sim, and G13 `ux_12` drives orders, end turn, and save/load (identical hash).

## 2026-10-09 16:20Z: MODE-01 verified (D064)

- **Reports:**
  - Full set: `build/reports/20261009T155817Z-gates-G1-G2-G3-G4-G10-G11-G12-G13-G14.md`
  - G5: `20261009T161241Z-gates-G5.md`
  - G6: `20261009T161452Z-gates-G6.md`
  - G2/G12/G13 after the whitespace fix: `20261009T161536Z-gates-G2-G12-G13.md`
- **Added:** skirmish 1v1 to 5v5 with points, income, victory type (annihilation is new), time of day and weather; a 5v5 AI soak in G5.

## 2026-10-09 18:05Z: MODE-02 and AUD-01 verified; G9 built (D065, D066)

- **Reports:**
  - Full set: `build/reports/20261009T174426Z-gates-G1-G2-G3-G4-G9-G10-G11-G12-G13-G14.md`
  - After the whitespace fix: `20261009T180228Z-gates-G2-G12-G13.md`
  - G5: `20261009T175923Z-gates-G5.md`
  - G6: `20261009T180135Z-gates-G6.md`
- **Voice lines.** 150 generated locally on Parish1: 33 briefings and 117 radio lines. All are at −16 LUFS, and all meet ≥ 95% transcription accuracy after at most 2 retries. None has had a human listen.
- **5v5 balance.** NATO won 29/80, inside the 35–65% band at its edge (95% interval 26–47%). Costs are unchanged.
- **Field note: Godot imports .csv files as translation tables.**
  - Symptom: `lines.csv` was empty in the export, and the battle crashed with IndexOutOfRange.
  - Fix: ship the matrix as JSON.
  - Check: `ux_14_radio` passes.
- **Field note: radio pacing.**
  - Symptom: in a headless build the audio player never reports a stream's end, and under time compression real-time pacing never drained the queue.
  - Fix: pace lines in game time.
  - Check: `ux_14_radio` plays 7 voiced reports.

## 2026-10-09 19:10Z: AUD-02 and AUD-03 verified (D067, D068)

- **Reports:**
  - Full set: `build/reports/20261009T185116Z-gates-G1-G2-G3-G4-G9-G10-G11-G12-G13-G14.md`
  - G5: `20261009T190735Z-gates-G5.md`
  - G6: `20261009T190947Z-gates-G6.md`
- **Added:**
  - 9 music stems from local ACE-Step: −18 LUFS, searched loop points, seam correlation ≥ 0.96;
  - 51 procedural SFX;
  - 3D sound with distance bands and line-of-sight occlusion;
  - music layers driven by battle intensity.
- **Not human-checked:** no audio has had a human listen. Donovan should spot-check.
- **Field note: faster-whisper hallucinates on instrumental audio.**
  - Symptom: "Thanks for watching!" transcribed from music.
  - Fix: the no-vocals check ignores these stock phrases and allows at most 4 other words.
  - Check: G9 passes all 9 stems; it's a heuristic, recorded in D067.

## 2026-10-09 19:55Z: SUP-01 and UX-03 verified (D069, D070)

- **Reports:**
  - Full set: `build/reports/20261009T193344Z-gates-G1-G2-G3-G4-G9-G10-G11-G12-G13-G14.md`
  - G5: `20261009T195040Z-gates-G5.md`
  - G6: `20261009T195254Z-gates-G6.md`
- **Added:** CEP and delay scenarios for WW2 and modern artillery; three tutorials; support and fire-mission controls in the battle.
- **Field note: the HUD card bar blocked map clicks.**
  - Symptom: a tutorial's target click never reached the battle.
  - Cause: four rows of card panels across the middle of the screen; containers caught the clicks.
  - Fix: one scrolling row at the bottom, with layout containers ignoring the mouse.
  - Check: `ux_17_tutorials` passes, and all 19 G13 scripts pass on the new layout.

## 2026-10-09 21:05Z: DET-08 verified; sim spotting 3x faster (D071, D072)

- **Reports:**
  - Full set: `build/reports/20261009T205008Z-gates-G1-G2-G3-G4-G9-G10-G11-G12-G13-G14.md`
  - G5: `20261009T210319Z-gates-G5.md`
  - G6: `20261009T210439Z-gates-G6.md`
- **Field note: G6 tick budget at the edge.**
  - Symptom: tick p95 8.15 ms against the 8.0 limit.
  - Cause: `HasLine` did three divisions plus a smoke-list scan per 8 m sample.
  - Fix: one cell lookup per sample, and the smoke scan only when smoke is active.
  - Check: bench hash unchanged (`c282297aea0879ab`), tick p95 3.1 ms, ratchet limit now 5.0 ms.
