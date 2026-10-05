# Build brief: SIX ACTUAL (v3: full-game build contract)

You are the lead engineer, technical artist and producer for a PC real-time tactics game called **Six Actual** (working title, see "Name" below). You will build it end to end, in phases, shipping a playable build at the end of every phase. Read this whole brief before writing any code. Then seed `DESIGN.md`, `ROADMAP.md`, `REQUIREMENTS.md` (from §F) and `docs/STATE.md` from it and keep them current.

**How this brief is organized.** §A is how you work. §0 is the machine. §1–§10 define the game and architecture. §11–§12 cover art and audio direction. §F is the requirements catalog and §G is the verification suite that proves them. §H is the 3D model specification. §I is the phase gates. §14–§16 are working rules, the name and references. Where sections overlap, the stricter rule wins.

---

## A. Operating protocol (read before every session)

This brief is written for **Grok 4.7 running as a coding agent** (Grok Build, or any harness exposing shell, file and MCP tools) on the user's Omarchy machine. It applies to any capable agent, but these rules exist because of how long-running agents fail. Follow them literally.

**A1. Your knowledge is out of date. Look things up.**

- Your training data ends around **May 2026**. Godot 4.7 (released June 2026), current Godot MCP servers, GodotSteam, ElevenLabs Eleven v4, and current Meshy and Tripo APIs may postdate it.
- Never write code against these from memory. Dump the exact engine API you are building against with `tools/godot/godot --headless --doctool docs/godot-api` and search it. Read each library's current README and changelog.
- Prove every API call compiles and runs before building on it. If docs and behavior disagree, behavior wins. Record it in `DECISIONS.md`.

**A2. State lives in files, not in your context.** Your context window is large (500K tokens) but it will be compacted, and sessions end. Keep these files current, because the next session (possibly a fresh instance of you) starts from them alone:

- `docs/STATE.md`: current phase; the requirement IDs in progress; the last verify result with date and report path; open blockers; the next 3 concrete actions. Rewrite it at the end of every work block, not just every session.
- `PROGRESS.md`: dated, append-only log of what changed and the verify outcome.
- `REQUIREMENTS.md`: the catalog in §F, with a status per ID (`todo`, `in-progress`, `verified`, `waived (reason, approval)`) and a link to the passing verifier output.
- `DECISIONS.md`, `ASSETS.md`, `LEGAL.md`, `ROADMAP.md`.

**A3. Session start ritual (do these, in order, every time).**

1. Read `docs/STATE.md`, then `ROADMAP.md`, then the open IDs in `REQUIREMENTS.md`, then the last 40 lines of `PROGRESS.md`.
2. Run `scripts/verify.sh --quick` and read the report. This tells you the true state of the repo before you change anything. If it is red, fixing it is your first task.
3. Pick the smallest open requirement in the current phase and write down the plan in `docs/STATE.md`.

**A4. Work loop (test-first, one requirement at a time).**

1. Write or extend the verifier for the requirement first. Run it and confirm it **fails** for the right reason.
2. Implement until it passes.
3. Run the phase gate (`scripts/verify.sh --phase N`).
4. Update `REQUIREMENTS.md`, `PROGRESS.md` and `docs/STATE.md`.
5. Commit with the requirement IDs in the message, for example `SIM-07 SIM-08: armor facing and penetration`.

**A5. Forbidden moves (any one of these voids a phase).**

- Skipping, deleting, `#if`-ing out or weakening a failing test or verifier to get green.
- Changing a threshold in `verify/thresholds.toml` or a golden hash without a `DECISIONS.md` entry explaining why the old value was wrong.
- Mocking or stubbing the simulation inside integration, scenario or soak tests.
- Hardcoding scenario outcomes, special-casing test seeds, or detecting the test harness at runtime.
- Claiming anything about visuals, audio or feel without opening the captured frames or files yourself. You have vision: look at the contact sheets.
- Reporting a placeholder or partial system as done.
- Writing large JSON or data files by hand. Generate them with scripts and validate them against JSON Schema.

**A6. Effort.** If your harness exposes reasoning effort, use the highest setting for architecture, netcode, determinism bugs and verifier design, and high for routine implementation. Use subagents or parallel tasks only for independent work: asset batches, audio batches, data entry from sources. Never run two agents against the same files.

**A7. Stop and ask the user only for these.** Name sign-off; art style board sign-off; any spend beyond the agreed budget; switching engines; content or faction sensitivity; or a requirement that cannot be met as written. In that last case, propose a specific waiver with evidence. Everything else: decide, record it in `DECISIONS.md`, continue.

**A8. Reports.** Every report to the user names exactly which state each item is in: **prepared** (written, not run), **verified** (a check that could fail passed, with a report path), **built** (an exported playable build exists, with a path), or **delivered** (handed over). Include the verify report path and any failures, verbatim.

## 0. Where you run: Omarchy (read first)

You are running on **Omarchy**, DHH's opinionated Arch Linux setup with the Hyprland (Wayland) desktop, bash, `yay` for packages (official repos plus the AUR), and `mise` for language toolchains. Everything in this brief must build, run and be verified on this machine. Windows builds are cross-exported from here.

**Ground rules on this machine**

- Work only inside the project directory the user gives you (default `~/Projects/six-actual`). Do not edit Omarchy, Hyprland, shell or system configs.
- You may need `sudo` only for package installs. Batch every system package into `scripts/bootstrap.sh`, show the user the full package list once, and let the user run it. Never run destructive or system-wide commands beyond that script.
- Pin every tool version in `tools/versions.env` so the build is reproducible.
- Secrets (ElevenLabs, 3D-generation keys) go in a gitignored `.env`. Never commit keys and never print them in logs.
- Before anything else, record the machine profile (`fastfetch` or `inxi -Fxz`, plus `vulkaninfo --summary`) in `PROGRESS.md`. Performance targets are measured on this machine and its GPU.

**Bootstrap (put this in `scripts/bootstrap.sh`, idempotent)**

```bash
# system packages (one sudo prompt, run by the user)
yay -S --needed git git-lfs base-devel blender python uv ffmpeg sox imagemagick \
  vulkan-tools mesa-utils fastfetch grim slurp jq unzip p7zip
git lfs install

# toolchains via mise (no sudo)
mise use -g dotnet@10      # use the .NET SDK the pinned Godot version requires; check its release notes
mise use -g node@lts       # for MCP servers that ship via npx

# engine: official Godot .NET build, pinned, unpacked into the repo's tools/ (no system install)
#   download Godot_v${GODOT_VERSION}-stable_mono_linux_x86_64.zip and the matching export templates
#   from github.com/godotengine/godot/releases, verify SHA-512 against the published checksums,
#   unpack to tools/godot/, install templates to ~/.local/share/godot/export_templates/${GODOT_VERSION}.stable.mono/
```

- Prefer the official Godot release zip pinned in `tools/versions.env` over the Arch repo package, which can lag a minor version behind.
- Godot runs natively on Wayland. If you hit a display or input bug under Hyprland, retry with `--display-driver x11` (XWayland) and record which one you used.
- Use Git LFS for meshes, textures and audio from day one.

**Agent tooling**

- Drive the Godot editor and running game through a Godot MCP server when one is available in your runtime. `godot-agent-loop` targets Godot 4.7 and works with Claude Code and Codex. Prefer that over hand-editing scene files blind, but everything must also work from plain files and the CLI.
- Use a Blender MCP server or `blender --background --python <script>` for the asset pipeline.
- Use the ElevenLabs MCP if your runtime exposes it, otherwise the Python SDK installed with `uv`.

**Commands you will use to verify work (all must pass before you report a phase as verified)**

```bash
dotnet test sim/                                   # unit tests + determinism test (sim core, no engine)
dotnet run --project sim/Headless -- --selftest    # AI-vs-AI match to completion, prints state hash
tools/godot/godot --headless --path game --import  # import assets, fail on errors
tools/godot/godot --headless --path game --export-release "Linux"   build/linux/SixActual.x86_64
tools/godot/godot --headless --path game --export-release "Windows" build/windows/SixActual.exe
tools/godot/godot --path game --write-movie build/verify/slice.avi --fixed-fps 30 -- --scenario=verify_slice
grim build/verify/desktop.png                      # Hyprland screenshot when a live window must be checked
```

The `--write-movie` run renders a scripted camera path through a verification scenario, so you can extract frames with `ffmpeg` and actually look at them before claiming anything is visible or correct.

## 1. The game in one paragraph

Six Actual is a large-scale combined-arms RTS in the tradition of **Steel Division 2**, **WARNO** and **Broken Arrow**, spanning three eras: **World War II (1944–45)**, **the Cold War (1989)** and **the modern day (2020s)**. You command a whole battlegroup through a staff of AI sub-commanders. At any moment you can **take personal control of an aircraft and fly it yourself**, while your executive officer (XO) runs the battle in your place. When your plane runs out of fuel or ammunition, is shot down, or you press a key, you snap back to the command table. Around the battles sits a **grand-strategy layer** (Hearts of Iron / Total War style) with production, research, fronts and, in that mode, **nuclear weapons**.

## 2. Pillars (every decision should serve these)

1. **Command, not clicking.** The player sets intent and delegates. Micro is always possible, never required.
2. **Support is the star.** Artillery, air power and logistics are deep, varied and era-authentic, and they decide battles.
3. **You can always get your hands dirty.** Flying an aircraft is a real, fun, skill-based mode, not a minigame.
4. **Three eras, one ruleset.** The same systems (line of sight, suppression, supply, morale) apply across eras. Era identity comes from units, doctrine, HUDs, audio and visuals.
5. **It has to look and sound like a war zone.** Smoke columns, tracer fire, burning wrecks, destructible towns, radio chatter, weather and time of day.

## 3. Core loop (per battle)

1. **Battlegroup (deck) building.** Pick a nation and a division or specialization for the era. Fill category slots (recon, infantry, armor, artillery, AA, air, logistics, support) within a point budget. Choose loadouts where the era allows (aircraft ordnance, vehicle upgrades).
2. **Deployment phase.** Place opening forces in your deployment zone, assign sectors to sub-commanders, and set initial orders.
3. **Battle.** Real-time with income-based reinforcements, as in Steel Division 2 and WARNO. The front line is visualized. Control zones or destruction scoring decide victory. Units call in support, consume ammo and fuel, and need resupply.
4. **Debrief.** Losses, kills, commander performance and XP (persistent in campaign and grand strategy).

## 4. Command structure (the signature system)

The player is the **Commander**. Under them:

| Seat | Count | Responsibility |
|---|---|---|
| **XO (second in command)** | 1 | Runs the entire battle with the Commander's standing intent while the player flies. Has no units of its own. |
| **Army commanders** | 2 | Infantry and mechanized maneuver, each assigned a sector or task force. |
| **Artillery commanders** | 2 | Fire missions, counter-battery, smoke, and positioning and relocating batteries. |
| **Air commanders** | 2 | One defaults to strike/CAS, the other to air superiority and recon. The player can reassign either. |
| **Armor commander** | 1 | Tank formations, breakthroughs and counter-attacks. |
| **Logistics commander** | 1 | Supply trucks and helicopters, repair, refuel, medevac, engineers, minefields and bridging. |

That is 8 sub-commanders plus the XO. Requirements:

- **Assignment.** Drag units or formations onto a commander's card, or paint a sector on the map. Any unit can be reassigned at any time. Unassigned units are controlled by the player directly.
- **Orders.** Each commander accepts high-level tasks: *attack objective*, *defend line*, *hold until relieved*, *screen flank*, *recon area*, *fire plan*, *CAP over area*, *CAS on request*, *interdict road*, *resupply sector*. Tasks can be queued and given deadlines.
- **Intent controls.** Per-commander sliders and toggles for aggression (cautious/balanced/aggressive), casualty tolerance, rules of engagement, priority target types, and "ask before spending support" vs "auto-approve".
- **Inter-commander requests.** Army and armor commanders request fire missions and CAS from the artillery and air commanders. Logistics answers resupply requests automatically. The player can see, approve, deny or reprioritize the request queue.
- **Personalities and traits.** Each commander has a name, portrait, voice (ElevenLabs) and 2–3 traits (for example *methodical*, *reckless*, *artillery-minded*). In campaign and grand strategy they gain XP, can be wounded, and can be replaced.
- **Reports.** Commanders talk over the radio (voiced lines plus a text log), drop map pings, and escalate when a task is failing ("Sector 2 cannot hold, requesting armor").
- **Override.** The player can grab direct control of any unit or formation. Ownership returns to the commander when released.
- **AI quality bar.** Commanders must use terrain and cover, keep formations, avoid feeding units in piecemeal, coordinate fire support before assaults, and pull back damaged units for resupply. Build them on a utility-AI or HTN planner over the simulation state, not on scripted triggers.

## 5. Take Control: flying aircraft

- **Entering.** Select any friendly aircraft that is airborne or about to launch and press **Take Control** (also on the unit card). The camera transitions to a chase or cockpit view. The XO announces it has command, for example: "XO has the battle, Commander. Good hunting."
- **While flying.** The battle does not pause. The XO commands using the standing intent, current orders and the request queue. A compact overlay shows the minimap, the XO's radio log and alerts. Era-specific HUD:
  - **WW2:** gunsight and simple gauges.
  - **Cold War:** analog-era HUD, radar warning receiver, chaff and flares.
  - **Modern:** helmet-mounted display, targeting pod, datalink markers.
- **Returning.** This is automatic when the aircraft goes *Bingo fuel* or *Winchester* (out of ordnance), when it is destroyed (eject animation, then a cut back to command), or when it lands or RTBs. The player can also return manually at any time with one key, and the AI pilot then takes over the aircraft. The XO gives a short sitrep on return.
- **Flight model.** An arcade-sim hybrid: energy and stall matter, but it is approachable. Offer mouse-aim (War Thunder style), gamepad and HOTAS. Era-appropriate performance, weapons and countermeasures.
- **Weapons.** Guns, rockets, dumb bombs, cluster munitions, napalm (where era-authentic), guided bombs and missiles (Cold War and modern), anti-radiation missiles for SEAD, and targeting pods.
- **Threats.** AAA, MANPADS and SAM systems with radar warning, plus enemy fighters. Ground AA should feel dangerous.
- **Scope.** Fixed-wing first. Helicopters (attack and transport) and gunship orbits (AC-130 style) arrive in a later phase.
- **Multiplayer.** Flying works in multiplayer. Inputs are sent as deterministic commands (see Architecture).

## 6. Support options (go wide and deep)

Every era needs a rich support menu. Support is called through the relevant sub-commander or directly by the player, with realistic delay, accuracy and cost. Minimum set:

**Artillery and fires:** mortars, towed and self-propelled guns, rocket artillery and MLRS, naval gunfire (map-dependent), smoke and illumination rounds, creeping and rolling barrages (WW2), counter-battery radar (Cold War and later), precision rounds (modern), loitering munitions and FPV drones (modern).

**Air:** close air support, strafing runs, dive bombing (WW2), level and carpet bombing, napalm, cluster munitions, interdiction, SEAD and DEAD, combat air patrol, escort, recon flights and photo recon, paratroopers and glider landings, airmobile helicopter insertion, gunship orbit, cruise missiles (Cold War and later), drones (MQ-9 class), and satellite or AWACS reveal (modern).

**Logistics and engineering:** supply trucks and helicopters, forward ammo dumps, field repair, refueling, medevac, engineers (demolition, minefields, obstacles, bridging, trench and fortification digging), and smoke screens.

**Electronic warfare (Cold War and modern):** radar jamming, comms jamming (slows enemy support response), GPS degradation of precision strikes, and decoys.

**Strategic (grand strategy mode only):** tactical and strategic nuclear weapons (see §8). The default is off in skirmish and multiplayer. A custom-match toggle may enable tactical nukes for fun matches.

## 7. Eras, factions and example units

These are starting rosters, not an exhaustive list. Each era should ship about 25–40 units per faction in its first playable version, then grow toward about 100 or more.

- **WW2 (1944–45): USA, UK/Commonwealth, Germany, USSR.** Rifle squads, MG42 and Bren teams, mortars, AT guns, Sherman, Cromwell, Panzer IV, Panther, Tiger, T-34-85, IS-2, M7 Priest, Wespe, Nebelwerfer, Katyusha. Aircraft: P-47, P-51, Typhoon, Spitfire, Fw 190, Bf 109, Ju 87, Il-2, B-26, B-17, Lancaster.
- **Cold War (1989): NATO (USA, West Germany, UK, France) vs Warsaw Pact (USSR, East Germany, Poland).** M1 Abrams, Leopard 2, Challenger, T-80, T-72, BMP-2, M2 Bradley, Marder, ATGM teams, MANPADS, M109, 2S3, MLRS, BM-21, ZSU-23-4, Gepard, SA-8, Hawk. Aircraft: A-10, F-16, F-15, Tornado, Jaguar, Su-25, MiG-29, MiG-23, Su-24.
- **Modern (2020s): USA/NATO, Russia, China.** M1A2 SEPv3, Leopard 2A7, T-90M, T-14, Type 99A, Stryker, BMP-3, ZBD-04, HIMARS, Tornado-S, PCL-191, Patriot, S-400, Pantsir, Javelin, Kornet, FPV and loitering-munition teams, EW vehicles. Aircraft: F-35, F-16V, A-10C, Su-34, Su-35, J-16, J-20, MQ-9, Orion.

Use real designations for authenticity, but no manufacturer logos or trademarked branding. Keep a `LEGAL.md` note listing anything that needs a trademark or licensing check before commercial release.

## 8. Game modes

1. **Skirmish.** 1v1 up to 5v5 vs AI, any era, any map, with configurable starting points, income, victory type, time of day and weather.
2. **Campaigns.** One per era, 6–10 operations each, with briefings, voiced commanders, persistent battlegroup and commander XP, and branching outcomes.
3. **Grand Strategy** (Hearts of Iron / Total War hybrid):
   - **Map.** A theater map per era (Western/Eastern Front for WW2, Central Europe 1989 for the Cold War, a choice of theaters for modern) split into provinces with terrain, cities, industry and supply.
   - **Economy.** Production queues, manpower, research trees per era, logistics networks and supply lines.
   - **Command.** Armies are led by your commanders, who carry their traits and XP.
   - **Battles.** When armies meet, choose to fight the battle in full RTS (a map generated from the province's terrain and weather) or auto-resolve with transparent odds.
   - **Diplomacy.** Light: alliances, ceasefires, lend-lease and war goals.
   - **Nuclear weapons.** Late WW2 (research-gated, US only, historically limited), Cold War and modern. An escalation ladder runs from tactical to theater to strategic. A global tension meter drives AI retaliation logic, fallout and attrition zones, production and morale penalties, and international consequences. In RTS battles a tactical nuke is a support card unlocked by escalation. It has a large, readable blast, shockwave, mushroom cloud and lingering fallout zone. No gore.
4. **Multiplayer.** PvP 1v1 up to 5v5 and co-op vs AI for skirmish. Grand strategy multiplayer comes in a later phase. Supports lobbies, the deck-building screen, ranked-ready result reporting, replays and reconnects.

## 9. Battlefield simulation requirements

- Terrain with elevation-based line of sight, forests, hedgerows, buildings and smoke that block sight.
- Fog of war, recon and detection by unit optics, with stealth for infantry in cover.
- Armor facing and penetration values, criticals (detracked, optics damaged, crew bailed), suppression and morale, ammo and fuel per unit, and resupply.
- **Destructible buildings and towns** built from structural chunks that collapse when their supports are destroyed, plus craters, burning wrecks, fires and rubble. A prototype of this system exists (see Reference material). Rebuild it properly in the engine.
- Weather (clear, rain, fog, snow) and time of day (dawn, day, dusk, night with flares and illumination) that affect sight and air operations.
- Readable at RTS height: unit icons and labels, front line, support call trajectories, and threat rings for AA.
- **Performance target:** 2,000 or more simultaneous simulated entities (infantry counted per squad member or per squad, decide and document) at 60 fps at 1440p on an RTX 3060-class GPU with a 6-core CPU. Measure on the user's Omarchy machine and report its numbers against this target.

## 10. Technical architecture (decided, change only with a written reason in `DECISIONS.md`)

- **Engine: Godot 4.7.x (.NET / C# build), Forward+ renderer.** Pin the exact patch version in `tools/versions.env`. Reasons:
  - It is native to Linux and Wayland, ships as a single binary, and needs no account sign-in or license activation, so an agent can install, build and export it entirely from the command line on Omarchy.
  - Scenes and resources are plain text (`.tscn`, `.tres`), which agents can read, diff and edit safely.
  - Headless import and export work out of the box, and the MIT license has no revenue strings.
  - Unity's Linux editor is still labeled experimental on the Arch wiki, its Hub is AUR-only on Arch, and there are open reports of the editor freezing or crashing under Hyprland. That is the wrong foundation for an unattended agent on this machine.
  - If you find a hard blocker in Godot that a different engine solves, write it up in `DECISIONS.md` and stop for the user's approval before switching.
- **Deterministic simulation core in pure C#** (`sim/`, a plain .NET class library with no Godot references). It covers map, units, combat, LOS, supply, commanders' AI and the grand-strategy model.
  - Fixed timestep and fixed-point math (no `float` in sim state).
  - The Godot project (`game/`) is view, input and audio only, and references the sim library.
  - This gives you headless AI-vs-AI testing with plain `dotnet`, replays, save and load, and **deterministic lockstep multiplayer**. It also keeps the game portable if the engine ever has to change.
  - Inside the sim, use a C# ECS library (Arch ECS or Friflo.Engine.ECS) or plain struct-of-arrays. Benchmark and record the choice.
- **Rendering at scale:** `MultiMeshInstance3D` or direct `RenderingServer` instancing for units, LODs and impostors, pooled VFX (GPU particles), and occlusion culling for towns. Turn on volumetric fog, SDFGI or VoxelGI where they hold frame rate, plus the HDR output and area lights added in 4.7.
- **Networking:** deterministic lockstep with input delay and rollback-free resync. Use GodotSteam or Steamworks.NET for lobbies and transport, with Godot's built-in ENet for LAN and local testing. Run desync detection by hashing sim state every N ticks.
- **Data-driven content:** every unit, weapon, support card, faction, era, commander trait and research node lives in versioned JSON in `data/`, loaded by the sim. No balance value lives in code or scenes.
- **Saves:** versioned, forward-migratable formats.
- **Repository layout:** `sim/`, `sim/Headless/`, `sim.Tests/`, `game/` (Godot project), `data/`, `assets_src/` (Blender and raw sources, LFS), `audio/`, `scripts/`, `tools/`, `docs/`.

## 11. Art direction

**Target look:** grounded realism with strong silhouettes, readable from 60–300 m camera height. Desaturated era-appropriate palettes (WW2 olive and earth, Cold War NATO green and Pact khaki, modern multicam tones), heavy atmospheric VFX and good lighting.

Model production, budgets, conventions and validation are specified in §H. Before mass production, produce a style board: 3 tanks, 2 aircraft, 1 infantry squad and 1 town block per era (Cold War first, for Phase 1). Stop for the user's sign-off.

## 12. Audio and voice (ElevenLabs plus free libraries)

- **Dialogue:** use ElevenLabs Text to Dialogue (Eleven v4 recommended, v3 also supported) for multi-speaker radio chatter.
  - Cast era- and nation-appropriate voices for the Commander's staff, XO, pilots and unit callouts.
  - Use audio tags for delivery (urgent, under fire, calm) and radio filtering in-engine.
  - Write a dialogue matrix (`dialogue/lines.csv`: speaker, era, faction, trigger, text, variants), then generate in batches.
- **Sound effects:** use the ElevenLabs Sound Effects API (up to 30 s per generation) for weapons, engines, explosions, ambience and UI.
  - Generate several variants per sound, pick the best and normalize loudness.
  - Supplement with CC0 sounds from Freesound and the royalty-free Sonniss GDC bundles.
- **Music:** use the ElevenLabs Music API (paid plan required) for era themes, menu music and dynamic combat layers (calm, tension, combat).
- **Cost control.** Cache every generated file with its prompt and settings in `audio/manifest.json`, and never regenerate an unchanged line.
- **Budgets.** Agree a credit budget with the user before bulk generation and report spend at each phase end.
- **ElevenLabs MCP.** The user has an ElevenLabs MCP connection available. Use it if your environment exposes it, otherwise use the official Python or JS SDK with an API key from the user.

## F. Requirements catalog (seed `REQUIREMENTS.md` from this, keep IDs stable)

Each requirement must map to at least one automated verifier (gate letter in brackets, see §G). "Human" means it also needs the user's sign-off; automate everything that can be automated anyway. Numbers below are minimums for version 1.0. Exceeding them is fine.

**Simulation (SIM)**
| ID | Requirement | Verifier |
|---|---|---|
| SIM-01 | Sim runs at a fixed 20 Hz tick with fixed-point math. No `float` or `double` in any type that is part of sim state. | [G2] Roslyn analyzer test fails the build on floats in `sim/State` |
| SIM-02 | Bit-identical state after N ticks for identical seed and inputs, across runs, processes and thread counts. | [G3] |
| SIM-03 | Replays: recorded inputs reproduce the final state hash exactly. | [G3] |
| SIM-04 | Terrain heightfield LOS with forests, hedges, buildings and smoke as occluders. Per-unit optics and stealth ratings. | [G4] `los_*` scenarios |
| SIM-05 | Fog of war per player. Clients only receive or reveal what their units detect, in the UI layer. | [G4] |
| SIM-06 | Armor facing (front, side, rear, top) with penetration-vs-armor resolution, including range falloff for KE rounds. | [G4] `armor_*` scenarios against a reference table |
| SIM-07 | Criticals: detracked, optics, engine, turret jam, crew bail, fire. | [G4] |
| SIM-08 | Suppression and morale with recovery, routing and surrender. | [G4] |
| SIM-09 | Ammo and fuel per unit, resupply by logistics units, refuel and rearm for aircraft at airbase (off-map). | [G4] |
| SIM-10 | Structural destruction: buildings are chunk grids. Unsupported chunks fall and damage what they hit. Rubble alters LOS and movement. | [G4] `collapse_*` scenarios |
| SIM-11 | Weather and time of day modify detection range, air operations and accuracy. | [G4] |
| SIM-12 | Pathfinding for wheeled, tracked, infantry and air on 8×8 km maps, with formations and road preference. Units never permanently stuck. | [G4] [G5] stuck-unit metric |
| SIM-13 | 2,000 or more simultaneous sim entities with a 95th-percentile tick time ≤ 8 ms headless on the user's machine. | [G6] |

**Command (CMD)**
| ID | Requirement | Verifier |
|---|---|---|
| CMD-01 | Player plus XO plus 8 sub-commanders: 2 army, 2 artillery, 2 air, 1 armor, 1 logistics. | [G4] [G13] |
| CMD-02 | Units and formations can be assigned and reassigned by drag or sector paint. Unassigned units stay under player control. | [G13] |
| CMD-03 | Orders: attack, defend line, hold until relieved, screen, recon, fire plan, CAP, CAS on request, interdict, resupply sector. Each has a queue and deadline. | [G4] one scenario per order |
| CMD-04 | Intent controls (aggression, casualty tolerance, ROE, priority targets, approval mode) measurably change behavior. | [G4] A/B scenarios with statistical assertions |
| CMD-05 | Inter-commander support requests flow to artillery, air and logistics. The player can approve, deny or reprioritize. | [G4] |
| CMD-06 | Commander AI is a utility AI or HTN planner over sim state, not scripted triggers. It must coordinate fires before assaults, use cover, avoid piecemeal attacks and withdraw damaged units. | [G5] behavior metrics (see §G5) |
| CMD-07 | Commanders have names, portraits, voices and traits. They report by voice, text log and map pings, and escalate failing tasks. | [G9] [G13] |
| CMD-08 | Commander XP, wounds and replacement persist in campaign and grand strategy. | [G4] save and load round trip |

**Flight (FLY)**
| ID | Requirement | Verifier |
|---|---|---|
| FLY-01 | Take Control on any eligible friendly aircraft. The XO takes command within 1 tick and announces it. | [G4] [G13] |
| FLY-02 | Automatic return on Bingo fuel, Winchester, destruction (eject) or landing. Manual return with one key at any time. The AI pilot resumes the aircraft. | [G4] one scenario per trigger |
| FLY-03 | Arcade-sim flight model with energy, stall and G limits, and era-specific performance from data. | [G4] climb, turn and stall tests against data envelopes |
| FLY-04 | Mouse-aim, gamepad and HOTAS input with remapping. | [G13] input replays |
| FLY-05 | Era HUDs: WW2 gunsight; Cold War HUD, RWR, chaff and flares; modern HMD, targeting pod and datalink. | [G8] frame checks plus a human review |
| FLY-06 | Weapons per era and loadout (guns, rockets, dumb, cluster, napalm, guided, ARM). Ground AA and fighters threaten the player-flown aircraft with readable warnings. | [G4] [G8] |
| FLY-07 | Flying works in multiplayer through lockstep inputs, with no desync. | [G3] [G4] `net_fly_*` |

**Support (SUP)**
| ID | Requirement | Verifier |
|---|---|---|
| SUP-01 | Artillery: mortars, towed and self-propelled guns, MLRS, naval gunfire, smoke and illumination, barrages, counter-battery, precision rounds, loitering munitions and FPV drones where era-appropriate. | [G4] each has a scenario with accuracy and delay assertions (CEP within data ±10%) |
| SUP-02 | Air: CAS, strafe, dive and level bombing, napalm, cluster, interdiction, SEAD/DEAD, CAP, escort, recon, paradrop and gliders, air assault, gunship orbit, cruise missiles, drones, AWACS and satellite reveal, per era. | [G4] |
| SUP-03 | Logistics and engineering: supply trucks and helicopters, ammo dumps, repair, refuel, medevac, mines, obstacles, bridging, fortifications, smoke. | [G4] |
| SUP-04 | EW for Cold War and modern: radar and comms jamming, GPS degradation, decoys, each with a measurable effect. | [G4] |
| SUP-05 | Every support option is data-driven, era-gated and costed, and responds with realistic delay. | [G10] [G4] |

**Eras and content (ERA)**
| ID | Requirement | Verifier |
|---|---|---|
| ERA-01 | WW2 (US, UK, Germany, USSR), Cold War 1989 (US, West Germany, UK, France vs USSR, East Germany, Poland), Modern (US/NATO, Russia, China). | [G10] |
| ERA-02 | Version 1.0 content: at least 60 units per faction (stretch 100), every one with a validated model (§H), data, icons, audio set and a deck card. | [G7] [G10] [G9] |
| ERA-03 | At least 4 battle maps per era, each 8×8 km with a real-world-inspired heightfield from public DEM data. | [G10] [G8] |
| ERA-04 | No anachronisms: unit, weapon and support availability is validated against era and year fields. | [G10] |

**Modes (MODE, NET, GS)**
| ID | Requirement | Verifier |
|---|---|---|
| MODE-01 | Skirmish 1v1 to 5v5 vs AI with configurable points, income, victory type, time of day and weather. | [G13] [G5] |
| MODE-02 | One campaign per era with at least 6 operations, briefings, voiced commanders, persistence and branching. | [G13] scripted playthroughs |
| NET-01 | Deterministic lockstep, Steam lobbies, deck screen, 1v1 to 5v5 PvP and co-op vs AI, replays, reconnect. | [G4] `net_*` with 8 local headless clients |
| NET-02 | A 20-minute 4v4 match with 8 clients completes with zero desync, and so do 2% packet loss and 150 ms latency soak runs. | [G4] |
| GS-01 | Grand strategy on one theater map per era: provinces, production, research, manpower, supply, armies led by commanders. | [G4] [G13] |
| GS-02 | Battles are fought in full RTS (map generated from province terrain and weather) or auto-resolved with transparent odds. Results feed back. | [G4] |
| GS-03 | Light diplomacy: alliances, ceasefires, lend-lease, war goals. | [G4] |
| GS-04 | Nuclear escalation: tactical, theater and strategic. Covers a tension meter, AI retaliation, fallout and attrition zones, and consequences. The tactical nuke is an RTS support card gated by escalation. No gore. | [G4] [G8] |
| GS-05 | A full grand-strategy run in each era reaches an end state without crash in an AI-vs-AI soak. | [G5] |

**Presentation (REN, AUD, UX)**
| ID | Requirement | Verifier |
|---|---|---|
| REN-01 | It reads as a war zone: smoke columns, tracers, burning wrecks, craters, dust, muzzle flashes, debris, fires that persist. | [G8] plus a human review |
| REN-02 | Readable at RTS height: unit icons, labels, front line, threat rings and support trajectories. | [G8] |
| REN-03 | Lighting and atmosphere: volumetric fog, time-of-day sky, era-graded color, night with flares and illumination. | [G8] |
| AUD-01 | Every dialogue trigger in `dialogue/lines.csv` has a generated file for every speaker variant. | [G9] |
| AUD-02 | Loudness standards: dialogue −16 LUFS ±1.5, SFX peaks ≤ −1 dBTP, music −18 LUFS ±2, no silent or clipped files. | [G9] |
| AUD-03 | Spatial audio with distance attenuation and occlusion. Radio filter on comms. Dynamic music layers (calm, tension, combat). | [G4] [G13] |
| UX-01 | Full menu flow: main menu, deck builder, lobby, battle, debrief, settings, credits (auto-generated from `ASSETS.md`). | [G13] |
| UX-02 | Accessibility: full remapping, colorblind palettes, subtitles, UI scale, pause and slow time in singleplayer. | [G13] |
| UX-03 | Tutorials: command basics, support, Take Control. | [G13] |

**Performance and quality (PERF, QA)**
| ID | Requirement | Verifier |
|---|---|---|
| PERF-01 | Benchmark scene at 1440p with 2,000 entities, full VFX and a big artillery barrage. 95th-percentile frame time ≤ 16.7 ms on the user's machine (record specs). Report the 1080p and 4K numbers too. | [G6] |
| PERF-02 | No frame over 100 ms during a 20-minute soak. Memory growth ≤ 5% after the first 5 minutes. | [G6] |
| QA-01 | Zero analyzer warnings. `dotnet format --verify-no-changes` clean. No `TODO` or `NotImplementedException` in any code path tied to a `verified` requirement. | [G12] |
| QA-02 | 80% or higher line coverage on `sim/`. | [G2] |
| QA-03 | Crash-free AI-vs-AI soak of 200 matches per phase. | [G5] |

## G. Verification suite (build it in Phase 0, grow it every phase)

One entry point: `scripts/verify.sh [--quick | --phase N | --full]`.

- It runs the gates below, writes a Markdown report to `build/reports/<UTC timestamp>-<mode>.md` with raw command outputs, and exits non-zero on any failure.
- `--quick` runs G1, G2, G3 and G10 (target: under 5 minutes).
- `--phase N` runs every gate tagged for that phase.
- `--full` runs everything.

All thresholds live in `verify/thresholds.toml`. The report prints the SHA-256 of `verify/` and of the thresholds file, so any change to the verifiers is visible.

| Gate | What it checks | How |
|---|---|---|
| **G1 Build** | Sim builds with warnings as errors; Godot imports with zero errors; Linux and Windows exports succeed. | `dotnet build -warnaserror`; `godot --headless --import` (parse the log for `ERROR`); `--export-release` for both presets |
| **G2 Unit tests** | All tests pass; sim line coverage ≥ 80%; float-in-state analyzer. | `dotnet test` with coverlet; the analyzer runs as a test |
| **G3 Determinism** | Identical state hash at ticks 100, 1,000 and 12,000 across 3 seeds × 2 separate processes × thread counts 1 and N; replay round trip; lockstep 8-client hash agreement. | `sim/Headless --determinism`; golden hashes stored in `verify/golden/` |
| **G4 Scenarios** | Every requirement with sim behavior has scripted scenarios: map, units, orders, seed, duration, and assertions on outcomes, ranges or statistics. | `verify/scenarios/*.json`, run by `sim/Headless --scenario`. Statistical scenarios run ≥ 50 seeds with confidence intervals. |
| **G5 AI soak and balance** | No crashes; no unit stuck over 120 s (≥ 99.5% unstuck); matches end before the time cap. Mirror-match win rate per faction 40–60%, cross-faction 35–65%. Commander behavior metrics: share of assaults preceded by fire support ≥ 70%; average force committed per attack ≥ 60% of the available assigned force; damaged-unit withdrawal rate ≥ 60%. | `sim/Headless --soak N` emitting CSV, then `scripts/analyze_soak.py` |
| **G6 Performance** | Headless tick p95; rendered frame time p95, p99 and max in the benchmark scene; memory growth. | Godot benchmark scene logs frame times to CSV, then `scripts/analyze_perf.py` |
| **G7 Assets** | Every model passes the validator in §H. | `blender --background --python scripts/validate_assets.py -- assets_src/` |
| **G8 Visual** | Movie-maker renders of every `verify/visual/*.json` camera path. Fails on: black or white frames; luminance outside thresholds; low image entropy (blank screens); any expected entity covering fewer than the minimum pixels in the debug **ID-mask render mode** (each entity drawn in a unique flat color); HUD elements missing from expected regions. Also produces contact sheets. | `godot --write-movie`, then `scripts/check_frames.py`. Then **you must open the contact sheets** and write a short visual review in `PROGRESS.md`. |
| **G9 Audio** | Every trigger and variant has a file; loudness and peak targets; duration bounds; no silence over 300 ms at the start or end. | `ffmpeg -af ebur128` and `loudnorm` analysis via `scripts/check_audio.py` against `audio/manifest.json` |
| **G10 Data** | JSON Schema validation for every data file; referential integrity; era and year legality; stat sanity ranges (speeds, armor, ranges, rates of fire within real-world-plausible bounds per class); no duplicate IDs. | `uv run check-jsonschema`, plus `scripts/validate_data.py` |
| **G11 Licensing** | Every file in `game/assets/` and `audio/` traces to an `ASSETS.md` entry with an allow-listed license (CC0, CC-BY 4.0, MIT, own work, or generated with a recorded tool and plan). Credits screen regenerated from it. | `scripts/check_licenses.py` |
| **G12 Code quality** | Analyzers; formatting; banned patterns (`TODO`, `NotImplementedException`, `Debug.Assert(false)`) in verified code; no test skips. | `dotnet format --verify-no-changes`; `scripts/check_code.py` |
| **G13 UX smoke** | The exported build runs recorded input scripts end to end: menu → deck → deployment → battle → Take Control → return → victory or defeat → debrief. Also campaign start, lobby flow and settings persistence. | Godot input replay harness with `--scenario` args, asserting on game-state events written to a log |

**Verifier integrity rules.**

- `verify/` changes go in their own commits, prefixed `VERIFY:`, each with a `DECISIONS.md` entry.
- Thresholds may only be tightened freely. Loosening one needs evidence and the user's approval.
- Golden hashes change only when sim rules intentionally change, and the commit names the requirement.

## H. 3D models: specification, production and validation

You build or source every model in the game. The quality bar is "would not look out of place in WARNO or Broken Arrow at RTS zoom, and holds up in the Take Control chase camera for aircraft."

**H1. Per-model spec, generated before any modeling: `data/assets/<id>.json`.**

- Real-world dimensions in meters (length, width, height), with the source cited.
- Reference images: public-domain sources only (US government technical manuals and photos, Wikimedia Commons public domain). Store their licenses in `ASSETS.md`.
- Part hierarchy.
- Triangle budgets per LOD.
- Texture set.
- Faction camo masks.
- Damage states.

**H2. Budgets.**

| Class | LOD0 tris | LOD1 | LOD2 | LOD3 / impostor | Textures |
|---|---|---|---|---|---|
| Tank, AFV, truck | 12k–25k | ≤ 50% | ≤ 20% | ≤ 5% or impostor | 2K albedo, normal, ORM; camo via mask |
| Aircraft | 15k–30k | ≤ 50% | ≤ 20% | impostor | 2K, plus a 4K hero option for Take Control |
| Helicopter | 12k–25k | ≤ 50% | ≤ 20% | impostor | 2K |
| Infantry soldier | 4k–8k | ≤ 50% | ≤ 15% | VAT crowd mesh | era atlas 2K |
| Building module | 2k–10k | per chunk | | | trim sheets and atlases |

**H3. Naming and rig conventions (the validator enforces these).**

- Scale: 1 unit = 1 m. +Y up, −Z forward. Transforms applied. Pivot at ground center.
- Ground vehicle nodes: `Root/Hull/Turret/Gun/Muzzle_0`, `Wheel_L_01..n`, `Wheel_R_01..n`, `Track_L`, `Track_R`, `Hatch_*`, `Exhaust_*`.
- Aircraft nodes: `Root/Fuselage`, `Aileron_L`, `Aileron_R`, `Elevator_L`, `Elevator_R`, `Rudder`, `Flap_*`, `Gear_*` with animations, `Prop_*` or `Exhaust_*`, `Hardpoint_*` (weapon mounts), `Cockpit` (a separate mesh for the cockpit view).
- Every vehicle has a `*_Destroyed` variant (charred material and missing parts) and damage decals.
- Infantry: one rigged base soldier per era (Mixamo-compatible skeleton) with nation kits as attachments. Animations: idle, walk, run, crouch, prone, crawl, fire (standing, crouched, prone), reload, throw, carry launcher, 4 deaths, surrender. Bake them to **vertex animation textures** for crowds so 2,000 soldiers render cheaply.
- Buildings: modular kits per theater (Normandy bocage, Fulda Gap towns, Baltic modern). Chunk size matches the sim destruction grid (2.5–3 m). Each chunk is its own mesh with interior faces, so collapses look solid.

**H4. Production routes, in order of preference.**

1. **Permissive libraries.** CC0 or CC-BY models from Poly Haven, ambientCG, Kenney, Quaternius, Fab's free tier, Sketchfab (downloadable, CC0 or CC-BY only), itch.io CC0 packs, and Meshy's public CC0 library. Verify each asset's license page.
2. **Parametric procedural modeling (always available, no external service).** Write Blender Python generators in `assets_src/procedural/`:
   - A ground-vehicle generator: hull from beveled, chamfered volumes; road wheels and track arrays; turret from a profile loft; barrel with mantlet and bore evacuator.
   - An aircraft generator: fuselage from cross-section lofts; wing planform from root and tip chord, sweep, dihedral and airfoil; tail surfaces; intakes; canopy.
   - A building-kit generator.
   - Drive them from the per-model spec plus measured reference silhouettes. This is the default route for anything libraries do not cover well.
3. **AI 3D generation** (Meshy, Tripo, Hunyuan3D) for complex organic or detailed parts, only within the user-approved budget. Generate from multi-view reference images, then remesh to budget.

**Every route ends in the same Blender cleanup pipeline (scripted):**

- Fix the silhouette against reference.
- Retopologize or decimate to budget.
- UV unwrap, then bake normals and AO.
- Apply PBR materials and the faction camo mask.
- Split parts per H3.
- Generate LODs and the impostor, and build collision.
- Export GLB.

**H5. Validator (`scripts/validate_assets.py`, gate G7). Fails the asset if any of these fail:**

- Dimensions within ±5% of the spec's real-world size.
- Triangle counts within budget for every LOD. The LOD count is correct.
- Required node names present and correctly parented. Pivots and transforms correct.
- No n-gons, no zero-area faces, no non-manifold edges on closed parts, consistent normals.
- UVs inside the 0–1 range, with no overlap where baked maps need unique UVs (texel density checked).
- Textures present, power-of-two, correct color space.
- `*_Destroyed` variant present for vehicles. Animations present for aircraft control surfaces and gear, and for infantry clip lists.
- Silhouette match: render orthographic side, top and front silhouettes. Compute IoU against the reference silhouette masks in `assets_src/refs/<id>/`. Minimum: 0.85 side, 0.80 top and front.
- `ASSETS.md` entry present with source and license.

**H6. Review artifacts.** For every model, render a turntable contact sheet (8 angles, plus a close-up of the LOD0 and LOD3 comparison) beside the reference into `build/assets_review/`. Open it yourself and note problems before marking the asset verified. Present the style board (Phase 1 in §I) to the user for sign-off before batch production.

## I. Phase gates (a phase is complete only when its gate passes and is recorded)

Run `scripts/verify.sh --phase N` and attach the report to the phase entry in `PROGRESS.md`. Then produce:

- A tagged commit.
- Exported Linux and Windows builds.
- A 2–4 minute capture video of the phase's headline features, recorded with Godot's movie maker.
- A short HTML summary page for the user, `build/reports/phase-N.html`, with screenshots, the verify summary, open issues and the next phase plan.

| Phase | Required requirement IDs at `verified` | Extra exit criteria |
|---|---|---|
| **0 Foundations** | SIM-01, SIM-02, SIM-03, QA-01, QA-02 (on existing code). G1, G2, G3, G10, G11 and G12 operational. | `scripts/bootstrap.sh` reproducible on a fresh Omarchy user account. API docs dumped. All tool versions pinned. |
| **1 Vertical slice (Cold War)** | SIM-04 to SIM-10 and SIM-12; CMD-01 to CMD-06; FLY-01 to FLY-03; SUP-01, SUP-02 and SUP-05 (Cold War subset); AUD-01 and AUD-02 (slice lines); UX-01 (subset); PERF-01 on the slice. | 25 units per side with validated models. One 8×8 km map. **Style board approved by the user.** A human-playable 20-minute skirmish. |
| **2 Three eras** | ERA-01, ERA-04, SIM-11, SUP-03, SUP-04, FLY-04 to FLY-06, REN-01 to REN-03, AUD-03. | 30 units per faction validated. 2 maps per era. |
| **3 Campaigns** | MODE-02, CMD-07, CMD-08, UX-03. | 3 operations per era, then expand to 6. |
| **4 Multiplayer** | NET-01, NET-02, FLY-07. | A real 2-machine LAN test by the user, recorded. |
| **5 Grand Strategy** | GS-01 to GS-05. | |
| **6 Content and polish (1.0)** | Everything else, including ERA-02, ERA-03, MODE-01, UX-02, PERF-02, QA-03. | 60 or more units per faction. 4 maps per era. A full `--full` verify run green twice in a row on different days. Release checklist. |

## 14. How you work

- **Plans and tracking:** keep `DESIGN.md`, `ROADMAP.md`, `PROGRESS.md` (dated entries), `DECISIONS.md`, `ASSETS.md` and `LEGAL.md` current.
- **Verify, don't assume.** Every claim of "works" must come with a check that could have failed: a unit test, a headless match result, a determinism hash, a profiler capture, or a screenshot or video from a batch-mode build.
  - In reports, keep these four states distinct: **prepared** (written, not run), **verified** (checked as above), **built** (playable build produced) and **delivered** (handed to the user).
- **Automated self-play.** Run AI-vs-AI matches nightly or each phase. Track win rates per faction, unit usage and kill/death ratios to drive balance, and flag degenerate strategies.
- **Small, reviewable commits.** No giant unreviewable drops. Each phase ends with a tagged build and a short changelog.
- **When to ask the user,** and only these:
  - Final name approval.
  - Art style board sign-off.
  - Any spending beyond the agreed budget (ElevenLabs, AI 3D credits, paid assets).
  - Switching engines.
  - Faction or content sensitivity questions.
  - Otherwise make the call, record it in `DECISIONS.md`, and keep going.
- **No fake progress.** Never stub a system and report it as done. Placeholders must be labeled as placeholders in-game and in `PROGRESS.md`.

## 15. Name

The working title is **SIX ACTUAL**. On military radio, "Six" is the commanding officer's callsign and "Actual" means the commander is speaking personally rather than through staff. That is exactly this game's hook: you are Six Actual until you climb into a cockpit and your XO takes the net. It is short, sounds right in voice lines ("Bravo, this is Six Actual"), works in all three eras, and a quick search found no existing game using it.

Use it in-fiction too: the player's radio callsign is "Six Actual", the XO is "Six Romeo" (stand-in), and commanders address the player that way in ElevenLabs dialogue.

Backup names if a conflict turns up: *Theater Command*, *Hammer & Anvil*, *Top Cover*. Before Phase 1 ends, run a collision check on Steam, the console stores, trademark databases (USPTO, EUIPO) and domains, then report back for the user's final sign-off.

## 16. Reference material (the user may attach)

- **Wreckage (browser prototype):** procedural cities with chunk-based structural destruction (cells collapse when unsupported), plus artillery, carpet bombing, MOAB and nuke effects.
- **Warhammer (browser prototype):** AC-130 gunship overwatch of a convoy, with three weapons, thermal view, flares vs MANPADS, radio chatter and a director that spawns ambushes.

Treat these as feel references only. Rebuild properly in Godot. Do not port the code.