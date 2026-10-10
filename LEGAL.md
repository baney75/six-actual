# Legal

## Project license

Six Actual's code and Claude-authored content (scripts, sim, game, scripted meshes, generated audio and data) are released under the GNU General Public License v3.0 (`LICENSE`), chosen by Donovan on 2026-10-10. Third-party material keeps its own terms, listed below: the Copernicus DEM-derived map heightfields (attribution and no-liability notice required), OpenStreetMap-derived statistics (ODbL), and the public-domain or CC references in `assets_src/refs/`. The local generation models' licences apply to the models, not to this repo.

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

## Copernicus DEM GLO-30 (D075)

The ERA-03 battle-map heightfields (`data/maps.json`, `scripts/generate_maps.py`) are resampled and smoothed from the Copernicus DEM GLO-30 public tiles (`s3://copernicus-dem-30m`, licence: https://documentation.dataspace.copernicus.eu/APIs/SentinelHub/Data/DEM/resources/license/License-COPDEM-30.pdf). The licence is free of charge and allows commercial use with attribution. Because the data is modified, the required notice is: "produced using Copernicus WorldDEM-30 © DLR e.V. 2010-2014 and © Airbus Defence and Space GmbH 2014-2018 provided under COPERNICUS by the European Union and ESA; all rights reserved". The licence also requires this sentence in any legal notice covering distribution: "The organisations in charge of the Copernicus programme by law or by delegation do not incur any liability for any use of the Copernicus WorldDEM-30". Both lines go on the credits screen. Source tiles stay in the gitignored `tools/cache/dem/` and do not ship.

## Research data

- `assets_src/research/anthro/ansurMen.csv`: ANSUR I (1988) US Army anthropometric survey, male raw data (n = 1774), from https://tools.openlab.psu.edu/publicData/ansurMen.csv (Penn State OpenLab mirror of Gordon et al. 1989, NATICK/TR-89/044). It is a US Government work, public domain. It is used only to derive body proportions (`ansur1988_men_means.json`) and is not shipped.
