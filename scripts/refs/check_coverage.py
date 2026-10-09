#!/usr/bin/env python3
"""Gate G14: reference coverage (contract section R5).

For every item in assets_src/registry.json past the `refs` stage:
  - every slot in verify/ref_coverage.toml for its class has enough tagged refs,
  - every number in its spec (data/assets/<id>.json "numbers") has a source,
  - every distributable ref is named in ASSETS.md,
  - every look-only ref lives under refs_private/.
For the whole tree: no look-only file or `refs_private` path appears under
game/, assets_src/**/exports/ or build/reports/.

`--selftest` builds synthetic trees and requires each bad one to fail for its
own reason, so this gate cannot pass by checking nothing.
"""
from __future__ import annotations

import hashlib
import json
import shutil
import sys
import tempfile
import tomllib
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SHIP_DIRS = ["game", "build/reports"]
TEXT_SUFFIXES = {".md", ".html", ".json", ".tscn", ".tres", ".cfg", ".cs", ".txt", ".csv", ".gd", ".godot", ".py"}


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def ship_files(root: Path):
    for d in SHIP_DIRS:
        base = root / d
        if base.exists():
            yield from (p for p in base.rglob("*") if p.is_file() and ".godot" not in p.parts)
    src = root / "assets_src"
    if src.exists():
        for exports in src.rglob("exports"):
            if exports.is_dir():
                yield from (p for p in exports.rglob("*") if p.is_file())


def check(root: Path, checklist_path: Path) -> list[str]:
    problems: list[str] = []
    cfg = tomllib.loads(checklist_path.read_text(encoding="utf-8"))
    before = set(cfg["stages"]["before_production"])
    classes = cfg["class"]
    registry_path = root / "assets_src" / "registry.json"
    items = json.loads(registry_path.read_text(encoding="utf-8"))["assets"] if registry_path.exists() else []
    assets_md = (root / "ASSETS.md").read_text(encoding="utf-8") if (root / "ASSETS.md").exists() else ""

    in_production = 0
    for item in items:
        iid, cls, stage = item["id"], item["class"], item["state"]
        if stage in before:
            continue
        in_production += 1
        if cls not in classes:
            problems.append(f"{iid}: class {cls} has no checklist")
            continue
        manifest = root / "assets_src" / "refs" / iid / "refs.json"
        refs = json.loads(manifest.read_text(encoding="utf-8"))["refs"] if manifest.exists() else []
        counts: dict[str, int] = {}
        for ref in refs:
            for slot in ref.get("slots", []):
                counts[slot] = counts.get(slot, 0) + 1
            kind = ref.get("kind")
            rel = ref.get("file", "")
            if kind == "distributable":
                if not ref.get("license") or rel not in assets_md:
                    problems.append(f"{iid}: distributable ref {rel} has no license entry in ASSETS.md")
            elif kind == "look-only":
                if not rel.startswith("refs_private/"):
                    problems.append(f"{iid}: look-only ref {rel} is outside refs_private/")
            else:
                problems.append(f"{iid}: ref {rel} has kind {kind!r}")
        for slot, need in classes[cls].items():
            if counts.get(slot, 0) < need:
                problems.append(f"{iid}: slot {slot} has {counts.get(slot, 0)} of {need}")
        spec = root / "data" / "assets" / f"{iid}.json"
        if not spec.exists():
            problems.append(f"{iid}: no spec at data/assets/{iid}.json")
        else:
            for num in json.loads(spec.read_text(encoding="utf-8")).get("numbers", []):
                if not str(num.get("source", "")).strip():
                    problems.append(f"{iid}: spec number {num.get('name')} has no source")

    private = root / "refs_private"
    private_files = [p for p in private.rglob("*") if p.is_file()] if private.exists() else []
    private_hashes = {sha(p) for p in private_files}
    private_paths = {p.relative_to(root).as_posix() for p in private_files}
    scanned = 0
    for path in ship_files(root):
        scanned += 1
        rel = path.relative_to(root).as_posix()
        if path.suffix.lower() in TEXT_SUFFIXES and path.stat().st_size < 50_000_000:
            text = path.read_text(encoding="utf-8", errors="ignore")
            if any(pp in text for pp in private_paths):
                problems.append(f"leak: {rel} names a look-only file")
        if private_hashes and sha(path) in private_hashes:
            problems.append(f"leak: {rel} is a copy of a look-only file")

    print(f"G14: {len(items)} registry items, {in_production} in production, {scanned} shippable files scanned")
    return problems


def selftest(checklist_path: Path) -> int:
    """Each bad tree must fail with its expected message; the good tree must pass."""
    cfg = tomllib.loads(checklist_path.read_text(encoding="utf-8"))
    slots = cfg["class"]["sound"]

    def build(tmp: Path, *, drop_slot=False, no_source=False, no_license=False, leak=False, leak_path=False, misplaced=False):
        (tmp / "assets_src" / "refs" / "snd").mkdir(parents=True)
        (tmp / "data" / "assets").mkdir(parents=True)
        (tmp / "refs_private" / "snd").mkdir(parents=True)
        (tmp / "game").mkdir()
        (tmp / "assets_src" / "registry.json").write_text(json.dumps(
            {"assets": [{"id": "snd", "class": "sound", "state": "detail"}]}))
        refs = []
        for i, (slot, need) in enumerate(slots.items()):
            if drop_slot and i == 0:
                continue
            for n in range(need):
                f = f"refs_private/snd/{slot}{n}.txt"
                (tmp / f).write_text(f"look-only {slot} {n}")
                refs.append({"file": f, "slots": [slot], "kind": "look-only"})
        dist = "assets_src/refs/snd/cc0.wav"
        (tmp / dist).write_text("cc0 bytes")
        refs.append({"file": dist, "slots": [], "kind": "distributable", "license": "CC0"})
        if misplaced:
            refs.append({"file": "assets_src/refs/snd/peek.txt", "slots": [], "kind": "look-only"})
        (tmp / "assets_src" / "refs" / "snd" / "refs.json").write_text(json.dumps({"refs": refs}))
        (tmp / "ASSETS.md").write_text("" if no_license else f"{dist} CC0\n")
        (tmp / "data" / "assets" / "snd.json").write_text(json.dumps(
            {"numbers": [{"name": "delay", "value": 1.46, "unit": "s", "source": "" if no_source else "c=343 m/s"}]}))
        if leak_path:
            (tmp / "game" / "notes.md").write_text(f"see refs_private/snd/{next(iter(slots))}0.txt")
        if leak:
            shutil.copy(tmp / "refs_private" / "snd" / f"{next(iter(slots))}0.txt", tmp / "game" / "leaked.txt")

    cases = [
        ("good", {}, None),
        ("missing slot", {"drop_slot": True}, "slot listening_near has 0"),
        ("unsourced number", {"no_source": True}, "has no source"),
        ("unlicensed ref", {"no_license": True}, "no license entry"),
        ("leaked file", {"leak": True}, "leak: game/leaked.txt is a copy"),
        ("leaked path", {"leak_path": True}, "leak: game/notes.md names"),
        ("look-only outside refs_private", {"misplaced": True}, "outside refs_private"),
    ]
    failed = 0
    for name, kw, expect in cases:
        with tempfile.TemporaryDirectory() as d:
            tmp = Path(d)
            build(tmp, **kw)
            problems = check(tmp, checklist_path)
        ok = (not problems) if expect is None else any(expect in p for p in problems)
        print(f"selftest {name}: {'ok' if ok else 'WRONG'} ({len(problems)} problems)")
        failed += 0 if ok else 1
    return failed


def main() -> int:
    checklist = REPO / "verify" / "ref_coverage.toml"
    if selftest(checklist):
        print("G14 selftest failed", file=sys.stderr)
        return 1
    problems = check(REPO, checklist)
    for p in problems:
        print(p, file=sys.stderr)
    return 1 if problems else 0


if __name__ == "__main__":
    raise SystemExit(main())
