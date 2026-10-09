---
name: anim-clip
description: Source, author or retarget one animation clip for an era rig per section D6, check root motion, foot slide and bone stretch, bake to VAT, and render pose sheets. Use for any infantry or crew animation.
---
# Animation clip (§D6)

1. **Ref pack** (`ref-pack`, class `animation_set`): field manual pages for the drill plus 2 or more footage refs.
2. Author the clip yourself (D021) in Blender scripts on the era base rig: procedural gait cycles from stride and cadence tables, keyed poses for drills taken from the field manual references, and aim offsets as additive poses. No CC0 libraries, no Mixamo.
3. When one clip is shared across era rigs, retarget it. Bone length change must stay at or below 2%.
4. Layer setup: lower-body locomotion, upper-body aim and fire, additive hit reactions, foot IK.
5. Checks:
   - root motion speed against sim speed for the movement mode (within 5%)
   - foot drift during contact ≤ 2 cm per frame
   - turn rate cap
   - clip coverage against the §D6 list
6. Bake to VAT at 30 fps, with LOD clips (fewer bones and frames).
7. Write a pose sheet (8 frames in a grid) to `build/detail_review/anim/<rig>/<clip>.png`, then send it to art-reviewer.

*The rig, VAT baker and metric scripts are not built yet (DET-04, DET-07).*
