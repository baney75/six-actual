---
name: new-unit
description: Add a unit end to end — sourced data, deck card, model brief, audio set, role scenario, soak slice (contract W1). Use whenever a unit is added to any era roster.
---
# New unit

1. **Data.** Add the unit to `scripts/generate_units.py` (never hand-edit `data/units.json`) with sourced real-world values: armor, weapons, mobility, optics, year in service. Put the sources in the generator's source table. Regenerate, then run `scripts/verify.sh --quick`. G10 must pass, including the era and year legality check.
2. **Card.** Add role icons, 1–4 loadout variants with cost deltas, a phase gate (A/B/C), and a "use it for" line. Propose a starting cost with `scripts/balance/cost_model.py`. *Not built yet; build it under MEC-01 or MEC-11 before the first card.*
3. **Model brief.** Write `data/assets/<id>.json` with sourced dimensions, class, budgets, parts, camo set, damage states, animations and five signature features. Run `python3 scripts/assets/seed_registry.py`. Then run the `ref-pack` skill, then `asset-pipeline`.
4. **Audio set.** Engine loop, fire sound for each weapon, movement, destroyed, and voice callouts per speaker set. Use `sound-design` (synthesis plus CC0) and `dialogue-batch` (local Qwen3-TTS). Never ElevenLabs.
5. **Role scenario.** Use the `scenario-test` skill. An AT unit must kill its target class at rated range in at least 60% of 50 seeds. Other roles get an equivalent statistical bar written into the scenario.
6. **Soak slice.** Use the `balance-pass` skill. No balance gate may regress.

Done means steps 1 and 5 pass in verify, and the asset and audio have their own review state recorded. Report each step as prepared, verified, built or delivered.
