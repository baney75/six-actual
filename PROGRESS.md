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

Performance targets in the brief are for an RTX 3060-class GPU. This machine is faster. Numbers still have to be measured here, and the 3060 target remains the bar.
