#!/usr/bin/env python3
"""Gate G5: AI-vs-AI soak (QA-03, CMD-06, SIM-12 stuck metric, MEC-09, MEC-10, GS-05, MEC-13) against verify/thresholds.toml [ai].

Runs `sim/Headless --soak`: 200 cross-faction matches, 100 NATO mirror and 100 Pact mirror (400 total; QA-03 needs >= 200), each to a
result under the rules' time cap. MEC-09 adds single-class runs: player 0 fields a deck of one sim kind (tank, ifv,
infantry; built from its faction's soak deck at equal total card value) against that faction's combined-arms deck,
in both factions' mirrors. GS-05 / MEC-13: an AI-vs-AI grand-strategy run per seed and era (data/theaters.json) must reach an
end state without crashing, and the AI must reinforce attacked provinces within 2 turns when a reserve is in reach.
Writes CSVs to build/soak/ and fails on any crash or missed threshold.
Usage: scripts/analyze_soak.py [--quick]   (--quick: 24 matches, for iteration; the gate uses the full run)
"""
from __future__ import annotations

import csv
import json
import io
import statistics
import subprocess
import sys
import tomllib
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SINGLE_CLASSES = ("tank", "ifv", "infantry")
AA_MIN_DEPLOYED_VALUE = 8000  # about 100 AA cards over the whole battery; below that the MEC-09 AA check is vacuous
CMD = ["dotnet", "run", "--no-build", "--project", "sim/Headless/SixActual.Headless.csproj", "-c", "Release", "--", "--soak"]


def soak(n: int, start: int, extra: list[str]) -> list[dict]:
    out = subprocess.run(CMD + [str(n), "--start", str(start), "--cap", "48000"] + extra, cwd=ROOT, capture_output=True, text=True, check=True).stdout
    return list(csv.DictReader(io.StringIO(out)))


def wilson(k: int, n: int, z: float = 1.959964) -> tuple[float, float]:
    if n == 0:
        return 0.0, 1.0
    p = k / n
    d = 1 + z * z / n
    c = p + z * z / (2 * n)
    m = z * ((p * (1 - p) / n + z * z / (4 * n * n)) ** 0.5)
    return (c - m) / d, (c + m) / d


def main() -> int:
    quick = "--quick" in sys.argv
    th = tomllib.loads((ROOT / "verify" / "thresholds.toml").read_text())["ai"]
    subprocess.run(["dotnet", "build", "sim/Headless/SixActual.Headless.csproj", "-c", "Release", "-warnaserror", "-v", "q"], cwd=ROOT, check=True, capture_output=True)
    n_cross, n_mirror = (12, 6) if quick else (200, 100)
    n_single = 6 if quick else 40
    runs = {
        "cross": soak(n_cross, 1, []),
        "mirror_nato": soak(n_mirror, 10001, ["--mirror", "nato"]),
        "mirror_pact": soak(n_mirror, 20001, ["--mirror", "pact"]),
    }
    # MEC-09 air war: both sides on their maverick (air) decks, so air defence has aircraft to shoot at.
    # 3x n_single: with matches ending sooner (D073) the AA cost check needs more matches for its 8000-point sample.
    air = soak(3 * n_single, 40001, ["--decks", "us_1989_air_cavalry,su_1989_air_assault"])
    singles = {"air_war": air}
    for kind in SINGLE_CLASSES:
        for side in ("nato", "pact"):
            singles[f"single_{kind}_{side}"] = soak(n_single, 30001, ["--mirror", side, "--single", kind])
    out_dir = ROOT / "build" / "soak"
    out_dir.mkdir(parents=True, exist_ok=True)
    for name, rows in {**runs, **singles}.items():
        with open(out_dir / f"{name}.csv", "w", newline="") as f:
            w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
            w.writeheader()
            w.writerows(rows)
    allrows = [r for rows in runs.values() for r in rows]
    bad = []

    def check(name: str, ok: bool, detail: str) -> None:
        print(f"{'pass' if ok else 'FAIL'} {name}: {detail}")
        if not ok:
            bad.append(name)

    crashed = sum(int(r["crashed"]) for r in allrows)
    check("crash_free (QA-03)", crashed == 0, f"{crashed} crashed of {len(allrows)}")
    # BRIEF G5: matches end before the time cap. At the cap the rules still award the match on score, so a capped
    # match has a winner; it is the tick count that shows it ran out of time (D073 review).
    cap = json.loads((ROOT / "data" / "rules.json").read_text())["victory"]["time_cap_ticks"]
    unfinished = sum(1 for r in allrows if int(r["winner"]) < 0 or int(r["ticks"]) >= cap)
    check("matches end before the time cap", unfinished == 0, f"{unfinished} of {len(allrows)} ran to the {cap}-tick cap or had no result")
    stuck = sum(int(r["stuck"]) for r in allrows)
    moving = sum(int(r["moving_units"]) for r in allrows)
    unstuck = 1 - stuck / max(1, moving)
    check("unstuck (SIM-12)", unstuck >= th["unstuck_min"], f"{unstuck:.4f} (stuck {stuck} of {moving} units that moved), need >= {th['unstuck_min']}")
    af = sum(int(r["assaults_fires"]) for r in allrows)
    an = sum(int(r["assaults_nofires"]) for r in allrows)
    share = af / max(1, af + an)
    check("fire support before assault (CMD-06)", share >= th["fire_support_before_assault_min"], f"{share:.3f} ({af}/{af + an}), need >= {th['fire_support_before_assault_min']}")
    com = sum(int(r["committed"]) for r in allrows)
    avail = sum(int(r["available"]) for r in allrows)
    committed = com / max(1, avail)
    check("force committed per attack (CMD-06)", committed >= th["force_committed_min"], f"{committed:.3f}, need >= {th['force_committed_min']}")
    dam = sum(int(r["damaged"]) for r in allrows)
    wd = sum(int(r["withdrawn"]) for r in allrows)
    wrate = wd / max(1, dam)
    check("damaged-unit withdrawal (CMD-06)", wrate >= th["damaged_withdrawal_min"], f"{wrate:.3f} ({wd}/{dam}), need >= {th['damaged_withdrawal_min']}")
    banks = [int(r[k]) for r in allrows for k in ("bank_pct_a", "bank_pct_b", "bank_pct_c") if r[k] != ""]
    med = statistics.median(banks) if banks else 0
    check("unspent bank median (MEC-10)", med < 30, f"{med}% of phase income, need < 30%")
    for name in ("mirror_nato", "mirror_pact"):
        rows = [r for r in runs[name] if int(r["winner"]) in (0, 1)]
        k = sum(1 for r in rows if int(r["winner"]) == 0)
        rate = k / max(1, len(rows))
        check(f"{name} win rate", th["mirror_win_min"] <= rate <= th["mirror_win_max"], f"player 0 {rate:.3f} ({k}/{len(rows)}), band {th['mirror_win_min']}-{th['mirror_win_max']}")
    rows = [r for r in runs["cross"] if int(r["winner"]) in (0, 1)]
    k = sum(1 for r in rows if int(r["winner"]) == 0)
    rate = k / max(1, len(rows))
    lo, hi = wilson(k, len(rows))
    check("cross-faction win rate (NATO)", th["cross_win_min"] <= rate <= th["cross_win_max"], f"{rate:.3f} ({k}/{len(rows)}, ci95 {lo:.2f}-{hi:.2f}), band {th['cross_win_min']}-{th['cross_win_max']}")
    # MEC-09: per AA card (over every run), aircraft card value destroyed per card value deployed.
    deployed: dict[str, int] = {}
    killed: dict[str, int] = {}
    for r in allrows + [r for rows in singles.values() for r in rows]:
        for item in filter(None, r["aa"].split("|")):
            uid, dep, kil = item.split(":")
            deployed[uid] = deployed.get(uid, 0) + int(dep)
            killed[uid] = killed.get(uid, 0) + int(kil)
    ratios = {u: killed[u] / deployed[u] for u in deployed if deployed[u] > 0}
    worst = max(ratios, key=ratios.get) if ratios else "-"
    detail = ", ".join(f"{u} {ratios[u]:.2f}" for u in sorted(ratios))
    # A check with no AA in play proves nothing: require a real sample of AA cards deployed (by value, 100 cards' worth).
    sample = sum(deployed.values())
    enough = sample >= AA_MIN_DEPLOYED_VALUE
    check("AA cost efficiency (MEC-09)", enough and all(v <= th["aa_air_value_ratio_max"] for v in ratios.values()), f"worst {worst} {ratios.get(worst, 0):.2f}, max {th['aa_air_value_ratio_max']}; AA value deployed {sample}, need >= {AA_MIN_DEPLOYED_VALUE} ({detail})")
    crashed_air = sum(int(r["crashed"]) for r in air)
    check("air-war run crash-free (MEC-09)", crashed_air == 0, f"{crashed_air} crashed of {len(air)}; aircraft deployed {sum(int(r['air0']) + int(r['air1']) for r in air)}")
    for kind in SINGLE_CLASSES:
        rows = [r for side in ("nato", "pact") for r in singles[f"single_{kind}_{side}"] if int(r["winner"]) in (0, 1)]
        k = sum(1 for r in rows if int(r["winner"]) == 0)
        rate = k / max(1, len(rows))
        crashed_s = sum(int(r["crashed"]) for side in ("nato", "pact") for r in singles[f"single_{kind}_{side}"])
        check(f"single-class {kind} vs combined arms (MEC-09)", rate <= th["single_class_win_max"] and crashed_s == 0, f"{rate:.3f} ({k}/{len(rows)}, {crashed_s} crashed), max {th['single_class_win_max']}")
    # MODE-01: 5v5 AI skirmishes on the shared skirmish setup: crash-free, decided, and every one of the 10 players deploys.
    teams_n = 4 if quick else 20
    teams = soak(teams_n, 50001, ["--teams", "5"])
    with open(out_dir / "teams_5v5.csv", "w", newline="") as fh:
        wr = csv.DictWriter(fh, fieldnames=list(teams[0].keys()))
        wr.writeheader()
        wr.writerows(teams)
    t_crash = sum(int(r["crashed"]) for r in teams)
    t_open = sum(1 for r in teams if int(r["winner"]) < 0 or int(r["ticks"]) >= cap)
    t_ff = sum(int(r["teammate_fire"]) for r in teams)
    t_idle = sum(1 for r in teams if int(r["min_deployed"]) < 1 or int(r["players"]) != 10)
    check("5v5 skirmish (MODE-01)", t_crash == 0 and t_open == 0 and t_idle == 0 and t_ff == 0,
          f"{len(teams)} matches: {t_crash} crashed, {t_open} reached the cap, {t_idle} with a player who never deployed, {t_ff} shots at teammates; fewest units by one player {min(int(r['min_deployed']) for r in teams)}")
    # GS-05 / MEC-13: grand strategy, AI vs AI, every era.
    gs_n = 20 if quick else 100
    gs_rows = []
    for era in ("ww2", "coldwar", "modern"):
        out = subprocess.run(["dotnet", "run", "--no-build", "--project", "sim/Headless/SixActual.Headless.csproj", "-c", "Release", "--", "--gs-soak", str(gs_n), "--era", era],
                             cwd=ROOT, capture_output=True, text=True, check=True).stdout
        rows = list(csv.DictReader(io.StringIO(out)))
        gs_rows += rows
        with open(out_dir / f"gs_{era}.csv", "w", newline="") as fh:
            wr = csv.DictWriter(fh, fieldnames=list(rows[0].keys()))
            wr.writeheader()
            wr.writerows(rows)
        crashed_gs = sum(int(r["crashed"]) for r in rows)
        ended = sum(1 for r in rows if int(r["winner"]) != -1)
        winners = {w: sum(1 for r in rows if r["winner"] == w) for w in sorted({r["winner"] for r in rows})}
        check(f"grand strategy {era} reaches an end state (GS-05)", crashed_gs == 0 and ended == len(rows), f"{ended}/{len(rows)} ended, {crashed_gs} crashed; outcomes {winners} (100 stalemate, 101 exchange)")
        ir = sum(int(r["attacks_in_reach"]) for r in rows)
        an = sum(int(r["answered"]) for r in rows)
        print(f"info: {era} reinforcement {an}/{ir}")
    ir = sum(int(r["attacks_in_reach"]) for r in gs_rows)
    an = sum(int(r["answered"]) for r in gs_rows)
    rate = an / max(1, ir)
    check("GS AI reinforces attacked provinces within 2 turns (MEC-13)", rate >= th["gs_reinforce_min"] and ir >= 50, f"{rate:.3f} ({an}/{ir} attacks the province survived with a reserve in reach), need >= {th['gs_reinforce_min']} over >= 50 cases")
    kills = statistics.mean(int(r["kills0"]) + int(r["kills1"]) for r in allrows)
    print(f"info: mean kills per match {kills:.1f}; matches {len(allrows)}; csv in build/soak/")
    return 1 if bad else 0


if __name__ == "__main__":
    raise SystemExit(main())
