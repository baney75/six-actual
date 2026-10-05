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

## D010 — Roster stats and meshes are game-scale, not a historical table

`data/units.json` holds 103 units named in the brief. Lengths are public-approximate meters. Armor, penetration, and speed are game-scale integers so the sim can stay fixed-point. They are not a claimed reference table. `scripts/build_models.py` builds a Blender mesh per unit from those dimensions: hull, turret, gun, and wheels, or a fuselage and wings, or a person. Silhouette checks against photos are not done. A destroyed GLB is written for vehicles and aircraft.

## D008 — Infantry counting is undecided

SIM-13 allows squad members or squads as entities. No choice yet. The performance target is not in Phase 0.
