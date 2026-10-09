---
name: ref-pack
description: Build a section R reference pack and board for any asset, effect, animation set or sound, meeting the verify/ref_coverage.toml checklist with every license logged. Required before production on any item.
---
# Reference pack (§R)

1. Look up the item's class in `assets_src/registry.json` and its slots in `verify/ref_coverage.toml`.
2. Search sources in this order:
   - US government: DVIDS, TM/FM manuals, NARA, LoC, NASA and USAF archives (check each rights field)
   - Wikimedia Commons through the API, filtered by `LicenseShortName`
   - museum CC0 collections
   - then look-only sources
3. Distributable files go to `assets_src/refs/<id>/`, with an `ASSETS.md` line: `assets_src/refs/<id>/<file> | URL | author | license | retrieved YYYY-MM-DD`.
4. Look-only files go to `refs_private/<id>/` (gitignored), with a row in `refs_private/log.csv` (url, date, slot). Never copy, trace, sample or embed them anywhere else.
5. Write `assets_src/refs/<id>/refs.json` with entries `{file, slots[], kind: "distributable"|"look-only", license, url, author, retrieved}`.
6. Put sourced numbers in the spec `data/assets/<id>.json` under `numbers: [{name, value, unit, source}]`. The source is a manual and page, a fact sheet, or a cited reference.
7. Build the board with `scripts/refs/board.py <id>`, which writes `build/refs/<id>.html`: a thumbnail grid with angle tags and license badges, look-only images as local links only, the dimension table, and checklist ticks. *board.py is not built yet. Build it with the first Phase 1 pack.*
8. Run `python3 scripts/refs/check_coverage.py` (G14). It must pass before the item's state moves past `refs`.
