#!/usr/bin/env python3
"""Generate data/support.json: the off-map support catalogue (SUP-02, SUP-05, MEC-03), Cold War 1989 slice.

    python3 scripts/generate_support.py [out.json]

Every option is data: era, side, Support Point cost, charges, recharge and response delay, and its effect. The sim
(`sim/Battle/Support.cs`) implements six effect types:
  barrage     `rounds` shells of an existing artillery weapon (data/weapons.json), dispersed by that weapon's CEP at a
              notional off-map firing range `range_m`
  precision   laser-guided rounds of that weapon landing within `cep_m` of the aim point; refused unless a friendly unit
              within `designator_m` has line of sight to it (SUP-01)
  smoke       smoke rounds of that weapon (screen radius and duration from rules.fires)
  illum       illumination rounds (rules.fires)
  strike_run  an air attack run: `impacts` blasts of `blast_radius_m` / `blast_damage` laid along `length_m` from the
              aim point on `heading_deg` (the sim's convention: 0 = east, counter-clockwise; the UI will let the
              player drag the run direction), one every `interval_ticks` once the aircraft arrives
  sead        an anti-radiation strike: destroys the nearest radar-directed air-defence unit (anti-air weapon of type
              "aaa": ZSU-23-4 Gun Dish, M163 ranging radar; the 1989 roster's SAMs are all infrared) within `radius_m`
  minefield   lays a scatterable anti-tank minefield of `density` (percent strike chance per cell entered by a
              vehicle) within `radius_m` (SUP-03; sim/Battle/Mines.cs)
  bridge / fortify / obstacle / breach
              engineer support on a strip `length_m` x `width_m` along `heading_deg` (SUP-03; sim/Battle/Engineering.cs):
              a gap-crossing span, a trench line, an anti-tank ditch, or a lane cleared of mines and obstacles
  jam_radar / jam_comms / decoy
              electronic warfare zone of `radius_m` for `duration_ticks` (SUP-04; sim/Battle/ElectronicWarfare.cs)
  air_landing `count` units of roster `unit` land within `scatter_m` of the aim point (paradrop, air assault)
  gunship     orbit for `duration_ticks`: every `interval_ticks` a burst on the nearest visible enemy within `radius_m`
  cruise      one precise blast at the aim point
  escort      fighter escort zone of `radius_m` for `duration_ticks`: enemy AAMs at our aircraft degraded (rules.combat.escort_pct)
  strike_run with "incendiary": napalm, sets vehicles in each burst on fire
  tactical_nuke
              a blast of `blast_radius_m` / `blast_damage` at the aim point (GS-04); refused unless the grand-strategy
              escalation has released tactical weapons for the battle
  reveal      spots every enemy unit (or only aircraft, `air_only`) within `radius_m` for `duration_ticks`

Response delays are doctrinal times compressed about 2x for game pace (a US FA battalion mission from call to
fire-for-effect runs ~3 min with adjustment; MLRS ~6 min from a hide; pre-planned CAS check-in to time-on-target
3-5 min); each option records its reasoning. Costs, charges and recharges are design values tuned against the
economy (rules.economy: SP = 20% of a destroyed unit's card cost).
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

# id: (side, type, sp_cost, charges, recharge_s, delay_s, params, why)
OPTIONS = {
    # ---------------- NATO (US Army / USAF in Germany)
    "nato_fa_battalion_155": ("NATO", "barrage", 60, 2, 240, 60, {"weapon": "how_155", "rounds": 18, "range_m": 12000},
        "M109 battalion (3 batteries x 6 guns), one volley: 18 rounds. ~2 min call-to-FFE halved"),
    "nato_mlrs_strike": ("NATO", "barrage", 90, 1, 480, 90, {"weapon": "mlrs_m26", "rounds": 12, "range_m": 25000},
        "one M270 MLRS ripple of 12 M26 rockets from a hide; ~3 min halved"),
    "nato_smoke_155": ("NATO", "smoke", 20, 3, 120, 60, {"weapon": "how_155", "rounds": 8, "range_m": 12000},
        "M825 WP smoke screen from a 155 mm battery, 8 rounds"),
    "nato_illum_155": ("NATO", "illum", 10, 4, 60, 45, {"weapon": "how_155", "rounds": 2, "range_m": 12000},
        "M485 illumination, 2 rounds"),
    "nato_a10_strafe": ("NATO", "strike_run", 70, 2, 300, 90,
        {"impacts": 10, "blast_radius_m": 12, "blast_damage": 60, "suppression": 40, "length_m": 500, "heading_deg": 0, "interval_ticks": 3},
        "A-10A GAU-8 gun run on call (CAS from a holding point, ~3 min halved): 10 bursts along 500 m"),
    "nato_cbu87_cluster": ("NATO", "strike_run", 80, 1, 360, 100,
        {"impacts": 24, "blast_radius_m": 15, "blast_damage": 45, "suppression": 60, "length_m": 300, "heading_deg": 0, "interval_ticks": 1},
        "F-16C CBU-87 CEM (202 bomblets each) pattern: 24 submunition clusters over 300 m"),
    "nato_mk82_bombing": ("NATO", "strike_run", 60, 2, 300, 100,
        {"impacts": 6, "blast_radius_m": 40, "blast_damage": 150, "suppression": 80, "length_m": 240, "heading_deg": 0, "interval_ticks": 4},
        "F-16C level/dive attack with 6 Mk 82 (227 kg), stick of 6 over 240 m"),
    "nato_f4g_sead": ("NATO", "sead", 80, 1, 480, 90, {"radius_m": 3000},
        "F-4G Wild Weasel AGM-88 HARM shot at the nearest emitting SAM within 3 km"),
    "nato_rf4c_recon": ("NATO", "reveal", 30, 2, 240, 60, {"radius_m": 1500, "duration_ticks": 600, "air_only": False},
        "RF-4C photo/IR recon pass: everything within 1.5 km revealed for 30 s"),
    "nato_e3_awacs": ("NATO", "reveal", 40, 1, 600, 30, {"radius_m": 15000, "duration_ticks": 2400, "air_only": True},
        "E-3A AWACS on station: all aircraft within 15 km revealed for 2 min"),
    "nato_fascam_155": ("NATO", "minefield", 50, 2, 360, 90, {"radius_m": 150, "density": 30},
        "155 mm FASCAM (M741 RAAM anti-tank mines, fielded 1980s): a battery mission seeds ~300 m of road; ~3 min halved"),
    "nato_avlb_bridge": ("NATO", "bridge", 40, 2, 600, 150, {"length_m": 20, "width_m": 8, "heading_deg": 90},
        "M60 AVLB scissors bridge (18.3 m span), emplaced in ~3 min; ~5 min with travel, halved"),
    "nato_ace_fortify": ("NATO", "fortify", 30, 2, 600, 300, {"length_m": 100, "width_m": 8, "heading_deg": 0},
        "M9 ACE / engineer squad digs a 100 m section of fighting positions: hours of digging compressed to 5 min"),
    "nato_ace_obstacle": ("NATO", "obstacle", 30, 2, 600, 300, {"length_m": 100, "width_m": 32, "heading_deg": 90},
        "M9 ACE anti-tank ditch, 100 m: compressed to 5 min"),
    "nato_miclic_breach": ("NATO", "breach", 40, 2, 480, 60, {"length_m": 100, "width_m": 16, "heading_deg": 90},
        "M58 MICLIC line charge clears a ~100 x 8-16 m lane through mines and wire"),
    "nato_ef111_jamming": ("NATO", "jam_radar", 50, 1, 600, 60, {"radius_m": 3000, "duration_ticks": 2400},
        "EF-111A Raven (1981) stand-off ALQ-99 radar jamming: enemy gun-dish radars degraded within 3 km for 2 min"),
    "nato_compass_call": ("NATO", "jam_comms", 40, 1, 600, 60, {"radius_m": 4000, "duration_ticks": 2400},
        "EC-130H Compass Call (1983) command-net jamming: enemy fire-control nets within 4 km slowed for 2 min"),
    "nato_chaff_corridor": ("NATO", "decoy", 30, 2, 480, 30, {"radius_m": 2000, "duration_ticks": 1800},
        "chaff corridor and flare discipline for a strike package: enemy missiles at our aircraft within 2 km degraded for 90 s"),
    "nato_w79_tactical": ("NATO", "tactical_nuke", 200, 1, 99999, 120, {"blast_radius_m": 400, "blast_damage": 3000, "suppression": 200},
        "W79 8-inch enhanced-radiation shell (in service 1981) from an M110A2 battery: only when the grand-strategy escalation has released tactical weapons (GS-04); one round"),
    "nato_iowa_ngfs": ("NATO", "barrage", 120, 2, 600, 120, {"weapon": "naval_406", "rounds": 27, "range_m": 20000},
        "Iowa-class battleship naval gunfire support: three 9-gun 16 in broadsides from 20 km offshore; ~4 min call to splash halved"),
    "nato_copperhead": ("NATO", "precision", 60, 2, 360, 90, {"weapon": "how_155", "rounds": 4, "cep_m": 3, "designator_m": 3000},
        "M712 Copperhead laser-guided 155 mm (1982): needs a friendly unit lasing the target within 3 km with line of sight"),
    "nato_ac130_gunship": ("NATO", "gunship", 110, 1, 720, 120, {"radius_m": 800, "duration_ticks": 1800, "interval_ticks": 60, "blast_radius_m": 10, "blast_damage": 120, "suppression": 60},
        "AC-130H Spectre orbit (night attack, 105 mm and 40 mm): a hit every 3 s on the nearest visible enemy within 800 m for 90 s"),
    "nato_tomahawk": ("NATO", "cruise", 150, 1, 900, 240, {"blast_radius_m": 40, "blast_damage": 600, "suppression": 100},
        "BGM-109 Tomahawk land-attack cruise missile (1983) from a ship: a precise 450 kg warhead on the aim point after ~8 min flight, halved"),
    "nato_pioneer_uav": ("NATO", "reveal", 30, 2, 360, 60, {"radius_m": 1200, "duration_ticks": 3600, "air_only": False},
        "RQ-2 Pioneer reconnaissance drone (1986): everything within 1.2 km revealed for 3 min"),
    "nato_kh11_pass": ("NATO", "reveal", 40, 1, 2400, 600, {"radius_m": 6000, "duration_ticks": 400, "air_only": False},
        "KH-11 imaging satellite pass (1976): a wide area revealed for 20 s, tasking and downlink delay 30 s"),
    "nato_f15_escort": ("NATO", "escort", 50, 2, 480, 30, {"radius_m": 3000, "duration_ticks": 2400},
        "F-15C fighter escort for the strike package: enemy air-to-air missiles at our aircraft within 3 km degraded for 2 min"),
    "nato_air_assault": ("NATO", "air_landing", 90, 1, 900, 120, {"unit": "us_mech_rifle_squad", "count": 3, "scatter_m": 60},
        "UH-60 air assault: a rifle platoon (3 squads, modelled with the mech squad's infantry stats) landed at the LZ"),
    # ---------------- Warsaw Pact (GSFG)
    "pact_art_battalion_152": ("Warsaw Pact", "barrage", 55, 2, 240, 60, {"weapon": "how_152", "rounds": 18, "range_m": 12000},
        "2S3 battalion volley, 18 rounds"),
    "pact_grad_strike": ("Warsaw Pact", "barrage", 85, 1, 480, 90, {"weapon": "grad_9m22u", "rounds": 40, "range_m": 15000},
        "one BM-21 Grad salvo of 40 rockets"),
    "pact_smoke_152": ("Warsaw Pact", "smoke", 20, 3, 120, 60, {"weapon": "how_152", "rounds": 8, "range_m": 12000},
        "152 mm smoke screen, 8 rounds"),
    "pact_illum_152": ("Warsaw Pact", "illum", 10, 4, 60, 45, {"weapon": "how_152", "rounds": 2, "range_m": 12000},
        "152 mm illumination, 2 rounds"),
    "pact_su25_strafe": ("Warsaw Pact", "strike_run", 65, 2, 300, 90,
        {"impacts": 8, "blast_radius_m": 12, "blast_damage": 55, "suppression": 40, "length_m": 450, "heading_deg": 180, "interval_ticks": 3},
        "Su-25 GSh-30-2 gun and S-8 rocket run: 8 bursts along 450 m"),
    "pact_rbk500_cluster": ("Warsaw Pact", "strike_run", 75, 1, 360, 100,
        {"impacts": 22, "blast_radius_m": 15, "blast_damage": 45, "suppression": 60, "length_m": 300, "heading_deg": 180, "interval_ticks": 1},
        "Su-24M RBK-500 cluster pattern: 22 submunition clusters over 300 m"),
    "pact_fab500_bombing": ("Warsaw Pact", "strike_run", 60, 2, 300, 100,
        {"impacts": 4, "blast_radius_m": 55, "blast_damage": 180, "suppression": 90, "length_m": 200, "heading_deg": 180, "interval_ticks": 5},
        "Su-24M stick of 4 FAB-500 (500 kg) over 200 m"),
    "pact_su24_kh25mp_sead": ("Warsaw Pact", "sead", 80, 1, 480, 90, {"radius_m": 3000},
        "Su-24M Kh-25MP anti-radiation missile at the nearest emitting SAM within 3 km"),
    "pact_uragan_mines": ("Warsaw Pact", "minefield", 50, 2, 360, 90, {"radius_m": 150, "density": 30},
        "BM-27 Uragan 9M27K3 remote anti-tank mining rockets (PTM-1 mines): ~300 m minefield; ~3 min halved"),
    "pact_mtu20_bridge": ("Warsaw Pact", "bridge", 40, 2, 600, 150, {"length_m": 20, "width_m": 8, "heading_deg": 90},
        "MTU-20 tank-launched bridge (18 m span), ~3 min to emplace; halved with travel"),
    "pact_bat_fortify": ("Warsaw Pact", "fortify", 30, 2, 600, 300, {"length_m": 100, "width_m": 8, "heading_deg": 0},
        "PZM-2 / BAT-M trench digger: 100 m of trench compressed to 5 min"),
    "pact_bat_obstacle": ("Warsaw Pact", "obstacle", 30, 2, 600, 300, {"length_m": 100, "width_m": 32, "heading_deg": 90},
        "BAT-M anti-tank ditch, 100 m, compressed to 5 min"),
    "pact_ur77_breach": ("Warsaw Pact", "breach", 40, 2, 480, 60, {"length_m": 100, "width_m": 16, "heading_deg": 90},
        "UR-77 Meteorit line charge (1978): a ~90 x 6-14 m lane through a minefield"),
    "pact_mi8ppa_jamming": ("Warsaw Pact", "jam_radar", 50, 1, 600, 60, {"radius_m": 3000, "duration_ticks": 2400},
        "Mi-8PPA airborne jammer (1980s): enemy air-defence gun radars degraded within 3 km for 2 min"),
    "pact_r330_comms": ("Warsaw Pact", "jam_comms", 40, 1, 600, 60, {"radius_m": 4000, "duration_ticks": 2400},
        "R-330 series VHF comms jamming station: enemy fire-control nets within 4 km slowed for 2 min"),
    "pact_chaff_corridor": ("Warsaw Pact", "decoy", 30, 2, 480, 30, {"radius_m": 2000, "duration_ticks": 1800},
        "chaff corridor and flares for a strike group: enemy missiles at our aircraft within 2 km degraded for 90 s"),
    "pact_3bv3_tactical": ("Warsaw Pact", "tactical_nuke", 200, 1, 99999, 120, {"blast_radius_m": 400, "blast_damage": 3000, "suppression": 200},
        "152 mm 3BV3 nuclear shell from a 2S3 battery: only when the escalation has released tactical weapons (GS-04); one round"),
    "pact_sovremenny_ngfs": ("Warsaw Pact", "barrage", 100, 2, 600, 120, {"weapon": "naval_130", "rounds": 40, "range_m": 15000},
        "Sovremenny-class destroyer AK-130 fire mission from the Baltic, 40 rounds at 15 km; ~4 min halved"),
    "pact_krasnopol": ("Warsaw Pact", "precision", 60, 2, 360, 90, {"weapon": "how_152", "rounds": 4, "cep_m": 3, "designator_m": 3000},
        "Krasnopol laser-guided 152 mm (1985): needs a friendly unit lasing the target within 3 km with line of sight"),
    "pact_su25_napalm": ("Warsaw Pact", "strike_run", 70, 1, 360, 100,
        {"impacts": 4, "blast_radius_m": 30, "blast_damage": 60, "suppression": 90, "length_m": 200, "heading_deg": 180, "interval_ticks": 4, "incendiary": True},
        "Su-25 ZAB-500 incendiary run: 4 canisters over 200 m set vehicles in the burst on fire"),
    "pact_rk55_cruise": ("Warsaw Pact", "cruise", 150, 1, 900, 240, {"blast_radius_m": 40, "blast_damage": 600, "suppression": 100},
        "RK-55 Relief ground-launched cruise missile, conventional warhead: precise blast on the aim point"),
    "pact_tu143_uav": ("Warsaw Pact", "reveal", 30, 2, 360, 60, {"radius_m": 1200, "duration_ticks": 1200, "air_only": False},
        "Tu-143 Reys reconnaissance drone (1976): a 1.2 km strip revealed for 1 min"),
    "pact_mig29_escort": ("Warsaw Pact", "escort", 50, 2, 480, 30, {"radius_m": 3000, "duration_ticks": 2400},
        "MiG-29 escort for the strike group: enemy air-to-air missiles at our aircraft within 3 km degraded for 2 min"),
    "pact_vdv_paradrop": ("Warsaw Pact", "air_landing", 90, 1, 900, 150, {"unit": "sov_motor_rifle_squad", "count": 3, "scatter_m": 150},
        "VDV parachute landing: a platoon (3 squads, modelled with the motor rifle squad's infantry stats) dropped around the DZ"),
    "pact_su24mr_recon": ("Warsaw Pact", "reveal", 30, 2, 240, 60, {"radius_m": 1500, "duration_ticks": 600, "air_only": False},
        "Su-24MR reconnaissance pass: everything within 1.5 km revealed for 30 s"),
    "pact_a50_awacs": ("Warsaw Pact", "reveal", 40, 1, 600, 30, {"radius_m": 15000, "duration_ticks": 2400, "air_only": True},
        "A-50 Mainstay (in service from 1985): all aircraft within 15 km revealed for 2 min"),
}


# WW2 1944 (ERA-01 slice; SUP-01/SUP-02 per era): US Army and Wehrmacht off-map support.
OPTIONS_WW2 = {
    "allies_105_battalion": ("Allies", "barrage", 55, 2, 240, 90, {"weapon": "us_105mm_m2a1", "rounds": 12, "range_m": 8000},
        "a 105 mm M2A1 battalion volley (3 batteries x 4 guns) through the forward observer; ~3 min halved"),
    "allies_smoke_105": ("Allies", "smoke", 20, 3, 120, 90, {"weapon": "us_105mm_m2a1", "rounds": 8, "range_m": 8000},
        "105 mm WP smoke screen, 8 rounds"),
    "allies_p47_strafe": ("Allies", "strike_run", 65, 2, 300, 120,
        {"impacts": 8, "blast_radius_m": 12, "blast_damage": 55, "suppression": 50, "length_m": 400, "heading_deg": 0, "interval_ticks": 3},
        "P-47D cab-rank strafing run (8 x .50, HVARs) along 400 m; IX TAC response ~4 min halved"),
    "allies_f5_recon": ("Allies", "reveal", 30, 2, 360, 90, {"radius_m": 1500, "duration_ticks": 600, "air_only": False},
        "F-5 (P-38) photo-reconnaissance pass: 1.5 km revealed for 30 s"),
    "allies_waco_glider": ("Allies", "air_landing", 90, 1, 1800, 180, {"unit": "us_rifle_squad_1944", "count": 3, "scatter_m": 200},
        "Waco CG-4A glider landing: a rifle platoon (3 squads) scattered over the landing zone"),
    "allies_b26_level": ("Allies", "strike_run", 80, 1, 480, 180,
        {"impacts": 8, "blast_radius_m": 35, "blast_damage": 150, "suppression": 90, "length_m": 400, "heading_deg": 0, "interval_ticks": 2},
        "B-26 Marauder formation level bombing: 8 x 500 lb along 400 m; pre-planned ~6 min halved"),
    "allies_p47_napalm": ("Allies", "strike_run", 70, 1, 360, 120,
        {"impacts": 2, "blast_radius_m": 30, "blast_damage": 60, "suppression": 90, "length_m": 60, "heading_deg": 0, "interval_ticks": 4, "incendiary": True},
        "P-47 with two 110 gal napalm fire bombs (used from mid-1944)"),
    "allies_82nd_paradrop": ("Allies", "air_landing", 90, 1, 1800, 180, {"unit": "us_rifle_squad_1944", "count": 3, "scatter_m": 300},
        "C-47 parachute drop of an airborne rifle platoon (3 squads, rifle-squad stats), widely scattered"),
    "axis_fallschirmjaeger_drop": ("Axis", "air_landing", 90, 1, 1800, 180, {"unit": "ger_grenadier_squad_1944", "count": 3, "scatter_m": 300},
        "Ju 52 Fallschirmjäger drop (rare by 1944, e.g. Operation Stösser): 3 squads, grenadier-squad stats"),
    "axis_lefh_battalion": ("Axis", "barrage", 55, 2, 240, 90, {"weapon": "ger_105mm_lefh18", "rounds": 12, "range_m": 8000},
        "a leFH 18 Abteilung volley (3 batteries x 4 guns); ~3 min halved"),
    "axis_nebelwerfer_battery": ("Axis", "barrage", 60, 1, 480, 90, {"weapon": "ger_150mm_nebelwerfer", "rounds": 18, "range_m": 5000},
        "a Nebelwerfer 41 battery ripple: 3 launchers x 6 rockets"),
    "axis_ju87_dive": ("Axis", "strike_run", 70, 1, 360, 120,
        {"impacts": 1, "blast_radius_m": 45, "blast_damage": 300, "suppression": 100, "length_m": 0, "heading_deg": 180, "interval_ticks": 1},
        "Ju 87 dive-bombing attack: one SC 500 placed on the aim point"),
}


# Modern 2026 (ERA-01 slice; SUP-01 loitering munitions and FPV drones; SUP-04 GPS degradation).
OPTIONS_MODERN = {
    "us_excalibur": ("NATO", "precision", 60, 2, 360, 60, {"weapon": "m284_155_he", "rounds": 2, "cep_m": 4, "gps": True},
        "M982 Excalibur GPS-guided 155 mm: ~4 m CEP, no designator; spoiled by GPS jamming (Wikipedia M982 Excalibur)"),
    "us_gmlrs": ("NATO", "precision", 90, 2, 600, 90, {"weapon": "gmlrs_m31", "rounds": 2, "cep_m": 2, "gps": True},
        "two GMLRS M31 unitary rockets from a HIMARS, GPS-guided (~1 m; 2 m used)"),
    "us_switchblade600": ("NATO", "loitering", 60, 2, 480, 60, {"radius_m": 3000, "strikes": 1, "interval_ticks": 1, "pen": 760, "blast_damage": 130, "suppression": 50},
        "Switchblade 600: 40 km / 40 min, Javelin-derived anti-armour warhead, top attack on the most valuable vehicle in view"),
    "us_m1_ec_gps_jam": ("NATO", "jam_gps", 40, 1, 900, 30, {"radius_m": 6000, "duration_ticks": 4800},
        "US EW detachment GPS/GLONASS denial over 6 km for 4 min (design)"),
    "us_tomahawk_block5": ("NATO", "cruise", 150, 1, 900, 240, {"blast_radius_m": 40, "blast_damage": 600, "suppression": 100},
        "Tomahawk Block V land-attack cruise missile from a destroyer: precise 450 kg warhead"),
    "us_f35_sead": ("NATO", "sead", 80, 1, 480, 90, {"radius_m": 6000},
        "F-35A suppression of enemy air defences: AARGM/GBU-53 against the nearest radar-directed air defence within 6 km"),
    "us_sat_pass": ("NATO", "reveal", 40, 1, 2400, 300, {"radius_m": 8000, "duration_ticks": 400, "air_only": False},
        "commercial/military imaging satellite pass with rapid tasking: wide-area reveal for 20 s"),
    "ru_kalibr_cruise": ("Russia", "cruise", 150, 1, 900, 240, {"blast_radius_m": 40, "blast_damage": 600, "suppression": 100},
        "3M14 Kalibr land-attack cruise missile: precise 450 kg warhead"),
    "ru_orlan10_recon": ("Russia", "reveal", 25, 3, 300, 30, {"radius_m": 2500, "duration_ticks": 3600, "air_only": False},
        "Orlan-10 reconnaissance drone over the front: 2.5 km revealed for 3 min (spotting for artillery)"),
    "us_e3g_awacs": ("NATO", "reveal", 40, 1, 600, 30, {"radius_m": 20000, "duration_ticks": 2400, "air_only": True},
        "E-3G AWACS on station: all aircraft within 20 km revealed for 2 min"),
    "us_f22_escort": ("NATO", "escort", 50, 2, 480, 30, {"radius_m": 4000, "duration_ticks": 2400},
        "F-22A escort for the strike package: enemy air-to-air missiles at our aircraft within 4 km degraded"),
    "us_ac130j_gunship": ("NATO", "gunship", 110, 1, 720, 120, {"radius_m": 1000, "duration_ticks": 1800, "interval_ticks": 60, "blast_radius_m": 10, "blast_damage": 130, "suppression": 60},
        "AC-130J Ghostrider orbit (105 mm, 30 mm, precision munitions): a hit every 3 s on the nearest visible enemy within 1 km for 90 s"),
    "us_uh60m_air_assault": ("NATO", "air_landing", 90, 1, 900, 120, {"unit": "us_infantry_squad_2026", "count": 3, "scatter_m": 60},
        "UH-60M air assault: an infantry platoon (3 squads) landed at the LZ"),
    "ru_a50u_awacs": ("Russia", "reveal", 40, 1, 600, 30, {"radius_m": 20000, "duration_ticks": 2400, "air_only": True},
        "A-50U Mainstay on station: all aircraft within 20 km revealed for 2 min"),
    "ru_su35_escort": ("Russia", "escort", 50, 2, 480, 30, {"radius_m": 4000, "duration_ticks": 2400},
        "Su-35S escort for the strike group"),
    "ru_mi8_air_assault": ("Russia", "air_landing", 90, 1, 900, 120, {"unit": "rus_motor_rifle_squad_2026", "count": 3, "scatter_m": 60},
        "Mi-8AMTSh air assault: a platoon (3 sections) landed at the LZ"),
    "ru_krasnopol_m2": ("Russia", "precision", 60, 2, 360, 90, {"weapon": "2a64_152_he", "rounds": 2, "cep_m": 3, "designator_m": 7000},
        "Krasnopol-M2 laser-guided 152 mm: needs a laser designator (drone or observer) within 7 km"),
    "ru_tornado_s_strike": ("Russia", "precision", 90, 2, 600, 90, {"weapon": "tornado_s_300", "rounds": 2, "cep_m": 10, "gps": True},
        "two 9M542 GLONASS-guided 300 mm rockets (Tornado-S, 120 km)"),
    "ru_lancet3": ("Russia", "loitering", 50, 3, 360, 90, {"radius_m": 3000, "strikes": 1, "interval_ticks": 1, "pen": 300, "blast_damage": 90, "suppression": 40},
        "ZALA Lancet-3: 40 km / 40 min, 3 kg shaped charge (pen design), TV terminal guidance, top attack"),
    "ru_fpv_strikes": ("Russia", "fpv", 30, 3, 240, 40, {"radius_m": 2500, "strikes": 4, "interval_ticks": 40, "pen": 450, "blast_damage": 70, "suppression": 40},
        "an FPV drone section: four strikes with RPG-7 warheads (<$1,000 each, 10-20 km) on vehicles in view; jamming cuts their success"),
    "ru_pole21_gps_jam": ("Russia", "jam_gps", 40, 1, 900, 30, {"radius_m": 8000, "duration_ticks": 4800},
        "Pole-21 GPS/GLONASS jamming over 8 km for 4 min"),
    "ru_r330zh_comms_jam": ("Russia", "jam_comms", 40, 1, 600, 60, {"radius_m": 5000, "duration_ticks": 2400},
        "R-330Zh Zhitel comms/drone-link jamming over 5 km"),
}


def build() -> dict:
    out = []
    items = [("coldwar", k, v) for k, v in OPTIONS.items()] + [("ww2", k, v) for k, v in OPTIONS_WW2.items()] + [("modern", k, v) for k, v in OPTIONS_MODERN.items()]
    for era, oid, (side, kind, cost, charges, recharge, delay, params, why) in items:
        out.append({
            "id": oid, "era": era, "side": side, "type": kind, "sp_cost": cost, "charges": charges,
            "recharge_ticks": recharge * 20, "delay_ticks": delay * 20, "params": params, "why": why,
        })
    return {"version": 1, "options": out}


def main() -> int:
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "data" / "support.json"
    weapons = {w["id"] for w in json.loads((ROOT / "data" / "weapons.json").read_text())["weapons"]}
    data = build()
    for o in data["options"]:
        w = o["params"].get("weapon")
        if w is not None and w not in weapons:
            raise SystemExit(f"{o['id']}: unknown weapon {w}")
    out.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    print(f"wrote {out} ({len(data['options'])} options)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
