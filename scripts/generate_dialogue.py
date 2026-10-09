#!/usr/bin/env python3
"""Generate dialogue/lines.csv, the radio dialogue matrix (BRIEF W3, AUD-01, CMD-07), and audio/voices.json (casting).

    python3 scripts/generate_dialogue.py           write both
    python3 scripts/generate_dialogue.py --check   G10/G9 drift check

Speakers are the XO and the 8 sub-commanders (CMD-01). Each speaker has the triggers its role produces in the sim
(assaults, withdrawals, approval requests, escalations, fire missions, CAS, resupply; the XO's Take Control handover and
phase changes). One row per speaker, trigger and era; the era changes the radio wording. Voices are designed locally
(Qwen3-TTS VoiceDesign, D020): a fixed description and seed per speaker, kept in audio/voices.json.
"""
from __future__ import annotations

import csv
import io
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

# speaker: (role title, voice description, seed)
VOICES = {
    "xo": ("Executive officer", "American male Army officer in his forties, calm and clipped, steady mid-low voice, speaking over a military radio net, confident, unhurried", 17),
    "army1": ("1st maneuver commander", "American male infantry officer in his thirties, brisk and energetic, mid voice, slightly breathless over a field radio", 21),
    "army2": ("2nd maneuver commander", "American female infantry officer in her thirties, firm and level, clear mid voice over a field radio", 22),
    "arty1": ("1st fires commander", "American male artillery officer in his fifties, gravelly low voice, methodical and precise over the radio", 23),
    "arty2": ("2nd fires commander", "American male artillery officer in his twenties, quick and exact, light tenor over the radio", 24),
    "air1": ("1st air commander", "American male pilot in his thirties, cool and laconic, relaxed drawl over aircraft radio", 25),
    "air2": ("2nd air commander", "American female pilot in her thirties, crisp and fast, bright clear voice over aircraft radio", 26),
    "armor": ("Armor commander", "American male tank officer in his forties, deep confident voice, loud over engine noise on the radio", 27),
    "logistics": ("Logistics commander", "American male supply officer in his fifties, patient and practical, warm mid voice over the radio", 28),
}

# Fictional commander names per era (CMD-07); no real person is depicted.
NAMES = {
    "xo": {"ww2": "Major Thomas Whitlock", "coldwar": "Major Daniel Hale", "modern": "Major Andrew Brennan"},
    "army1": {"ww2": "Captain Joseph Kowalski", "coldwar": "Captain Raymond Ortiz", "modern": "Captain Marcus Reed"},
    "army2": {"ww2": "Captain Henry Maddox", "coldwar": "Captain Ellen Shaw", "modern": "Captain Laura Kim"},
    "arty1": {"ww2": "Major Walter Briggs", "coldwar": "Major Frank Dawson", "modern": "Major Samuel Price"},
    "arty2": {"ww2": "Lieutenant Paul Hendricks", "coldwar": "Lieutenant Kevin Moore", "modern": "Lieutenant Jason Park"},
    "air1": {"ww2": "Captain Robert Lane", "coldwar": "Major Glen Carver", "modern": "Major Tyler Grant"},
    "air2": {"ww2": "Lieutenant Edward Hayes", "coldwar": "Captain Rachel Turner", "modern": "Captain Megan Ross"},
    "armor": {"ww2": "Captain William Archer", "coldwar": "Major Curtis Boyd", "modern": "Major Nathan Cole"},
    "logistics": {"ww2": "Captain George Fuller", "coldwar": "Major Harold Webb", "modern": "Major Steven Barnes"},
}

# trigger -> {era: text}; "all" applies to every era not listed.
LINES = {
    "xo": {
        "take_control": {"all": "I have the battle. Your aircraft.", "ww2": "I have the command, sir. The plane is yours."},
        "return_control": {"all": "You have the battle back. Handing over."},
        "phase_change": {"all": "New phase. Fresh reserves are available.", "ww2": "Reinforcements are coming up. New phase."},
        "objective_taken": {"all": "Objective secured.", "ww2": "We hold the objective."},
        "objective_lost": {"all": "We lost the objective. Retaking it.", "ww2": "They have taken the objective. We go back in."},
    },
    "army1": {
        "assault_started": {"all": "First maneuver, moving out. Assault under way.", "ww2": "First battalion advancing. Assault under way."},
        "withdraw": {"all": "First maneuver pulling damaged vehicles back to refit."},
        "request": {"all": "First maneuver requesting approval to attack."},
        "escalation": {"all": "First maneuver, the attack has stalled. I need help."},
        "contact": {"all": "Contact front. Engaging.", "ww2": "Enemy in front of us. Engaging."},
    },
    "army2": {
        "assault_started": {"all": "Second maneuver, assault is moving.", "ww2": "Second battalion going in now."},
        "withdraw": {"all": "Second maneuver, falling back to rearm."},
        "request": {"all": "Second maneuver requesting approval to push forward."},
        "escalation": {"all": "Second maneuver, we are pinned. Request support."},
        "contact": {"all": "Second maneuver, contact. Returning fire.", "ww2": "Second battalion under fire. Returning fire."},
    },
    "arty1": {
        "fire_mission": {"all": "Shot, over. Rounds on the way.", "ww2": "Guns firing. Rounds on the way."},
        "battery_located": {"all": "Counter-battery radar has their guns. Firing back.", "ww2": "Sound ranging has their battery. Firing back."},
        "request": {"all": "First fires requesting clearance for the fire mission."},
        "escalation": {"all": "First fires, we are taking counter-battery. Displacing."},
    },
    "arty2": {
        "fire_mission": {"all": "Second fires, rounds complete on target.", "ww2": "Second battery firing for effect."},
        "battery_located": {"all": "Second fires, enemy guns located. Engaging."},
        "request": {"all": "Second fires requesting approval to fire."},
        "escalation": {"all": "Second fires, ammunition low. Need resupply."},
    },
    "air1": {
        "cas_on_station": {"all": "Air one on station. Ready for tasking.", "ww2": "Fighter-bombers overhead. Ready to strike."},
        "request": {"all": "Air one requesting a strike on the marked target."},
        "escalation": {"all": "Air one, heavy flak. Need the defenses suppressed.", "modern": "Air one, surface-to-air threat. Need suppression."},
        "withdraw": {"all": "Air one, winchester. Returning to rearm."},
    },
    "air2": {
        "cas_on_station": {"all": "Air two checking in. Weapons hot.", "ww2": "Second flight overhead. Ready."},
        "request": {"all": "Air two requesting clearance to engage."},
        "escalation": {"all": "Air two, we have lost an aircraft. Request cover."},
        "withdraw": {"all": "Air two, bingo fuel. Heading home."},
    },
    "armor": {
        "assault_started": {"all": "Armor moving. Tanks forward.", "ww2": "Tanks rolling. Follow us in."},
        "withdraw": {"all": "Armor pulling back damaged tanks for repair."},
        "request": {"all": "Armor requesting approval to counterattack."},
        "escalation": {"all": "Armor, we are losing tanks. Request fire support."},
        "contact": {"all": "Enemy armor. Engaging.", "ww2": "Panzers. Engaging.", "modern": "Enemy tanks, thermal contact. Engaging."},
    },
    "logistics": {
        "resupply": {"all": "Logistics, resupply under way. Ammunition and fuel moving forward."},
        "request": {"all": "Logistics requesting escort for the convoy."},
        "escalation": {"all": "Logistics, the supply route is cut. Need it cleared."},
    },
}
ERAS = ["ww2", "coldwar", "modern"]
PRIORITY = {"escalation": 1, "take_control": 1, "return_control": 1, "objective_lost": 1, "contact": 2, "request": 2}


def rows() -> list[dict]:
    out = []
    for speaker, triggers in LINES.items():
        for trigger, texts in triggers.items():
            for era in ERAS:
                out.append({
                    "id": f"{speaker}_{trigger}_{era}", "speaker": speaker, "era": era, "faction": "player", "trigger": trigger,
                    "priority": PRIORITY.get(trigger, 3), "text": texts.get(era, texts["all"]), "variants": 1,
                    "audio_tags": "radio",
                })
    return out


def render() -> tuple[str, str]:
    buf = io.StringIO()
    w = csv.DictWriter(buf, fieldnames=["id", "speaker", "era", "faction", "trigger", "priority", "text", "variants", "audio_tags"], lineterminator="\n")
    w.writeheader()
    w.writerows(rows())
    voices = {k: {"role": v[0], "names": NAMES[k], "mode": "design", "instruction": v[1], "seed": v[2]} for k, v in VOICES.items()}
    return buf.getvalue(), json.dumps({"version": 1, "engine": "Qwen3-TTS-12Hz-1.7B VoiceDesign (local, D020)", "speakers": voices}, indent=2) + "\n"


def main() -> int:
    lines, voices = render()
    targets = {ROOT / "dialogue" / "lines.csv": lines, ROOT / "audio" / "voices.json": voices}
    if "--check" in sys.argv[1:]:
        stale = [str(p.relative_to(ROOT)) for p, t in targets.items() if not p.exists() or p.read_text() != t]
        print(f"generate_dialogue: {len(rows())} lines, {'stale: ' + ', '.join(stale) if stale else 'up to date'}")
        return 1 if stale else 0
    for p, t in targets.items():
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(t)
    print(f"generate_dialogue: {len(rows())} lines, {len(VOICES)} speakers")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
