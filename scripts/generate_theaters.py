#!/usr/bin/env python3
"""Generate data/theaters.json: one grand-strategy theater map per era (GS-01).

    python3 scripts/generate_theaters.py [out.json]

Provinces are real-world-inspired regions on a schematic 0-1000 grid (x east, y north): terrain (open, forest, hills,
urban) drives battle maps (GS-02) and combat modifiers; industry is production points per turn; manpower is the
recruit pool per turn. Owners follow the historical front line at the start date. Factions belong to sides; some
start outside the war (neutral) and can be drawn in by alliances (GS-03). Values are designed (relative economic
weight of the region, not statistics); each province records its reasoning.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

# era: (start, factions {id: (side, capital province, at war at start)}, provinces
#        [(id, name, x, y, terrain, owner faction, industry, manpower, neighbours, why)])
THEATERS = {
    "coldwar": ("1989-08", {
        "usa": ("NATO", "frankfurt", True), "frg": ("NATO", "bonn", True), "uk": ("NATO", "hannover", True),
        "fra": ("NATO", "strasbourg", False),
        "ussr": ("Warsaw Pact", "berlin_east", True), "gdr": ("Warsaw Pact", "leipzig", True), "pol": ("Warsaw Pact", "szczecin", False),
    }, [
        ("hamburg", "Hamburg", 420, 900, "urban", "frg", 4, 3, ["hannover", "rostock", "bremen"], "port and industry; BAOR/FRG northern flank"),
        ("bremen", "Bremen-Oldenburg", 300, 860, "open", "frg", 2, 2, ["hamburg", "hannover", "ruhr"], "North German Plain"),
        ("hannover", "Hannover", 430, 760, "open", "uk", 3, 2, ["hamburg", "bremen", "ruhr", "kassel", "magdeburg"], "I (BR) Corps sector"),
        ("ruhr", "Ruhr", 200, 680, "urban", "frg", 5, 4, ["bremen", "hannover", "bonn", "kassel"], "industrial heartland"),
        ("bonn", "Bonn-Cologne", 180, 560, "urban", "frg", 3, 3, ["ruhr", "frankfurt", "strasbourg"], "federal capital"),
        ("kassel", "Kassel", 400, 620, "hills", "frg", 2, 2, ["hannover", "ruhr", "frankfurt", "fulda", "erfurt"], "Weser uplands"),
        ("fulda", "Fulda Gap", 450, 520, "hills", "usa", 1, 1, ["kassel", "frankfurt", "nuremberg", "erfurt"], "the Fulda Gap, V Corps"),
        ("frankfurt", "Frankfurt", 330, 470, "urban", "usa", 4, 3, ["bonn", "kassel", "fulda", "nuremberg", "strasbourg"], "Rhein-Main air base and finance"),
        ("nuremberg", "Nuremberg", 520, 380, "forest", "usa", 3, 2, ["frankfurt", "fulda", "munich", "pilsen", "dresden"], "VII Corps, Franconian forest"),
        ("munich", "Munich", 560, 220, "hills", "frg", 4, 3, ["nuremberg", "strasbourg", "pilsen"], "Bavaria"),
        ("strasbourg", "Alsace", 200, 330, "forest", "fra", 3, 3, ["bonn", "frankfurt", "munich"], "French II Corps, Rhine crossing"),
        ("rostock", "Rostock", 560, 930, "open", "gdr", 2, 2, ["hamburg", "berlin_east", "szczecin"], "Baltic coast"),
        ("magdeburg", "Magdeburg", 560, 760, "open", "ussr", 2, 2, ["hannover", "berlin_east", "leipzig"], "3rd Shock Army"),
        ("berlin_east", "Berlin (East)", 680, 800, "urban", "ussr", 4, 3, ["rostock", "magdeburg", "leipzig", "szczecin", "dresden"], "GSFG headquarters, Wünsdorf"),
        ("erfurt", "Erfurt-Thuringia", 520, 600, "forest", "ussr", 2, 2, ["kassel", "fulda", "leipzig"], "8th Guards Army"),
        ("leipzig", "Leipzig-Halle", 620, 650, "urban", "gdr", 4, 3, ["magdeburg", "berlin_east", "erfurt", "dresden"], "chemical industry"),
        ("dresden", "Dresden", 720, 560, "hills", "gdr", 3, 2, ["leipzig", "berlin_east", "nuremberg", "pilsen"], "1st Guards Tank Army"),
        ("pilsen", "Pilsen-Bohemia", 680, 380, "forest", "ussr", 3, 2, ["dresden", "nuremberg", "munich"], "Central Group of Forces"),
        ("szczecin", "Szczecin", 800, 900, "open", "pol", 2, 3, ["rostock", "berlin_east"], "Polish Front"),
    ]),
    "ww2": ("1944-09", {
        "usa": ("Allies", "paris", True), "uk": ("Allies", "brussels", True), "fra": ("Allies", "paris", True),
        "ger": ("Axis", "berlin", True),
    }, [
        ("antwerp", "Antwerp", 250, 820, "urban", "uk", 3, 2, ["brussels", "arnhem"], "the port the Allies needed"),
        ("brussels", "Brussels", 230, 720, "urban", "uk", 3, 2, ["antwerp", "liege", "paris"], "liberated September 1944"),
        ("paris", "Paris", 100, 450, "urban", "fra", 4, 3, ["brussels", "metz", "liege"], "liberated August 1944"),
        ("liege", "Liège-Ardennes", 330, 640, "forest", "usa", 2, 2, ["brussels", "paris", "aachen", "metz"], "Ardennes, First Army"),
        ("metz", "Metz-Lorraine", 300, 420, "hills", "usa", 2, 2, ["paris", "liege", "saar"], "Third Army, Moselle forts"),
        ("arnhem", "Arnhem", 380, 860, "open", "ger", 2, 2, ["antwerp", "ruhr"], "the bridge too far"),
        ("aachen", "Aachen-Hürtgen", 420, 650, "forest", "ger", 2, 2, ["liege", "ruhr", "cologne"], "Westwall, Hürtgen forest"),
        ("saar", "Saar", 420, 420, "hills", "ger", 3, 2, ["metz", "cologne", "frankfurt"], "Westwall industry"),
        ("ruhr", "Ruhr", 520, 780, "urban", "ger", 5, 3, ["arnhem", "aachen", "cologne", "berlin"], "Germany's arsenal"),
        ("cologne", "Cologne", 500, 640, "urban", "ger", 3, 3, ["aachen", "ruhr", "saar", "frankfurt"], "the Rhine"),
        ("frankfurt", "Frankfurt", 560, 480, "urban", "ger", 3, 2, ["saar", "cologne", "berlin"], "Rhine-Main"),
        ("berlin", "Berlin", 900, 700, "urban", "ger", 5, 4, ["ruhr", "frankfurt"], "the Reich capital"),
    ]),
    "modern": ("2026-05", {
        "usa": ("NATO", "warsaw", True), "pol": ("NATO", "warsaw", True), "ltu": ("NATO", "vilnius", True),
        "deu": ("NATO", "berlin", False),
        "rus": ("Russia", "pskov", True), "blr": ("Russia", "minsk", True),
    }, [
        ("berlin", "Berlin", 80, 600, "urban", "deu", 5, 3, ["warsaw", "gdansk"], "German rear area"),
        ("gdansk", "Gdańsk", 300, 820, "urban", "pol", 3, 2, ["berlin", "warsaw", "kaliningrad", "suwalki"], "Baltic port"),
        ("warsaw", "Warsaw", 360, 560, "urban", "pol", 5, 4, ["berlin", "gdansk", "suwalki", "bialystok", "brest"], "Polish capital"),
        ("suwalki", "Suwałki Gap", 560, 760, "forest", "pol", 1, 1, ["gdansk", "warsaw", "bialystok", "kaliningrad", "vilnius", "grodno"], "the gap between Kaliningrad and Belarus"),
        ("bialystok", "Białystok", 520, 600, "forest", "pol", 2, 2, ["warsaw", "suwalki", "grodno", "brest"], "eastern Poland"),
        ("vilnius", "Vilnius", 760, 760, "urban", "ltu", 2, 2, ["suwalki", "kaunas", "grodno", "minsk"], "Lithuanian capital"),
        ("kaunas", "Kaunas", 680, 860, "open", "ltu", 2, 2, ["vilnius", "kaliningrad", "pskov"], "NATO eFP brigade"),
        ("kaliningrad", "Kaliningrad", 470, 880, "urban", "rus", 3, 2, ["gdansk", "suwalki", "kaunas"], "Russian exclave, 11th Corps"),
        ("grodno", "Grodno", 680, 640, "forest", "blr", 2, 2, ["suwalki", "bialystok", "vilnius", "minsk", "brest"], "Belarusian border"),
        ("brest", "Brest", 600, 460, "open", "blr", 2, 2, ["warsaw", "bialystok", "grodno", "minsk"], "Brest gate"),
        ("minsk", "Minsk", 900, 600, "urban", "blr", 4, 3, ["vilnius", "grodno", "brest", "pskov"], "Belarusian capital"),
        ("pskov", "Pskov", 950, 900, "open", "rus", 4, 4, ["kaunas", "minsk"], "76th Guards Air Assault"),
    ]),
}


def build() -> dict:
    out = {"version": 1, "theaters": []}
    for era, (start, factions, provinces) in THEATERS.items():
        ids = {p[0] for p in provinces}
        for p in provinces:
            for n in p[8]:
                if n not in ids:
                    raise SystemExit(f"{era}/{p[0]}: unknown neighbour {n}")
                other = next(q for q in provinces if q[0] == n)
                if p[0] not in other[8]:
                    raise SystemExit(f"{era}: {p[0]} -> {n} is not symmetric")
            if p[5] not in factions:
                raise SystemExit(f"{era}/{p[0]}: unknown owner {p[5]}")
        for f, (side, cap, _) in factions.items():
            if cap not in ids:
                raise SystemExit(f"{era}/{f}: unknown capital {cap}")
        out["theaters"].append({
            "era": era, "start": start,
            "factions": [{"id": f, "side": s, "capital": c, "at_war": w} for f, (s, c, w) in factions.items()],
            "provinces": [{"id": p[0], "name": p[1], "x": p[2], "y": p[3], "terrain": p[4], "owner": p[5], "industry": p[6],
                           "manpower": p[7], "neighbours": p[8], "why": p[9]} for p in provinces],
        })
    return out


def main() -> int:
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "data" / "theaters.json"
    data = build()
    out.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"wrote {out} ({sum(len(t['provinces']) for t in data['theaters'])} provinces in {len(data['theaters'])} theaters)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
