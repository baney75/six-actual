# SIX ACTUAL: full-game build contract (v5)

**For:** Claude Opus 5.5 running in Claude Code on the user's Omarchy machine (Arch Linux + Hyprland).
**Role:** lead engineer, technical artist, 3D modeler, VFX and animation artist, sound designer and producer.
**Mission:** build *Six Actual*, a three-era combined-arms RTS, to a shippable 1.0, including every 3D model, in phases, with a playable build and machine-checked evidence at the end of each phase.

Read the whole contract before writing code. Then create `CLAUDE.md`, `DESIGN.md`, `ROADMAP.md`, `REQUIREMENTS.md` (from §F) and `docs/STATE.md`, and keep them current.

**How this contract is organized.**

- §A: how you operate (Claude Code setup, state, subagents, hooks, rules).
- §B: the machine (Omarchy toolchain).
- §C: the mechanics bible (what to take from the best games and what to avoid).
- §1–§10: the game design and architecture.
- §11: art direction. §R: reference research. §M: the modeling studio (pipelines, contact sheets, review loops). §D: the detail bible (explosions, destruction, troop movement, vehicle and aircraft feel, animation, sound, environment, readability). §W: other production workflows.
- §12: audio.
- §F: the requirements catalog. §G: the verification suite. §I: the phase gates.
- §14–§16: working rules, the name, and reference material.

Where two rules overlap, the stricter one wins.

---

## A. Operating model (Claude Code on Omarchy)

**A1. Your knowledge has a date. Check it.**

Before relying on any API, verify the current version and docs. This covers Godot 4.7.x (.NET), Blender 5.2 LTS, the MCP servers, ElevenLabs and Steamworks.

- Dump the Godot API you build against with `godot --headless --doctool docs/godot-api` and search it.
- Read each library's current README and changelog.
- Prove each call compiles and runs before building on it.
- When docs and behavior disagree, behavior wins. Log it in `DECISIONS.md`.

**A2. Project memory is files, not context.** Long sessions get compacted and new sessions start cold. Keep these files current:

- `CLAUDE.md` (under 200 lines): project map, build and verify commands, conventions, links to the files below. Claude Code loads it every session, so keep it lean and accurate.
- `docs/STATE.md`: phase, requirement IDs in progress, last verify result (date and report path), blockers, and the next 3 concrete actions. Rewrite it at the end of every work block.
- `PROGRESS.md`: dated, append-only log of changes, with the verify outcome for each.
- `REQUIREMENTS.md`: status per ID (`todo`, `in-progress`, `verified`, `waived (reason, approval)`), each linked to the passing report.
- `DECISIONS.md`, `ASSETS.md`, `LEGAL.md`, `ROADMAP.md`, `docs/art-bible.html`.

**A3. Session-start ritual (every session, in order).**

1. Read `docs/STATE.md`, `ROADMAP.md`, the open IDs in `REQUIREMENTS.md`, and the last 40 lines of `PROGRESS.md`.
2. Run `scripts/verify.sh --quick` and read the report. If it is red, fixing that is the first task.
3. Pick the smallest open requirement in the current phase and write the plan into `docs/STATE.md`. For anything architectural, plan in Claude Code's plan mode first.

**A4. Work loop: verifier first, one requirement at a time.**

1. Write or extend the verifier, run it, and confirm it fails for the right reason.
2. Implement until it passes.
3. Run `scripts/verify.sh --phase N`.
4. Update `REQUIREMENTS.md`, `PROGRESS.md` and `docs/STATE.md`.
5. Commit with the requirement IDs in the message.

**A5. Claude Code setup (create these in Phase 0, under `.claude/`).**

Check `claude --help` and the current Claude Code docs for exact syntax. Features change.

**Subagents** (`.claude/agents/*.md`), each with a narrow brief and only the tools it needs:

| Agent | Job | Must not |
|---|---|---|
| `sim-engineer` | Deterministic C# simulation, tests, scenarios | Touch art or Godot scenes |
| `godot-engineer` | View layer, UI, input, VFX hookup, exports | Change sim rules |
| `ai-designer` | Commander and XO planners, behavior metrics, soak analysis | Hardcode scenario outcomes |
| `modeler` | One asset at a time through the §M pipeline, producing contact sheets | Mark its own asset approved |
| `art-reviewer` | Scores model contact sheets (§M5) and detail sheets (§D) against their rubrics, fresh context each time | See the modeler's reasoning, only the spec, reference pack, sheets and rubric |
| `ref-hunter` | Builds reference packs and boards to the §R coverage checklist, logs licenses | Put look-only material anywhere shippable |
| `fx-animator` | VFX (§D1, D2, D8) and animation (§D3–D6), rendering frame-strip and pose sheets | Mark its own effects or clips approved |
| `audio-producer` | Sound design entries (§D7), dialogue matrix, ElevenLabs batches, loudness and spectrogram checks | Spend past budget |
| `qa-verifier` | Runs gates, reads reports, opens frames and sheets, files defects | Edit product code |

**Hooks** (`.claude/settings.json`):

- **Before stopping:** a hook runs `scripts/verify.sh --quick` and blocks the stop if it fails, unless `docs/STATE.md` records the failure as a known blocker.
- **After editing:** a hook runs `dotnet format` on touched C# files and the data validator on touched JSON.
- **Before editing:** a hook blocks writes to `verify/` and `verify/thresholds.toml` unless the commit message will be prefixed `VERIFY:`. Implement this as a pre-commit check plus a hook warning.

**Skills** (`.claude/skills/`): write project skills for the repeatable procedures so every session runs them the same way.

- `new-unit` (data, then model brief, then card, then audio set).
- `asset-pipeline` (the §M stages).
- `contact-sheets` (render any CS sheet for any asset).
- `scenario-test` (author a G4 scenario).
- `phase-gate` (run and package a phase).
- `dialogue-batch` (ElevenLabs batch with manifest).
- `ref-pack` (§R pack and board for any item).
- `vfx-effect`, `anim-clip`, `sound-design` (the §D production and check loops).

**Parallel work:** use git worktrees for independent streams, for example asset batches, an audio batch, and the sim feature branch. Never run two agents on the same files. Merge through `qa-verifier` passing `--quick`.

**MCP servers:**

- Godot: `godot-agent-loop` (targets 4.7, works with Claude Code).
- Blender: `mcp-for-blender` (the renamed `blender-mcp`; install with `claude mcp add blender uvx mcp-for-blender`), plus plain `blender --background --python` for batch jobs.
- ElevenLabs: the user has an ElevenLabs connection. Use the MCP if it is exposed in your session, otherwise the Python SDK.
- Everything must also work from plain CLI scripts, so CI never depends on an MCP being up.

**GUI automation:** see §B4 (cua-driver first, with a recorded fallback). Prefer headless and CLI paths whenever they exist.

**A6. Effort and context.**

- Use maximum reasoning effort for architecture, netcode, determinism bugs, verifier design and art review.
- Delegate wide, independent work to subagents so the main context holds decisions, not file dumps.
- Before a long autonomous stretch, write the plan and the stop conditions into `docs/STATE.md`.

**A7. Forbidden moves (any one voids the phase).**

- Weakening, skipping, deleting or `#if`-ing out a failing test or verifier.
- Loosening a threshold or editing a golden hash without a `DECISIONS.md` entry and user approval.
- Mocking the simulation in scenario, soak or integration tests.
- Hardcoding outcomes, special-casing seeds, or detecting the test harness.
- Describing visuals, models or audio you have not opened. You can see images: look at every contact sheet you cite.
- A modeler approving its own asset.
- Hand-writing large data files. Generate them with scripts and validate against schema.
- Reporting placeholders as done.

**A8. Stop and ask the user only for:**

- Name sign-off.
- Art bible and style board sign-off.
- Any spend beyond the agreed budget.
- Engine switch.
- Content or faction sensitivity.
- An asset that fails review after 4 iterations.
- A requirement that cannot be met as written. Bring evidence and a specific waiver proposal.

Everything else: decide, record it in `DECISIONS.md`, and continue.

**A9. Reporting.** The user reads polished HTML, not walls of Markdown. At every phase end, and whenever you need a decision, write `build/reports/<topic>.html`: self-contained, with screenshots and contact sheets embedded, the verify summary, open issues and the decision needed. Keep the chat handoff to a few lines with the file path.

In every report, state for each item whether it is:

- **prepared** (written, not run);
- **verified** (a check that could fail passed, with the report path);
- **built** (an exported build exists, with the path); or
- **delivered** (handed to the user).

---

## B. The machine: Omarchy (Arch Linux + Hyprland)

You run in **Claude Code** on the user's **Omarchy** machine: Arch Linux, the Hyprland (Wayland) desktop, bash, `yay` for official-repo and AUR packages, and `mise` for language toolchains. Everything in this contract must build, run and be verified here. Windows and macOS builds are cross-exported from this machine.

**B1. Profile first.** Record these in `PROGRESS.md`:

- `fastfetch` (or `inxi -Fxz`)
- `vulkaninfo --summary`
- `nvidia-smi` or `lspci -k | grep -A3 VGA`
- `free -h` and `df -h ~`
- the Hyprland version (`hyprctl version`)
- `claude --version`

Note the GPU vendor and driver, because NVIDIA proprietary, NVIDIA open and AMD Mesa behave differently under Wayland and Vulkan. The full asset library and builds need roughly 150 GB free. If space is short, warn the user before Phase 1.

**B2. Ground rules on this machine.**

- Work only inside the project directory (default `~/Projects/six-actual`). Never edit Omarchy, Hyprland, shell, systemd or other system configuration, and never touch other repositories.
- `sudo` is only for system packages. Put every system package in `scripts/bootstrap.sh`, show the user the full list once, and let the user run it. Never run destructive or system-wide commands outside that script.
- Pin every tool version in `tools/versions.env`. Prefer pinned official release archives unpacked into `tools/` over rolling Arch packages for the engine and Blender, because a `yay -Syu` must never silently change a build.
- Secrets go in a gitignored `.env`. Never print them in logs.

**B3. Bootstrap (`scripts/bootstrap.sh`, idempotent).**

```bash
# system packages (the user runs this once; one sudo prompt)
yay -S --needed git git-lfs base-devel python uv ffmpeg sox imagemagick jq unzip p7zip \
  vulkan-tools mesa-utils fastfetch grim slurp wl-clipboard libxkbcommon-x11 \
  noto-fonts openal                           # add the user's GPU driver packages only if missing; never swap drivers
git lfs install

# toolchains via mise (no sudo)
mise use -g dotnet@10      # use the .NET SDK the pinned Godot version requires (read its release notes)
mise use -g node@lts       # MCP servers that ship through npx

# engine: official Godot .NET Linux build, pinned, unpacked into tools/godot/ (not the Arch package)
#   Godot_v${GODOT_VERSION}-stable_mono_linux_x86_64.zip plus the matching .NET export templates
#   from godotengine.org or the GitHub releases; verify SHA-512; templates go to
#   ~/.local/share/godot/export_templates/${GODOT_VERSION}.stable.mono/
# Blender: official 5.2.x LTS Linux tarball from download.blender.org, verify SHA-256,
#   unpack to tools/blender/ (not the rolling Arch package)
```

**B4. Omarchy-specific rules.**

- **Wayland.** Godot 4.7 runs natively on Wayland. Blender 5.2 runs under Wayland or XWayland. If you hit input or window bugs, retry with `--display-driver x11` (Godot) or `WAYLAND_DISPLAY= ` (Blender, forcing XWayland), and record which one you used.
- **Headless first.** Renders, imports, exports, contact sheets and verification run headless (`--headless`, `blender --background`), so they never depend on a focused window.
- **Long jobs** (overnight renders, soaks) run under `systemd-inhibit --what=idle:sleep --why="six-actual batch" <cmd>` so the machine does not sleep. Tell the user when a long batch starts.
- **Thermals.** Watch for drift in fixed benchmarks during long renders. Log GPU temperatures (`nvidia-smi --query-gpu=temperature.gpu --format=csv -l 30` or `sensors`) alongside perf captures.
- **Screenshots of live windows:** `grim -g "$(hyprctl activewindow -j | jq -r '"\(.at[0]),\(.at[1]) \(.size[0])x\(.size[1])"')" out.png`.
- **GUI automation:** when something can only be checked or done through a GUI, use **cua-driver** (trycua). The user prefers it to built-in computer use. First confirm it supports this Linux and Wayland setup. If it doesn't, fall back to `grim` screenshots, `wtype` or `ydotool` input, and the Godot and Blender MCP servers, and record that in `DECISIONS.md`. Prefer headless and CLI paths whenever they exist.
- **Exports from Omarchy:**
  - **Linux x86_64** (primary test target).
  - **Windows x86_64** (cross-export; set the icon and metadata with `rcedit` under Wine only if needed, otherwise skip).
  - **macOS universal** (unsigned zip, untested here unless the user provides a Mac).
- **Performance gates** measure this Omarchy machine (Vulkan, Forward+). If the user has a Windows PC, also capture there with an exported build plus `scripts/perf_capture.ps1`, which writes a CSV back. A missing Windows run is reported, not faked.

**B5. Commands that define "verified":**

```bash
dotnet test sim/                                    # unit tests, determinism, analyzers
dotnet run --project sim/Headless -- --selftest     # AI-vs-AI match to completion, prints state hash
tools/godot/godot --headless --path game --import
tools/godot/godot --headless --path game --export-release "Linux"   build/linux/SixActual.x86_64
tools/godot/godot --headless --path game --export-release "Windows" build/windows/SixActual.exe
tools/godot/godot --headless --path game --export-release "macOS"   build/macos/SixActual.zip
tools/godot/godot --path game --write-movie build/verify/slice.avi --fixed-fps 30 -- --scenario=verify_slice
tools/blender/blender --background --factory-startup --python scripts/validate_assets.py -- assets_src/
scripts/verify.sh --quick | --phase N | --full
```

`--write-movie` needs a rendering context. On Omarchy, run it inside the user's Hyprland session (it opens a window briefly), or in a nested headless Wayland compositor (`cage` or `weston --backend=headless`) when the user is away. Pick one, script it, and record it.

## C. Mechanics bible: steal the best, avoid the known failures

These are design decisions, not suggestions. Each line names the source game, the mechanic, and how Six Actual uses or fixes it. Record any deviation in `DECISIONS.md` with a reason.

**C1. Steal (adopt and adapt)**

| # | Source | Mechanic | Six Actual version |
|---|---|---|---|
| 1 | Steel Division 2 | **Battle phases.** A favors light units and recon, B unlocks medium units, C unlocks heavy armor. Phases A and B last 10 minutes; C runs to the end. | Three phases per battle in every era. Each card has a phase gate. Each deck picks an **income profile** that peaks in a different phase (SD2's Vanguard, Maverick, Balanced and Juggernaut shapes). |
| 2 | Steel Division 2 | **Dynamic frontline.** It moves with each side's influence: pushing forward advances it, routs and losses pull it back. Recon and AT teams don't move it. Units outside friendly territory or cut off from supply can't shed suppression. | The frontline is computed from unit influence fields each sim tick and drawn on the map. Being encircled blocks morale recovery and resupply. Commanders read the line to plan. |
| 3 | Steel Division 2 / WARNO | **Morale ladder:** shaken, then pinned, then routed, with routed units falling back automatically. Command units steady nearby troops. | Same ladder. HQ units steady their area. Routed units path to the nearest friendly HQ. |
| 4 | World in Conflict | **Tactical aid.** No base building. A second currency is earned by **kills, capturing and fortifying objectives, repairing, and transporting troops**, so support work pays off. | **Support Points (SP)**, separate from deployment points, earned by combat *and* support actions: resupply delivered, repairs, medevac, captures, recon spotting that leads to kills. Spent on off-map support (§6). |
| 5 | World in Conflict | **Recharge cadence.** Each aid has up to three uses before it recharges. The nuke is available but delayed. | Every off-map support has charges plus a recharge timer. The tactical nuke (grand strategy, or a custom-match toggle) has a long arming delay that both sides are warned about. |
| 6 | World in Conflict | **Destruction with consequences.** A destroyed bridge forces a detour, or a rebuild via tactical aid. Bombed buildings collapse and smoke for minutes. | Bridges, buildings and roads are destructible sim objects that change pathing and line of sight. Engineers or a support card rebuild bridges. Smoke columns block sight. |
| 7 | Broken Arrow | **Unit customization in the deck:** loadouts per vehicle and pylon configurations per aircraft. Logistics vehicles resupply ammo and fuel. Recon and jammers matter. | Every armed card has 1–4 loadout variants with cost deltas. Every aircraft has a pylon editor with era-legal weapons. Jammers are units (Cold War and modern). |
| 8 | Broken Arrow | **Missile duels and interception.** Active protection systems intercept incoming missiles; ballistic missiles can be shot down. | Modern-era APS and point defense are simulated as interception rolls with ammo. Artillery rockets and cruise missiles can be intercepted by SHORAD and C-RAM where era-legal. |
| 9 | Broken Arrow | **Combined arms as interlocking levers:** armor protects AA, AA protects artillery. | Balance targets require each class to be necessary. The soak test (§G5) fails if a single-class deck wins more than 55% against a combined-arms deck. |
| 10 | Gates of Hell / Men of War | **Direct control.** Take any unit; 1st-person, 3rd-person and top-down views switch instantly. | Our **Take Control** (§5): any aircraft in v1, then ground vehicles and teams in a later phase. |
| 11 | Company of Heroes 3 | **Cover and retreat.** Cover is color-coded (green heavy, yellow light, red open) and only protects from the front arc. Retreating squads keep their veterancy. | Cover tiers come from terrain and props, show on hover, and are directional. A retreat order paths to safety at a speed bonus. Veterancy (3 levels) persists through retreats. |
| 12 | Company of Heroes 3 | **Battlefield creates cover.** Wrecks and craters become new cover. | Wrecks and craters are spawned as cover objects with tiers. |
| 13 | Hearts of Iron IV | **Battle planner.** Draw frontlines and offensive arrows; assigned divisions execute; manual overrides cost the planning bonus. | Sub-commander orders are drawn the same way (front, offensive arrow, spearhead, garrison area). Letting a plan build preparation time grants an attack bonus; micromanaging a commander's units decays it. |
| 14 | Hearts of Iron IV | **Division designer and supply.** Templates of battalions; supply flows from hubs; armies stall when they outrun supply. | Grand strategy (GS-01) uses formation templates and supply hubs, rail and road logistics, and an out-of-supply attrition curve. |
| 15 | WARNO Army General | **Persistent battalions** on a strategic map. Supply trucks that survive come back resupplied next battle. Bigger logistics vehicles resupply smaller ones (helicopter to truck to unit). | Campaign and grand strategy persist unit state between battles. The logistics chain is tiered. |
| 16 | Regiments | **Persistent multi-phase operations.** Wrecks and scarred terrain remain between phases, time of day advances, and you fight at night with NVGs or illumination. **Operational Points** replenish losses or buy task forces. **Event cards** shake up missions. | Campaign operations run 3–5 phases on the same map with persistent wrecks, craters and fortifications, advancing time of day, OP-style resupply between phases, and an event deck. |
| 17 | Regiments | **Task forces chosen during the battle** instead of only a pre-built deck. Approachable for newcomers. | "Field Requests": at each phase change you pick 1 of 3 offered task forces, in addition to your deck. This is the default for campaign; skirmish can toggle it. |
| 18 | Total War / Company of Heroes 3 | **Campaign map to battle link,** with auto-resolve as an option. | GS-02. Auto-resolve shows its odds breakdown before you commit. |

**C2. Avoid (known failures, written as requirements)**

| # | Failure seen in reviews or player reports | Rule |
|---|---|---|
| 1 | Gates of Hell: AI-controlled units badly underperform compared with direct control (players report anti-tank rockets nearly always hit under direct control but mostly miss under AI), so direct control becomes mandatory in PvP. | **No direct-control buff.** A player-flown aircraft uses the same hit model as an AI pilot. The skill edge comes from choices like target selection, approach and timing, not stat multipliers. Gate: a scenario shows AI-pilot and scripted-player runs on identical attack profiles have hit rates within ±10%. |
| 2 | Original Call to Arms: first-person mode removed situational awareness and pushed players to fight with as few units as possible. | While flying, you keep a minimap, the XO log and alert toasts. The XO gets no handicap. An aircraft can't be flown for more than one sortie in a row without the player returning to command. |
| 3 | Gates of Hell: exhausting micro across hundreds of units, no tactical pause. | Sub-commanders absorb the micro (§4). Singleplayer has tactical pause and slow-motion. Formation and line-up orders exist. |
| 4 | Broken Arrow: one AA unit can wipe out expensive jets, so air power feels like luck. | Air loss is never a coin flip. Radar warning, terrain masking, SEAD/DEAD counterplay and readable AA threat rings exist, and the AI uses them. Gate: no single AA card destroys more than 1.5× its cost in aircraft on average in soak runs. |
| 5 | Broken Arrow: unit roles are hard to read (multiple near-identical launcher teams). | Every card shows role icons, an effective-range bar and a one-line "use it for" sentence. The UX smoke test checks they exist for every card. |
| 6 | Steel Division 2: phase C income gets so high players can't spend it. | Income is tuned so the median unspent bank stays under 30% of phase income in soak runs. Commanders spend on the player's behalf if allowed. |
| 7 | WARNO Army General: the strategic AI ignores victory conditions and fails to reinforce attacked formations; logistics and command on the campaign map feel basic. | The GS AI is an HTN planner with explicit goals. Gate: in AI-vs-AI grand strategy, the AI reinforces an attacked province within 2 turns in at least 80% of cases when reserves are available. |
| 8 | Company of Heroes 3: the dynamic campaign was criticized for feeling less dynamic than marketed. | Each operation outcome must change the next map state (control, wrecks, supply), not just a score. A test checks that 2 different outcomes produce 2 different next states. |

**C3. Signature experiences (what reviewers should remember)**

1. Watching the XO coordinate a combined-arms assault while you roll in for a strafing run.
2. A town collapsing floor by floor under artillery, then becoming the new defensive line.
3. Nights lit by flares and illumination rounds, with tracers everywhere.
4. Voiced radio chatter that tells you what's happening before the UI does.

## 1. The game in one paragraph

Six Actual is a large-scale combined-arms RTS in the tradition of **Steel Division 2**, **WARNO** and **Broken Arrow**, spanning three eras: **World War II (1944–45)**, **the Cold War (1989)** and **the modern day (2020s)**. You command a whole battlegroup through a staff of AI sub-commanders. At any moment you can **take personal control of an aircraft and fly it yourself**, while your executive officer (XO) runs the battle in your place. When your plane runs out of fuel or ammunition, is shot down, or you press a key, you snap back to the command table. Around the battles sits a **grand-strategy layer** (Hearts of Iron / Total War style) with production, research, fronts and, in that mode, **nuclear weapons**.

## 2. Pillars (every decision should serve these)

1. **Command, not clicking.** The player sets intent and delegates. Micro is always possible, never required.
2. **Support is the star.** Artillery, air power and logistics are deep, varied and era-authentic, and they decide battles.
3. **You can always get your hands dirty.** Flying an aircraft is a real, fun, skill-based mode, not a minigame.
4. **Three eras, one ruleset.** The same systems (line of sight, suppression, supply, morale) apply across eras. Era identity comes from units, doctrine, HUDs, audio and visuals.
5. **It has to look and sound like a war zone.** Smoke columns, tracer fire, burning wrecks, destructible towns, radio chatter, weather and time of day.

Every pillar is backed by specific mechanics in §C. Read §C before designing any system.

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
- **Performance target:** 2,000 or more simultaneous simulated entities (infantry counted per squad member or per squad, decide and document) at 60 fps at 1440p on an RTX 3060-class GPU with a 6-core CPU. Measure on the user's Omarchy machine and, if available, the user's Windows PC (§B4), and report both against this target.

## 10. Technical architecture (decided, change only with a written reason in `DECISIONS.md`)

- **Engine: Godot 4.7.x (.NET / C# build), Forward+ renderer.** Pin the exact patch version in `tools/versions.env`. Reasons:
  - It is native to Linux and Wayland (Vulkan Forward+), ships as a single pinned binary, and needs no account sign-in or license activation, so an agent can install, build and export to Linux, Windows and macOS entirely from the command line on Omarchy.
  - Scenes and resources are plain text (`.tscn`, `.tres`), which agents can read, diff and edit safely.
  - Headless import and export work out of the box, and the MIT license has no revenue strings.
  - Unity's Linux editor is still labeled experimental on the Arch wiki, its Hub is AUR-only, and users report freezes under Hyprland. Unity and Unreal both need account sign-ins and keep much state in opaque formats. Godot's text scenes plus a pure C# simulation give an unattended agent the most control and the most verifiable build on this machine.
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

Model production, budgets, conventions, contact sheets and review are specified in §M. Before mass production, produce a style board: 3 tanks, 2 aircraft, 1 infantry squad and 1 town block per era (Cold War first, for Phase 1). Stop for the user's sign-off.

## R. Reference research (find it, log it, prove coverage)

Nothing gets modeled, animated, voiced or sound-designed from memory. Every asset, effect, animation set and sound has a **reference pack** that meets a coverage checklist before production starts. The `ref-hunter` subagent builds packs; `qa-verifier` checks coverage (gate G14).

**R1. Two kinds of reference.**

| Kind | May be used for | Rules |
|---|---|---|
| **Distributable** | Ortho silhouette masks, texture sources, decals, in-game imagery | Public domain, CC0, CC-BY 4.0, or the user's own. Logged in `ASSETS.md` with URL, author, license and retrieval date. |
| **Look-only** | Studying shape, detail placement, color, motion, sound character | Any publicly viewable source. Stored only in gitignored `refs_private/`, never shipped, traced, sampled into textures, or included in reports. Logged in `refs_private/log.csv` with URL and date. |

Sound references are look-only by default: they describe what a sound should feel like. Never sample them.

**R2. Where to look (in order).**

- **US government (generally public domain):**
  - DVIDS (dvidshub.net) for modern US equipment photos and video.
  - Army technical manuals (TM series) and field manuals for dimensions, crew procedures, formations and drills.
  - The National Archives catalog (catalog.archives.gov) and Library of Congress for WW2 photos, film and drawings.
  - NASA and USAF archives for aircraft.
  - Check each item's rights field. Some DVIDS items are not public domain.
- **Wikimedia Commons**, through its API (`action=query&prop=imageinfo&iiprop=url|extmetadata`). Filter by `LicenseShortName` in {PD, CC0, CC BY 4.0, CC BY-SA 4.0 (look-only unless the share-alike obligation is accepted in `DECISIONS.md`)}.
- **Museum open-access collections** with CC0 terms: the Smithsonian (open access, CC0 items only) and others with explicit CC0 flags.
- **Look-only sources** (never distributed): manufacturer brochures, walkaround photo sites, modeling-kit reference books, documentaries and combat footage, and WARNO, Broken Arrow and Steel Division 2 footage for game-feel comparison.
- **Dimensions and data:** technical manuals, official fact sheets, and peer-reviewed or well-sourced references. Record every number with its source in the spec.
- **Terrain and maps:** USGS 3DEP and Copernicus GLO-30 elevation data, and OpenStreetMap (ODbL attribution in credits).
- **Motion:** field manuals (squad drills, bounding overwatch, react to contact), training and documentary footage (look-only), and the CC0 animation libraries in §D6.
- **Sound:** CC0 libraries (Freesound with the CC0 filter, the Sonniss GDC bundles under their license), plus look-only listening references logged with timestamps.

**R3. Coverage checklist per asset class (`verify/ref_coverage.toml`).**

| Class | Required before blockout |
|---|---|
| Ground vehicle | Front, rear, left, right, top (or a plan drawing), four 3/4 views, the running gear close-up, turret details, the rear deck and engine, the interior or hatch-open view (for crew animations), each era or variant difference, 2+ camo examples per faction, a destroyed or burned-out example, and the dimension sources |
| Aircraft | Side, top and front (or 3-view drawings), cockpit and canopy, gear and doors, each pylon config, the exhaust or prop, the afterburner or engine glow (look-only footage), and a wreck or crash example |
| Infantry kit | Front, side and back uniform per nation and era, webbing, helmet, each weapon held (photos of the grip and stance), and the radio or launcher carry |
| Building kit | Street-level photos of 10+ buildings of that region and era, roof types, window and door patterns, damaged and collapsed examples, and rubble piles |
| Explosion or VFX | 5+ footage clips of the real event at different distances, with notes on timing (flash, fireball, smoke rise speed, debris arc) |
| Sound | 3+ listening references per sound (near, mid and far where possible), with a written character note |
| Animation set | Field manual pages for the drill plus 2+ footage references per motion |

**R4. Reference board per item.** `scripts/refs/board.py <id>` writes `build/refs/<id>.html`:

- A grid of thumbnails, each with its angle tag, license badge (green for distributable, gray for look-only) and source link.
- The spec's dimension table with sources.
- The coverage checklist with ticks.

Look-only images appear on the board as local file links only, so they are never embedded in shared reports. `art-reviewer` opens this board alongside the contact sheets.

**R5. G14 Reference coverage gate.** Production on an item cannot start until all of these hold:

- Every checklist slot has at least one tagged reference.
- Every spec number has a source.
- Every distributable reference has a license entry.

The gate also checks that no look-only file path appears anywhere under `game/`, `assets_src/` exports or `build/reports/`.

## M. Modeling studio: pipelines, contact sheets, review loops

You build or source every 3D asset in the game. The quality bar is that each asset would not look out of place in WARNO or Broken Arrow at RTS zoom, and that aircraft hold up in the Take Control chase camera.

Blender 5.2 LTS (pinned in `tools/blender/`; Cycles on the GPU through CUDA/OptiX for NVIDIA or HIP for AMD, with any CPU fallback recorded) is the modeling tool. Every step is scripted (`assets_src/pipeline/*.py`, run with `tools/blender/blender --background --factory-startup --python`). The MCP is for interactive inspection and fixes, never the only path.

### M1. Asset lifecycle (states live in `assets_src/registry.json`, mirrored to `build/assets_review/board.html`)

```
spec → refs → blockout → [BLOCKOUT REVIEW] → detail → retopo/LOD → UV/bake → materials → rig/split
     → [FULL REVIEW] → in-engine lineup → [ENGINE REVIEW] → approved → (variant fan-out)
```

- An asset can only advance through a review gate when both the automated validator (G7) passes **and** `art-reviewer` scores it at or above the threshold (M5).
- Rejected assets go back to the named stage with a written fix list.
- **After 4 rejections at the same gate, stop and escalate to the user** with the latest sheets and the reviewer notes.

### M2. Inputs per asset (create these before any modeling)

0. **Reference pack first.** Run `ref-pack` (§R) and pass gate G14 before anything below.
1. **Spec** `data/assets/<id>.json` (schema-validated):
   - Real dimensions in meters (length, width, height, track width, wheelbase, gun length, wingspan, etc.), each with a cited source.
   - Class and its triangle budgets.
   - Part list, faction camo set, damage states.
   - Animation list.
   - **Five signature features** that make it recognizable (for example, for a T-72: the "Dolly Parton" turret profile on the B model, the 6 road wheels with return rollers, the snorkel stowed on the turret rear, the bustle-less turret, and the IR searchlight to the right of the gun).
2. **Reference pack** `assets_src/refs/<id>/` (built by §R; the items below are the modeling-specific parts):
   - Public-domain or permissively licensed images only: US government photos and manuals, or Wikimedia Commons public domain or CC0/CC-BY. Each one gets a license line in `ASSETS.md`.
   - **Orthographic silhouette masks** (side, top, front) traced from references with `scripts/refs/trace_ortho.py`, scaled to the spec's real dimensions. These are the ground truth for silhouette scoring.
   - Where no good ortho exists, draw the mask from measured dimensions plus photos. Mark `mask_confidence: low` and require human review for that asset.
3. **Era art bible** (`docs/art-bible.html`, user-approved): palettes per faction and era, camo patterns, wear levels, decal language, material library, and lighting rigs.

### M3. Production routes (pick per asset, record the route in the registry)

| Route | When | How |
|---|---|---|
| **P: Parametric procedural** (default for vehicles, aircraft and building kits) | Anything with a measurable silhouette | Python generators in `assets_src/procedural/`. See below. |
| **L: Library** | Props, foliage, HDRIs, textures, generic clutter | Poly Haven, ambientCG, Kenney, Quaternius, Fab (free tier), Sketchfab downloadable CC0/CC-BY, Meshy's CC0 public library. Verify the license page per asset. Then run it through the same cleanup, scale and validation. |
| **G: AI-generated** | Organic or complex detail (stowage, sandbags, crew figures, rubble chunks) within the approved budget | Meshy, Tripo or Hunyuan3D, from multi-view reference renders of the blockout. Always remesh to budget, re-UV and re-bake. Never ship a raw generated mesh. |
| **H: Hybrid** | Most hero units | Procedural hull and turret (P), plus generated or library stowage and details (G/L), unified in one bake. |

**Procedural generators (P):**

- `ground_vehicle.py`: hull from chamfered volumes defined by a profile table; glacis angles; road-wheel arrays with suspension offsets; return rollers; drive and idler sprockets; tracks as an instanced link array along a curve (a link mesh for LOD0, a baked strip for LOD1+); turret from lofted cross-sections; gun with mantlet, fume extractor and muzzle brake; hatches, periscopes and stowage bins as parametric parts.
- `aircraft.py`: fuselage from cross-section stations (a station table of y/z radii along x); wing planform from root and tip chord, sweep, dihedral, twist and NACA airfoil; tail surfaces; intakes; canopy frame; pylons placed from the hardpoint list; movable control surfaces as separate objects with correct hinge pivots.
- `helicopter.py`, `infantry_kit.py` (equipment attachments for one rigged base soldier per era), `building_kit.py` (modular walls, floors, roofs and openings on the sim chunk grid, 2.5–3 m, with interior faces for collapses), `props.py`.

### M4. Contact sheets (the core of review)

`scripts/sheets/render_sheets.py <id> --stage <stage>` renders every sheet below into `build/assets_review/<id>/<stage>/`, plus a `sheet_meta.json` (camera, lighting rig, render settings, git hash).

- Fixed lighting rigs, neutral gray background, consistent focal length and a 1 m grid overlay, so sheets stay comparable across assets and over time.
- EEVEE renders previews; Cycles renders the full-review hero shots.

| Sheet | Content | Catches |
|---|---|---|
| **CS-1 Ortho overlay** | Side, top and front orthographic renders, each with the reference silhouette mask overlaid in red and the model in blue. IoU score printed per view. | Wrong proportions, scale and missing parts |
| **CS-2 Turntable** | 8 angles at 45° steps, 3/4 elevation, in neutral lighting | Silhouette from every side; asymmetry bugs |
| **CS-3 Reference match** | Each reference photo beside a render from the matched camera (solved with `scripts/sheets/match_camera.py` from 6+ hand-placed point correspondences, or approximated from photo EXIF and known dimensions) | Detail placement and "does it read as the real thing" |
| **CS-4 Wireframe and topology** | LOD0 wireframe over shaded, triangle density heatmap, plus n-gon, non-manifold and flipped-normal highlights | Wasted triangles, bad topology, holes |
| **CS-5 LOD strip** | LOD0 to LOD3 and the impostor side by side, each rendered at its switch distance on a fixed screen-height ruler; triangle count under each | LOD popping and silhouette collapse at distance |
| **CS-6 UV and texel** | UV layout, a checker map on the model (texel density uniformity), and baked normal and AO previews | Stretching, seams, bake errors |
| **CS-7 Materials and camo** | The model in each faction camo scheme and wear level, plus a material ID view | Wrong palette, mask bleed, missing decals |
| **CS-8 Damage states** | Pristine, damaged, destroyed (charred, missing parts) and the wreck as cover | Damage readability, wreck silhouette |
| **CS-9 Articulation** | Turret at 0/90/180/270°; gun at min and max elevation; aircraft control surfaces at limits; gear up and down; hatch states | Pivot errors, clipping |
| **CS-10 RTS readability** | The model in-engine at the four RTS camera heights (60, 120, 200, 300 m) on three backgrounds (summer field, urban rubble, snow), in day, dusk and night (thermal and NVG looks where era-legal) | Whether it reads at gameplay distance |
| **CS-11 Scale lineup** | The model among 6 approved assets of neighboring classes, plus an infantryman and a 10 m ruler | Scale drift across the roster |
| **CS-12 Hero shot** (aircraft and flagship units) | Chase-camera and cockpit frames from the Take Control camera, in Godot | Close-up quality where the player sees it most |

Sheets **CS-10 to CS-12 render in Godot** (`game/tools/asset_lineup.tscn`, driven by `--write-movie` with a scripted camera), so review sees the real shaders, LODs, fog and lighting, not only Blender.

### M5. Review loop (independent reviewer, numeric rubric)

1. `modeler` finishes a stage, runs the validator, and renders the stage's sheets.
2. `art-reviewer` starts in a **fresh context**. It sees only the spec, the reference pack, the sheets and `docs/art-rubric.md`. It never sees the modeler's notes or reasoning.
3. It opens every sheet (it must cite each sheet file it looked at) and scores 1–5 on each criterion:

| Criterion | Weight | 5 means |
|---|---|---|
| Silhouette and proportion | 25% | Every ortho IoU is above threshold and nothing in CS-2 is off |
| Signature features | 20% | All five signature features are present and correctly placed |
| Detail density and consistency | 10% | Detail level matches neighbors in CS-11; no noisy or empty zones |
| Topology and budget | 10% | Clean CS-4; within budget with ≥10% headroom |
| UV, bake and materials | 15% | No CS-6 artifacts; camo and wear match the art bible |
| LOD and RTS readability | 15% | Identifiable at 200 m in CS-10 by class and faction; no LOD pop in CS-5 |
| Articulation and damage | 5% | CS-8 and CS-9 are correct |

4. **Pass:** weighted score ≥ 4.0 **and** no criterion below 3.
   - Blockout review scores only silhouette, proportion and signature features.
   - The reviewer writes `review.md`: scores, the 3 most important fixes (each with sheet file and region), and a verdict.
5. **Blind recognition test** (vehicles and aircraft, at full review). A separate fresh reviewer gets CS-2 and CS-10 for this asset mixed with 3 assets of the same class, without names. It must identify the correct unit from the list of 4 names. A failed identification means a forced rejection on signature features.
6. Every 25 approved assets, the user gets `build/assets_review/batch-<n>.html`: a grid of CS-2 and CS-10 for the batch with scores. The user can veto individual assets.

### M6. Automated validator (G7, `scripts/validate_assets.py`)

Fails the asset on any of these:

- **Dimensions** within ±3% of the spec (bounding box and key measured points, such as gun length and wingspan).
- **IoU** against the ortho masks: side ≥ 0.88, top ≥ 0.85, front ≥ 0.82. For `mask_confidence: low`, all three drop by 0.05 and human review is required.
- **Triangles** within budget for every LOD. LOD ratios are correct. The impostor exists.
- **Required nodes** present per class naming (M7). Pivots and transforms are correct. +Y up, −Z forward, 1 unit = 1 m.
- **Mesh hygiene:** no n-gons in exported meshes, no zero-area faces, no non-manifold edges on closed parts, consistent normals, and no loose verts.
- **UVs** inside 0–1. No overlap where bakes need unique UVs. Texel density within ±20% of the class target.
- **Textures:** present, power-of-two, correct color space, and within the size budget.
- **Variants and animations:** `*_Destroyed` variant and damage decals present; animation clips present per class.
- **Licensing:** an `ASSETS.md` entry, and the route recorded.

### M7. Budgets and conventions

| Class | LOD0 tris | LOD1 | LOD2 | LOD3 / impostor | Textures |
|---|---|---|---|---|---|
| Tank, AFV, truck | 15k–30k | ≤ 50% | ≤ 20% | ≤ 5% plus an octahedral impostor | 2K albedo, normal, ORM; camo via mask channels |
| Aircraft | 20k–40k | ≤ 50% | ≤ 20% | impostor | 2K, plus a 4K hero set for Take Control |
| Helicopter | 15k–30k | ≤ 50% | ≤ 20% | impostor | 2K |
| Infantry soldier | 5k–9k | ≤ 50% | ≤ 15% | VAT crowd mesh | era atlas 2K |
| Building module | 2k–10k per module | per chunk | | | trim sheets and atlases |

**Node naming:**

- Ground vehicles: `Root/Hull/Turret/Gun/Muzzle_0..n`, `Wheel_L_01..`, `Wheel_R_01..`, `Track_L`, `Track_R`, `Hatch_*`, `Exhaust_*`, `Optic_*`.
- Aircraft: `Root/Fuselage`, `Aileron_L/R`, `Elevator_L/R`, `Rudder`, `Flap_*`, `Slat_*`, `Gear_*`, `Prop_*` or `Exhaust_*`, `Hardpoint_*`, `Cockpit`, `Canopy`.

**Infantry:**

- One rigged base soldier per era with nation kits as attachments.
- Clips: idle, walk, run, crouch, prone, crawl, fire (stand, crouch, prone), reload, throw, carry launcher, 4 deaths, surrender, and wounded drag.
- Baked to **vertex animation textures** so 2,000 soldiers render cheaply.

### M8. Variant fan-out

Approved base models spawn variants (for example M1, M1IP and M1A1; T-72A, T-72B and T-72B3) through **modifier specs**, not copies.

- A variant spec lists part swaps, added parts and dimension deltas.
- Variants re-run the full validator and contact sheets, but only need the blockout-level reviewer pass plus the blind recognition test against their base model.

### M9. Batch production and scheduling

- Produce in waves by phase. The Phase 1 Cold War slice (25 units per side) comes first; the user signs off the **style board** (3 tanks, 2 aircraft, 1 infantry squad and 1 town block per era) before mass production.
- Run `modeler` and `art-reviewer` in pairs across 2–3 git worktrees in parallel.
- Run heavy Cycles renders overnight under `systemd-inhibit`.
- Track asset throughput and review pass rates per route in `build/assets_review/board.html`. If a route's first-pass approval rate drops below 40% over 10 assets, stop and fix that route's generator or prompt before continuing.

## D. Detail bible: the small things that make it a war zone

Every subsection gives specific behavior, numbers to start from, and how it is checked. The numbers are starting targets: tune them against references (§R), and record changes in `DECISIONS.md`. "Sheet" means a contact sheet rendered by `scripts/sheets/` into `build/detail_review/`, which `art-reviewer` must open and score with `docs/detail-rubric.md`. Visual timing is measured at fixed 60 fps captures.

### D1. Explosions

**Anatomy.** Every explosion is a timed sequence of layers, not a single sprite:

| Layer | Timing (from detonation) | Notes |
|---|---|---|
| Flash | 0–2 frames | Bright additive sphere plus a real light; light radius scales with yield and decays over 150–400 ms |
| Fireball | 0–0.6 s (large: up to 1.5 s) | Rising, rolling, cooling from white-yellow to orange to dark red to soot; spawns smoke |
| Shock ring | 0–0.3 s | Ground-level dust skirt expanding radially; visible distortion ring for large yields only |
| Ejecta | 0.05–2.5 s | Surface-specific debris (dirt clods, concrete chunks, snow, water column) on ballistic arcs, with small dust trails; lands and stays as decals or props |
| Sparks and embers | 0–3 s | Additive streaks with gravity and bounce |
| Smoke column | 0.3 s – 60 s+ | Volumetric or particle billboards; rises, spreads and drifts with the map wind; color by fuel (gray dust, black oil, white phosphorus) |
| Scorch and crater | Permanent | Decal plus a terrain deformation scaled to yield; becomes cover (CoH-style) |
| Camera | 0–0.8 s | Shake scaled by distance and yield, with a hard cap; never shake the RTS camera for distant small blasts |
| Sound | Delayed by distance ÷ 343 m/s | See D7 |

**Variants (each a separate effect, not a tint):**

- Surface: dirt, mud, sand, grass, concrete, asphalt, snow, water (column plus spray plus rings), and building interior (smoke out of windows first).
- Type: HE, airburst (no crater, downward dust cone), white phosphorus (white streamers that burn for 60 s), napalm (rolling fire line, black smoke, ground fire for 2–5 min), cluster (staggered submunition pops over an area), thermobaric (two-stage pressure flash), mine, satchel and demolition, vehicle ammo cook-off (repeated pops for 5–30 s, then a final turret toss for some classes), fuel explosion, aircraft crash, and the nuke (§8, with flash, fireball, shock dome, stem, mushroom cap, and lingering fallout dust).
- Scale: grenade, mortar, 105–155 mm, rocket, 500 lb, 2,000 lb, MOAB-class, and tactical nuke. Every number (fireball radius, smoke height, crater size) scales from a yield table in `data/vfx/yields.json`.

**Checks:**

- A **frame-strip sheet** per variant: 12 frames at fixed times (0, 1, 2, 4, 8, 15, 30, 60, 120, 300, 900 and 1800 frames) on 3 surfaces, at RTS height and close.
- An automated timing test reads the VFX event log. Flash duration must be ≤ 2 frames, smoke still present at 60 s for large yields, and the sound delay correct within 1 frame at 500 m.
- G6: a barrage of 40 simultaneous 155 mm impacts stays within the frame budget.

### D2. Destruction

**Buildings.**

- Buildings are chunk grids (SIM-10) with materials: brick, concrete, timber or plaster.
- A hit removes chunks and shows the interior (floors, wallpaper and plaster color, beams).
- Unsupported chunks fall with a short delay. They spawn dust plumes that pour out of windows and the base, plus debris that stays as rubble cover.
- Timber burns and spreads to neighboring chunks over minutes. Concrete cracks first (decal), then breaks.
- Glass shatters into particles and leaves empty frames.
- Collapsing buildings kill or injure occupants by their position in the sim, and the dust cloud blocks sight for 20–60 s.

**Other destruction:**

- Trees topple in the direction of force. Bushes and fences flatten under tracked vehicles.
- Walls and hedges can be breached by tanks, which leaves a gap that changes pathing.
- Bridges have spans that drop.
- Roads crater.
- Wrecks persist all battle (and across campaign phases), burn, then smolder and become cover.
- Terrain is deformed by craters: a heightfield edit in the sim with matching visual deformation.

**Checks:**

- **Collapse sheet:** 16 frames of a scripted collapse per building material, from 2 angles.
- **Before and after sheets** of a town block under a scripted barrage.
- G4 scenarios: line of sight changes through new breaches, pathing reroutes around rubble, and occupants take damage.
- Persistence test: the post-battle state hash includes wrecks and craters, and they reload in the next campaign phase.

### D3. Troop movement and infantry behavior

**Squads** are 6–12 soldiers with individual positions (rendered through VAT crowds), not a single blob.

- **Formations:** column on roads; line or wedge in the open; file through woods. Spacing is 3–5 m in WW2 and 5–10 m in the modern era (field manual reference).
- Soldiers spread out when under fire.
- **Movement modes:**
  - Walk (1.4 m/s), run (3.5–4 m/s), sprint (5.5 m/s, short).
  - Crouch-run under fire; crawl (0.4 m/s) when pinned.
  - **Bounding overwatch:** half the squad covers while half moves, triggered by commanders on assault orders.
- **Reactions:**
  - Contact: hit the dirt within 0.5–1.0 s; return fire from the best cover within 15 m.
  - Pinned soldiers stay prone and fire less.
  - Routed soldiers run back toward the friendly HQ, dropping heavy weapons sometimes.
  - Surrender: hands up and weapons dropped.
- **Cover use:** each soldier picks a cover slot (wall, window, crater, wreck, tree) facing the threat. Soldiers in buildings appear at windows. Soldiers never stand in the open next to free cover (G5 metric: ≥ 85% of soldiers under fire are in cover or prone within 3 s).
- **Casualties:** dying soldiers fall with a death animation chosen by cause (shot, blast, fall). The body stays (count-capped, oldest faded after a long delay). Wounded soldiers are dragged by a squadmate or medic.
- **Vehicles and infantry:**
  - Infantry mount and dismount through the vehicle's actual doors or ramps (rear ramp for BMP and Bradley, roof hatches for some), with staggered timing (0.4–0.8 s apart).
  - Infantry keep clear of moving vehicles. A vehicle never drives through friendly soldiers: it stops or reroutes.
  - Tank riders ride on hull decks in WW2 and Soviet doctrine where era-legal.
- **Crowd quality:**
  - No foot sliding: the gate measures average foot drift during ground contact ≤ 2 cm per frame.
  - Foot IK on slopes.
  - Upper-body aim toward the target. Weapon muzzles line up with tracers.
  - No clipping into walls (raycast test).
  - Turn rates are capped (no instant spins).
  - Idle variety: at least 6 idles per era, randomized so no two neighbors play the same clip in sync.

**Checks:**

- **Squad sheets:** top-down frame strips of each formation and of a react-to-contact drill.
- **Pose sheets:** every infantry clip at 8 frames in a grid.
- The foot-slide metric script.
- G5 cover-usage and stuck metrics.
- A synchronized-idle detector: correlation of neighboring soldiers' animation phase stays < 0.6.

### D4. Vehicle feel

- **Suspension:** a per-wheel spring and damper visual model. The hull pitches back on acceleration, dips on braking, rolls in turns, and rocks on firing (recoil scaled by caliber).
- **Tracks:** the track texture scrolls at exactly the ground speed (UV scroll from wheel angular velocity). Road wheels spin to match. Tracks leave decals that fade over minutes. Tracked vehicles pivot-steer on the spot where era-legal.
- **Wheeled vehicles** steer front wheels visually.
- **Turret and gun:** real traverse rates (degrees per second from data) and elevation limits. The turret visibly lags behind the target and must finish turning before firing.
- **Firing:** recoil, a muzzle blast that kicks up a dust ring around the vehicle (bigger on dry ground), and smoke drifting from the muzzle. Shell casings eject on WW2 and Cold War tanks with open hatches.
- **Exhaust:** exhaust smoke puffs on acceleration and cold start (black diesel for Soviet tanks, a heat shimmer for turbines like the M1).
- **Dust trails** scale with speed, ground type and weather (none in rain or mud, where mud spray takes over).
- **Smoke dischargers** fire grenades in the real pattern and build a smoke screen within 1–2 s.
- **Damage visible on the model:** penetration holes, scorch, a lost track (the vehicle slews), a burning engine deck, and crew bailing out through the actual hatches.
- **Destruction outcomes,** chosen by cause: knocked out (smoking), brewed up (burning 2–10 min), ammo cook-off with turret toss, and a burned-out hulk.

**Checks:** a vehicle **behavior sheet** per class showing accelerate, brake, turn, fire, and three damage states. An automated track-scroll test (texture speed versus ground speed within 3%). A turret-traverse test against data.

### D5. Aircraft feel

- Control surfaces deflect with the actual inputs (AI or player). Gear and flaps animate on approach and departure.
- Contrails above era-appropriate altitudes, wingtip vapor in hard turns, afterburner flame with a heat haze, and props with blur discs.
- **Countermeasures:** flares with the right count and pattern, chaff clouds, and the missile visibly diverting.
- **Ordnance:** weapons separate from the correct pylons, bombs fall with real ballistics, retarded bombs show their fins or chutes, and guns show muzzle flash, smoke and casing ejection plus impact walking.
- **Hits:** damage trails (smoke, then fire, then a spiral). The ejection seat, canopy and parachute are visible. The crash site leaves a burning wreck and crater.
- Helicopter rotor wash flattens grass and kicks up dust near the ground.

**Checks:** an aircraft **behavior sheet** (takeoff, attack run, hard turn, countermeasures, hit and crash), plus a test that pylon release positions match `Hardpoint_*` nodes.

### D6. Animation production

**Sources, in order:**

1. CC0 libraries, starting with the Quaternius Universal Animation Library and similar. Verify each license.
2. Blender keyframe authoring, scripted where possible (procedural gait, aim offsets).
3. Retargeting to the base soldier rig.

Mixamo needs an interactive Adobe account and has its own terms. Use it only if the user downloads the clips and confirms the license in `DECISIONS.md`.

**Clip list per era rig (minimum):**

- idle ×6, walk, run, sprint
- crouch walk, crouch idle
- prone idle, crawl
- fire stand, fire crouch, fire prone
- reload (rifle, MG, launcher)
- throw
- launcher shoulder and fire
- mount, dismount
- climb in and out of a window
- cover idle and peek
- wounded fall and drag
- 4 deaths: shot forward, shot back, blast thrown, burning
- surrender
- crew bailout (hatch and door)
- MG team and mortar team setup and teardown
- radio operator
- officer point and order gestures

**Rules:**

- **Layering:** a lower-body locomotion layer, an upper-body aim and fire layer, additive hit reactions, and foot IK.
- Root motion is extracted and checked against movement speeds in the sim.
- **Crowds:** bake every clip to VAT at 30 fps with LOD clips (fewer bones and frames for distant soldiers).
- Vehicles and aircraft animate procedurally from sim state (D4, D5); no baked clips except for hatches, ramps, gear and crew.

**Checks:**

- Pose sheets per clip.
- An automated root-motion-to-sim-speed test (within 5%).
- The foot-slide metric (D3).
- A clip coverage test against the list above.
- A retarget check: no bone stretched beyond 2% length change.

### D7. Sound production

Every sound has a design entry in `audio/design/<id>.yaml`:

- purpose
- look-only listening references (§R)
- layers
- distance bands
- variants
- loop points
- priority
- maximum concurrent instances

**Layering.** Weapons and explosions are built from layers:

| Layer | Content | Example |
|---|---|---|
| Transient | The first 0–50 ms snap | Muzzle crack, detonation click |
| Body | 50–400 ms tone | Barrel boom, fireball roar |
| Mechanics | Weapon-specific | Bolt, breech, autoloader, belt link, casing tinkle |
| Low end | Sub-bass thump (< 100 Hz) | Large guns, explosions |
| Tail | Environment-dependent reflection | Open field slapback, town echo, forest diffusion |
| Debris | After-sounds | Falling dirt, glass, metal clank |

**Distance and space:**

- Each weapon and explosion has **near (< 50 m), mid (50–500 m) and far (> 500 m) versions**. Far versions lose transients and high frequencies and gain a long low rumble.
- Sound arrives delayed by distance ÷ 343 m/s, so far explosions flash first and are heard later.
- Supersonic rounds have a crack ahead of the report when passing near the listener.
- Occlusion and obstruction low-pass sounds through buildings and terrain.
- Environment reverb zones: open, forest, town, interior and valley.

**Mix:**

- The music director ducks music under heavy combat dialogue.
- Radio voices go through a band-pass and saturation chain with squelch tails, and the chain varies by era: WW2 gets heavier noise, the modern era a cleaner digital squelch.
- A voice priority system: critical alerts beat chatter.
- Concurrency limits per category, with distance-based culling.
- Sounds follow the camera at RTS height: the listener sits at the camera's ground focus point, not at the camera itself, so the battlefield sounds near.

**Ambience beds** per map and time of day: wind, birds or insects (silenced near combat and recovering after 2–5 min of quiet), distant artillery rumble, burning crackle near fires, rain on metal and leaves.

**Vehicles:**

- Engine loops in RPM bands crossfaded by speed and load.
- Track clatter by speed and surface; gearbox whine; turret traverse motor.
- Hydraulics, idle and shutdown.
- Distinct signatures, e.g. a turbine whine for the M1 versus diesel for the T-72.

**Production:**

1. Generate candidates with the ElevenLabs Sound Effects API: several variants per layer, prompts saved in the manifest.
2. Supplement with CC0 recordings.
3. Edit with `sox` and `ffmpeg` scripts: trim, fade, normalize, and build loops with seamless crossfades.
4. Assemble layers with a small Python mixer into final variants.
5. Make **at least 4 variants per frequently heard sound** (rifle shot, MG burst, explosion size, footsteps per surface) so repeats don't sound robotic.

**Checks (G9 additions):**

- A **spectrogram sheet** per sound family (near, mid, far side by side) that `audio-producer` and `qa-verifier` open.
- Automated checks:
  - Loop seams: the correlation across the loop point is ≥ 0.9, and there is no click (sample discontinuity) above a threshold.
  - Far versions have less energy above 4 kHz than near versions.
  - Large explosions have energy below 100 Hz above a set level.
  - Variant similarity: no two variants nearly identical.
  - The distance-delay test from D1.
- An **audition page** (`build/audio_review/<batch>.html`) with players for near, mid and far versions and every variant, for the user to spot-check.

### D8. Environment, weather and light

- **Wind** is a single map value that drives smoke drift, foliage sway, flag direction, dust and the rain angle.
- **Rain** wets surfaces (darker albedo, more specular, puddles in craters) and swaps dust trails for mud spray. **Snow** leaves tracks and footprints.
- **Fog** layers in valleys at dawn.
- **Night:**
  - Dark, but readable with UI aids.
  - Illumination rounds float under parachutes for 30–60 s, cast moving shadows and drop sparks.
  - Tracers are brighter. Fires light their surroundings.
  - NVG and thermal views where era-legal: thermal shows hot engines, fresh exhaust and recent firing positions, and cools wrecks over time.
- **Foliage:** grass bends around vehicles and stays flattened along tracks. Trees sway in the wind and fall when hit.
- **Water** gets ripples from impacts, splash columns and wakes.
- **Time of day** changes the sky, sun angle, color grading per era (WW2 warmer and desaturated, Cold War cooler, modern neutral with more contrast) and shadow length.

**Checks:** a weather and time sheet: the same scene in clear, overcast, rain, fog, snow and night (with illumination) at 3 times of day. A test that the wind direction matches the smoke drift vector.

### D9. Readability, UI feedback and camera

- **Unit icons and labels** use NATO-style symbols (APP-6 shapes for Cold War and modern; period-styled for WW2), scale with zoom, and fade when units are visible up close.
- **Status pips:** suppression, morale, ammo, fuel and veterancy.
- **Orders** show as path lines with waypoints, plus attack and support markers that show the incoming time of flight.
- **Threat display:** AA threat rings on hover, line-of-sight preview on alt-hover, cover indicators (D3) while placing infantry.
- **Feedback:** every order gets a confirmation (a voice callout from the right unit type, a short UI flash). Every kill, loss and support arrival generates a log entry and a map ping.
- **Camera:** smooth RTS camera with edge-scroll, middle-drag orbit, height presets (60, 120, 200, 300 m) and a cinematic follow on any unit. **Take Control** transitions with a 0.6–1.2 s eased camera move, never a cut, except on ejection.
- **Accessibility** per UX-02.

**Checks:** UI sheets at 4 zoom levels, plus G13 replay assertions that every order type produces a confirmation event.

### D10. Polish checklist (runs before every phase gate)

`docs/polish-checklist.md` lists every item above as a yes/no line with the sheet or test that proves it. A phase report includes the checklist with links. An unchecked item that belongs to the phase blocks the gate.

## W. Other production workflows

Each workflow below is a project skill in `.claude/skills/` with its own checklist and verifier.

**W1. New unit (`new-unit`).**

1. Gather real-world data with sources (armor, weapons, mobility, optics, year in service) and write it to `data/units/<id>.json`. G10 must pass, including the era and year legality check.
2. Create the deck card: role icons, loadout variants, phase gate, cost. Use `scripts/balance/cost_model.py` to propose a starting cost from stats.
3. Write the model brief, then run the §M pipeline.
4. Write the audio set: engine loop, weapon fire per weapon, movement, destroyed, and voice callouts by speaker set.
5. Write a scenario test proving the unit's role works. An AT unit must kill its target class at its rated range in at least 60% of 50 seeds.
6. Run a soak slice with the unit added and check that no balance gate regresses.

**W2. Map (`new-map`).**

1. Take a public DEM (USGS 3DEP, Copernicus GLO-30) for a real location, crop it to 8×8 km and record the license.
2. Generate the road, rail and river network from OpenStreetMap data (ODbL attribution in credits), then simplify it.
3. Place towns from the building-kit generator, with density matching the region's era.
4. Generate a contact sheet: a top-down map render, the 4 RTS heights, deployment zones, and lines of sight from each objective.
5. Run AI-vs-AI soak on the map. No side's win rate may exceed 60% from spawn position alone across mirrored decks.

**W3. Dialogue and voice (`dialogue-batch`).**

1. Keep `dialogue/lines.csv` as the matrix: id, speaker, era, faction, trigger, priority, text, variants and audio tags.
2. **Casting:** design or pick voices with the ElevenLabs voice tools, one per recurring speaker (XO, the 8 commanders, pilots, unit callout sets per nation), and save the voice IDs in `audio/voices.json`.
3. Generate in batches of at most 50 lines with Text to Dialogue (use the current recommended model; check the docs). Cache every result with its prompt hash in `audio/manifest.json` and never regenerate unchanged lines.
4. Run the G9 loudness and silence checks, then a transcription round trip (ElevenLabs transcription). The transcript must match the script text at ≥ 95% word accuracy, or the line is regenerated (at most 2 retries).
5. Make an audition sheet: an HTML page with players for 20 random new lines per batch. Listen-check them by transcribing and checking duration and emotion tags, and include it in the phase report so the user can spot-check.

**W4. Sound effects and music.** Generate SFX with variants through ElevenLabs, supplemented by CC0 libraries (Freesound CC0, the Sonniss GDC bundles). Every file is loudness-normalized and logged. Music has three stems per era theme (calm, tension, combat), cross-faded by the music director system from battle intensity.

**W5. VFX (`vfx-pass`).**

- Build a VFX library: muzzle flash per caliber class, tracers, impacts per surface, explosions (small, medium, large, structure), smoke columns, fires, dust, napalm, illumination flares, nuke stages.
- Each effect gets a contact sheet: a 9-frame strip at fixed intervals on dark and light backgrounds, plus its GPU cost from a benchmark.
- G6 enforces a budget: the full benchmark barrage must stay within the frame target on the Omarchy machine.

**W6. Balance (`balance-pass`).**

1. Run soak (§G5) after every content wave.
2. `scripts/balance/report.py` produces an HTML page covering win rates per faction and deck archetype, cost-efficiency per card (kills value ÷ cost), unused cards, and outliers above 2σ.
3. Adjust costs before stats. Never tune a card from fewer than 200 matches of data.

**W7. Phase package (`phase-gate`).** Covered in §I.

## 12. Audio and voice (ElevenLabs plus free libraries)

Sound design detail (layers, distance bands, propagation, mix, ambience, vehicles, production and checks) is in §D7. This section covers sources and budgets.

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
| ERA-02 | Version 1.0 content: at least 60 units per faction (stretch 100), every one with an approved model (§M), data, icons, audio set and a deck card. | [G7] [G10] [G9] |
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


**Mechanics from §C (MEC)**
| ID | Requirement | Verifier |
|---|---|---|
| MEC-01 | Three battle phases with per-card phase gates and four deck income profiles. | [G4] [G10] |
| MEC-02 | Dynamic frontline from unit influence. Recon and AT teams don't move it. Encircled units can't shed suppression or resupply. | [G4] `front_*` scenarios |
| MEC-03 | Support Points earned by combat and by support actions (resupply, repair, medevac, capture, spotting), spent on off-map support with charges and recharge timers. | [G4] |
| MEC-04 | Directional cover tiers, retreat order, and veterancy that survives retreat. Wrecks and craters become cover. | [G4] |
| MEC-05 | Battle-planner orders (front, offensive arrow, spearhead, garrison) with a preparation bonus that decays under micromanagement. | [G4] |
| MEC-06 | Field Requests: pick 1 of 3 task forces at each phase change (campaign default, skirmish toggle). | [G4] [G13] |
| MEC-07 | Multi-phase campaign operations with persistent wrecks, craters and fortifications, advancing time of day, and an event deck. | [G4] |
| MEC-08 | No direct-control stat buff: AI-pilot and scripted-player hit rates on identical attack profiles are within ±10%. | [G4] |
| MEC-09 | No single AA card destroys more than 1.5× its cost in aircraft on average; a single-class deck wins ≤ 55% against combined arms. | [G5] |
| MEC-10 | Unspent bank stays under 30% of phase income (median, soak). | [G5] |
| MEC-11 | Every card shows role icons, an effective-range bar and a "use it for" line. | [G13] |
| MEC-12 | Campaign outcomes change the next map state; two outcomes give two different states. | [G4] |
| MEC-13 | GS AI reinforces an attacked province within 2 turns in ≥ 80% of cases when reserves exist. | [G5] |

**Art pipeline (ART)**
| ID | Requirement | Verifier |
|---|---|---|
| ART-01 | Every shipped asset has a spec, reference pack, ortho masks, all stage contact sheets and an approved `review.md` from a fresh-context reviewer. | [G7] |
| ART-02 | Every vehicle and aircraft passes the blind recognition test. | [G7] |
| ART-03 | `board.html` and batch review pages are generated and current. | [G7] |

**Details and references (DET, REF)**
| ID | Requirement | Verifier |
|---|---|---|
| REF-01 | Every asset, effect, animation set and sound has a reference pack meeting the §R3 checklist before production. No look-only material appears in shippable paths. | [G14] |
| DET-01 | Explosions follow the §D1 layer anatomy, with surface, type and scale variants driven by `data/vfx/yields.json`. | [G8] frame strips, [G4] timing log |
| DET-02 | Destruction per §D2: material-based building chunks, interiors, dust plumes, fire spread, glass, breaches, toppling trees, bridge spans, persistent wrecks and craters. | [G4] [G8] |
| DET-03 | Infantry per §D3: formations with era spacing, movement modes, bounding overwatch, contact reactions, cover slots (≥ 85% in cover or prone within 3 s under fire), casualties and drags, door and ramp mounting. | [G5] [G8] [G13] |
| DET-04 | Crowd quality: foot drift ≤ 2 cm per frame during contact, foot IK, aim layering, no wall clipping, capped turn rates, and neighbor idle phase correlation < 0.6. | [G8] metrics |
| DET-05 | Vehicle feel per §D4: suspension, track scroll within 3% of ground speed, real traverse rates, recoil and muzzle dust, exhaust, dust trails by surface and weather, visible damage, and cause-based destruction outcomes. | [G4] [G8] |
| DET-06 | Aircraft feel per §D5: control surfaces driven by inputs, vapor and contrails, countermeasures, correct pylon release, damage trails, ejection, crash sites, rotor wash. | [G4] [G8] |
| DET-07 | The §D6 animation clip list is complete for each era rig. Root motion is within 5% of sim speed, retarget bone stretch is ≤ 2%, and VAT crowd LODs exist. | [G8] |
| DET-08 | Sound per §D7: layered design entries, near, mid and far bands, distance delay within 1 frame at 500 m, occlusion, reverb zones, era radio chains, ambience beds, ≥ 4 variants for frequent sounds, and seamless loops. | [G9] |
| DET-09 | Environment per §D8: one wind value drives smoke, foliage, flags and rain; wet surfaces; snow tracks; illumination rounds; thermal and NVG; trampled grass; water impacts; era color grading. | [G8] |
| DET-10 | Readability per §D9: APP-6 style symbols, status pips, order lines with time of flight, threat rings, LOS preview, cover indicators, a confirmation for every order, and eased Take Control transitions. | [G8] [G13] |
| DET-11 | The §D10 polish checklist is complete, with links, for every item belonging to the phase. | [G15] |

## G. Verification suite (build it in Phase 0, grow it every phase)

One entry point: `scripts/verify.sh [--quick | --phase N | --full]`.

- It runs the gates below, writes a Markdown report to `build/reports/<UTC timestamp>-<mode>.md` with raw command outputs, and exits non-zero on any failure.
- `--quick` runs G1, G2, G3, G10 and G14 (target: under 5 minutes).
- `--phase N` runs every gate tagged for that phase.
- `--full` runs everything.

All thresholds live in `verify/thresholds.toml`. The report prints the SHA-256 of `verify/` and of the thresholds file, so any change to the verifiers is visible.

| Gate | What it checks | How |
|---|---|---|
| **G1 Build** | Sim builds with warnings as errors; Godot imports with zero errors; macOS, Windows and Linux exports succeed. | `dotnet build -warnaserror`; `godot --headless --import` (parse the log for `ERROR`); `--export-release` for all three presets |
| **G2 Unit tests** | All tests pass; sim line coverage ≥ 80%; float-in-state analyzer. | `dotnet test` with coverlet; the analyzer runs as a test |
| **G3 Determinism** | Identical state hash at ticks 100, 1,000 and 12,000 across 3 seeds × 2 separate processes × thread counts 1 and N; replay round trip; lockstep 8-client hash agreement. | `sim/Headless --determinism`; golden hashes stored in `verify/golden/` |
| **G4 Scenarios** | Every requirement with sim behavior has scripted scenarios: map, units, orders, seed, duration, and assertions on outcomes, ranges or statistics. | `verify/scenarios/*.json`, run by `sim/Headless --scenario`. Statistical scenarios run ≥ 50 seeds with confidence intervals. |
| **G5 AI soak and balance** | No crashes; no unit stuck over 120 s (≥ 99.5% unstuck); matches end before the time cap. Mirror-match win rate per faction 40–60%, cross-faction 35–65%. Commander behavior metrics: share of assaults preceded by fire support ≥ 70%; average force committed per attack ≥ 60% of the available assigned force; damaged-unit withdrawal rate ≥ 60%. | `sim/Headless --soak N` emitting CSV, then `scripts/analyze_soak.py` |
| **G6 Performance** | Headless tick p95; rendered frame time p95, p99 and max in the benchmark scene; memory growth. | Godot benchmark scene logs frame times to CSV, then `scripts/analyze_perf.py` |
| **G7 Assets** | Every model passes the validator in §M6 and has an approved review (§M5) for its current stage. | `tools/blender/blender --background --python scripts/validate_assets.py -- assets_src/` |
| **G8 Visual** | Movie-maker renders of every `verify/visual/*.json` camera path. Fails on: black or white frames; luminance outside thresholds; low image entropy (blank screens); any expected entity covering fewer than the minimum pixels in the debug **ID-mask render mode** (each entity drawn in a unique flat color); HUD elements missing from expected regions. Also produces contact sheets. | `godot --write-movie`, then `scripts/check_frames.py`. Then **you must open the contact sheets** and write a short visual review in `PROGRESS.md`. |
| **G9 Audio** | Every trigger and variant has a file; loudness and peak targets; duration bounds; no silence over 300 ms at the start or end; plus the §D7 checks (loop seams, distance-band spectra, low-end presence, variant similarity, distance delay) and spectrogram sheets. | `ffmpeg -af ebur128` and `loudnorm` analysis via `scripts/check_audio.py` against `audio/manifest.json` |
| **G10 Data** | JSON Schema validation for every data file; referential integrity; era and year legality; stat sanity ranges (speeds, armor, ranges, rates of fire within real-world-plausible bounds per class); no duplicate IDs. | `uv run check-jsonschema`, plus `scripts/validate_data.py` |
| **G11 Licensing** | Every file in `game/assets/` and `audio/` traces to an `ASSETS.md` entry with an allow-listed license (CC0, CC-BY 4.0, MIT, own work, or generated with a recorded tool and plan). Credits screen regenerated from it. | `scripts/check_licenses.py` |
| **G12 Code quality** | Analyzers; formatting; banned patterns (`TODO`, `NotImplementedException`, `Debug.Assert(false)`) in verified code; no test skips. | `dotnet format --verify-no-changes`; `scripts/check_code.py` |
| **G14 Reference coverage** | Every item in production has a complete §R3 checklist, sourced spec numbers and license entries; no look-only paths in shippable or report locations. | `scripts/refs/check_coverage.py` |
| **G15 Detail sheets and polish** | Every §D sheet for items in the phase exists, is newer than the content it shows, and has a reviewer score ≥ 4.0 (no criterion < 3); the polish checklist is complete. | `scripts/sheets/check_detail.py` |
| **G13 UX smoke** | The exported build runs recorded input scripts end to end: menu → deck → deployment → battle → Take Control → return → victory or defeat → debrief. Also campaign start, lobby flow and settings persistence. | Godot input replay harness with `--scenario` args, asserting on game-state events written to a log |

**Verifier integrity rules.**

- `verify/` changes go in their own commits, prefixed `VERIFY:`, each with a `DECISIONS.md` entry.
- Thresholds may only be tightened freely. Loosening one needs evidence and the user's approval.
- Golden hashes change only when sim rules intentionally change, and the commit names the requirement.

## I. Phase gates (a phase is complete only when its gate passes and is recorded)

Run `scripts/verify.sh --phase N` and attach the report to the phase entry in `PROGRESS.md`. Then produce:

- A tagged commit.
- Exported macOS, Windows and Linux builds.
- A 2–4 minute capture video of the phase's headline features, recorded with Godot's movie maker.
- A short HTML summary page for the user, `build/reports/phase-N.html`, with screenshots, the verify summary, open issues and the next phase plan.

| Phase | Required requirement IDs at `verified` | Extra exit criteria |
|---|---|---|
| **0 Foundations** | SIM-01, SIM-02, SIM-03, QA-01, QA-02 (on existing code). G1, G2, G3, G10, G11 and G12 operational. | `scripts/bootstrap.sh` reproducible on a fresh Omarchy user account. `.claude/` agents, hooks and skills from §A5 in place and tested. API docs dumped. All tool versions pinned. |
| **1 Vertical slice (Cold War)** | SIM-04 to SIM-10 and SIM-12; MEC-01 to MEC-05, MEC-08; ART-01 to ART-03 (slice assets); REF-01, DET-01 to DET-05, DET-07, DET-08, DET-10 and DET-11 (Cold War slice scope); CMD-01 to CMD-06; FLY-01 to FLY-03; SUP-01, SUP-02 and SUP-05 (Cold War subset); AUD-01 and AUD-02 (slice lines); UX-01 (subset); PERF-01 on the slice. | 25 units per side with validated models. One 8×8 km map. **Art bible and style board approved by the user.** A human-playable 20-minute skirmish. |
| **2 Three eras** | DET-06, DET-09, ERA-01, ERA-04, SIM-11, SUP-03, SUP-04, FLY-04 to FLY-06, REN-01 to REN-03, AUD-03. | 30 units per faction validated. 2 maps per era. |
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
