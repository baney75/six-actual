---
name: asset-pipeline
description: Take one Claude-authored 3D asset through the section M lifecycle (spec → refs → blockout → review → detail → LOD → UV/bake → materials → rig → full review → engine review → approved → variants). Use for every model.
---
# Asset pipeline (§M1–M9)

States live in `assets_src/registry.json`. A modeler subagent does the building. art-reviewer, in a fresh context, does every review.

| Step | Command or action | Exists? |
|---|---|---|
| Spec | `data/assets/<id>.json` with sourced numbers and five signature features | schema: build it with the first spec |
| Refs | `ref-pack` skill, then `python3 scripts/refs/check_coverage.py` (G14) | yes |
| Ortho masks | `scripts/refs/trace_ortho.py <id>`, scaled to spec dimensions | no |
| Blockout | `assets_src/procedural/ground_vehicle.py`, `aircraft.py`, `helicopter.py`, `infantry_kit.py`, `building_kit.py` or `props.py` | no (the legacy `scripts/build_models.py` makes placeholders only) |
| Validate | `tools/blender/blender --background --factory-startup --python scripts/validate_assets.py -- assets_src/` (G7, §M6) | no |
| Sheets | `contact-sheets` skill | no |
| Review | Spawn `art-reviewer` with only the spec, the board, the sheets and `docs/art-rubric.md` | rubric yes |

Rules:
- Advance a gate only when G7 passes and the review scores ≥ 4.0 with no criterion below 3.
- A rejection sends the asset back to the named stage with the fix list.
- After the 4th rejection at the same gate, stop and escalate to Donovan with the sheets and reviewer notes.
- Route per D021: everything is authored by Claude. Use P (parametric generator) or H (generator plus scripted detail parts from `assets_src/procedural/parts/`). Routes L (library) and G (AI-generated) are off. Materials and camo are procedural shaders, baked. Record the route in the registry.
- At full review, run the blind recognition test with a second fresh art-reviewer: CS-2 and CS-10 of this asset plus 3 of the same class, unnamed. A wrong identification is a forced rejection.
- Every 25 approvals, write `build/assets_review/batch-<n>.html` for Donovan.
- If a route's first-pass approval rate falls below 40% over 10 assets, stop and fix the generator.
- Variants (§M8) are modifier specs, not copies. They re-run the validator and sheets, plus a blockout-level review and the blind test against the base model.
- Run `~/.local/bin/parish-media status` before Cycles renders. Wrap overnight batches in `systemd-inhibit --what=idle:sleep --why="six-actual batch"`.
