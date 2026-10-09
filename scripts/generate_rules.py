#!/usr/bin/env python3
"""Generate data/rules.json: every global tuning number the battle sim reads.

No balance value lives in sim code (contract section 10). Each number has a
`why` note here; tune it here, regenerate, and record big changes in DECISIONS.md.
All values are integers so the sim stays fixed-point.

Usage: scripts/generate_rules.py [out_path]   (default data/rules.json)
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

# (value, why) pairs; the why is dropped from the output.
RULES = {
    "version": (1, "rules schema version"),
    "tick_hz": (20, "SIM-01 fixed tick"),
    "los": {
        "step_m": (8, "ray sample spacing; equals the default terrain cell"),
        "eye_height_dm": (25, "vehicle commander / standing soldier eye height, 2.5 m"),
        "target_height_dm": (20, "aim point height above ground, 2 m"),
        "forest_height_dm": (180, "mature Central European mixed forest canopy, 18 m"),
        "forest_block_pct_m": (12000, "density% x metres of canopy a ray may cross before it is blocked: 120 m of 100% forest"),
        "smoke_height_dm": (250, "smoke screen height, 25 m"),
        "hedge_height_dm": (30, "Normandy-style hedgerow on its bank, ~3 m; blocks a tank's 2.5 m eye line"),
    },
    "detect": {
        "stealth_forest_bonus": (150, "metres subtracted from enemy optics when the target stands in forest"),
        "stealth_moving_penalty": (100, "metres added back when the target moved this tick"),
        "firing_reveal_m": (2000, "a unit that fired is visible to anyone with LOS within this range for reveal_ticks"),
        "reveal_ticks": (100, "5 s muzzle-flash and dust signature"),
        "spot_interval_ticks": (4, "each target's detection is re-evaluated every 0.2 s, staggered by unit id (SIM-13 budget)"),
    },
    "move": {
        "nav_cell_m": (16, "pathfinding grid cell; 500x500 on an 8 km map; fine enough that a blown 20 m breach opens a route (D2)"),
        "replan_ticks": (40, "re-path at most every 2 s when blocked"),
        "dismount_ticks": (600, "a carried infantry team fights on foot for 30 s after it last fired, was hit or was suppressed, then remounts (MEC-09; design)"),
        # Cost per nav cell, percent of open-ground cost, per movement class.
        "cost_open": ({"tracked": 100, "wheeled": 100, "infantry": 100}, "baseline"),
        "cost_road": ({"tracked": 60, "wheeled": 40, "infantry": 80}, "roads favor wheels (FM 3-21.71 march rates)"),
        "cost_forest": ({"tracked": 300, "wheeled": 600, "infantry": 140}, "vehicles crawl through woods; infantry barely slowed"),
        "cost_rubble": ({"tracked": 400, "wheeled": 0, "infantry": 200}, "rubble: slow for tracks, impassable (0) for wheels"),
        "max_slope_pct": ({"tracked": 60, "wheeled": 30, "infantry": 100}, "M1 gradeability 60%; trucks ~30%"),
        "speed_road": ({"tracked": 100, "wheeled": 100, "infantry": 100}, "unit speed_mm_s is its road speed"),
        "speed_open": ({"tracked": 75, "wheeled": 55, "infantry": 90}, "cross-country fraction of road speed"),
        "speed_forest": ({"tracked": 30, "wheeled": 15, "infantry": 70}, "in woods"),
        "speed_rubble": ({"tracked": 25, "wheeled": 0, "infantry": 50}, "climbing over collapsed buildings"),
    },
    "structure": {
        "chunk_m": (3, "building chunk width; sim grid of section M3 building kits (2.5-3 m)"),
        "floor_dm": (28, "storey height 2.8 m: MBO §47(1) 2.40 m clear height plus ~0.35 m slab (data/assets/wgerman_town_block_1989.json storey_height_typical_m 2.75), on the 1 dm grid"),
        "collapse_delay_ticks": (10, "0.5 s between losing support and falling (D2 'short delay')"),
        "fall_damage": (60, "hp a falling chunk deals to a unit under it"),
        "rubble_dm_per_chunk": (6, "each fallen chunk raises its rubble cell by 0.6 m"),
        "materials": (
            {
                "brick": {"hp": 400, "armor": 30},
                "concrete": {"hp": 600, "armor": 50},
                "timber": {"hp": 150, "armor": 5},
                "plaster": {"hp": 120, "armor": 5},
            },
            "chunk durability; relative ordering concrete > brick > timber ~ plaster. Absolute values get tuned against the D1 yield table (data/vfx/yields.json) when artillery lands",
        ),
    },
    "combat": {
        "arc_front_half_deg": (45, "front arc +-45 deg from hull heading; rear arc is the mirror; the rest is side"),
        "air_altitude_m": (20, "targets above this are air targets; ground-only weapons ignore them"),
        "shooter_moving_pct": (50, "accuracy multiplier when the shooter moved this tick (1989 stabilised MBTs fire on the move at a penalty)"),
        "target_moving_pct": (75, "accuracy multiplier against a moving target"),
        "turret_jam_half_deg": (10, "a jammed turret only fires within +-10 deg of the hull heading"),
        "nonpen_suppression": (15, "suppression from a hit that does not penetrate"),
        "nonpen_minor_crit_pct": (8, "chance a non-penetrating hit still breaks optics or a track"),
        "crit_base_pct": (25, "chance a penetrating hit also causes a critical"),
        "crit_per_100mm_overmatch_pct": (25, "extra critical chance per 100 mm of penetration above armor"),
        "crit_weights": ({"tracked": 20, "optics": 15, "engine": 15, "turret": 15, "bail": 15, "fire": 20}, "relative odds of each SIM-07 critical"),
        "fire_hp_per_tick": (1, "a burning vehicle loses 1 hp per tick (20 hp/s)"),
        "vet_accuracy_pct": ([100, 110, 120, 130], "veterancy 0-3 accuracy multipliers (MEC-04)"),
        "vet_kills": ([0, 2, 5, 10], "kills needed for veterancy 0-3 (MEC-04): promotion is earned, and kept through a retreat"),
        "cover_light_pct": (70, "hit chance multiplier in light cover: inside forest, behind a hedge or wreck on the shooter's side, infantry in a crater (MEC-04; design)"),
        "cover_heavy_pct": (40, "hit chance multiplier inside a standing building (MEC-04; design); fire from aircraft only respects heavy cover"),
        "cover_object_m": (12, "a wreck, hedge or crater counts as cover within 12 m of the target"),
        "crater_min_blast_m": (20, "HE impacts with a blast radius of at least 20 m (artillery, bombs) leave a crater"),
        "mine_damage": (60, "hp a mine strike deals to a vehicle (with a thrown track); half that as a hit on infantry (SUP-03; design: a TM-62/M15-class blast immobilises, often without a kill)"),
        "mine_deplete_pct": (20, "each strike takes 20 points off the cell's density (SUP-03; design)"),
        "jam_radar_pct": (50, "radar-directed guns firing from inside an enemy radar-jamming zone hit half as often (SUP-04; design: noise jamming forces optical/degraded tracking)"),
        "decoy_pct": (50, "missiles fired at aircraft inside their own side's chaff/decoy corridor hit half as often (SUP-04; design)"),
        "gps_jam_cep_x": (25, "satellite-guided rounds landing inside an enemy GPS-jamming zone scatter 25x their CEP (SUP-04; sourced trend: Excalibur fell from 70% to 6% efficiency within six weeks of Russian EW, Wikipedia M982 Excalibur; factor design)"),
        "fpv_success_pct": (60, "chance an FPV drone strike reaches its target (SUP-01; design)"),
        "fpv_jammed_pct": (20, "FPV strike success inside an enemy comms-jamming zone (SUP-01/04; design: jamming severs the video/control link)"),
        "escort_pct": (40, "enemy air-to-air missiles at aircraft inside their side's fighter-escort zone hit 40% as often (SUP-02; design: the escort engages the interceptors first)"),
        "jam_comms_delay_ticks": (600, "batteries inside an enemy comms-jamming zone take 30 s more to start a fire mission (SUP-04; design: relays and retransmission under jamming)"),
        "crater_max": (512, "craters kept per battle (oldest overwritten)"),
        "direct_control_hit_pct": (0, "extra hit chance for a player-flown aircraft: 0 by design, so Take Control is a skill, never a stat buff (MEC-08); kept as data so G4 proves the parity check would catch one"),
        "reveal_on_fire_ticks": (100, "a unit that fires is revealed for 5 s (see detect.firing_reveal_m)"),
    },
    "morale": {
        "suppression_max": (200, "cap"),
        "decay_per_tick": (1, "suppression shed per tick in friendly territory (20/s)"),
        "hq_decay_bonus": (1, "extra decay per tick within hq_radius_m of a friendly command unit"),
        "hq_radius_m": (300, "command units steady troops nearby (C1-3)"),
        "shaken_at": (40, "suppression >= this: shaken"),
        "pinned_at": (80, "suppression >= this: pinned, will not advance"),
        "routed_at": (130, "suppression >= this: routed, falls back to home"),
        "recover_margin": (20, "drop this far below a threshold to step back up the ladder (hysteresis)"),
        "accuracy_pct": ([100, 70, 40, 10], "accuracy multiplier for normal, shaken, pinned, routed"),
        "surrender_enemy_m": (150, "a routed unit with a detected enemy this close may surrender"),
        "surrender_after_ticks": (200, "routed for 10 s with an enemy that close: surrenders"),
        "casualty_suppression": (30, "suppression added to a squad per member lost / vehicle per penetration"),
    },
    "supply": {
        "radius_m": (60, "a logistics vehicle resupplies friendly units within 60 m"),
        "ammo_interval_ticks": (20, "one round per mount per second while in range"),
        "fuel_per_tick": (2, "fuel units transferred per tick"),
        "fuel_per_supply_point": (10, "10 fuel units cost 1 supply point"),
        "repair_interval_ticks": (20, "a recovery vehicle restores 1 hp per second to each damaged friendly vehicle in supply radius (SUP-03; design)"),
        "repair_crit_ticks": (1200, "60 s of field repair fixes one critical: thrown track, engine, optics or turret drive (SUP-03; FM 9-43-2 battle damage repair, abstracted)"),
        "medevac_ticks": (600, "a halted medevac aircraft returns one lightly wounded soldier to a friendly squad in supply radius every 30 s, if the squad has not been hit in the last 30 s (SUP-03; design)"),
        "dump_refill_per_tick": (5, "an ammunition dump reloads a friendly truck in supply radius at 5 supply points per tick (SUP-03; design)"),
        "aircraft_turnaround_ticks": (2400, "2 minutes off-map to rearm and refuel at the airbase"),
        "bingo_fuel_pct": (25, "aircraft go home at 25% fuel (Bingo)"),
    },
    "economy": {
        "phase_ticks": ([12000, 12000], "phase A and B last 10 minutes each; C runs to the end (C1-1, Steel Division 2)"),
        "start_dp": (600, "deployment points at the start of the battle"),
        "income_per_minute": (
            {
                "vanguard": [60, 40, 30],
                "maverick": [35, 60, 35],
                "balanced": [45, 45, 45],
                "juggernaut": [30, 40, 65],
            },
            "deployment points per minute in phase A, B, C; each profile peaks in a different phase",
        ),
        "sp_kill_pct": (20, "support points earned = 20% of the destroyed unit's card cost"),
        "field_request_auto_ticks": (600, "an XO-run player's Field Requests offer is taken after 30 s if the player has not chosen (MEC-06; design)"),
        "sp_spot_assist_pct": (10, "the player whose unit spotted the victim earns 10% too (recon pays, C1-4)"),
        "sp_per_supply_points": (10, "1 support point per 10 supply points delivered (logistics pays, C1-4)"),
    },
    "frontline": {
        "cell_m": (64, "influence grid cell; 125x125 on an 8 km map"),
        "update_ticks": (20, "territory recomputed once a second"),
        "radius_m": (500, "a unit's influence fades linearly to zero at 500 m"),
        "flip_margin": (50, "influence units (weight x 100 at the unit): a cell changes owner only when the other side beats its owner by this much; a lone weight-2 squad takes ground within ~375 m"),
        "weight_by_kind": (
            {"infantry": 2, "tank": 3, "ifv": 2, "artillery": 0, "aa": 1, "air": 0, "supply": 0, "command": 1},
            "front-moving weight per sim kind; the roster's front_weight overrides (recon and AT teams are 0, C1-2)",
        ),
    },
    "fires": {
        "setup_ticks": (200, "10 s to lay the guns and compute data for a new mission (FM 6-40 'fire for effect' timelines are minutes; abstracted)"),
        "smoke_radius_m": (40, "screen radius of one smoke round"),
        "smoke_ticks": (2400, "a smoke round screens for 2 minutes"),
        "max_pending": (512, "rounds in flight at once"),
        "cb_locate_ticks": (600, "30 s for the enemy's counter-battery radar (AN/TPQ-37, ARK-1) to locate a firing battery"),
        "cb_error_m": (50, "location error of a counter-battery fix"),
        "illum_radius_m": (500, "one illumination round lights a 500 m radius (D8)"),
        "illum_ticks": (900, "under its parachute for 45 s (D8: 30-60 s)"),
    },
    "command": {
        "plan_ticks": (20, "every commander re-plans once a second"),
        "assemble_pct": (70, "an attack waits until 70% of the assigned, fit force is at the staging point (no piecemeal attacks, section 4)"),
        "assemble_radius_m": (250, "a unit within 250 m of the staging point counts as assembled"),
        "assemble_timeout_ticks": (2400, "or 2 minutes, whichever comes first"),
        "staging_distance_m": (1500, "staging point this far short of the objective, scaled by aggression"),
        "prep_wait_ticks": (1600, "wait up to 80 s for preparatory fires to land before the assault"),
        "prep_rounds": (6, "rounds per battery in a preparatory fire mission"),
        "damaged_hp_pct": (50, "a unit below 50% hp (or with crew-bail/tracked) withdraws to resupply"),
        "low_ammo_pct": (25, "a unit with its main weapon below 25% asks logistics for resupply"),
        "objective_radius_m": (300, "an objective is taken when no enemy and some friendly combat unit is within 300 m"),
        "fail_strength_pct": (40, "an attack whose committed force falls below 40% is failing: escalate"),
        "defend_spacing_m": (120, "frontage per unit on a defend line"),
        "cover_search_m": (100, "a defender looks this far around its slot for forest or building cover"),
        "formation_spacing_m": (60, "lateral spacing in an assault line"),
        "cas_on_station_m": (1500, "a CAS aircraft is on station within 1.5 km of the request point: inside GAU-8 (1.8 km) and Maverick range"),
        "cas_loiter_ticks": (1200, "it then works the target area for 60 s before it is free for other tasks"),
        "cap_orbit_m": (2000, "a CAP flies a racetrack of 2 km radius around its point"),
        "cap_leg_ticks": (300, "one orbit leg per 15 s (2.8 km at ~190 m/s)"),
        "planner_prep_ticks": (2400, "a battle-planner order reaches full preparation after 2 minutes left to its commander (MEC-05; design)"),
        "planner_prep_bonus_pct": (20, "full preparation gives the plan's units +20% hit chance (MEC-05; design)"),
        "planner_micro_decay": (25, "each direct order to one of the plan's units costs 25 of 100 preparation points (micromanagement, MEC-05; design)"),
        "planner_garrison_radius_m": (200, "a garrison order occupies buildings within 200 m of its point"),
        "priority_target_pct": (300, "a seat's priority target class scores 3x in its units' target choice: a strong preference that still yields to a much better shot"),
        "cas_threat_margin_m": (1000, "CAS is not flown into a target area within a spotted enemy air-defence weapon's reach plus 1 km (attack run and egress); suppress it first"),
        "screen_withdraw_m": (600, "a screen post that takes fire falls back one bound (600 m) away from the shooter: it trades space for time, it does not fight"),
        "screen_contact_ticks": (100, "a post counts as engaged for 5 s after its last hit"),
    },
    "victory": {
        "points_per_minute": (10, "victory points per minute per held objective (conquest); 10 (was 6) so AI matches end before the 40-minute cap: 0/200 capped, median 24 min (D073)"),
        "win_points": (600, "first to 600 points wins"),
        "time_cap_ticks": (48000, "40 minutes; then the higher score wins"),
        "annihilation_grace_ticks": (1200, "annihilation victory (MODE-01): a side with no ground combat units left loses, counted from the first minute so both sides can deploy"),
    },
    "weather_optics_pct": (
        {"clear": 100, "overcast": 90, "rain": 75, "fog": 45, "snow": 60, "night": 40},
        "detection range multiplier per condition (SIM-11 baseline)",
    ),
    "weather_accuracy_pct": (
        {"clear": 100, "overcast": 100, "rain": 90, "fog": 75, "snow": 85, "night": 70},
        "hit chance multiplier per condition (SIM-11; design): obscured aim in fog and darkness, wet optics and wind in rain and snow",
    ),
    "weather_air_ops": (
        {"clear": 1, "overcast": 1, "rain": 1, "fog": 0, "snow": 1, "night": 1},
        "1 = aircraft fly; 0 = grounded (SIM-11: low-visibility fog closes 1989 airfields and visual CAS): air cards cannot deploy, air strikes and SEAD are refused, CAS is not dispatched",
    ),
    "time_of_day": (
        {"names": ["dawn", "day", "dusk", "night"], "optics_pct": [70, 100, 70, 40], "accuracy_pct": [90, 100, 90, 70]},
        "MEC-07 operations advance the clock one step per phase; dark steps cut detection and accuracy like weather (night = the weather 'night' values; thermal sights cancel night)",
    ),
    "operation": (
        {"spawn_pullback_m": 500},
        "MEC-12: after a phase the loser's entry point falls back 500 m toward its map edge and the winner holds the objectives",
    ),
    "grand_strategy": {
        "turn_limit": (40, "a war that is still undecided after 40 turns (about 10 months at a week a turn) ends in a negotiated stalemate"),
        "strength_per_point": (5, "one production point raises 5 army strength (and costs 1 manpower per 5 strength)"),
        "army_max": (100, "an army is at most 100 strength (a corps-sized formation)"),
        "start_army_per_industry": (8, "each province starts with an army of 8 strength per industry point under its owner"),
        "research_base": (30, "the first technology level costs 30 production; each further level 15 more"),
        "research_step": (15, "cost added per technology level"),
        "tech_max": (5, "five technology levels per era"),
        "tech_bonus_pct": (8, "each technology level adds 8% to combat power"),
        "terrain_defence_pct": ({"open": 100, "forest": 125, "hills": 135, "urban": 150}, "defender combat power multiplier by province terrain"),
        "commander_bonus_pct": (10, "a commander adds 10% to his army's power, plus 1% per 10 XP up to 10%"),
        "commander_xp_win": (10, "XP for a won battle (CMD-08)"),
        "commander_xp_loss": (3, "XP for a lost battle"),
        "commander_wound_pct": (15, "chance per battle that the commander is wounded"),
        "commander_wound_turns": (3, "turns a wounded commander is out; a replacement (0 XP) leads meanwhile"),
        "loss_winner_pct": (15, "strength the winner loses in a battle"),
        "loss_loser_pct": (35, "strength the loser loses in a battle"),
        "attrition_unsupplied_pct": (10, "an army cut off from its faction's capital loses 10% strength a turn"),
        "lend_lease_pct": (20, "a lend-lease agreement transfers 20% of the giver's production each turn"),
        "ceasefire_turns": (6, "a ceasefire holds for 6 turns"),
        "tension_battle": (1, "tension per battle"),
        "tension_capture": (3, "tension per province captured"),
        "tension_tactical": (20, "tension from which tactical nuclear weapons are released (an RTS support card)"),
        "tension_theater": (40, "tension from which theater strikes on provinces are possible"),
        "tension_strategic": (70, "tension at which a strategic exchange ends the war for everyone"),
        "fallout_turns": (4, "a struck province is a fallout zone for 4 turns"),
        "fallout_attrition_pct": (20, "armies in a fallout zone lose 20% strength a turn"),
        "theater_strike_industry": (3, "a theater strike destroys up to 3 industry of the province"),
        "reinforce_turns": (2, "MEC-13: the AI sends a reserve to an attacked province within 2 turns"),
        "attack_odds_permille": (550, "the AI attacks when its odds are at least 55%"),
    },
    "thermal": (
        {"optics_pct": 85, "ignores": ["fog", "night"]},
        "thermal sights (roster `thermal`) keep at least 85% of their detection range and full accuracy in fog and darkness (SIM-11; M1A1/Bradley/TOW-2 TIS of the period)",
    ),
}


def strip(node):
    if isinstance(node, tuple):
        return strip(node[0])
    if isinstance(node, dict):
        return {k: strip(v) for k, v in node.items()}
    return node


def main() -> int:
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "data" / "rules.json"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(strip(RULES), indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
