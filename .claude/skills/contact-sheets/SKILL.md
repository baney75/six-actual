---
name: contact-sheets
description: Render any CS-1..CS-12 contact sheet for an asset at a given stage into build/assets_review/<id>/<stage>/ with sheet_meta.json. Use after every modeling stage and before every review.
---
# Contact sheets (§M4)

Command: `scripts/sheets/render_sheets.py <id> --stage <stage> [--sheets CS-1,CS-2,...]`. *Not built yet. Build it under ART-01 with the first Phase 1 asset.*

Fixed for every sheet: lighting rigs, neutral gray background, the focal length, a 1 m grid, and `sheet_meta.json` (camera, rig, render settings, git hash).

| Sheet | Renderer | Content |
|---|---|---|
| CS-1 | Blender EEVEE | Side, top and front ortho. Mask in red, model in blue, IoU printed per view. |
| CS-2 | EEVEE | 8 angles at 45°, 3/4 elevation |
| CS-3 | EEVEE/Cycles | Each reference photo beside a render from the matched camera (`scripts/sheets/match_camera.py`). Distributable refs only in the saved sheet; look-only matches stay in `refs_private/`. |
| CS-4 | EEVEE | Wireframe over shaded, density heatmap, n-gon, non-manifold and flipped-normal highlights |
| CS-5 | EEVEE | LOD0–3 and impostor at switch distance, with triangle counts |
| CS-6 | EEVEE | UV layout, checker texel density, normal and AO bake previews |
| CS-7 | Cycles | Each faction camo and wear level, plus a material-ID view |
| CS-8 | Cycles | Pristine, damaged, destroyed, wreck as cover |
| CS-9 | EEVEE | Articulation limits |
| CS-10 | Godot `game/tools/asset_lineup.tscn` `--write-movie` | 60/120/200/300 m × summer, urban and snow × day, dusk and night (thermal and NVG where era-legal) |
| CS-11 | Godot | Lineup with 6 approved neighbors, an infantryman and a 10 m ruler |
| CS-12 | Godot | Take Control chase and cockpit frames (aircraft and flagships) |

After rendering, open every sheet with Read before you cite it. Godot sheets need the GPU: run `~/.local/bin/parish-media status` first.
