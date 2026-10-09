#!/usr/bin/env python3
"""Generate data/campaign.json: one branching campaign per era (MODE-02).

    python3 scripts/generate_campaign.py [out]          write (default data/campaign.json)
    python3 scripts/generate_campaign.py --check        G10: graph rules, decks exist, every briefing has its audio

Each era is six levels deep on two tracks: level 1 has one operation; from level 2 on there is an advance operation
("a") and a fallback operation ("f"). A win sends the campaign to the next level's advance operation, a loss to its
fallback operation, so every path is exactly six operations and both outcomes change what comes next. Briefings are
read by the brigade operations officer; their audio is generated locally (scripts/generate_briefings.py, D020) into
game/assets/audio/briefings/<era>/<id>.wav.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
AUDIO = ROOT / "game" / "assets" / "audio" / "briefings"

# (id, name, player deck, enemy deck, victory, time of day, weather, briefing)
ERAS = {
    "coldwar": ("Brigade S3", [
        ("cw1", "Covering Force", "us_1989_armored_cavalry", "su_1989_forward_detachment", "conquest", "dawn", "overcast",
         "Commander, the Pact crossed the border at oh-four-hundred. Our covering force holds the Fulda corridor. Trade space for time, keep the cavalry alive, and hold the crossroads until the brigade is up."),
        ("cw2a", "Hold at Huenfeld", "us_1989_armored", "su_1989_motor_rifle", "conquest", "day", "clear",
         "The covering force bought us the time we needed. The brigade is in position east of Huenfeld. The motor rifle regiment will come straight at us. Take the objectives and hold them."),
        ("cw2f", "Delay to the Vogelsberg", "us_1989_armored_cavalry", "su_1989_tank_division", "annihilation", "dusk", "rain",
         "We lost the crossroads. The lead tank regiment is pushing west in the rain. Fall back toward the Vogelsberg and bleed every company that follows. Do not lose the brigade."),
        ("cw3a", "Counterattack", "us_1989_heavy_division", "su_1989_motor_rifle", "conquest", "day", "clear",
         "The line held and corps has released the heavy brigade. We counterattack at first light. Retake the villages along the main road before the second echelon closes up."),
        ("cw3f", "The Second Line", "us_1989_armored", "su_1989_tank_division", "conquest", "night", "clear",
         "We are on the second line now. The enemy will try a night attack with fresh tanks. Use the thermals. Hold the high ground and let them come to us."),
        ("cw4a", "Air Assault", "us_1989_air_cavalry", "su_1989_air_assault", "conquest", "day", "overcast",
         "Their airborne battalion has landed behind us. Air cavalry is attached. Find them, pin them, and clear the landing zones before they link up with the main body."),
        ("cw4f", "Rearguard", "us_1989_armored_cavalry", "su_1989_forward_detachment", "annihilation", "dusk", "fog",
         "Fog is rolling in and a forward detachment is trying to cut the road behind us. The rearguard is yours. Destroy that detachment or we lose the way out."),
        ("cw5a", "Restore the Border", "us_1989_heavy_division", "su_1989_tank_division", "conquest", "day", "clear",
         "The enemy is falling back to the border. Their tank division is covering the withdrawal. Push through it and put us back on the inner German border."),
        ("cw5f", "Last Line", "us_1989_armored", "su_1989_motor_rifle", "conquest", "night", "snow",
         "This is the last line before the Rhine crossings. Reinforcements land in two days. Hold here through the night, whatever it costs."),
        ("cw6a", "Exploitation", "us_1989_armored", "su_1989_motor_rifle", "annihilation", "day", "clear",
         "Their front is breaking. Corps wants the last motor rifle regiment destroyed before it can regroup. Finish it."),
        ("cw6f", "Relief", "us_1989_heavy_division", "su_1989_tank_division", "conquest", "dawn", "overcast",
         "The reinforcing division is coming up behind us. One more attack, Commander. Take back the crossroads and hand them the line intact."),
    ]),
    "ww2": ("Combat Command S3", [
        ("ww1", "Breakout", "us_1944_armored_division", "ger_1944_infanterie_division", "conquest", "dawn", "overcast",
         "Sir, the bombers are finished and the line in front of us is broken. Push the combat command through the gap and take the road junction before the Germans can close it."),
        ("ww2a", "Into the Bocage", "us_1944_infantry_division", "ger_1944_infanterie_division", "conquest", "day", "clear",
         "We are through. The hedgerow country is ahead. The infantry leads, the tanks follow close. Take the hamlets one field at a time."),
        ("ww2f", "Hold the Bridgehead", "us_1944_infantry_division", "ger_1944_panzer_division", "conquest", "dusk", "rain",
         "The breakout stalled and a panzer division is counterattacking in the rain. Dig in around the bridgehead and keep the bazookas forward."),
        ("ww3a", "Armored Thrust", "us_1944_armored_division", "ger_1944_aufklaerung", "conquest", "day", "clear",
         "Their reconnaissance battalion is all that stands between us and open country. Run it down and keep moving."),
        ("ww3f", "Counterstroke", "us_1944_armored_division", "ger_1944_panzer_division", "annihilation", "day", "overcast",
         "The panzers are overextended. We hit them in the flank this morning. Destroy them in the open before they reach cover."),
        ("ww4a", "River Crossing", "us_1944_cavalry_group", "ger_1944_infanterie_division", "conquest", "dawn", "fog",
         "The cavalry has found a crossing under the fog. Get across, seize the far bank, and hold it for the engineers."),
        ("ww4f", "Close Air Support", "us_1944_ixth_tac_air", "ger_1944_schlachtgeschwader", "conquest", "day", "clear",
         "The weather has cleared and the fighter-bombers are ours today. Use them. Break the attack on the ridge before it starts."),
        ("ww5a", "Encirclement", "us_1944_armored_division", "ger_1944_panzer_division", "annihilation", "day", "clear",
         "The pocket is closing. What is left of their panzer division is trying to break out. Do not let it."),
        ("ww5f", "The Long Night", "us_1944_infantry_division", "ger_1944_infanterie_division", "conquest", "night", "snow",
         "Snow and a long night ahead. They will attack before dawn. Hold the village and keep the guns registered on the approaches."),
        ("ww6a", "Pursuit", "us_1944_cavalry_group", "ger_1944_aufklaerung", "conquest", "day", "clear",
         "The enemy is in full retreat. Pursue, take the last crossroads, and do not give them time to form a line."),
        ("ww6f", "Turning Point", "us_1944_armored_division", "ger_1944_infanterie_division", "conquest", "dawn", "overcast",
         "Reinforcements are up and the weather is turning. We attack at dawn. Take back everything we gave up."),
    ]),
    "modern": ("Brigade Operations Officer", [
        ("md1", "Suwalki Gap", "us_2026_stryker_bct", "ru_2026_btg", "conquest", "dawn", "overcast",
         "Commander, battalion tactical groups crossed into the gap an hour ago. Expect drones overhead and jamming on every net. Hold the corridor until the armored brigade arrives."),
        ("md2a", "Armored Response", "us_2026_abct", "ru_2026_motor_rifle", "conquest", "day", "clear",
         "The armored brigade is up. The motor rifle brigade is digging in on the high ground. Take it back before their artillery is registered."),
        ("md2f", "Delay in Depth", "us_2026_stryker_bct", "ru_2026_tank_army", "annihilation", "dusk", "rain",
         "We could not hold the corridor. Tanks are coming through in the rain. Delay in depth, use the Javelins, and keep your formations dispersed."),
        ("md3a", "Counter-Battery", "us_2026_abct", "ru_2026_motor_rifle", "conquest", "day", "clear",
         "Their guns are hitting the road junction. Find the batteries, silence them, and then take the junction."),
        ("md3f", "Night Defense", "us_2026_abct", "ru_2026_tank_army", "conquest", "night", "clear",
         "They will attack at night. Our thermals see further than theirs. Hold the ridge and make them pay for every kilometer."),
        ("md4a", "Air Assault", "us_2026_combat_aviation", "ru_2026_army_aviation", "conquest", "day", "overcast",
         "Their attack helicopters are hunting our supply routes. Aviation brigade is attached. Clear the sky and secure the landing zones."),
        ("md4f", "Under the Fog", "us_2026_stryker_bct", "ru_2026_btg", "annihilation", "dawn", "fog",
         "Fog grounds everything that flies today. A battalion group is moving to cut our rear. Find it and destroy it."),
        ("md5a", "Breakthrough", "us_2026_armored_division", "ru_2026_tank_army", "conquest", "day", "clear",
         "Their tank army is committed. Division wants us through the gap before their second echelon arrives. Break through."),
        ("md5f", "Hold the Line", "us_2026_abct", "ru_2026_motor_rifle", "conquest", "night", "snow",
         "Snow tonight and an attack before morning. Allied reinforcements are a day out. Hold the line."),
        ("md6a", "Restore the Border", "us_2026_armored_division", "ru_2026_motor_rifle", "annihilation", "day", "clear",
         "The last brigade on our side of the border is cut off. Destroy it and the border is ours again."),
        ("md6f", "Reinforcement", "us_2026_abct", "ru_2026_tank_army", "conquest", "dawn", "overcast",
         "The allied division is behind us now. One more attack. Take back the gap and hand them the line."),
    ]),
}


def build() -> dict:
    eras = {}
    for era, (speaker, ops) in ERAS.items():
        nodes = []
        for i, (oid, name, deck, enemy, victory, tod, weather, text) in enumerate(ops):
            level = int(oid.rstrip("af")[-1])
            nxt = None if level == 6 else level + 1
            prefix = oid.rstrip("af")[:-1]
            nodes.append({
                "id": oid, "name": name, "level": level, "player_deck": deck, "enemy_deck": enemy, "victory": victory,
                "time_of_day": tod, "weather": weather, "seed": 1000 + (100 * list(ERAS).index(era)) + i,
                "speaker": speaker, "briefing": text,
                "audio": f"res://assets/audio/briefings/{era}/{oid}.wav",
                "next_win": f"{prefix}{nxt}a" if nxt else None,
                "next_loss": f"{prefix}{nxt}f" if nxt else None,
            })
        eras[era] = {"start": ops[0][0], "operations": nodes}
    return {"version": 1, "eras": eras}


def problems(c: dict) -> list[str]:
    out = []
    decks = {d["id"]: d for d in json.loads((ROOT / "data" / "decks.json").read_text())["decks"]}
    for era, e in c["eras"].items():
        nodes = {n["id"]: n for n in e["operations"]}
        for n in e["operations"]:
            for k in ("player_deck", "enemy_deck"):
                d = decks.get(n[k])
                if d is None:
                    out.append(f"{era}/{n['id']}: unknown deck {n[k]}")
                elif d["era"] != era:
                    out.append(f"{era}/{n['id']}: {n[k]} is a {d['era']} deck")
            if n["player_deck"] in decks and n["enemy_deck"] in decks and decks[n["player_deck"]]["side"] == decks[n["enemy_deck"]]["side"]:
                out.append(f"{era}/{n['id']}: both decks on one side")
            for k in ("next_win", "next_loss"):
                if n[k] is not None and n[k] not in nodes:
                    out.append(f"{era}/{n['id']}: {k} {n[k]} missing")
            if n["next_win"] is not None and n["next_win"] == n["next_loss"]:
                out.append(f"{era}/{n['id']}: win and loss lead to the same operation (no branch)")
            wav = AUDIO / era / f"{n['id']}.wav"
            if not wav.exists():
                out.append(f"{era}/{n['id']}: briefing audio missing ({wav.relative_to(ROOT)})")
        # every path from the start is exactly six operations
        lengths = set()

        def walk(nid: str, depth: int) -> None:
            n = nodes[nid]
            if n["next_win"] is None:
                lengths.add(depth)
                return
            walk(n["next_win"], depth + 1)
            walk(n["next_loss"], depth + 1)
        if e["start"] in nodes:
            walk(e["start"], 1)
        if lengths != {6}:
            out.append(f"{era}: path lengths {sorted(lengths)}, need every path = 6")
        reach = set()
        stack = [e["start"]]
        while stack:
            x = stack.pop()
            if x in reach or x not in nodes:
                continue
            reach.add(x)
            stack += [y for y in (nodes[x]["next_win"], nodes[x]["next_loss"]) if y]
        if reach != set(nodes):
            out.append(f"{era}: unreachable operations {sorted(set(nodes) - reach)}")
    return out


def main() -> int:
    c = build()
    if "--check" in sys.argv[1:]:
        current = json.loads((ROOT / "data" / "campaign.json").read_text())
        out = problems(current)
        if current != c:
            out.append("data/campaign.json differs from scripts/generate_campaign.py; regenerate")
        # self-test: a broken copy must be caught
        bad = json.loads(json.dumps(c))
        bad["eras"]["coldwar"]["operations"][1]["next_loss"] = bad["eras"]["coldwar"]["operations"][1]["next_win"]
        if not any("no branch" in p for p in problems(bad)):
            out.append("check_campaign self-test: a missing branch was not detected")
        for p in out:
            print(p)
        print(f"check_campaign: {sum(len(e['operations']) for e in c['eras'].values())} operations, {len(out)} problems")
        return 1 if out else 0
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "data" / "campaign.json"
    out.write_text(json.dumps(c, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
