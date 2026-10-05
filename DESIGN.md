# Six Actual

Working title. Six is the commander's callsign. Actual means the commander is on the net in person. The player is that commander, and can leave the table to fly an aircraft while the XO runs the battle.

The full contract is `docs/BRIEF.md`. This file is the design that code should follow. Where they disagree, the brief's stricter rule wins until a `DECISIONS.md` entry says otherwise.

## Pillars

1. The player sets intent and delegates. Direct control is always available and never required.
2. Artillery, air, and logistics decide battles.
3. Flying an aircraft is a real mode, and the battle keeps running under the XO.
4. WW2 (1944–45), Cold War (1989), and the modern day share one ruleset. Eras differ by units, doctrine, HUD, audio, and look.
5. The battlefield has to read as a war zone at RTS camera height.

## Battle loop

Build a battlegroup, deploy it, fight a real-time battle with income and a front line, then debrief. Eight sub-commanders plus the XO take high-level orders. The player can grab any unit, including any friendly aircraft.

## Simulation

The match lives in `sim/`, a C# library with no Godot types. Godot (`game/`) is view, input, and audio. State uses integers and `Fp` (Q48.16). The only clock is a 20 Hz tick (`Simulation.TickHz`). One second is 20 ticks. Phase 0 steps positions from orders. Combat, sight, and supply are not in the tick yet.

Commands are data. Replays store them and reproduce the state hash. Clients that receive the same commands must hash the same.

## Content

Units, weapons, support, factions, eras, traits, and research live in versioned JSON under `data/`. No balance number lives in code. The seed catalog has the three eras and no units.

## What is explicitly later

Line of sight, armor, morale, supply, destruction, commanders, flight, support, campaigns, networking, and grand strategy are specified in `REQUIREMENTS.md` and are not in this tree yet. Nuclear weapons exist only in grand strategy, and the tactical-nuke battle card is off unless a match turns it on.
