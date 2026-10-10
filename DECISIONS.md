# Decisions

## D001 — Godot 4.7.2 stable .NET

Pinned in `tools/versions.env`. Editor zip SHA-512 matches the official `SHA512-SUMS.txt` from the 4.7.2-stable GitHub release (published 2026-08-18). `tools/godot/godot --version` prints `4.7.2.stable.mono.official.ed1daf0bf`.

## D002 — .NET SDK 10.0.401, projects target net8.0

The machine's Arch package is a .NET 10 runtime without an SDK. SDK `10.0.11` is not a published SDK version. The 10.0.11 runtime corresponds to SDK `10.0.111`; the newest 10.0 SDK on 2026-10-04 is `10.0.401`.

GodotSharp 4.7.2 declares `net8.0`. A net10 library cannot be referenced by that game project, so `sim/`, `sim/Headless/`, `sim.Tests/`, and `game/` target `net8.0`. SDK `8.0.425` is installed into the same dotnet root so the net8 reference pack exists. `global.json` still selects SDK 10.0.401.

## D003 — Plain arrays, not an ECS, for Phase 0

`MatchState` is a class holding an `EntityState` array. No third-party ECS. Revisit with a benchmark before SIM-13. The Phase 0 world is dozens of entities, so an ECS would not change the result that the gates measure.

## D004 — Q48.16 fixed point

`Fp` stores a `long` with 16 fractional bits. Multiply and divide use `Int128` and checked casts. Overflow throws. There is no binary floating-point type in `sim/`.

## D005 — Lockstep in Phase 0 is in-process

G3 runs eight independent simulations on the same seed and orders and requires equal hashes. Steam, ENet, and delay belong to Phase 4 (NET-01). This does not claim multiplayer works.

## D006 — Tool pins live in the repo

`mise.toml` pins the SDK and uv for this project. The brief's `mise use -g` would change the user account's global tools. The repo pin is enough, and bootstrap does not edit shell or desktop config.

## D007 — First golden hashes

`verify/golden/determinism.txt` is the first baseline for this sim. It does not replace an earlier hash.

## D009 — Godot export needs a classic solution beside the game

Godot 4.7.2 looks for `game/SixActual.sln` and exits the C# export if it only finds the repo `SixActual.slnx`. The game directory holds a classic solution. The repo root keeps `SixActual.slnx` for the sim, tests, and analyzers. `dotnet` 10 creates `slnx` by default.

## D011 — A box silhouette is a failed model

`scripts/build_models.py` grades every mesh before it writes the GLB. It rasterizes the side and top views and rejects the model when the silhouette fills its own bounding rectangle the way a box does. Tanks have to show a glacis, a barrel, and road wheels. Aircraft have to show a lofted fuselage and wings. The check is in the build, so a later edit that collapses a mesh back into a crate fails the Blender run. The meshes are still low poly. They are not photo-matched, and aircraft tails sit a little proud of the fuselage.

## D010 — Roster stats and meshes are game-scale, not a historical table

`data/units.json` holds 103 units named in the brief. Lengths are public-approximate meters. Armor, penetration, and speed are game-scale integers so the sim can stay fixed-point. They are not a claimed reference table. `scripts/build_models.py` builds a Blender mesh per unit from those dimensions: hull, turret, gun, and wheels, or a fuselage and wings, or a person. Silhouette checks against photos are not done. A destroyed GLB is written for vehicles and aircraft.

## D008 — Infantry counting is undecided

SIM-13 allows squad members or squads as entities. No choice yet. The performance target is not in Phase 0.

## D012 — Contract v5 replaces v3; Phase 0 reopened

`docs/BRIEF.md` is now contract v5, copied verbatim from Donovan's 2026-10-08 message. `REQUIREMENTS.md` is generated from its section F by `scripts/seed_requirements.py`, which keeps existing status and evidence. G10 runs `--check`, so a missing ID fails.

SIM-05, SIM-07 and SIM-08 drop from `verified` to `in-progress`. Under v5 their verifier is G4, and their only evidence is a unit test. This tightens the status. No check was weakened.

## D013 — Blender 5.2.2 LTS from the official tarball

`tools/versions.env` pins `blender-5.2.2-linux-x64.tar.xz`, SHA-256 `84098912…a168`, from download.blender.org (checksum file `blender-5.2.2.sha256`). Bootstrap unpacks it to `tools/blender/`. The Arch `blender` package (5.2.1) is out of the bootstrap list, so `yay -Syu` cannot change asset builds. 5.2.2 is the newest 5.2 LTS patch on 2026-10-08. The 103 placeholder GLBs were built with 5.2.1 and are rebuilt on the next `build_models.py` run.

## D014 — macOS export: universal, ad-hoc signed, ETC2/ASTC on

The `macOS` preset exports `build/macos/SixActual.zip` as universal (arm64 and x86_64). `codesign/codesign=1` is Godot's built-in ad-hoc signing; the 4.7.2 doc dump leaves the enum blank, and the export produced `_CodeSignature/CodeResources`. Apple Silicon refuses fully unsigned arm64 binaries, so the build is ad-hoc signed but not notarized. Godot refused a universal export until `rendering/textures/vram_compression/import_etc2_astc=true`, so that is on. Untested on a Mac.

## D015 — G14 design and the placeholder state

`verify/ref_coverage.toml` encodes §R3 per class, with `helicopter` added. `scripts/refs/check_coverage.py` gates every registry item past the `refs` stage. It also scans `game/`, `assets_src/**/exports/` and `build/reports/` for look-only files: an exact byte copy, or text naming a real `refs_private/` file. Messages never name the private file, because reports are scanned on the next run. A `--selftest` builds 7 synthetic trees, and each bad tree must fail for its own reason. `assets_src/registry.json` is generated from `data/units.json`. The 103 Phase 0 meshes are `placeholder` / route `legacy-box-gate`. They have no reference packs and are not ART-01 assets.

## D016 — Hook mechanics

- **Stop:** `.claude/hooks/stop_verify.sh` runs `verify.sh --quick`. It skips the run when a fingerprint (HEAD, status, diff, untracked file hashes) matches the last green run. The "known blocker" escape is a line starting `KNOWN-RED:` in `docs/STATE.md`.
- **verify/ guard:** the PreToolUse hook blocks Edit/Write into `verify/` and blocks Bash segments that both name `verify/` and look like a write. The unlock is `.claude/verify-unlock` (gitignored), which holds the reason and decision id. The hard backstop is `scripts/githooks/commit-msg` (`core.hooksPath`). It requires the `VERIFY:` prefix, a staged DECISIONS.md, and nothing else staged. The contract says "pre-commit check", but a pre-commit hook cannot read the message, so this uses commit-msg.
- **PostToolUse:** `dotnet format --include` on C#. Validators plus the schema check on `data/` JSON. Hand edits of generated `units.json` and `catalog.json` are rejected.

All three were tested by triggering them (PROGRESS 2026-10-08).

## D017 — MCP servers pinned at project scope

`.mcp.json` pins `@beremaran/godot-agent-loop@3.0.1` (MIT, targets Godot 4.7; the npm scope differs from the brief's bare name) and `mcp-for-blender==2.1.9` (PyPI, the renamed ahujasid blender-mcp). Every pipeline step stays a CLI script, so CI never depends on them. They are not exercised yet. Godot children get a sanitized environment, so a .NET run through the MCP may need `GODOT_MCP_CHILD_ENV_ALLOW` for the mise PATH.

## D018 — GUI automation route

cua-driver 0.34.0 `health_report` on Parish1 passes the AT-SPI and screen-capture checks through XWayland. Its Wayland backend is experimental and opt-in (`CUA_DRIVER_RS_ENABLE_WAYLAND=1`), and is not enabled. GUI checks use cua-driver against Godot run with `--display-driver x11`. Fallback is grim plus wtype. Headless paths come first.

## D019 — Twelve project skills

§A5 lists ten skills. W2 and W6 name `new-map` and `balance-pass`, so both exist too. W5's `vfx-pass` is the same procedure as `vfx-effect`. Skills that depend on unbuilt scripts say so, and name the requirement that builds them.

## D020 — Donovan: no ElevenLabs; audio comes from local models on Parish1 (2026-10-08)

Donovan's direction overrides §12, §D7 production step 1, §W3 and §W4. No ElevenLabs call of any kind: no speech, SFX, music, voice design or transcription, through the MCP or the SDK.

| Need | Local route | License |
|---|---|---|
| Dialogue, callouts, commander voices | Qwen3-TTS 12Hz 1.7B through `~/.local/bin/parish-media voice`. `design` mode casts each recurring speaker from a written description; `custom` mode (Ryan, Aiden) is the fallback. Max 1500 characters per call. | Apache-2.0 |
| Music stems (calm, tension, combat per era) | ACE-Step 1.5 through `parish-media music` | MIT |
| Sound effects | Procedural synthesis and layering in `scripts/audio/` (numpy, sox, ffmpeg), plus CC0 recordings logged in ASSETS.md. No local SFX model is installed. | own work / CC0 |
| Transcription round trip | faster-whisper 1.2.1 `small.en`, CPU int8, in `tools/asr/` (project venv), so it never takes the GPU lock | MIT (model weights MIT) |
| Concept and portrait images | FLUX.2 klein 4B through `parish-media image`, only for 2D (commander portraits, briefing art, UI). Never as 3D models or traced into meshes. | Apache-2.0 |

Rules:
- Every GPU job goes through `parish-media` (job lock, AC and busy guards). Check `parish-media status` first. Never kill a process to free the GPU.
- Record every generation in `audio/manifest.json`: tool, model, mode, speaker or instruct text, seed, and the receipt path.
- No credit budget applies. GPU time on Parish1 is the cost; batches run when the GPU is idle and Donovan is not gaming.
- The transcription check maps spoken spellings of callsigns and acronyms (XO → "exo"/"ex-o", HQ, SEAD) through a table in `dialogue/aliases.csv` before scoring. The 95% threshold is unchanged.

Smoke test, 2026-10-08:
- `design` mode, line "XO has the battle, Commander. Good hunting." in 14.2 s; GPU released afterwards (job `20261008-144351-voice-f03841`).
- 4.16 s, 24 kHz mono, −19.1 LUFS, peak −3.1 dBFS. Needs normalizing to −16.
- Local transcript: "Exo has the battle, Commander. Good hunting."

## D021 — Donovan: Claude models every 3D asset itself (2026-10-08)

This overrides §M3 routes G and L for meshes. Every mesh in the game is authored by Claude in scripted Blender 5.2.2: route P (procedural generators), or hand-written bmesh scripts for parts generators don't cover (stowage, sandbags, crew, rubble, foliage, props). There is no Meshy, Tripo, Hunyuan3D or other AI mesh generation, and no downloaded meshes from Poly Haven, Kenney, Quaternius, Fab, Sketchfab or similar. Route H becomes "procedural + scripted detail".

Materials and textures are authored too: procedural Blender shaders baked to the §M7 texture sets, and camo patterns generated by script. Skies come from Godot's procedural sky. CC0 photo textures and HDRIs are not used for now. That is the strict reading of "model everything yourself", and Donovan can relax it.

Animation (§D6) is authored by script in Blender: procedural gait, keyed drills and retargeting to Claude's own rig. CC0 animation libraries are not used under this reading either.

References (§R) are unchanged: they inform shape and are never shipped. The §M5 review loop and G7 still apply in full. A modeler subagent still never approves its own asset.

Consequence: the §M9 first-pass approval watch (40% over 10 assets) now measures Claude's own generators, so a route failing that watch means fixing the generator.

## D022 — New data-driven battle sim and the G4 scenario format

`sim/Battle/` (BattleWorld, Sight, TerrainBuilder, Rules, Scenario) replaces `BattleSim` piece by piece. The old class keeps its unit tests until the view (`game/BattleView.cs`) moves over; then it and its tests are retired, and the replacement scenarios must already cover the same behavior. State lives in `sim/State/Battle/` so the SIM-01 analyzer covers it. Global tuning numbers come from `data/rules.json`, generated by `scripts/generate_rules.py` with a reason per number. `Sight.IntSqrt` is pure integer.

Scenarios live in `verify/scenarios/*.json` and run with `sim/Headless --scenario`. A check passes when every seed passes, or, for a check with `ci_lower_pct`, when the 95% Wilson lower bound of the pass rate meets it. Adding scenarios only adds verification. Each batch is its own `VERIFY:` commit that cites this entry.

Terrain fixtures: `hill` (dome), `ridge` (segment with linear falloff), `rect` plateau, `forest` (density 0–100), `road`, and timed `smoke`. Heights combine by maximum.

## D023 — Entity counting, staggered spotting, two-level A* (SIM-13)

- **Entities** (the D008 question): an entity is one soldier, one vehicle, or one aircraft. The sim's unit is a squad or team: one `UnitState` with `Members`. The 2,000-entity benchmark is about 546 sim units (rifle squads count 9 entities each).
- **Spotting** re-evaluates each target's detection mask every `detect.spot_interval_ticks` (4 ticks = 0.2 s), staggered by unit id, with a full pass on tick 0. A unit crossing a ridge is noticed within 0.2 s. The cost is a quarter of all-pairs. All 21 scenarios pass unchanged.
- **Pathfinding** runs a coarse A* on 64 m cells (the minimum passable cost per block, so roads are never undervalued). Then the exact 16 m A* searches a corridor one coarse cell either side, falling back to the full search. Search buffers are reused with generation stamps. The averaged-cost version broke `path_road_preference` and was rejected.
- **G6 (headless half):** `scripts/analyze_perf.py` runs `sim/Headless --bench 2000 2400` in two processes. Two forces start 2.5 km apart on a generated 8 km map with hills, forest, roads and villages. The gate requires p95 ≤ `perf.tick_p95_ms`, contact (kills > 0), and equal world hashes.
- Measured on Parish1 (i9-14900HX), 2026-10-08: p50 4.0 ms, p95 6.1 ms, p99 7.1 ms, max 57 ms (the opening burst of path requests). It ran on the placeholder roster, and must be re-run when `data/roster.json` lands.

Addendum, same day: on the sourced roster (D024), p95 first rose to 11.6 ms. Units carry 3–4 weapons, and every mount scanned all enemies with a sight ray. Fixed by four changes:
- per-player spotted-enemy lists
- line of sight checked only from the best-scored candidate down, cached per shooter per tick
- a 10-tick idle rescan backoff
- an exact grid traversal for building checks, and a fast integer square root

Result: p95 4.1 ms, p99 5.5 ms.

## D024 — Sourced Cold War roster and the tuning layer

`assets_src/research/cw1989_roster_sources.json` holds the researched facts for the 50 slice units, with per-field sources. Penetration and armour figures are marked as estimates. `scripts/generate_roster.py` turns them into `data/roster.json` and `data/weapons.json`:
- Researched numbers go in unchanged.
- A range becomes its midpoint and is listed in the unit's `estimates`.
- A missing or implausible number comes from a design table and is listed in `design`, with a `design_note`.

Hit chances, reload times, damage, suppression, HP, stealth, optics ranges and size are game tuning, not real-world data.

Choices:
- Tank front armour = 60% turret + 40% hull.
- Kontakt-1 ERA adds 200 mm against CE frontally and 100 on the side skirts.
- HP = 40 + 2 × weight in tonnes; infantry have 10 HP per member.
- Fuel is tenths of a kilometre of range (10 per km).
- Aircraft range is abstracted to 500 km.

Conflicts the research flagged were resolved this way:
- M1A1 armour: the ru.wikipedia baseline, not the M1A1HA figure.
- M829A1: 650 mm at 2 km.
- T-80BV: the en.wikipedia figures.
- Missile: Kobra, not Refleks.

Values that look wrong in the research were overridden with a note: the M48 Chaparral's weight, the 2S12's weight (mortar only), and the M106's range units. `BattleData.Load` uses the roster whenever `data/roster.json` exists. The legacy `data/units.json` remains only for the old `BattleSim` and its tests.

## D025 — Style-board reference packs; one privacy incident

The reference-pack agent built §R packs for the 7 Cold War style-board items: m1a1, t80bv, m60a3, a10a, su25, us_rifle_squad_1989 and wgerman_town_block_1989.
- **Files:** 148 distributable (PD, CC0, CC BY 4.0, each checked against Commons `LicenseShortName`) and 24 look-only in `refs_private/`.
- **Specs:** `data/assets/<id>.json`, every number sourced, five signature features each.
- **Coverage:** run as if all seven were in production, G14 reports 0 problems (2026-10-08). The items stay at state `refs` until modelling starts.

Weak slots, accepted and noted in the packs:
- `engine_glow` for the A-10 and Su-25: neither has an afterburner, and the refs show no visible glow.
- `top_or_plan` for t80bv and m60a3: oblique photos plus look-only plan drawings.
- Era stand-ins: 1945 German rubble photos, USMC 1989 kit, a T-80U interior.

`data/assets/*.json` are hand-written research specs, like `assets_src/research/*.json`, not generated game data. A schema for them is due before G7.

The town-block footprint medians come from OpenStreetMap (ODbL), and need the attribution line in credits (LEGAL.md).

**Privacy incident:** the agent's HTTP User-Agent for Wikimedia Commons API calls and one Overpass call contained Donovan's email address. The agent removed it on noticing. This breaks the rule against sending his email to unrelated services, and he is told in the session report. Future agents get an explicit instruction: no personal data in request headers.

## D026 — G5 soak harness; balance status

`sim/Headless --soak` runs AI-vs-AI matches. Each match gets a generated 8 km map with 5 symmetric objectives, the `data/decks.json` decks (from `scripts/balance/cost_model.py`), AI XOs with aggression 50–79, and conquest scoring under the 40-minute rules cap. `scripts/analyze_soak.py` (G5) runs 200 cross-faction matches plus 100 per mirror, then checks `verify/thresholds.toml [ai]`, crash-free (QA-03) and the MEC-10 bank median.

Bugs the soak found, fixed:
- withdrawn units re-withdrew every plan tick
- a detracked vehicle kept its move goal, so it showed as "stuck"
- an off-by-one stopped matches before the time-cap judgement
- the map generator overlapped villages (2 crashes in 400 matches)

Balance: NATO wins 37% cross-faction (CI 31–44%), inside the 35–65% band but at its edge. The matches are decided by objective timing more than kills (kills are about even), and the Pact's fast phase-A wheeled cards get to the middle first. Two cost passes made it worse and were reverted: NATO ×0.92 gave 34.5%, and pricing speed gave 14%. This stays open W6 work. Better levers to try: deck composition, card phase, and a fast NATO phase-A card. Battles are also passive, about 23 kills in 40 minutes.

## D027 — Ortho masks from photos: top cuts and perspective

`scripts/refs/trace_ortho.py` traces masks with GrabCut from distributable photos. It takes `--bg` hint boxes (known background, such as the gap under the belly) and fills holes. Two flags record limits of a photo mask:
- `--top-cut`: the hint box deliberately cuts through the vehicle to exclude crew, antennas and stowage. The mask then holds no information above its own height, so CS-1 clips the model silhouette at that height before computing IoU. The thresholds are unchanged; the measurement covers only the region the reference observed.
- `--height-m`: a perspective photo, near-orthographic front views especially, compresses height relative to the near-plane width. The mask's height is rescaled to a sourced dimension, and the mask is marked `low` confidence (−0.05 IoU, and human review required per §M6).

When no distributable plan view exists, the top mask is synthesised from the spec's plan dimensions and marked `low`. That makes it nearly circular, so it checks length, width and gun line only, and human review covers the rest.

## D028 — Drawing masks: erased stores, gear-up silhouettes, inconsistent views

Aircraft references are often line-art or silhouette 3-views. `trace_ortho.py --lineart` traces them as `drawing` confidence (photo thresholds). Three additions, all recorded in the mask json:
- `--erase X0 Y0 X1 Y1` (repeatable, needs `--erase-why`) removes source pixels before tracing. It is used for drawn stores (bombs, rocket pods, tanks, missiles). Stores are loadout attachments, not airframe. Erase boxes are chosen by eye on a gridded enlargement of the source and stay out of the airframe outline. Short pylon stubs stay in.
- `"gear_up": true` on a mask makes `render_sheets.py` pass `--gear-up` to Blender, which hides the model's `Gear_*`/`Wheel_*` nodes in the silhouettes only. The model stays gear-down.
- G7 accepts a twin control surface as `<name>_L` + `<name>_R` (section M7's "Rudder(_L/_R)", for twin-fin aircraft).

Drawing views can disagree with each other. Both aircraft drawings so far have a front view 18–20% taller than the other view at the same horizontal scale:
- A-10 front: 3.78 m gear-up height against a 3.16 m side view.
- Su-25 front: 5.65 m against a sourced 4.8 m.

The A-10 reviewer confirmed this independently: rescaling the front reference by 0.83 lines up nacelles, canopy and fins. When a sourced height exists, the inconsistent view is height-normalised with `--height-m` (marked `low`). Without one (A-10: the drawing is gear-up, the sourced height is gear-down), the view stays as drawn and fails IoU honestly. That case joins the IoU waiver proposal awaiting Donovan, together with views that have no distributable orthographic reference at all (T-80BV side and front, Su-25 side).

## D029 — Storey height 2.8 m; building-kit sheets; centred plan registration

- **Storey height** `structure.floor_dm` changed from 30 to 28 (2.8 m). The source is the sourced typical storey: MBO §47(1) requires 2.40 m clear height, plus about 0.35 m of slab, giving 2.75 m (`data/assets/wgerman_town_block_1989.json`). 2.8 m is the nearest value on the 1 dm grid. After the change, G2, G3, G4 (every scenario and mutant) and G10 pass (`build/reports/20261009T000609Z-gates-G4-G10.md`, `20261009T000738Z-gates-G2-G3.md`).
- **Kit generator** `assets_src/procedural/building_kit.py` reads `chunk_m` and `floor_dm` from `data/rules.json`, so the visual kit cannot drift from the SIM-10 collapse grid. Each wall chunk is its own node, with its openings, its inner faces and its slab. Roofs are one bay per chunk along the ridge.
- **Building kits get no CS-1 IoU.** Section M2's table specifies street photos, not orthographic drawings, as building-kit references. `render_sheets.py` renders kits with `--kit`: turntable, elevations and plan at 40 px/m (CS-2b), and street-level plus RTS-camera views (CS-2c). G7 checks kits against the sourced distribution numbers (storey height, median roof pitch, median footprint sides) with a 10% tolerance, because the 3 m chunk grid quantises footprints. The IoU check is skipped for them.
- **Plan-view IoU registration** now centres both silhouettes on the centreline (`centre_v`). Plan views have no ground line, and aligning on one edge pushed any span difference entirely to one side. Side and front views stay ground-aligned.

## D030 — Exact close-up scale, part shapes, sourced palettes and camo textures

- **CS-2b scale fixed.** Close-ups used `ortho_scale = 1.03 × extent` with a resolution of `extent × px + 40`, so the labelled px/m was off by about 3–4%. That explains reviewers' pixel measurements being "1.5% small" (A-10) and the rifle reading 1.044 m (squad). The resolution is now `extent × 1.03 × px`, with the sensor fit on the longer side, so the label is exact. Earlier reviews' pixel measurements carry that error. The build json geometry was always exact.
- **Vehicle parts** take `shape: cyl_z | cyl_x | cyl_y` and `yaw_deg` (`ground_vehicle.part_mesh`), for drums, cupolas, snorkels and ERA laid along an arc.
- **Palettes.** `assets_src/research/palettes_1989.json` was compiled by the ref-hunter: 31 colours in 7 schemes, each with its standard, sRGB value and source.
  - 19 are sourced. The M81 colours come from the measured CIELAB values in the 1981 Natick development report (DTIC ADA096884). The FS chips come from Lab-backed colour-database values.
  - 12 are null. These include all six Su-25 colours, because no Soviet designation or measurement was found. They stay null, are never invented, and the Su-25 camo is rendered visibly grey.
  - Values fetched through search snippets (sites returning HTTP 403) are tagged `snippet`.
- **Camo textures.** `scripts/textures/camo.py` generates tileable patterns from the palette: band-limited periodic noise, domain-warped and quantile-thresholded per colour, with the physical tile size in the sidecar json. Colours are picked by name, so a null entry can never shift the order. The schemes are M81 woodland, NATO three-colour CARC, A-10 European I and Soviet 4BO; the Su-25 is a placeholder.

## D031 — Air commander tasks (CMD-03, CMD-05) and airborne scenario spawns

- **New air-seat orders** (`sim/Battle/Command.cs`):
  - `cap`: free aircraft fly a four-point racetrack, `command.cap_orbit_m` (2 km) around the point, one leg per `cap_leg_ticks`.
  - `cas`: free attack aircraft hold at a standby point.
  - `interdict`: free attack aircraft steer toward spotted enemy vehicles in a box, supply vehicles preferred, and otherwise sweep to its centre.
- **CAS requests.** An army seat's attack now files a CAS request alongside its preparatory fire request, if an air seat has a free attack aircraft. The request goes through the same approval mode as fires (CMD-05).
  - The air seat dispatches the nearest free ground-attack aircraft.
  - The aircraft comes on station within `cas_on_station_m` (1.5 km) and works the area for `cas_loiter_ticks` (60 s), then the request closes.
  - A lost or off-map aircraft is replaced.
  - `SupportRequest.OnStationTick` is new and hashed. The G3 golden is the Phase 0 entity sim, so it is unaffected.
- **Airborne scenario spawns.** Scenario aircraft without an explicit `altitude_m` now start at the deploy altitude (`Economy.DeployAltitudeM`, 300 m) instead of 0. At 0 m, combat treated them as ground targets: air-to-air missiles could not engage them, and ground cannons could. Both existing aircraft scenarios set `altitude_m` explicitly, so they are unchanged.
- **New G4 scenarios (new checks only, no threshold changes):**
  - `cmd_cas`: the full request cycle; the target is beyond Maverick range of the standby point, so only the request can produce the strike.
  - `cmd_cas_approval`: the request waits for the player.
  - `cmd_cap`: the fighter starts beyond missile range, so only the CAP brings it into the fight.
  - `cmd_interdict`: targets start more than 8 km out.
- **Mutants:** zero loiter, an endless loiter (the aircraft never releases) and an off-map orbit each fail a scenario. A zero station radius is not a valid mutant, because the aircraft reaches the exact request point anyway. The interdiction scenario fails if the order does nothing (the `has_goal` check, and the truck survives).
- **Screen** now has its own planner instead of reusing recon. Posts spread along the line with return-fire ROE. A post that is engaged (hit or fired on) and not already moving falls back `command.screen_withdraw_m` (600 m) away from the nearest enemy its side can see. That covers misses: suppression records when a unit was fired on but not by whom, and `LastAttacker` stays reserved for kill credit. Scenario `cmd_screen`; mutant `screen_holds_under_fire`.
- **Bug fix: return-fire ROE at match start.** `LastHitByTick` defaults to 0, so for the first 200 ticks every return-fire unit counted as recently hit and fired freely. A value of 0 now means never hit. The only edge left is a hit on exactly tick 0.

- **One sortie per request.** When a CAS loiter ends, the aircraft gets `UnitState.SortieDone` (hashed). It flies home and turns around off-map like Bingo or Winchester (`Rtb` value 3). Before this, an aircraft that hovered burned no fuel, because fuel is charged per km moved, so it could loiter forever.
- **Air-defence gating.** No CAS is flown into a spotted enemy air-defence umbrella: anti-air weapon reach plus `command.cas_threat_margin_m` (1 km). The request waits, and the air seat files a SEAD fire request on the threat with artillery.
- **CAS needs a CAS-tasked air seat.** Army attacks request CAS only from an air seat tasked `cas` (on standby). With automatic CAS from every attack, the G5 cross-faction rate swung to NATO 98% (196/200). The swing came from the requests alone (17/40 NATO wins with them disabled, 37/40 enabled), but the mechanism is not understood:
  - aircraft scored only 1 and 4 kills across 40 matches;
  - blinding aircraft sensors did not change it;
  - one sortie per request did not change it either.

  Until that is understood, AI matches fly CAS only when the XO or player tasks it, and G5 is back in band (NATO 0.365, `build/reports/20261009T014502Z-gates-G2-G4-G5.md`). Open as W6 balance work.
- **Soak CSV columns added:** `cas0`, `cas1`, `air0`, `air1`, `air_lost0`, `air_lost1`, `air_kills0`, `air_kills1`, for the air war.

## D032 — CMD-05 approve, deny and reprioritise

Scenario `approve`/`deny` orders take an optional `priority` (lower is served first), which reaches the existing `Command.Decide` reprioritise path. New G4 scenario `cmd_request_decisions`:
- Three seats file preparatory fire requests to one battery that asks before spending.
- The player denies one, approves one at default priority, and approves the third with priority 0.
- The battery fires the reprioritised request first. Until tick 2000 the median impact sits within 200 m of its objective and more than 600 m from the other.
- It then fires the other request, and never fires the denied one.
- Without the priority, id order would put the impacts on the wrong objective and the check fails. There is no rules mutant, because priority ordering is code, not data.

## D033 — CMD-04 priority targets and statistical intent A/B

- **Priority targets** is a new seat intent: field `priority`, set to a UnitDef kind; 0 means none. Its units' target scores for that class are multiplied by `command.priority_target_pct` (300%). The preference is strong but still yields to a much better shot. `SeatState.PriorityKind` existed but was unused; it is now set, hashed and applied.
- **New scenario checks:** `first_kill_kind` (the class of the first unit a shooter kills) and `seat_withdrawals_at_least` (withdraw events for a seat's units).
- **Statistical A/B over 50 seeds, asserting non-overlapping 95% Wilson intervals:**
  - `cmd_intent_stats_tolerance`: tolerance 15 withdraws in at least 60% of seeds; tolerance 85 in at most 50%. Thrown tracks and crew bail-outs withdraw a unit at any tolerance, so B is not zero.
  - `cmd_intent_stats_priority`: preferring infantry makes the squad the first kill in at least 90% of seeds, against at most 80% with no preference. The enemy holds fire, so the test measures target choice, not survival.
- Preferring tanks barely changes an M2's choices, correctly: its TOW already hunts tanks, and its 25 mm keeps engaging targets it can hurt.
- **Mutants:** `priority_targets_ignored` and `never_withdraw_damaged`.
- Aggression, ROE and approval mode keep their deterministic A/B scenarios (`cmd_intent_ab`, `cmd_requests`, `cmd_cas_approval`).

## D034 — Take Control and the flight model (FLY-01..03) in the battle sim

The FLY evidence so far was in the legacy Phase 0 sim. `sim/Battle/Flight.cs` now covers the battle sim.

- **Take Control** (`take_control` order). The player flies one aircraft at a time; taking another hands the first back. The player's XO takes command the same tick (`XoTookCommand`, hashed `XoActive`/`XoRestore`). `XoActive` was not hashed before.
- **Return of control** (`ControlReturned`). The triggers are Bingo (1), Winchester (2), ejection when destroyed (3), landing within 600 m of home below 50 m at under 1.5× stall speed (4), and manual `release` (5). The AI pilot resumes; the Supply RTB logic and the air commander ignore flown aircraft.
- **Flight model**, integer only, from the sourced envelopes in `assets_src/research/flight_envelopes_1989.json` (via `generate_roster.py` into a roster `flight` block):
  - Thrust `a_max·throttle`, with `a_max` from the sourced climb rate at a best-climb speed of 0.55·vmax.
  - Drag `a_max·(v/vmax)²`, so full throttle settles exactly at vmax.
  - Gravity along the flight path, and a turn-energy bleed.
  - Turn rate capped by the G limit: `g√(G²−1)/v`.
  - Stall below the sourced stall speed: the nose drops whatever the stick says.
  - Ground contact destroys the aircraft.
- Several sourced "stall" speeds are landing or approach speeds, labelled as such in the research file. The F-15C and MiG-29 climb rates are peak zoom figures, so they are not climb-tested.
- **Bug found while testing:** the turn-rate expression overflowed 32-bit `int` before widening, which made the turn rate zero. Fixed.
- **Scenarios:** `fly_take_control`, `fly_return_bingo`/`winchester`/`eject`/`landing`, `fly_env_speed`/`climb`/`turn`/`stall`. They check speed within 97–100% of vmax, climb within ±10% of the sourced rate, turn within ±5% of the G-limited rate, and stall onset with sink.
- **Mutants:** a low G limit, a stall speed too low to stall, and a weak climb each fail a scenario. A weak climb does not fail the speed scenario: lower thrust slows the approach but the same vmax is reached.

## D035 — Cover tiers, wrecks and craters, veterancy, retreat (MEC-04)

- **Directional cover** multiplies hit chance (`Combat.CoverPct`):
  - Heavy (`cover_heavy_pct` 40%): inside a standing building.
  - Light (`cover_light_pct` 70%): inside forest; behind a hedge or a wreck on the shooter's side, within `cover_object_m` (12 m) and 60° of the line of fire; or infantry in a crater.
  - A wreck on the wrong side gives nothing.
  - Fire from aircraft respects only heavy cover, and airborne targets get none.
  - The values are design values with reasons in `generate_rules.py`.
- **Wrecks and craters.** Wrecks are destroyed ground vehicles, listed once per combat tick. Craters come from HE impacts with a blast radius of at least 20 m: a hashed ring of `crater_max`, plus a `crater` scenario terrain feature.
- **Veterancy** is now earned by kills (`vet_kills` 0, 2, 5, 10), with a `Promoted` event. Before this it never rose.
- **Retreat order.** The unit holds fire and moves home even when pinned. At home it is reinforced (full hp and members, no suppression, normal morale) and keeps its veterancy (`Reinforced` event).
- **Scenarios:**
  - `mec_cover_tiers` evaluates the real hit-chance function against derived values: 39% open, 27% forest or wreck in front, 39% wreck behind, 15% building, 6→4% for infantry in a crater, and 58% for the A-10 both open and in forest.
  - `mec_veterancy_retreat`.
- **Mutants:** `no_light_cover`, `no_heavy_cover`, `veterancy_unreachable`.

## D036 — Battle planner and preparation (MEC-05)

- **Planner orders.** `front` and `offensive_arrow` are scenario and UI names for the defend and attack tasks. `spearhead` and `garrison` are new:
  - Spearhead: a narrow column on the axis, tanks then IFVs then the rest, `formation_spacing_m` apart. It drives through the objective without preparatory fires.
  - Garrison: infantry take distinct ground-floor building chunks within `planner_garrison_radius_m` (200 m), nearest first; vehicles hold at the point.
- **Preparation** (`CommandTask.Prep`, hundredths, hashed).
  - It grows to full over `planner_prep_ticks` (2400) while the plan is left to its commander.
  - Each direct move or stop order to one of the plan's units costs `planner_micro_decay` (25 points) and emits `PrepLost`.
  - Full preparation gives `planner_prep_bonus_pct` (+20%) hit chance.
  - These are design values in the HoI4 tradition, with reasons in `generate_rules.py`.
- Existing attack and defend tasks now accumulate preparation too. All 66 prior scenarios still pass.
- **Scenarios:**
  - `mec_planner_prep`: a front left alone reaches 100 and a 60% hit chance; a micromanaged front ends at 30 or below and 50–53%.
  - `mec_spearhead`: final column order and spacing, with no fire request.
  - `mec_garrison`: infantry inside buildings, the IFV outside.
- **Mutants:** `no_prep_bonus`, `micromanagement_free`.

## D037 — Off-map support catalogue (MEC-03, SUP-02, SUP-05 Cold War slice)

- **Catalogue.** `data/support.json` is generated by `scripts/generate_support.py`, with a G10 drift check. It holds 20 Cold War options, 10 per side. Each has era, side, SP cost, charges, recharge and response delay. Delays are doctrinal times compressed about 2× for game pace; costs, charges and recharges are design values; each option records its reasoning.
- **Effect types** (`sim/Battle/Support.cs`):
  - barrage, smoke and illumination: off-map shells through Fires, CEP from the weapon at a notional firing range;
  - air strike run: impacts along a line on a heading in the sim convention, 0 = east, counter-clockwise;
  - SEAD: kills the nearest radar-directed air-defence unit. The 1989 roster's SAMs are all infrared, so the emitters are the radar-directed guns (ZSU-23-4, M163).
  - reveal: recon pass or AWACS, fed into the spotting mask.
- **Charges.** A call is allowed while a charge remains. Each spent charge recharges after `recharge_ticks`, one at a time. Refusals: wrong side (1), short of SP (2), no charge (3), wrong era (5). A first version blocked any call during the timer even with charges left, which made charges a cooldown; fixed before commit.
- **Era gating.** A world era (scenario `era`, default `coldwar`, hashed) refuses options from other eras.
- **Spot-assist SP within a side.** When the shooter had no line of sight to its victim (indirect fire on a spotted target), the killer's player also earns `sp_spot_assist_pct` (`SpotAssist` event). Before this only other players' spotting paid, which never applies in a 1v1.
- **Mutant runner.** `check_scenarios.py` mutants accept `support:` keys, and the mutant data root now includes `support.json` and `decks.json`.
- **Scenarios:** `sup_offmap_economy`, `sup_era_gate`, `sup_air_strikes`, `sup_sead_reveal`, `sup_smoke`, `econ_sp_spot_assist`.
- **Mutants:** `support_instant`, `support_free`, `sead_blind`, `no_spot_assist`.
- **Not in this slice:** paradrop and air assault (spawning units), escort and CAP as support calls, gunship orbit, cruise missiles, drones and satellites. The last three belong to the modern era.

## D038: Shipped decks per income profile (MEC-01)

- **Decks.** `scripts/balance/cost_model.py` now ships 8 decks: one per income profile per side.
  - balanced: `us_1989_armored` and `su_1989_motor_rifle`. These remain the G5 soak decks and are unchanged.
  - juggernaut: heavy and tank division.
  - vanguard: armored cavalry and forward detachment.
  - maverick: air cavalry and air assault.
- **Contents.** All 8 decks use the same unit pool and costs. Card counts scale per sim kind to fit each profile's tempo, a designed value recorded in `PROFILE_SCALE`.
- **Income.** A deck carries its income profile. A player given a shipped deck gets that deck's profile unless the scenario names one.
- **G10.** `scripts/check_decks.py` runs after the drift check. It verifies that every unit exists and belongs to the deck's side, that phases, counts and costs are valid, that each deck has a phase-A card, and that each side has all four profiles. It runs a self-test on six broken copies first.
- **G4.** New scenario `econ_shipped_deck_profiles` and mutant `shipped_deck_income_ignored`. The mutant runner accepts `decks:` keys.

## D039: Direct-control parity (MEC-08)

- **No buff by construction.** Player-flown aircraft fire through the same `Combat.Fire` and `HitChance` path as AI pilots. The new rule `combat.direct_control_hit_pct` is 0 by design. It exists only so G4 can prove that the parity check catches a buff.
- **Measurement.**
  - `Fired` events now carry the number of shots actually taken in `Value`. That field held the mount slot before, and nothing read it. A salvo stops when its target dies.
  - Per-shot hit rate = (shots − misses) / shots, for one mount.
  - New aggregate check `hit_rate_parity_pct`: it sums shots and misses over every seed. It passes when both shooters reach `x` shots and their rates are within `value`% (relative). The runner judges aggregate checks once, after all seeds.
- **Scenario `mec_no_control_buff`.**
  - Two A-10As make the same level pass at 300 m and 155.6 m/s past identical rows of BMP-2s, GAU-8 only.
  - The AI flies one on a move order. The player flies the other through Take Control at throttle 63, which holds the same speed.
  - Over 40 seeds: flown 330/1295 (25.5%), AI 333/1343 (24.8%).
- **Mutants:** ±25% (`direct_control_buff`, `direct_control_nerf`) fail. A +15% buff survives: about 8.5% measured, because close-range shots clamp at 100%. The gate catches buffs of roughly 20% or more at this sample size. Tightening it means more seeds; that is a cost trade-off, not a waiver.

## D040: AI deployment, artillery handling and deck caps (MEC-09; W6 pass 2)

G5 now measures MEC-09: per-AA-card aircraft value destroyed, and single-class decks (tank, IFV, infantry) against combined arms in both faction mirrors (80 matches each). Thresholds come from the brief: AA ratio at most 1.5, single-class win rate at most 0.55.

**Findings that drove the changes**
- **Deploy order.** The AI bought the cheapest affordable card the moment it could, so it never saved up. No phase-B or phase-C card (tanks, M2A2, BMP-2, A-10) was ever deployed in any soak match.
- **W6.** NATO aircraft made zero kills. The only NATO aircraft were the OH-58D and UH-60, and they sat at base because the XO never tasks air seats.
- **Guns** never moved:
  - the MLRS (10 km minimum range on an 8 km map) could never fire;
  - batteries died at spawn to counter-battery fire;
  - a battery given a mission while relocating never fired, because guns can't fire on the move.
- **Infantry** walked at 5 km/h on an 8 km map and never reached the fight.
- **Piecemeal combat.** An IFV-only deck won 84% against combined arms. The combined side fed support units piecemeal, and objectives stayed with whoever held them last.

**Changes**
- **AI deploy (`Command.AiDeploy`):**
  - saves for the most-needed legal card instead of buying cheaper ones;
  - with fewer than two guns alive, artillery comes first;
  - skips indirect-fire cards whose minimum range is at least the map size;
  - buys AA only once enemy aircraft are visible;
  - prefers the highest-penetration card within a kind when the visible enemy is mostly armour;
  - below 10 manoeuvre units (infantry, tanks, IFVs) alive, buys only manoeuvre cards, plus its first two guns.
- **Artillery:**
  - idle batteries displace into range of the nearest objective they don't hold;
  - shoot and scoot: a battery that fired within the counter-battery locate time moves 300 m sideways;
  - a fire mission halts a moving battery;
  - an assault with no gun in range holds for up to one more assembly timeout while its guns come forward.
- **Mounted movement:**
  - roster `carrier` / `mounted_speed_mm_s` / `mounted_move_class` for nine infantry teams, each carrier sourced to FM 71-2 or FM 100-2-3;
  - a carried team moves at its carrier's speed unless it fired, was hit or was suppressed within `move.dismount_ticks` (600).
- **Decks:**
  - per-card `max_count` availability cap, twice the balanced deck's count; G10 enforces `count <= max_count`;
  - single-class soak decks take each card at its cap;
  - deck artillery count raised from 4 to 8.
- **Cost model (W6 pass 2, costs before stats).** Direct-fire weapon value scales with reach: range / 2 km, clamped 0.25–1. Effects:
  - rifle squads 125/150 → 75/80;
  - Dragon team 90 → 60;
  - most vehicles fall 5–20 points.
- **Fired events.** `Value` = shots taken (D039).

**Tried and reverted:**

| Change | Result |
| --- | --- |
| M3A2 to phase B | NATO IFV-only rose to 35/40 |
| XO CAS standby for air seats | Combined side's helicopters flew into IFV fire; Pact IFV-only rose to 29/40 |
| Not buying aircraft | Parked aircraft still defend the base; IFV-only rose to 62/80 |
| Truck exemption from the manoeuvre floor | Cross-faction NATO 0.78 |
| Several AI mix changes | No effect |

**Open (W6).** The XO doesn't employ aircraft. The automatic-CAS balance swing (D031) is still unexplained. G5 status at the time of writing is in PROGRESS.md.

**Final configuration (G5 report `build/reports/20261009T050637Z-gates-G2-G4-G5-G6-G10.md`).** Added after the first draft of this entry:
- counter-battery fire only from batteries holding three preparation missions' worth of rounds;
- SAM and AAA mounts add damage / 2 × reach to their value (Strela-10 rises from 70 to 105);
- NATO side cost scale ×1.15. ×1.05 left cross-faction NATO at 0.715; ×1.15 brings it to 0.575;
- manoeuvre floor raised to 12;
- availability cap set to 150% of the balanced deck's count, the most any shipped profile deck takes;
- the XO gives air seats a CAS standby a quarter of the way to the map centre;
- the AA check needs at least 8,000 points of AA deployed, so a battery with no AA in play can't pass it vacuously.

**Results:**

| Check | Result | Limit |
| --- | --- | --- |
| CMD-06 fire support before assault | 0.755 | ≥ 0.70 |
| Mirrors, player 0 wins | 0.51 / 0.55 | 0.40–0.60 |
| Cross-faction NATO wins | 0.575 | 0.35–0.65 |
| Single-class tank / IFV / infantry wins | 0.00 / 0.34 / 0.075 | ≤ 0.55 |
| AA check | 0 AA deployed (red) | ≥ 8,000 points deployed |

**Why AA is still red.** The AI buys AA only after it sees enemy aircraft, and it barely buys aircraft under the manoeuvre floor. Relaxing the floor for air and AA (from 6 manoeuvre units) produced only 430 points of AA deployed. It also broke CMD-06 (0.665), the Pact mirror (0.62) and IFV-only (0.59), so it was reverted.

**Next step (W6).** A real AI air war: air seats that fly sorties the enemy can see, and an aircraft purchase rule that doesn't starve the ground force.

**Resolution of the AA check.**
- **Rules added.** Enemy aircraft in sight with no AA alive: an AA card comes first. AA is exempt from the manoeuvre floor while enemy aircraft are in sight. A maverick deck's aircraft are exempt from the floor (air doctrine).
- **New soak run.** G5 adds an 80-match air war, `us_1989_air_cavalry` against `su_1989_air_assault`.
- **Result.** 9,835 points of AA deployed in total. Worst card: M48 Chaparral at 0.47 of its cost in aircraft destroyed. All of G5 passes: `build/reports/20261009T052635Z-gates-G2-G4-G5-G6-G10.md`.

## D041: Field Requests (MEC-06, sim half)

- **Data.** `scripts/generate_field_requests.py` builds `data/field_requests.json`, with a G10 drift check: 16 Cold War task forces, 4 per side for each of phases B and C.
  - Each is a company- or battery-sized attachment (FM 71-2 cross-attachment; FM 100-2-3 regimental reinforcement), and each records why.
  - Each is sized within 15% of a per-side value budget, using the balanced deck's card costs: NATO B 480 / C 700, Pact B 400 / C 600. NATO's budget is higher because its prices carry the ×1.15 side scale. The choice is about composition, not size.
- **Offers.** At each phase change, a match with `field_requests` on (scenario flag; campaign default, skirmish toggle) offers each player 3 different task forces of their side, era and the new phase. They're drawn with the battle RNG, and the offers are hashed. An offer not taken lapses at the next phase change.
- **Taking an offer.** The `FieldRequest` order (15) with choice 0–2 brings the units in at the player's entry point, 20 m apart, free of DP and in addition to the deck. The units carry their card cost for SP and the AI's spending mix.
- **XO-run players.** The XO takes the most valuable offer after `economy.field_request_auto_ticks` (600, 30 s).
- **Events.** `FieldOffered` (51) and `FieldTaken` (52).
- **G4.** Scenarios `mec_field_requests` and `mec_field_requests_off`, and mutants `field_request_xo_never_picks` / `field_request_xo_picks_instantly`.
- **UI half.** The phase-change offer panel is G13 work and needs the Godot view.

## D042: Field repair, obstacles, fortifications and minefields (SUP-03, part 1)

- **Repair.**
  - The M88A1 and BREM-1 get the roster flag `repair` (design).
  - A halted recovery vehicle repairs every damaged friendly ground vehicle in supply radius:
    - 1 hp per `supply.repair_interval_ticks` (20);
    - one critical per `supply.repair_crit_ticks` (1200, 60 s), in the order track, engine, optics, turret;
    - burning or bailed-out vehicles can't be repaired.
  - A withdrawn vehicle back at full health with no criticals rejoins the fight. Withdrawn units used to stay out for good.
  - New per-unit `RepairTicks`, hashed; event `Repaired` (53).
- **Terrain layers** (hashed): `Obstacle`, `Fort`, `MineDensity`, `MineTeam`; map features `obstacle`, `trench`, `minefield` (density, team).
  - Obstacles stop tracked and wheeled navigation; infantry crosses.
  - Infantry in a trench cell has heavy cover, the same as a building.
- **Mines** (`Mines.cs`).
  - Entering an enemy-mined cell rolls against its density: the full density for vehicles, a quarter of it for infantry.
  - A vehicle strike throws a track and deals `combat.mine_damage` (60). An infantry strike causes casualties as a hit of 30.
  - Each strike thins the cell by `combat.mine_deplete_pct` (20). The laying team's units know the lanes.
  - Event `MineStrike` (54).
- **Scatterable mines.**
  - New support type `minefield`: `nato_fascam_155` (M741 RAAM) and `pact_uragan_mines` (9M27K3 / PTM-1).
  - Each costs 50 SP, with 2 charges, a 90 s delay, a 150 m radius and density 30.
- **Bug fixed: scenario teams.** Scenario players never had a team set, so every player in a scenario was team 0. That affected territory, counter-battery and mines. Players now default to team `player & 1`, with an optional `team`.
- **G4.** Scenarios `sup_repair` and `sup_engineering`; mutants `repair_never_fixes_criticals`, `mines_harmless`, `fascam_empty`, `trenches_no_cover`.
- **Still open for SUP-03:**
  - supply helicopters;
  - ammo dumps;
  - medevac;
  - bridging (AVLB, MTU-20 — not in the roster yet);
  - engineer-built fortifications and mine clearing.

## D043: Engineer support, ammunition dumps, supply and medevac helicopters (SUP-03, part 2)

- **Engineer support** (`Engineering.cs`): new support types `bridge`, `fortify`, `obstacle` and `breach`. Each works on a strip, `length_m` along `heading_deg` by `width_m` across.
  - NATO options: M60 AVLB (20 m span), M9 ACE fighting positions, M9 ACE anti-tank ditch, M58 MICLIC lane.
  - Pact options: MTU-20, PZM-2 / BAT-M trench, BAT-M ditch, UR-77 lane.
  - Each records its reasoning and a compressed delay: digging takes hours in reality, compressed to 5 min; the line charges take 60 s.
- **Ammunition dumps.** Map feature `ammo_dump` (team, supply, hp), in the hashed `WorldState.Dumps`.
  - A dump reloads its team's trucks at `supply.dump_refill_per_tick` (5) and resupplies other units in radius directly.
  - HE blasts damage it; at 0 hp its stock is lost (`DumpDestroyed`, event 56).
- **Supply helicopters.** UH-60A (supply 300) and Mi-8MT (supply 400) resupply while halted, and reload when they return from the airbase.
- **Medevac.** UH-60A and Mi-8MT carry `medevac`. Halted, they return one lightly wounded soldier per `supply.medevac_ticks` (600) to a friendly squad in radius that hasn't been hit in that time (`Medevaced`, event 57).
- **Scenario fixes.**
  - An infantry unit's `hp_pct` now sets its member count; before, it changed hp and left members at full strength.
  - New unit field `supply_left`.
  - New checks: `members_at_least`, `supply_left_at_least`, `dump_alive`.
- **G4.** Scenarios `sup_engineer_support` and `sup_logistics`, and 5 mutants.

## D044: Electronic warfare (SUP-04, Cold War slice)

- **Mechanism** (`ElectronicWarfare.cs`). EW zones are off-map support with a radius and duration, owned by the caller's team, and hashed as `WorldState.EwZones`.
- **Radar jamming.** Enemy radar-directed guns (weapon type `aaa`) firing from inside the zone: hit chance × `combat.jam_radar_pct` (50).
- **Comms jamming.** Enemy batteries inside the zone get `combat.jam_comms_delay_ticks` (600) added to fire-mission setup.
- **Decoys.** Enemy SAMs and AAMs fired at the owner's aircraft inside the zone: hit chance × `combat.decoy_pct` (50).
- **Options:** EF-111A, EC-130H Compass Call and a chaff corridor (NATO); Mi-8PPA, R-330 and a chaff corridor (Pact).
- **GPS degradation** is a modern-era effect. It arrives with the modern guided-munition catalogue, so SUP-04 stays in progress until then.
- **G4.** Scenario `sup_electronic_warfare`, which also checks that each effect expires; 3 mutants.

## D045: Weather and darkness: accuracy, air operations, thermal sights (SIM-11)

- **Accuracy.** `weather_accuracy_pct` multiplies hit chance: clear 100, overcast 100, rain 90, fog 75, snow 85, night 70 (design).
- **Air operations.** `weather_air_ops`: fog grounds aircraft (`WorldState.AirGrounded`, set at build and hashed).
  - Air cards are refused at deploy, and the AI doesn't try to buy them.
  - Strike-run and SEAD support are refused with the new reason 6 (weather).
  - CAS isn't dispatched.
- **Thermal sights.** The roster's `thermal` flag (M1A1, M60A3 TTS, Bradleys, M901, TOW-2 team, AH-64A, OH-58D, A-10A) was present but unused. In fog and at night, thermal sights now:
  - keep at least `thermal.optics_pct` (85) of their detection range;
  - cancel the accuracy penalty.
- **G4.** Scenarios `sim_weather_night` and `sim_weather_fog`; 3 mutants.
- **Pitfall found while testing.** Deployed units don't take a scenario's global hold-fire ROE. A deployed A-10 killed the target a spotting check was watching, which read as lost detection. Ordering the checks fixed the scenario.
- **`los_fog_weather` updated under D045.** Its observer was an M1A1, which now has a working thermal sight. It's now a non-thermal T-80BV, so the check still proves that fog limits unaided optics, with the same two assertions. One assertion was added: a thermal M1A1 detects a tank at 1,200 m in fog. The scenario got stricter, not weaker.

## D046: SIM-12 closed out

- **Formations.** New scenario `path_formation_line`: an assault ends in line abreast at 60 m spacing. Columns are already covered by `mec_spearhead`.
- **Air pathing.** New scenario `path_air_direct`: aircraft fly the straight line over a water barrier that leaves ground units with no route.
- **8 km performance.** Covered by G6: 2,000 entities path across an 8 km map, with p95 under 8 ms.
- **Stuck metric.** Covered by G5's unstuck check on 8 km soak maps (≥ 0.995).
- **Mutants:** `formation_bunched` and `aircraft_crawl`.

## D047: Multi-phase operations, time of day, event deck, outcome-driven map state (MEC-07, MEC-12)

- **Persistent world.** An operation's phases are fought on one world, so wrecks, craters, rubble, fortifications, obstacles and minefields persist naturally. Scenarios declare `phase_breaks` (tick, optional forced outcome).
- **`Operation.Advance`** at each break:
  - moves the clock one step: dawn, day, dusk, night;
  - hands every objective to the winner;
  - pulls the loser's entry point back `operation.spawn_pullback_m` (500);
  - draws one weighted card from the event deck;
  - restarts score, the time cap and the economy phases. `OperationStartTick` is hashed with `TimeOfDay`, `OperationPhase` and `OperationEvents`.
- **Time of day.** `time_of_day` multiplies optics (70 / 100 / 70 / 40) and accuracy (90 / 100 / 90 / 70). Thermal sights cancel night. Flares restore daylight inside their light.
- **Event deck.** `scripts/generate_operation_events.py` builds `data/operation_events.json`, with a G10 drift check: 7 Cold War cards (fog bank, rain front, clear skies, overcast, REFORGER battalion, second echelon, night resupply). Effects are weather, DP to a side, and resupply.
- **G4.**
  - `op_persistence`: crater, wreck and FASCAM minefield persist across two breaks; clock, events and economy reset.
  - `op_outcome_nato` / `op_outcome_pact`: two outcomes give two map states.
  - Mutant `loser_never_falls_back`.
- **Still open for MODE-02 / campaign:** linking operations into the campaign layer, briefings and voices.

## D048: Grand strategy (GS-01..05, MEC-13, CMD-08)

- **Theaters.** `scripts/generate_theaters.py` builds `data/theaters.json`, with a G10 drift check and symmetric adjacency checked at generation:
  - WW2, Western Front, September 1944: 12 provinces;
  - Cold War, Central Europe, August 1989: 19 provinces, 7 factions, France starting outside the war;
  - modern, Suwałki and the Baltic, 2026: 12 provinces.
- **Sim** (`sim/GrandStrategy/`), deterministic, integer-only, JSON save/load and hash:
  - production from industry, manpower, research to 5 tech levels (+8% each), armies up to 100 strength, a commander bonus;
  - supply attrition when cut off from the capital;
  - one province of movement per turn, along shortest paths through friendly territory.
- **Battles (GS-02).** Odds are shown in permille. Terrain defence: forest ×1.25, hills ×1.35, urban ×1.5.
  - Auto-resolved by a roll against the odds, or fought as a full AI-vs-AI RTS battle on an 8 km map generated from the province terrain (`GsBattleMap`) in the turn's weather (`GsRunner.Rts`).
  - Results feed back into strength (winner −15%, loser −35%), ownership, commander XP, wounds and tension.
- **Diplomacy (GS-03).** Alliances bring a neutral faction into the war; ceasefires hold 6 turns; lend-lease moves 20% of production; a side whose war goals are all held wins.
- **Nuclear (GS-04).** Tension rises per battle and capture.
  - Thresholds: tactical 20, theater 40, strategic 70.
  - A theater strike destroys up to 3 industry and leaves 4 turns of fallout at −20% strength per turn. The struck side's AI retaliates in kind. A strategic release ends the war for everyone.
  - No gore. The tactical release as an RTS support card and the G8 blast scene are still open.
- **AI.**
  - Reinforce attacks with the nearest rear reserve, or a quiet front-line sector (MEC-13).
  - Top up front-line armies, research with any surplus, attack at odds of 55% or better, preferring war goals.
  - Escalate when losing.
- **Commanders (CMD-08).** XP for battles, wounds (out 3 turns, under a 0-XP replacement), persisting through save and load.
- **G4.** Scenario type `grand_strategy`, run by the same G4 runner, with 9 scenarios and 6 mutants.
- **G5.** An AI-vs-AI soak per era (100 runs each) must reach an end state with no crash (GS-05). The reinforcement rate (attacks the province survived, with a reserve within 2 provinces, observable before the war ended) must reach `gs_reinforce_min` 0.80. Measured before the gate run: WW2 97%, Cold War 76%, modern 1/1; 88% aggregate.
- **Known balance issue (W6-class).** NATO wins about 90% of Cold War AI-vs-AI runs in about 6 turns, and the Cold War reinforcement rate on its own is under 80%. There's no requirement threshold for GS side balance yet.

## D049: Tactical release as an RTS card; supply scenario (GS-04, GS-01)

- **Tactical release.** New support type `tactical_nuke`: `nato_w79_tactical` (W79 8-inch, 1981) and `pact_3bv3_tactical` (152 mm), at 200 SP, one round, 120 s delay, a 400 m blast and a crater.
  - Refused with reason 7 unless `WorldState.NuclearRelease` ≥ 1.
  - A grand-strategy battle fought in RTS gets the release once tension reaches the tactical threshold (`GsBattle.NuclearRelease`, passed to `Soak.Fight`). Scenarios can set `nuclear_release`.
  - No gore: blast and crater only.
- **Supply.** New scenario `gs_supply`: with the US capital in Soviet hands (scenario `start.owners`), US armies lose 10% a turn while a supplied FRG army doesn't.
- **Crater recording** factored into `TerrainBuilder.AddCrater`.
- **Mutants:** `tactical_nuke_dud`, `gs_supply_free`.

## D050: Deterministic lockstep networking (NET-01, NET-02, FLY-07)

- **Lockstep session** (`sim/Net/Lockstep.cs`).
  - Each client runs its own `WorldState`. Each tick it sends its inputs for tick t + delay to every peer; an empty packet still confirms the tick.
  - Packets arrive after latency ± jitter or are lost. Unacknowledged packets are resent after a round trip.
  - A client steps tick T only when it holds every client's inputs for T, applied sorted by player and sequence.
  - Hashes are recorded per client every `hash_interval` ticks and compared once all clients have reported.
  - Replay is the confirmed input log. A reconnecting client re-simulates the log, then rejoins. Plain lockstep, so the others stall while it's away.
  - A test-only fault (`fault_client`) proves the detector catches divergence.
- **Harness** (`NetRunner`): an N-vs-N Cold War skirmish on 8 km Bench maps, one client per player, AI commanders under each player plus scripted moves; for `fly`, a Take Control and a stick input every second.
- **G4 scenarios.**
  - `net_4v4_clean`, `net_4v4_loss2` (2% loss) and `net_4v4_latency150` (150 ms, 30 ms jitter): each a 24,000-tick (20-minute) 4v4 with 8 clients.
  - `net_reconnect_replay`, `net_fly_lockstep` and `net_desync_detector`.
  - Measured: 0 desyncs in 240 hash checks per 4v4 run; replays match; 55,172 packets lost and recovered in the loss run; 0 ms stall at 150 ms with an 8-tick delay.
- **G4 runtime.** Each mutant now runs only its `must_fail` scenarios (`check_scenarios.py`). The unmutated suite still runs in full first. The network scenarios add about 7 minutes to the full run.
- **Not covered:** Steam lobbies, the deck screen and real sockets (NET-01) need the client and Steamworks.

## D051: Aircraft loadouts and threat warnings (FLY-06, sim half)

- **Loadout variants** are separate cards of one airframe (roster `VARIANTS`, with `variant_of` and a `loadout` design note):
  - `a10a_cbu87`: GAU-8, CBU-87 CEM, AIM-9;
  - `su25_zab500`: ZAB-500 incendiaries and S-8 rockets;
  - `su24m_kh58`: two Kh-58 anti-radiation missiles.
- **New weapons**, with service years for ERA-04:
  - `cbu87` (type `cluster`);
  - `zab500` (type `incendiary`: sets ground vehicles in the burst on fire);
  - `kh58` (type `arm`: targets only radar-directed AA).
- **Threat warnings.** When a radar-directed gun or a missile fires at a player-flown aircraft, the sim raises `ThreatWarning` (59): Value 1 for radar gunfire, 2 for a missile launch. The HUD presentation is G8 work.
- **G4.** Scenarios `fly_loadouts` and `fly_threat_warnings`; mutant `napalm_does_not_burn`.
- **Note.** Taking control activates the XO, and its seat ROE can override a scenario's hold-fire for the player's other units. The scenario keeps its AI A-10 out of reach.

## D052: Naval gunfire and precision rounds (SUP-01, Cold War)

- **Naval gunfire.** New weapons `naval_406` (Iowa class 16 in, reactivated 1982–88; CEP 0.4% of range) and `naval_130` (AK-130; 0.6%). New support options `nato_iowa_ngfs` (27 rounds at 20 km) and `pact_sovremenny_ngfs` (40 rounds at 15 km).
- **Precision rounds.** New support type `precision`: rounds land within `cep_m` of the aim point, and the call is refused (reason 8) unless a friendly unit within `designator_m` has line of sight to it. Options: `nato_copperhead` (M712, 1982) and `pact_krasnopol` (1985), each 4 rounds, CEP 3 m, designator range 3 km.
- **G4.**
  - `sup_naval_gunfire`: median miss of 54 impacts within ±10% of the 80 m data CEP; first splash exactly at the delay.
  - `sup_precision_rounds`: the 8-round median is asserted within 1–5 m around the 3 m CEP. The sample is too small for ±10%, and that's stated in the scenario. First impact exactly at the delay.
  - `sup_precision_needs_designator`; 3 mutants.
- **Era-appropriate gaps.** Loitering munitions and FPV drones arrive with the modern catalogue. WW2 artillery arrives with the WW2 roster.

## D053: Remaining Cold War air support (SUP-02)

- **New support types:**
  - `air_landing`: paradrop or air assault; `count` units of a roster `unit` land within `scatter_m`. Until airborne units are in the roster, the troops use the rifle-squad stats.
  - `gunship`: an orbit that hits the nearest visible enemy every `interval_ticks`;
  - `cruise`: one precise blast;
  - `escort`: a zone that cuts enemy AAM accuracy, `combat.escort_pct` 40;
  - strike runs gain an `incendiary` flag for napalm.
- **Options:**
  - NATO: AC-130H, BGM-109 Tomahawk, RQ-2 Pioneer, KH-11 pass, F-15C escort, UH-60 air assault.
  - Pact: Su-25 ZAB-500 run, RK-55, Tu-143, MiG-29 escort, VDV drop.
- **G10.** `check_era.py` also checks that an air-landing unit belongs to the option's era and side.
- **G4.** Scenario `sup_air_coldwar_more`; 5 mutants.
- **Still open for SUP-02:** gliders (WW2), and every option for the WW2 and modern eras.

## D054: WW2 1944 slice: US Army vs Wehrmacht (ERA-01 start; SUP-01/02/05 per era)

- **Research.** `assets_src/research/ww2_1944_roster_sources.json` holds 32 units. Every number is sourced, from Wikipedia vehicle and gun articles, armedconflicts.com (citing Hunnicutt's *Sherman*), airpages.ru and battleorder.org, all fetched 2026-10-09.
  - Ranges are stored as midpoints and marked as estimates.
  - Gaps are filled with design values, each marked.
  - The Opel Blitz speed isn't in its source and is a design value.
- **Generator.** `scripts/generate_roster.py` now builds two eras from `scripts/roster_ww2.py`.
  - Armor mode `W`: effective thickness = plate / cos(slope); front = 60% turret + 40% hull.
  - New kinds: `td`, `towed_gun`, `mortar_team`, plus a `move_class` override.
- **Penetration.** Tables follow the Wikipedia gun articles. Figures at 30° are converted to normal impact with the per-range ratio from the KwK 43 article, which tabulates both: ×1.15 at 100 m, rising to ×1.33 at 2,000 m.
- **Decks.** Eight WW2 decks, four income profiles per side (`Allies` / `Axis`). `check_decks` now covers every side that ships decks.
- **Support (era `ww2`).** 105 mm battalion barrages (both sides), US smoke, a P-47 strafe, F-5 photo recon, a Waco glider landing, a Nebelwerfer battery, a Ju 87 dive attack.
- **G4.** Scenario `ww2_armor_and_support`: Sherman front-on against a Panther fails to penetrate, side-on kills; Panther kills Sherman head-on; glider, dive bomb, era gate. Mutants `sherman_75_overpowered` and `glider_lands_nobody`.
- **Not yet.**
  - The UK and USSR (ERA-01 lists four WW2 factions) and the modern era.
  - WW2 battle balance: the cost model under-prices the heavy tanks, at 150 for the Tiger II against 110 for the Sherman. A WW2 soak and cost pass are needed.
  - WW2 art.

## D055: Modern 2026 slice: US Army vs Russian Ground Forces (ERA-01; SUP-01 drones; SUP-04 GPS)

- **Research.** `assets_src/research/modern_2026_roster_sources.json` holds 24 units, sourced from Wikipedia, ru.wikipedia (Abrams protection estimates), CRS IF12397 (M-SHORAD) and battleorder.org / US Army ROTC (squads), all fetched 2026-10-09. Protection figures are public estimates; gaps are marked design.
- **Tables.** `scripts/roster_modern.py` holds 37 weapons. The generator now builds three eras, and decks are filtered by era, since "NATO" exists in both the Cold War and modern rosters. Eight modern decks.
- **New mechanics:**
  - weapon `top_attack` (Javelin, TOW-2B): the strike meets top armor;
  - support `loitering` (Switchblade 600, Lancet-3): a top-attack strike on the most valuable visible vehicle;
  - support `fpv`: strikes that succeed with `fpv_success_pct` 60, or `fpv_jammed_pct` 20 inside enemy comms jamming;
  - precision `gps` (Excalibur, GMLRS, Tornado-S): no designator, but CEP × `gps_jam_cep_x` (25) inside an enemy GPS-jamming zone;
  - support `jam_gps` (Pole-21, US EW).
  - The GPS factor is a design value. The trend it follows is sourced: Excalibur efficiency fell from 70% to 6% under Russian EW (Wikipedia M982).
- **G4.** Scenarios `modern_top_attack_drones`, `modern_gps_precision` and `modern_gps_jammed`; 4 mutants.
- **Not yet:**
  - China (ERA-01 lists US/NATO, Russia, China);
  - modern art;
  - the FPV jamming check: covered by data and code, not yet asserted by a scenario;
  - modern balance soak.

## D056: Air support for every era (SUP-02, SUP-05)

- **WW2.** Added B-26 level bombing, P-47 napalm, an 82nd Airborne paradrop and a Fallschirmjäger drop. With D054's strafe, Ju 87 dive bombing, F-5 recon and Waco gliders, WW2 has every type SUP-02 lists that existed in 1944. Cluster munitions, SEAD, gunships, cruise missiles, drones, AWACS and satellites didn't.
- **Modern.** Added Tomahawk Block V and Kalibr cruise missiles, F-35A SEAD, a satellite pass, Orlan-10 recon, E-3G and A-50U AWACS, F-22 and Su-35S escort, the AC-130J gunship, and UH-60M and Mi-8AMTSh air assault. With D055's loitering munitions, FPV drones and GPS-guided precision, the modern era covers every SUP-02 type except gliders.
- **Catalogue.** 87 options across three eras. Each records its era, side, cost, charges, recharge, delay and reasoning.
  - G10: drift check plus era and side checks, including air-landing units.
  - G4: era gating (`sup_era_gate`, the WW2 scenario refusing a Cold War option).
  - G4: delays asserted on representative options of every type: first impact or arrival at the scheduled tick in `sup_offmap_economy`, `sup_naval_gunfire`, `sup_precision_rounds`, `sup_electronic_warfare`, `modern_gps_jammed`, and the air-landing count checks.
- **G4.** Scenarios `sup_air_ww2` and `sup_air_modern`; 2 mutants.

## D057: The remaining ERA-01 factions: WW2 British and Red Army, Cold War allies, China

- **WW2.** British Army and Red Army units are in `scripts/roster_ww2.py`, including each army's own recon (Daimler armoured car with the 2-pdr, T-70, BA-64B). Allied decks filter by nation: 4 US, 4 UK and 4 Soviet decks.
- **Cold War 1989.**
  - `assets_src/research/cw1989_allies_sources.json` holds 38 units: Bundeswehr, BAOR, the French Army, the NVA and the Polish People's Army, each with its own supply truck. West Germany adds the PAH-1 and Alpha Jet. Sources are Wikipedia, fetched 2026-10-09.
  - `scripts/roster_cw_allies.py` holds the tables. Penetration of the 1980s NATO tank rounds, MILAN 2 and HOT 2 is not in the sources and is a design value.
  - Thermal sights are given only where fielded by 1989: Leopard 2A4 (WBG-X), Challenger 1 (TOGS) and MILAN with MIRA.
  - `NATION_VARIANTS` copy a sourced unit into an army that really fielded it, without new stats. Examples: the M109, M113 and M577 in the Bundeswehr, M109s in BAOR, and the 2S1, ZSU-23-4, Mi-24, Mi-8, MiG-29, Su-22M4 and Ural-4320 in the NVA and Polish forces.
- **Modern.**
  - China (PLA Ground Force) has 11 sourced units in `modern_2026_roster_sources.json`, including the HJ-12 with top attack. China is a new side, "China".
  - Modern squads ride their carriers (Stryker 9, BTR-82A 7, ZBL-08 7 dismounts), as the Cold War squads do.
  - US squads carry 2 M136 AT4. Squad issue is standard; the penetration is a design value.
- **Decks.** Every deck now names its era and nation, and `check_decks` (G10) adds two rules:
  - every card is of the deck's era and nation;
  - every faction (side, era, nation) ships all four income profiles.
  56 decks in total.
- **Cost model (`scripts/balance/cost_model.py`).** The US and Soviet 1989 decks and the US and German 1944 decks keep their exact cards and costs, so the G5 soak is unchanged.
  - The 1.15 NATO premium (W6 pass 1) priced US thermals against 1989 Soviet optics. It now applies only to US 1989 units. Other NATO armies and the modern era don't get it.
  - `ERA_COST_SCALE`: modern ×0.75. Modern unit values ran about 35% over 1989 on the same income, and a modern AI fielded about 17 units in 30 minutes.
  - `NATION_COST_SCALE`, set from the faction soak: West Germany 0.65; UK and France (1989) 0.7; East Germany 0.9; China 0.9; US (modern) 0.8. The allied discounts are large because those decks have fewer card types than the US and Soviet decks, so the AI has fewer options for the same budget.
  - `ARTILLERY_CARD_CAP` 4: no artillery card holds more than 4 guns. A side's only artillery card used to take the whole artillery budget (8 × 25-pdr), and the AI's guns-first rule spent it before buying any manoeuvre unit.
- **Faction soak** (`SOAK_BUYS=1` prints each AI's buys and objective captures). Balanced decks, 80 matches on fresh seeds (110001+), player 0 listed first, band 35–65%:
  - **1989:**
    - US vs USSR 41/39
    - West Germany vs USSR 42/38
    - UK vs USSR 46/33
    - France vs USSR 47/33
    - US vs NVA 41/39
    - US vs Poland 46/34
  - **2026:**
    - US vs Russia 30/50
    - China vs Russia 34/46
    - US vs China 38/42
  - **1944:**
    - US vs Germany 43/37
    - UK vs Germany 34/46
    - USSR vs Germany 20/60: **out of band**
- **Red Army 1944** (superseded by D059). Cost has no effect, and adding the PTRD-41 and 82-BM-37 made it worse (14/80): they split the infantry and artillery budgets. A WW2 artillery cap of 2 moved the Soviets to 25/80 and the UK down to 28/80, both within the noise of 80 matches, so it wasn't kept. The open item is a WW2 AI and deck pass: Soviet doctrine (tank riders, mass) isn't modelled.
- **G4.** Scenarios `era_factions_coldwar` and `era_factions_modern`, with 6 mutants:
  - sourced armor duels: MILAN 2 vs a T-72M1, Challenger 1 vs a T-72M1, HJ-12 top attack vs a T-90M, and the Type 99A's gun vs a T-90M front;
  - two nation-deck AI commanders field armies.
  G5 still gates only the US–Soviet 1989 matchup.
- **Not yet:**
  - art for the new units;
  - faction-specific voices.

## D058: Gates fail on any failing command, not only the last (G2 hid 17 test failures)

- **Symptom.** `verify.sh` reported G2 pass while `dotnet test` failed 17 tests.
  - 16 grand-strategy and net scenarios were fed to the battle `Scenario.Load` by `BattleScenarioTests`.
  - A comment in `Supply.cs` contained the word "double", which `SourceScanTests` rejects.
  - Since the GS and net scenarios were added (D046–D050), every "G2 pass" in this session was wrong on tests. Coverage still passed.
- **Cause.** `run_gate` ran each gate as `if "$@"`. Bash disables errexit in a conditional, so only a gate's last command counted (for G2, `check_coverage.py`). G1, G10 and G12 had the same hole.
- **Fix.**
  - `run_gate` now runs `( set -e; gate )` as a plain statement and tests `$?`. An AND-OR list would disable errexit again, subshell included. A scratch script confirmed a mid-gate `false` fails the gate.
  - `BattleScenarioTests` runs battle scenarios in the battle theory, and GS scenarios in their own theory on `GsRunner`.
  - Net scenarios stay in G4 only. Each runs 8 lockstep clients for minutes, and under coverage instrumentation G2 ran past 30 minutes. A new 1v1 lockstep unit test (300 ticks, 5% loss, replay must match) keeps `sim/Net` under test in G2. The net scenarios had never run in xUnit, because they failed at load.
  - The `Supply.cs` comment was reworded.
- The same hole hid a G12 failure: `dotnet format` whitespace in `sim/Battle/Rules.cs`. It's fixed: `build/reports/20261009T134347Z-gates-G12.md`. G1, G3, G10, G11 and G14 pass under the new rule.
- This tightens a check; no threshold changed.

## D059: Red Army tank riders (ERA-01 balance)

- Soviet rifle squads (1944) ride the T-34-85 as tank desant: they move at the tank's speed until they fire, are hit or are suppressed. This uses the MEC-09 carrier rule, the same as Cold War and modern squads (`CARRIERS_WW2` in `scripts/roster_ww2.py`).
- Measured on 80 matches on seeds 110001+ against the Wehrmacht balanced deck:
  - tank riders alone took the Red Army from 20/80 to 46/80;
  - a second set (120001+) gave 60/80, 66% combined, so it overshot.
- `NATION_COST_SCALE[("Soviet Union", "ww2")] = 1.15` gives 35/80 and 40/80: 75/160, 47%. At 1.3 the result was 75/160 too, with wider spread; 1.15 is the smaller change.
- All twelve faction pairs are now inside 35–65%.

## D060: G13 UX smoke on the exported build; the game runs the Phase-1 battle sim

- **Harness.**
  - `--ux <script> --ux-log <file>` starts `UxDriver` (`game/Ux.cs`), which presses named controls (wildcards allowed), selects options, sends keys, sets time compression and waits for logged events.
  - Screens log game-state events (`UxLog`) as JSON lines: screen shown, deck chosen, unit deployed, control taken or returned, offers, battle over, debrief, campaign result, settings. They never log what the script did, so a match proves the game reached that state.
  - `scripts/check_ux.py` runs `build/linux/SixActual.x86_64 --headless` on each `verify/ux/*.json` in order, with one shared, freshly wiped XDG user dir so persistence can be tested across processes. It matches each script's `expect` list in order. Its self-test proves each expectation kind can fail.
  - `gate_g13` exports Linux first, fails on any `ERROR:` or exception in the export log, then runs the scripts. The `ERROR:` check catches a silent failure seen while building this: a .NET publish failure leaves the old DLL in the build while the export exits 0.
- **Game.**
  - The battle view is now `SkirmishView`, on `sim/Battle` (`Skirmish.Create`, shared with `Bench`). The Phase-0 prototype view (`BattleView.cs`) is removed.
  - The sim's JSON ships in the export (`include_filter`, synced by `scripts/sync_game_data.py`, drift-checked in G10) and is copied to `user://` at startup, because the sim reads files with System.IO.
  - Player orders are stamped with the current tick: the sim applies an order only on the tick it names.
- **Screens.**
  - Main menu; deck choice by era, with the Field Requests toggle; the battle with card bar, Take Control and return, the Field Requests panel, pause, speed and surrender; debrief.
  - Campaign (MODE-02) on the battle sim, saved to `user://campaign.json`.
  - A local lobby. Its placeholder transport is labeled as such; Steam lobbies come with NET-01.
  - Settings (UX-02) saved to `user://settings.cfg`.
  - Visuals are placeholder boxes, labeled in-game.
- **MEC-11 cards** (`game/CardInfo.cs`): a role glyph (a placeholder until the icon set is drawn), the range bar (longest weapon, full at 4 km) and a "use it for" line from role and armament.
- **Scripts (7):**
  - `ux_01` skirmish smoke (UX-01, MODE-01, FLY-01, MEC-11); `ux_02` Field Requests (MEC-06); `ux_03` campaign (MODE-02); `ux_04` lobby (NET-01).
  - `ux_05` and `ux_06`: settings save, then load in a new process (UX-02).
  - `ux_07`: skirmish in WW2 (UK) and modern (China).

## D061: G13 coverage for input, accessibility, deck builder and credits

- **Input** (`game/Controls.cs`). Named actions with key and pad bindings, both remappable: a captured key, pad button or stick direction becomes the binding. Bindings are saved in `user://settings.cfg` and re-applied at startup.
  - A HOTAS stick reports as a joypad axis, so it binds the same way.
  - Mouse-aim steers a flown aircraft toward the pointer while the right button is held and no stick input is given.
  - The first stick input of each flight is logged with its source (keys, joypad or mouse).
- **Accessibility.**
  - Palettes recolor the sides, using Okabe-Ito pairs per color-vision deficiency.
  - Subtitles show the XO's radio lines (Take Control and return) when on. Voice lines arrive with AUD-01.
  - Slow time runs at half speed; pause.
- **UX-01.** A deck builder starts from a shipped deck, clamps each card between 0 and its `max_count`, and saves to `user://decks.json`. `Skirmish.Create` takes custom decks after the shipped ones, so shipped deck indices don't move. Credits are generated from `ASSETS.md`.
- **Driver and checker.**
  - New driver steps: `joy_button`, `joy_axis`, `mouse_aim`. The pointer is converted to window coordinates, because the UI-scale setting changes the canvas size; a 60/40 request read back 27/52 before this fix.
  - New checker expectations: `range`, `forbid`, `absent`, each with a self-test case.
- **Negative control.** With rebinding made a no-op, `ux_05` fails on the saved key.
- **Review.** A Haiku review of the new game code found two unchecked file-open results, two unguarded event indexes and one inconsistent navigation call. All five are fixed; G12 and G13 pass.
- **Scripts:** `ux_08_rebound_controls`, `ux_09_credits_deck_builder`, `ux_10_flight_inputs`, plus additions to `ux_01`, `ux_05` and `ux_06`. 10 scripts in total.

## D062: G13 for commanders and fog; fixed logical resolution for scripts

- **Commander panel** (CMD-01/02):
  - The 8 seats appear with live counts. Arm a seat, then drag a rectangle on the map: every friendly ground unit inside gets an `AssignSeat` order.
  - The `seats` event reads the counts back from the sim after the orders apply, so the check sees sim state, not UI intent.
  - Left-click selects a unit; right-click moves it if it's under the player's control.
- **Fog** (SIM-05). Every 600 ticks the view logs enemies alive, spotted (`Sight.Visible`) and drawn. The sample is taken after the views sync on that frame: sampling mid-frame at 400 ticks per frame showed 7 spotted against 5 drawn, because the views were a frame stale.
- **Fixed resolution.** A headless Godot window is 64×64, and `--resolution` is ignored. So under `--ux` the root runs viewport stretch at 1280×720, and the driver maps canvas positions to window positions through `GetFinalTransform()`, as Godot maps real input. Normal play is unchanged.
- **Driver and checker.**
  - New driver steps: `drag`, `click`, and `click_unit`, which clicks a friendly unit's projected position.
  - New expectation key `equal_fields`.
  - `min_count` now resumes after its first match, so a later expectation on the same event can still match.
- **Negative control.** Drawing every enemy fails `ux_11` (shown ≠ spotted).

## D063: The strategy screen runs the GS sim (GS-01 in G13)

- `game/StrategyScreen.cs` replaces the Phase-0 strategy prototype:
  - the theater map per era: provinces in owner colors, neighbour links, army strengths, a named button per province;
  - the player's faction (the first at-war faction of side 0) with production, manpower, tech and research;
  - armies with their commanders;
  - orders: move (select an army's province, then the target), build in the capital, and research;
  - end turn, with the AI playing every other faction;
  - save and load of the sim's JSON state (CMD-08), with an FNV-1a hash logged so a load can be checked against its save.
- Battles auto-resolve on this screen. RTS battles inside grand strategy are verified headless (GS-02).
- **G13 `ux_12_strategy`.** It moves the Fulda army to Kassel, funds research and builds; the next turn shows research 4 and a fourth army. Save, end turn, load gives the identical state hash. The WW2 theater opens.
- **New checker key** `same_as`: a field equals the same field of the latest earlier event of another type.
- **Observed, not fixed here.** In this script NATO wins the Cold War theater on turn 4. That's the open GS balance item (NATO wins about 90% of AI runs); GS-05 measures it.

## D064: Skirmish options and annihilation victory (MODE-01)

- **Sim.**
  - `WorldState.VictoryType` (hashed): 0 conquest, 1 annihilation. Under annihilation, a team with no ground combat units left (alive, not routed, not supply) loses, checked from `rules.victory.annihilation_grace_ticks` (1200, one minute) so both sides can deploy. Conquest points still run: reaching `win_points` also ends an annihilation battle, and points decide the time-cap fallback (corrected in D073).
  - Scenario key `"victory"` and battle check `"winner"`.
  - G4 `victory_annihilation` (no winner at 1150, player 0 at 1300) and `victory_conquest_default` (the same fight, no winner), with mutants `annihilation_never` and `annihilation_no_grace`.
- **`Skirmish.Options`.**
  - 1 to 5 players per team: even players are team 0 from the south, odd players team 1 from the north, and every player but the human is an AI XO.
  - Starting DP, an income-profile override, victory type, time of day, and weather (which also sets whether aircraft are grounded).
  - A 1v1 keeps its old spawn points.
- **Game.** The deck screen has option buttons (Size, Points, Income, Victory, TimeOfDay, Weather). `battle_started` logs each option as the sim world holds it, and the placeholder scene darkens at night and fogs in fog, rain and snow.
- **G13 `ux_13_skirmish_options`.** A 5v5 with 2000 DP, juggernaut income, annihilation, night and fog: 10 players, 9 AI XOs, aircraft grounded. It plays to the end.
- **G5.** `--soak N --teams 5` (5v5, Cold War balanced decks). `analyze_soak` checks that 5v5 matches are crash-free and decided, and that every one of the 10 players deploys: 4 matches in quick mode, 20 in full.
- **Observed, not gated.** The Warsaw Pact won 4/4 of the first 5v5 matches. The 1.15 NATO premium was measured on 1v1, so 5v5 balance is open.

## D065: Branching campaigns with voiced briefings (MODE-02)

- **`data/campaign.json`** (generated by `scripts/generate_campaign.py`; G10 `--check`).
  - Each of the three eras is six levels deep on two tracks: level 1 has one operation, and levels 2–6 each have an advance and a fallback operation. A win leads to the next advance operation, a loss to the next fallback one, so every path is exactly six operations and both outcomes change what comes next.
  - Each operation carries its decks, victory type, time of day, weather, seed and a briefing.
  - The check proves every path is 6, every operation is reachable, decks match the era with opposing sides, win and loss differ, and every briefing has its audio. A self-test catches a removed branch.
- **Voices** (D020, local).
  - `scripts/generate_briefings.py`: Qwen3-TTS CustomVoice through `parish-media` (Ryan for Cold War and modern, Aiden for WW2), then two-pass ffmpeg loudnorm to −16 LUFS / −1.5 dBTP. A busy GPU is waited out, never interrupted.
  - Result: 33 lines, measured −16.0 to −16.3 LUFS.
  - `scripts/qa_briefings.py` transcribes each line with the local faster-whisper and scores word recall against the text: mean 0.985, min 0.929, floor 0.85. Place names (Fulda) are the usual misses.
  - Not listened to by a person; Donovan's ear is the final check.
- **Game.** The campaign screen follows the graph, with three campaigns. The briefing screen plays the line with its text, and deploy runs the operation's battle. Progress is saved in `user://campaign.json`: the sim's CMD-08 state, plus the current operation and the path taken.
- **G13.**
  - `ux_03_campaign`: the voiced cw1 briefing, then surrender; the loss leads to cw2f, whose briefing is voiced.
  - `ux_03b_campaign_resume`: a new process resumes at cw2f.
- **Commander battle voices** (radio reports, CMD-07, AUD-01) remain open.

## D066: Radio dialogue, the voice pipeline and G9

- **Matrix** (`scripts/generate_dialogue.py`). `dialogue/lines.csv` holds 117 lines: the XO and 8 commanders, each with the triggers its role produces in the sim, worded per era. `audio/voices.json` holds the casting: a designed voice per speaker (Qwen3-TTS VoiceDesign, description and seed) and fictional commander names per era.
- **Pipeline** (`scripts/voice_lines.py`, for briefings and dialogue).
  - Local synthesis through `parish-media`; a busy GPU is waited out, never interrupted.
  - Two-pass ffmpeg loudnorm to −16 LUFS / −1.5 dBTP. If a line still misses by more than 1 LU (short, peaky lines under the peak ceiling), it is peak-limited and normalized again.
  - Edges are trimmed to under 300 ms of silence.
  - Transcription with the local faster-whisper, scored to BRIEF W3: ≥ 0.95 word accuracy, at most 2 retries with new seeds.
  - The scorer normalizes number words, plural stems and compounds split or joined, and accepts the spoken variants in `dialogue/aliases.csv` (corps → "Kors", Fulda). Real misses still fail: "Airtubingofuel" scores 0.33 and was regenerated.
  - Result: 150 lines, mean 0.998, min 0.950.
  - `audio/manifest.json` records every file: text, seed, prompt hash, LUFS, true peak, duration, edge silence, word accuracy.
  - Two tool facts found along the way: the voice tool never overwrites, so raw files are named per seed; and `--speaker` must follow `--mode <mode>`.
- **G9** (`gate_g9`): the matrix drift check, then `scripts/check_audio.py`. It checks coverage of every dialogue row × variant and every briefing, re-measures loudness (dialogue −16 ± 1.5 LUFS), true peak (≤ −1 dBTP), silence and clipping, duration (0.4–60 s), edge silence (≤ 300 ms) and word accuracy (≥ 0.95), and fails a manifest that has gone stale against its files. A self-test covers silent, clipped, quiet and short synthetic files. SFX and music checks and the §D7 spectral checks wait for SFX and music to exist.
- **Game radio** (`game/Radio.cs`, CMD-07, AUD-03 radio chain).
  - Sim events map to the commander who would report them: assaults, escalations, requests, withdrawals by seat; fire missions and battery fixes to the fires seats; CAS check-ins to the air seats; resupply; the XO's handover, phases and objective swings; contact to the seat of the nearest friendly unit.
  - Playback: lines play on a "Radio" bus (band-pass at 1.8 kHz plus light clipping) from a priority queue. Each speaker speaks at most every 30 s and repeats a report at most every 2 min of game time; urgent lines are exempt.
  - Pacing is in game time (a line holds the net for its length in ticks), so it matches real time at normal speed, compresses consistently under fast-forward, and works headless, where the audio player never reports a stream's end.
  - Display: a text log names the commander, and a map ping appears at located events. Commanders are listed with a trait from their seat's intent settings.
  - Godot imports `.csv` files as translation tables, so the matrix ships as `dialogue_lines.json`.
- **G13 `ux_14_radio`.** Commanders are named, the first report pings the map, six or more voiced reports play on the Radio bus, including the XO's handover. Fire missions start reporting once the XO has the battle.
- **Not done** (CMD-07): portraits need the style-board sign-off; commander voices are designed, not approved by ear.

## D067: Battle music, generated locally, with dynamic layers

- **Stems** (`scripts/generate_music.py`): ACE-Step 1.5 through `parish-media`, 9 stems (calm, tension, combat per era), 60 s each, instrumental.
  - Two-pass loudnorm to −18 LUFS / −1.5 dBTP, the AUD-02 music target.
  - Loop points are searched: the loop start is where the spectrum is most stable across 50 ms either side, and the loop end best matches the audio just before it. The end then crossfades into the 3 s before the loop start, so the wrap is continuous source audio at a stable moment.
  - Encoded as Vorbis q5.
  - Seams: spectral correlation across the loop point 0.964–0.996, wrap jump at most 2.7× the typical sample step. Before the search, with a fixed loop point, correlation was 0.41–0.98.
- **Checks** (G9, `check_audio.py`):
  - −18 ± 2 LUFS, ≤ −1 dBTP, not silent or clipped, 20–90 s;
  - spectral flatness < 0.5 (white noise is ~1; the dense modern combat stem is 0.36);
  - no vocals: at most 4 transcribed words beyond faster-whisper's stock hallucinations on instrumental audio ("Thanks for watching!", "Thank you", "music"). That's a heuristic, not proof; a human listen is the real check;
  - seam correlation ≥ 0.9 and wrap jump ≤ 8×.
  - The self-test proves white noise fails tonality and a stepped loop fails the seam check.
- **`game/MusicDirector.cs`** (AUD-03 dynamic layers). The three stems loop together and crossfade by intensity: shots plus 5 per kill over the last 30 s of game time. Tension starts at 4 and combat at 20. It steps down only below half of those, after the first version flapped between calm and tension. Fades run in game time.
- **G13 `ux_15_music`.** Three stems load; calm leads; combat when the fighting peaks; it steps back down as the fighting eases.
- **Still missing for AUD-02 and AUD-03:** SFX, and with them distance attenuation and occlusion.

## D068: Procedural sound effects and spatial audio

- **`scripts/generate_sfx.py`** (D020: procedural synthesis, no paid service): 51 files synthesized with numpy at 48 kHz.
  - Rifle shot, 5-round MG burst and tank cannon, each in near, mid and far versions (far is low-passed with a longer tail), 4 variants each.
  - Small, medium and large explosions, 4 variants each; the large ones have a strong sub-100 Hz boom.
  - Diesel and turbine engine loops, built from integer cycles and circular filtered noise so they loop seamlessly.
  - A UI click.
  - Peaks at −3 dBFS.
- **G9** (`check_audio.py`, BRIEF D7):
  - SFX peaks ≤ −1 dBTP, not silent or clipped, 0.03–10 s;
  - far versions carry under half of the near version's energy above 4 kHz;
  - large explosions put ≥ 20% of their energy below 100 Hz;
  - no two variants correlate ≥ 0.95;
  - engine loops pass the seam check.
  - The self-test proves the identical-variant, bright-far and missing-low-end rules fail.
- **`game/SfxPlayer.cs`** (AUD-03).
  - Shots (family by weapon type), impacts and vehicle kills play as 3D sounds at their source, from a 16-player pool with at most 4 new sounds per frame.
  - The version (near < 300 m, mid < 1500 m, far) is chosen by distance to the listener, and inverse attenuation fades it further.
  - Occlusion uses the sim's own `Sight.HasLine` from the listener's ground point (terrain, forest, hedges, smoke, buildings): an occluded sound is low-passed at 900 Hz and lowered 6 dB.
- **"Latest alert" (Home):** jumps the camera to the last map ping.
- **G13 `ux_16_sfx`.** From the start line the distant fighting is far and occluded; after a jump to the alert, nearer versions play. In this run: 37 near, 97 mid, 274 far, 269 occluded, none missing.
- **Not yet** (DET-08): distance delay, reverb zones, ambience beds, engine loops on vehicles.

## D069: CEP and delay scenarios for WW2 and modern artillery (SUP-01)

- Nine G4 scenarios, `fires_cep_<era>_<unit>`: WW2 leFH 18, M2A1 105 mm, 25-pounder, ZiS-3, GrW 34 and 81 mm M1 mortars; modern M109A7, 2S19M2 and PLZ-05.
  - Each has six guns firing 30 rounds at 75% of maximum range.
  - Median miss must lie within ±10% of the data CEP (`cep_permille` × range).
  - First impact must be within ±5 ticks of setup (200) plus flight time, measured from the nearest gun. Guns stand 10 m apart, and at mortar speed (170 m/s) the 50 m spread is 6 ticks. The first draft predicted from the farthest gun and failed for exactly that reason.
- GPS-guided rockets (GMLRS, Tornado-S; CEP 0–1‰) are already covered by `modern_gps_precision` and `modern_gps_jammed`.
- **Mutants:** `lefh18_wider_than_data`, `us_81mm_wider_than_data`, `m284_slow_shells`.

## D070: Tutorials, battle support controls and a clickable battlefield (UX-03)

- **Tutorials** (`game/Tutorial.cs`, `game/TutorialScreen.cs`).
  - Command basics: call, select, move, assign to a commander.
  - Support: an off-map strike, then a fire mission from the player's own battery.
  - Take Control: phase B with a fast clock, call an aircraft, fly, hand back.
  - Each step completes on the game-state event it teaches (through a `UxLog.Logged` hook), so a tutorial advances only when the player really did the thing.
- **Battle support controls (MEC-03 in the UI).** A Support panel lists the off-map options for the player's era and side with SP costs; arm one, then click the target. A Fire mission control works for a selected own battery. The view logs support called, arrived and refused, and fire missions.
  - `Skirmish.Options.StartSp`.
  - `Skirmish.Create` now sets the world era, which support orders check. It had been left at the default.
- **HUD layout.** The card bar used to fill four rows across the middle of the screen and swallowed clicks meant for the map; a real player would have hit the same wall. It's now one scrolling row along the bottom.
  - Layout containers ignore the mouse, so only buttons catch clicks.
  - The radio log moved to the top left, and the subtitle and tutorial panel sit above the card bar.
- Card buttons carry the unit class: `Card_<i>_<kind>_<air|ground>`. Existing `Card_*_ground` patterns still match.
- **G13 `ux_17_tutorials`** runs all three tutorials to completion, step by step.

## D071: Sound detail per BRIEF D7 (DET-08)

- **Synthesis** (`scripts/generate_sfx.py`, 80 files).
  - Weapons gain mechanics layers: bolt clicks, belt links, a breech clank. Explosions gain debris grains.
  - Far versions gain a low rumble. Reverb tails are low-passed with their band: the first sheet showed broadband hiss in the far tail, because the impulse-response noise was unfiltered.
  - The FFT low-pass is zero-padded: unpadded, it wrapped ringing to the buffer start and drew click lines on the spectrogram.
  - Non-loop files get a 1 ms fade-in.
  - New: supersonic cracks, footsteps per surface (dirt, grass, gravel, concrete; 4 variants each), radio squelch tails per era, and seamless 8 s ambience beds (wind, birds, insects, rain, fire crackle, distant artillery rumble). Ambience events stay clear of the loop point.
  - A design entry per family in `audio/design/<family>.yaml`: purpose, references, layers, bands, variants, loop points, priority, maximum concurrent.
- **Game** (`SfxPlayer`, `AmbienceDirector`, `Radio`, `MusicDirector`).
  - Bands are the brief's: near < 50 m, mid 50–500 m, far beyond. The listener is the camera's ground focus point.
  - Sound is delayed by distance ÷ 343 m/s, scheduled on fractional sim time, so at real-time speed it starts within a frame. Measured: 0–7 ms error at 4.8–6.8 km.
  - A round passing within 30 m cracks overhead.
  - Concurrency limits: weapons 8, explosions 4, cracks 2, culling the farthest.
  - Reverb zones from the sim terrain: interior, town, forest, valley, open.
  - Ambience: wind; birds by day, insects at night; rain; fire near recent wrecks; rumble from far impacts. Wildlife falls silent in combat, starts recovering after 2 minutes of quiet and is back about a minute later.
  - The music ducks 6 dB under radio. Radio chains vary by era (WW2 narrow and saturated, modern wide and clean), with the era's squelch tail.
  - The radio log now reports the real bus (`Radio_<era>`) instead of a fixed "Radio" label; `ux_14` updated.
- **G9 additions.**
  - A design entry and a spectrogram sheet (near, mid, far side by side, via ffmpeg `showspectrumpic`) per family.
  - An audition page, `build/audio_review/index.html`, listing every SFX file, the music and 20 sampled lines.
  - At least 4 variants for rifle, MG, explosion and footsteps.
  - The seam measure is now band-energy correlation (30 log bands). Raw FFT bins flag a seamless noise loop, because two windows of the same noise have equal band energy but uncorrelated bins. Music loop points are searched with the same measure; all seams ≥ 0.924.
- **G13 `ux_18_sound_detail`** (night): insects plus wind; a reverb zone; music ducked under radio; the radio on `Radio_coldwar` with squelch; wildlife silenced in combat; 20 real-time sounds each within 17 ms of distance ÷ 343 m/s.
- Footsteps are not yet played: infantry has no animated feet (DET-03/04).

## D072: Spotting made 3x cheaper; the G6 tick budget tightened to 5 ms

- **Symptom.** G6 failed at a tick p95 of 8.15 ms against its 8.0 limit. Recent runs had crept up to 7.1–8.0 ms as features were added. Spotting cost 3.1–3.6 ms per tick, and other work was running on Parish1.
- **Measured fixes.** Every change is a pure shortcut: the 2000-entity bench world hash stays `c282297aea0879ab` throughout.
  1. `Sight.Spot` skips observer–target pairs beyond the largest possible detection range: optics at `BattleRules.MaxOpticsPct`, the highest the rules allow (cached), plus the moving-target penalty, or the firing-reveal range. Spotting went 3.1 → 3.0 ms.
  2. `Structures.Blocks` skips its cell walk when the sight line clearly misses every building footprint padded by two cells. Little gain on this map.
  3. `Sight.HasLine` computes each sample's cell once (it had been three `CellOf` divisions) and skips the per-sample smoke scan when no smoke is active. Spotting went 3.0 → 0.90 ms, tick p95 7.3 → 3.1 ms.
- **Ratchet.** `verify/thresholds.toml` `tick_p95_ms` goes from 8.0 to 5.0. That's a tightening, which needs no approval; it leaves headroom for machine-load noise and fails on a regression back toward the old cost.

## D073: Fixes from the adversarial pre-merge review

Three independent reviewer agents (sim determinism, gate integrity, data and licensing) all rejected the first merge candidate. Fixed:

- **Teammates fired on each other in N-vs-N skirmishes** (reproduced: 99–168 friendly shots per 2v2 match).
  - `Sight.Spot` and `Combat.SpottedLists` now test the *team*, not just the player.
  - Per-player masks and lists are sized to the real player count (`Sight.PlayerSlots`, up to 31); before, players 8 and 9 in a 5v5 aliased players 0 and 1.
  - Locked by the unit test `Teammates_never_spot_or_fire_on_each_other_in_a_2v2`, and by a `teammate_fire` column in `--soak --teams`, which G5 requires to be 0.
  - The 2-player bench hash was unchanged by the fix. The earlier 5v5 balance figures (D064: Warsaw Pact 4/4, NATO 29/80) were taken with the bug and are void.
- **G5 "matches end before the time cap" could not fail.** At the cap the rules still award the match on score, so "no winner" never happened. Capped matches are now counted by tick, and 18% of 1v1 matches had been reaching the cap.
  - `victory.points_per_minute` goes from 6 to 10. Measured on 200 matches: 0 capped, median 24 min, NATO 113/87; winners at 6 and 8 points per minute were identical, so it changes pace, not balance.
- **G13 `sfx` cutoff and volume** were derived from the occlusion flag, not read from the player. They're now logged at play time from `AudioStreamPlayer3D`.
- **G9 trusted recorded scores.** Manifests now carry each file's SHA-256, and `check_audio` fails a file that differs from its manifest.
- **`check_ux`** now requires the field to exist for `forbid`, `same_as` and `equal_fields`, and fails on engine errors in the build output. Its self-test covers those cases, plus `nonempty` and the every-event rule of `min_count`, which also crashed when it had no hits.
- **`ux_01`** asserts a numeric range on every card.
- **Scenarios** with no checks or no seeds now fail.
- **Sim:**
  - `Skirmish.Create` looks up the era after validating the decks.
  - `MaxOpticsPct` bounds weather × time-of-day as a product.
  - Rules and roster reject negative stealth values, which the range bound relies on.
  - `WorldHash` covers the remaining mutable fields: fuel remainder, home, path state, mission battery and mount, shell kind and shooter, flare radius and start, battery-fix state, player team, deck, stock and spawn.
- **Repo:**
  - `.gitignore` covers `__pycache__`, `*.pyc`, `*.blend1`, `assets_src/work/` (scripted meshes are rebuildable) and debug masks.
  - Sidecar paths are made portable (no home-directory paths).
  - A look-only citation was removed from `data/assets/t80bv.json`.
- **Fallout of the stricter checks.**
  - The engine-error check caught "N resources still in use at exit" in every G13 script. The driver now frees every screen and quits a frame later; no more leak reports.
  - Shorter matches cut the AA cost sample to 7,690 points, under its 8,000 floor. The air-war soak now runs 3× `n_single` matches, the same floor with more data.
- **Not changed.** GS-05 counts a turn-limit stalemate as an end state, the game's defined end. Weak sources for the UK and Soviet 1944 rifle squads are noted for a later research pass.

## D074: PERF capture mode; PERF-02 and PERF-01 numbers are preliminary

- **What.** `--perf <seconds> <out.json> [--res WxH]` (`game/PerfCapture.cs`) runs a rendered 5v5 AI battle in a real window at real time, vsync off, with an off-map barrage every 30 s. `scripts/check_perf.py` runs it and applies `verify/thresholds.toml [perf]`: `soak` for PERF-02, `resolutions` for PERF-01 numbers, `evaluate <capture> --log <log>` to re-check a capture. It reads `parish-media status` first and refuses on battery or a busy GPU. It deletes any old capture before running, so a stale file cannot pass.
- **Measured honestly:**
  - The tiling window manager ignores requested window sizes. The 3D world therefore renders into an offscreen viewport at exactly the requested resolution, with main-window 3D off. Video memory scaling (110 / 160 / 295 MB at 1080p / 1440p / 4K) confirms the larger render targets are real.
  - Godot's delta smoothing snapped `delta` to 1/720 s, so frame times are wall-clock (`Time.GetTicksUsec`). GPU time comes from `ViewportGetMeasuredRenderTimeGpu`.
  - The static-memory monitor reads 0 in release builds, so memory is process RSS from `/proc/self/statm`.
  - The first 20-minute soak failed memory (13.6% growth). The cause was the capture itself: it stored every frame time, about 2.7 M doubles plus list-growth copies. A forced-GC census showed the live heap rising in 4 MB and 8 MB doubling steps, while Godot node and object counts stayed flat and the sim alone stayed flat (31.6 → 32.5 MB over 24 000 ticks). Frame and GPU times now go into preallocated 0.01 ms histograms. The forced-GC probe was removed afterwards, since it distorts what it measures.
  - After review, the worst frame and the 100 ms count cover every frame, warm-up included. Percentiles still skip the first 10 s.
- **PERF-02 is not verified.** The adversarial review was right that BRIEF G6 measures PERF-02 in the benchmark scene (the PERF-01 scene: 2,000 entities, full VFX), which does not exist yet, and that G6 does not run `check_perf.py`. The skirmish-scene soak is preliminary evidence (`build/reports/20261010T004734Z-perf.md`, built before the review fixes): 20 min at 1440p, worst frame after warm-up 61.9 ms, 0 frames ≥ 100 ms, RSS growth 1.74% after 5 min.
  - The frames over 33 ms land on the same sim ticks in every run (2761, 2881, 2994, 6681, 8701, 10441), so they are deterministic heavy ticks, worth a profile.
- **PERF-01 stays todo** for the same reason. With placeholder meshes, no full VFX and about 215 units, frames take 0.3–3 ms and the GPU renders in 0.13–0.47 ms at any resolution.

## D075: ERA-03 battle maps from the Copernicus DEM GLO-30

- **Allowed by Donovan** ("Allow Copernicus DEM"). Licence: free, commercial use allowed, attribution required. Because the data is modified, the notice is "produced using Copernicus WorldDEM-30 © DLR e.V. 2010-2014 and © Airbus Defence and Space GmbH 2014-2018 provided under COPERNICUS by the European Union and ESA; all rights reserved", plus the licence's no-liability sentence. Both are in LEGAL.md and ASSETS.md and on the credits screen (a new "Map data" section, which also carries the OSM credit LEGAL.md already required).
- **Maps:** 4 real 8×8 km areas per era.
  - WW2: Hürtgen (Vossenack ridge), Oosterbeek, Seelow Heights, Noville.
  - Cold War: Fulda Gap, Hof Gap, Eichsfeld, Lüneburg Heath.
  - Modern: Suwałki Gap, Narva, Tapa, Daugavpils.
- **Pipeline.** `scripts/generate_maps.py` crops each area from the public COG tiles (cached in gitignored `tools/cache/dem/`; read with tifffile in `tools/dem`, Python 3.12). It applies a 3×3 box blur, samples 251×251 points at 32 m and stores decimetres above the map minimum as base64 uint16 (2.0 MB for all 12).
- **Sim.** A new `heightfield` terrain feature uses integer bilinear sampling, so it is deterministic and hashed with the rest of the terrain. `Skirmish.Options.Map` replaces the generated hills but keeps the seed's forests, roads and villages: the hill draws still happen, so the RNG sequence is unchanged. The bench hash is unchanged.
- **Checks:**
  - Unit tests (`MapTests`) cover exact interpolation, determinism, 4 maps per era, unchanged vegetation for a seed, and that an unknown map id fails.
  - G10 gains `scripts/check_maps.py`: counts, coverage, real relief ≥ 20 m, the verbatim attribution, tile sha256s, and the game copy in sync. When the tiles are cached it also regenerates byte for byte; otherwise it says "skipped", never "passed".
  - A hillshade contact sheet matched the real landforms: the Kall valley ridges, the Rhine below the Veluwe edge, the Seelow escarpment over the Oderbruch, the Daugava bend.
- **Known limits.** GLO-30 is a surface model, so large forest blocks read as raised plateaus (visible at Lüneburg and Tapa), and rivers and lakes are not yet water or blocked cells. The skirmish screen offers the maps; campaign operations still use generated ground. ERA-03 stays in progress until G8 (visual) exists.

## D076: NET-01 LAN / direct-IP play; Steam deferred

- **Donovan's call** ("LAN/IP play instead" of Steam). Steam lobbies stay out of scope until he asks.
- **`sim/Net/LockstepPeer.cs`** is one machine's side of a real-network battle, in a star topology: the host relays every input.
  - Inputs are sealed for tick t + 4. Each packet carries the sender's whole unacknowledged window (up to 32 ticks), so the next packet repairs a lost one. With nothing new to send, the window is resent every 20 ms.
  - Receivers acknowledge the highest contiguous tick per player. Acks are clamped to what was actually sent: one forged max ack used to stop all resends and crash `Poll`.
  - A client may speak only for itself. Orders stamped for another tick or player are stripped, but the tick is always accepted: dropping a whole tick for one bad order stalled every peer, which the review's test exposed.
  - Inputs more than two input delays plus a window ahead are ignored, which bounds memory.
  - The host compares every client's state hash every 20 ticks, deduplicated per (tick, peer) and pruned after 50 intervals.
  - Inputs are pruned once executed and acknowledged by every destination. The confirmed input log replays to the same state.
- **`sim/Net/NetOrders.cs`** screens every order at execution time, on the world as it stands at that tick. It drops another player's unit or seat, an index outside its table, a point off the map, an out-of-range parameter, `Blast` (a test fixture) and unknown kinds. Every peer drops the same orders, so they stay in sync. Before this, a crafted `SetIntent` crashed every peer, and a client could steer the host's aircraft or units. Offline play and the G4 battle scenarios do not go through it.
- **`sim/Net/Lan.cs`** runs over UDP: Hello (AI flag + name) / Welcome / Setup / SetupAck or Refuse, with a session tag.
  - A client takes the Setup only once it holds a slot (a lost Welcome used to give it slot -1), and acknowledges only after building its world.
  - If it cannot build the battle (map missing, map data different per `MapSha`, unknown deck), it sends Refuse with the reason. The host then ends the session and shows the reason.
  - The host drops a client's lockstep packets until that client has acknowledged the Setup.
- **Game:**
  - The lobby gains Host LAN game, Join by IP and Start LAN battle (UDP 47610, or `--lan-port`). The host builds and validates the battle before sending it, and refuses custom decks online.
  - The battle view uses the local slot (`_me`) for its units, seats, paint, flight, radio and support events. Allies show as friendly and stay visible.
  - Pause and speed-up are off online. The debrief headline and result judge from your own team. The LAN endpoint keeps pumping through the debrief so the last packets get through.
- **Checks:**
  - G4 `lan_2v2_udp_loss5`: 4 peers on real localhost sockets, a 10-minute battle on the Fulda map with slot 0 human, 5% of datagrams dropped. It checks desyncs 0, ≥ 1,600 hash checks, matching replays, client orders executed and units destroyed.
  - Unit tests:
    - 3 peers over lossy UDP.
    - The genuine packet accepted while the same bytes relabelled for another player are not.
    - A forged max ack still leaves resends going.
    - Crafted orders (out-of-range seat, another player's unit, `Blast`, negative support index) dropped on both peers with no desync.
    - A lost Welcome, then the Setup taken only with a slot.
    - A missing or different map fails with a reason.
    - A corrupted client detected (≥ 4 desyncs).
  - G13 `ux_19_lan`: the game hosts from the lobby and the headless `--net-bot` joins over UDP. At tick 600 (within 60 s) the host has made 27–30 hash comparisons with no desync and stalled at most 20 polls. `check_ux` fails if the bot never joined.
- **Evidence:** `build/reports/20261010T015202Z-gates-G1-G2-G3-G4-G5-G6-G9-G10-G11-G12-G13-G14.md` (G1–G3, G5, G6, G9–G11, G13, G14 pass; G13 includes `ux_19_lan`) and `build/reports/20261010T020905Z-gates-G4-G12.md` (G4 including `lan_2v2_udp_loss5`, all 122 mutants killed; G12). The full run's G4 failure was the mutant harness not copying `maps.json` into its temp data root, so every mutant run crashed and was reported as "survived". The harness now copies it and reports a crash as CRASHED. Its G12 failure was formatting.
- **Not done, so NET-01 stays in progress.** No per-player deck choice in the LAN lobby (the host's skirmish decks apply). No reconnect over real sockets (only in the simulated session). A peer that leaves stalls the others.
- **Latent, for the terrain mesh.** The view draws sim (x, y) as Godot (x, z) with the camera facing +Z, so screen-right is −X: real maps would render mirrored east-west. Fix the axis mapping when the ground mesh lands, with a test on a known landform.

## D077: Smaller fixes from the reviewer follow-up list

- The legacy roster path validates stealth through `BattleRules.NonNegative`, like the sourced roster.
- `generate_briefings.py` writes portable sidecars (`portable()`).
- `generate_music.py` re-measures a stem whose file hash differs from the hash recorded in its sidecar, and records the hash. It has not been re-run, so existing sidecars get the hash on the next run.
- Second-pass review minors, open: the host shows no on-screen reason when a client refuses and other clients are not told the session ended; QueueTask carries its deadline in the order's player field (no UI sends it yet); the crafted-orders test does not assert the orders reached the host before screening; `ux_19` has little headroom on `stalled_polls` (13 at start-up, limit 20).
