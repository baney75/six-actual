# Legal

Working title: Six Actual. A store, trademark, and domain collision check is due before Phase 1 ends. Not run yet.

Unit names in the brief use real military designations. Do not add manufacturer logos or trademarked branding. Anything that needs a license review before a commercial release gets a line here.

No third-party game code is imported. The Wreckage and Warhammer browser prototypes, if supplied later, are feel references only.

## Local generation models (D020)

- Qwen3-TTS 12Hz 1.7B CustomVoice and VoiceDesign: Apache-2.0 (model card `license:` field, read 2026-10-08).
- ACE-Step 1.5: MIT.
- FLUX.2 klein 4B FP8: Apache-2.0 (`flux-manifest.json`). 2D only.
- SDXL Base and Refiner: CreativeML OpenRAIL++-M. Commercial use is allowed under its use restrictions. Prefer FLUX for anything shipped.
- faster-whisper 1.2.1 and the Whisper `small.en` weights: MIT. Used for checks only; nothing from it ships.

Re-read each license before 1.0. Voices designed from text descriptions do not imitate a real person. Do not prompt for a named person's voice.

## OpenStreetMap (ODbL)

Building footprint statistics for the West German town block (`data/assets/wgerman_town_block_1989.json`) come from OpenStreetMap data. Credit: "© OpenStreetMap contributors, ODbL". This line must appear on the credits screen, and the maps under W2 will add more OSM-derived data.

## Research data

- `assets_src/research/anthro/ansurMen.csv`: ANSUR I (1988) US Army anthropometric survey, male raw data (n = 1774), from https://tools.openlab.psu.edu/publicData/ansurMen.csv (Penn State OpenLab mirror of Gordon et al. 1989, NATICK/TR-89/044). It is a US Government work, public domain. It is used only to derive body proportions (`ansur1988_men_means.json`) and is not shipped.
