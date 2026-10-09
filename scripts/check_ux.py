#!/usr/bin/env python3
"""G13 UX smoke: run the exported Linux build headless on every recorded input script in verify/ux/ and check the
game-state events it logs (D060).

    python3 scripts/check_ux.py                 all scripts, after a self-test of the matcher
    python3 scripts/check_ux.py --binary PATH   another build (default build/linux/SixActual.x86_64)
    python3 scripts/check_ux.py ux_03_campaign  only scripts whose id starts with the given prefixes
    python3 scripts/check_ux.py --dir DIR       scripts from another directory (drafting new scripts)

Scripts run in file-name order and share one user data dir (XDG_DATA_HOME under build/ux/), wiped once at the start, so
a later script can check what an earlier one saved (settings persistence). A script passes when the build exits 0,
logs "ux_done", and its "expect" list matches the log in order. Expectation keys:
  event          event name                        fields      exact values
  min_count      at least n such events            nonempty    these fields non-empty in every such event
  range          {field: [lo, hi]} inclusive        equal_fields [[a, b], ...]: field a equals field b
  same_as        {field: event}: equals that field of the latest earlier such event
  forbid         {field: value} must not equal       absent      {"absent": name}: no such event anywhere
  array_len      {field: n} list length            positive    these fields > 0
  tick_within    {"of": event, "max": n}: tick minus the tick of the latest earlier `of` event is at most n
"""
from __future__ import annotations

import json
import os
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SCRIPTS = ROOT / "verify" / "ux"
OUT = ROOT / "build" / "ux"


def matches(ev: dict, exp: dict, log: list[dict], at: int) -> bool:
    if ev.get("event") != exp["event"]:
        return False
    for k, v in exp.get("fields", {}).items():
        if ev.get(k) != v:
            return False
    for k, v in exp.get("forbid", {}).items():
        if k not in ev or ev[k] == v:
            return False
    for k in exp.get("nonempty", []):
        if not ev.get(k):
            return False
    for k, n in exp.get("array_len", {}).items():
        if not isinstance(ev.get(k), list) or len(ev[k]) != n:
            return False
    for k, (lo, hi) in exp.get("range", {}).items():
        if not isinstance(ev.get(k), (int, float)) or not lo <= ev[k] <= hi:
            return False
    for k, other in exp.get("same_as", {}).items():
        prior = [e for e in log[:at] if e.get("event") == other]
        if not prior or k not in ev or k not in prior[-1] or ev[k] != prior[-1][k]:
            return False
    for k1, k2 in exp.get("equal_fields", []):
        if k1 not in ev or k2 not in ev or ev[k1] != ev[k2]:
            return False
    for k in exp.get("positive", []):
        if not isinstance(ev.get(k), (int, float)) or ev[k] <= 0:
            return False
    tw = exp.get("tick_within")
    if tw:
        prior = [e for e in log[:at] if e.get("event") == tw["of"]]
        if not prior or "tick" not in ev or ev["tick"] - prior[-1].get("tick", -10**9) > tw["max"]:
            return False
    return True


def check(log: list[dict], expect: list[dict]) -> list[str]:
    """Ordered match: each expectation is found after the previous one's position."""
    problems = []
    pos = 0
    for exp in expect:
        if "absent" in exp:
            hits = [e for e in log if e.get("event") == exp["absent"]]
            if hits:
                problems.append(f"{exp['absent']}: expected no such event, found {len(hits)}")
            continue
        if "min_count" in exp:
            hits = [i for i, e in enumerate(log) if i >= pos and matches(e, exp, log, i)]
            if len(hits) < exp["min_count"]:
                problems.append(f"{exp['event']}: {len(hits)} matching events after position {pos}, need {exp['min_count']}")
                continue
            # every event of that name must satisfy the field rules, not just enough of them
            bad = [e for e in log if e.get("event") == exp["event"] and not matches(e, exp, log, log.index(e))]
            if bad:
                problems.append(f"{exp['event']}: {len(bad)} events fail {exp}")
            pos = hits[0] + 1 if hits else pos  # later expectations may match among these events too
            continue
        found = next((i for i in range(pos, len(log)) if matches(log[i], exp, log, i)), None)
        if found is None:
            problems.append(f"missing after position {pos}: {json.dumps(exp)}")
        else:
            pos = found + 1
    if not log or log[-1].get("event") != "ux_done":
        problems.append(f"script did not finish (last event {log[-1] if log else None})")
    return problems


def self_test() -> None:
    log = [{"event": "a", "tick": 1}, {"event": "b", "x": 1, "tick": 2, "l": [1, 2]}, {"event": "c", "tick": 9}, {"event": "ux_done"}]
    good = [{"event": "a"}, {"event": "b", "fields": {"x": 1}, "array_len": {"l": 2}, "tick_within": {"of": "a", "max": 1}}, {"event": "c"}]
    if check(log, good):
        raise SystemExit(f"check_ux self-test: a matching log failed: {check(log, good)}")
    broken = {
        "order": [{"event": "c"}, {"event": "a"}],
        "field": [{"event": "b", "fields": {"x": 2}}],
        "tick": [{"event": "c", "tick_within": {"of": "b", "max": 1}}],
        "array": [{"event": "b", "array_len": {"l": 3}}],
        "count": [{"event": "a", "min_count": 2}],
        "positive": [{"event": "b", "positive": ["tick", "zzz"]}],
        "forbid": [{"event": "b", "forbid": {"x": 1}}],
        "absent": [{"absent": "c"}],
        "forbid on a missing field": [{"event": "a", "forbid": {"nope": 1}}],
        "same_as on missing fields": [{"event": "c", "same_as": {"nope": "a"}}],
        "equal_fields on missing fields": [{"event": "b", "equal_fields": [["nope", "nada"]]}],
        "nonempty": [{"event": "a", "nonempty": ["x"]}],
        "min_count count": [{"event": "a", "min_count": 2}],
        "range": [{"event": "b", "range": {"x": [2, 3]}}],
        "equal_fields": [{"event": "b", "equal_fields": [["x", "tick"]]}],
        "same_as": [{"event": "c", "same_as": {"tick": "a"}}],
    }
    for name, exp in broken.items():
        if not check(log, exp):
            raise SystemExit(f"check_ux self-test: '{name}' was not detected")
    # min_count applies its rules to every event of that name, not just enough of them
    mixed = [{"event": "b", "x": 1}, {"event": "b", "x": 2}, {"event": "ux_done"}]
    if not check(mixed, [{"event": "b", "min_count": 1, "fields": {"x": 1}}]):
        raise SystemExit("check_ux self-test: a min_count event that breaks its rules was not detected")
    if not check(log[:-1], [{"event": "a"}]):
        raise SystemExit("check_ux self-test: an unfinished script was not detected")


def main() -> int:
    args = sys.argv[1:]
    binary = ROOT / "build" / "linux" / "SixActual.x86_64"
    if "--binary" in args:
        i = args.index("--binary")
        binary = Path(args[i + 1])
        del args[i:i + 2]
    scripts_dir = SCRIPTS
    if "--dir" in args:
        i = args.index("--dir")
        scripts_dir = Path(args[i + 1])
        del args[i:i + 2]
    self_test()
    if not binary.exists():
        print(f"missing build {binary}; run the G1 export first")
        return 1
    scripts = sorted(scripts_dir.glob("*.json"))
    if args:
        scripts = [s for s in scripts if any(s.stem.startswith(a) for a in args)]
    if not scripts:
        print("no UX scripts")
        return 1
    xdg = OUT / "xdg"
    shutil.rmtree(xdg, ignore_errors=True)
    xdg.mkdir(parents=True)
    env = dict(os.environ, XDG_DATA_HOME=str(xdg))
    failed = 0
    for path in scripts:
        spec = json.loads(path.read_text())
        log_path = OUT / f"{spec['id']}.jsonl"
        try:
            r = subprocess.run([str(binary), "--headless", "--", "--ux", str(path), "--ux-log", str(log_path)],
                               cwd=ROOT, env=env, capture_output=True, text=True, timeout=spec.get("timeout_s", 900))
            rc, tail = r.returncode, (r.stdout + r.stderr)[-1500:]
        except subprocess.TimeoutExpired:
            rc, tail = -1, "timeout"
        log = [json.loads(line) for line in log_path.read_text().splitlines() if line.strip()] if log_path.exists() else []
        problems = check(log, spec["expect"])
        if rc != 0:
            problems.insert(0, f"exit code {rc}")
        # An engine error or unhandled C# exception fails the script even when the expected events appeared.
        errors = [ln for ln in tail.splitlines() if ln.startswith("ERROR:") or "Unhandled exception" in ln or "Exception:" in ln] if rc != -1 else []
        if rc == 0:
            full = r.stdout + r.stderr
            errors = [ln for ln in full.splitlines() if ln.startswith("ERROR:") or "Unhandled exception" in ln or ("Exception" in ln and " at " not in ln and "ux " not in ln)]
        if errors:
            problems.append(f"engine errors in the build output: {errors[:3]}")
        reqs = ",".join([spec["requirement"], *spec.get("also", [])])
        if problems:
            failed += 1
            print(f"FAIL {spec['id']} [{reqs}]")
            for p in problems:
                print(f"    {p}")
            print("    --- build output tail ---\n    " + tail.replace("\n", "\n    "))
        else:
            print(f"pass {spec['id']} [{reqs}] :: {len(log)} events, {len(spec['expect'])} expectations matched in order")
    print(f"ux scripts={len(scripts)} failed={failed}")
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
