#!/usr/bin/env python3
"""Procedural sound effects (D020: procedural synthesis, no paid service; D068).

    python3 scripts/generate_sfx.py

Synthesised with numpy at 48 kHz mono, written to game/assets/audio/sfx/<family>/<name>.wav, listed in
audio/sfx_manifest.json with the synthesis parameters. Families (BRIEF D7):
  rifle, mg        a shot / a 5-round burst in near, mid and far versions (far = less high end, longer tail), 4 variants
  cannon           tank gun: crack plus a deep boom, near/mid/far, 4 variants
  explosion        small, medium, large (large: strong energy below 100 Hz), 4 variants each
  engine           diesel (low firing pulses) and turbine (whine over rush): seamless 4 s loops
  crack            supersonic crack of a passing round, 4 variants
  footstep         dirt, grass, gravel, concrete footfalls, 4 variants each
  squelch          radio squelch tail per era (ww2 noisy, coldwar burst, modern digital chirp)
  ambience         seamless 8 s beds: wind, birds, insects, rain, fire crackle, distant artillery rumble
  ui               click
Weapons and explosions are layered (transient, body, mechanics, low end, debris; tail by distance), BRIEF D7. Every
family has a design entry in audio/design/<family>.yaml. Every file peaks at -3 dBFS (under the -1 dBTP AUD-02 limit).
"""
from __future__ import annotations

import json
import wave
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "game" / "assets" / "audio" / "sfx"
SR = 48000
PEAK = 10 ** (-3 / 20)


def lowpass_fast(x: np.ndarray, hz: float) -> np.ndarray:
    """FFT low-pass with a short taper. Zero-padded to twice the length so the filter's ringing cannot wrap around
    to the start (a circular FFT filter clicks at the buffer edges otherwise)."""
    n = len(x)
    spec = np.fft.rfft(np.concatenate([x, np.zeros(n)]))
    f = np.fft.rfftfreq(2 * n, 1 / SR)
    taper = np.clip((hz * 1.2 - f) / (hz * 0.4), 0, 1)
    return np.fft.irfft(spec * taper, 2 * n)[:n]


def tail(x: np.ndarray, seconds: float, rng: np.random.Generator, wet: float, cutoff: float = 20000) -> np.ndarray:
    """Reverberant tail: the dry sound convolved with decaying noise, low-passed like the band it belongs to (distant
    reflections are dark too)."""
    n = int(seconds * SR)
    ir = rng.standard_normal(n) * np.exp(-np.arange(n) / (n / 5))
    ir[0] = 1 / wet
    y = np.convolve(x, ir * wet)[: len(x) + n]
    return lowpass_fast(y, cutoff) if cutoff < 20000 else y


def clank(n: int, at_s: float, hz: float, amp: float, decay_s: float) -> np.ndarray:
    """Mechanics layer: a metallic ring (bolt, belt link, breech) starting at at_s."""
    x = np.zeros(n)
    i = int(at_s * SR)
    m = n - i
    t = np.arange(m) / SR
    x[i:] = (np.sin(2 * np.pi * hz * t) + 0.5 * np.sin(2 * np.pi * hz * 2.76 * t)) * np.exp(-t / decay_s) * amp
    return x


def shot(rng: np.random.Generator, pitch: float, decay_ms: float, mechanics: bool = True) -> np.ndarray:
    """Layers (BRIEF D7): transient crack, body thump, mechanics (bolt click at 0.3 s); tail added by distance()."""
    n = int(0.45 * SR)
    t = np.arange(n) / SR
    crack = rng.standard_normal(n) * np.exp(-t / (decay_ms / 1000))
    thump = np.sin(2 * np.pi * 90 * pitch * t) * np.exp(-t / 0.05) * 0.8
    bolt = clank(n, 0.30, 2600 * pitch, 0.08, 0.012) + clank(n, 0.36, 3100 * pitch, 0.06, 0.010) if mechanics else 0
    return crack + thump + bolt


def distance(x: np.ndarray, band: str, rng: np.random.Generator) -> np.ndarray:
    """Near < 50 m, mid 50-500 m, far > 500 m (BRIEF D7): far versions lose the transient and high end and gain a
    long low rumble."""
    if band == "near":
        return tail(x, 0.3, rng, 0.08)
    if band == "mid":
        return tail(lowpass_fast(x, 5000), 0.6, rng, 0.15, 5000)
    far = tail(lowpass_fast(x, 1800), 1.2, rng, 0.3, 1800)
    n = len(far)
    t = np.arange(n) / SR
    rumble = lowpass_fast(rng.standard_normal(n), 120) * np.exp(-t / 0.8) * np.max(np.abs(far)) * 0.6
    return far + rumble


def explosion(rng: np.random.Generator, size: str, pitch: float) -> np.ndarray:
    """Layers: transient click, body roar, low-end boom, debris (falling dirt grains after the blast)."""
    dur, sub, cutoff = {"small": (0.9, 70, 3500), "medium": (1.6, 50, 2500), "large": (3.0, 35, 1600)}[size]
    n = int(dur * SR)
    t = np.arange(n) / SR
    env = np.exp(-t / (dur / 4))
    body = lowpass_fast(rng.standard_normal(n), cutoff) * env
    boom = np.sin(2 * np.pi * sub * pitch * t * (1 - 0.3 * t / dur)) * np.exp(-t / (dur / 3)) * (2.5 if size == "large" else 1.2)
    crack = rng.standard_normal(n) * np.exp(-t / 0.02) * 0.6
    debris = np.zeros(n)
    for _ in range({"small": 12, "medium": 25, "large": 45}[size]):
        i = int(rng.uniform(0.25, 0.95) * n)
        m = min(n - i, int(0.02 * SR))
        debris[i:i + m] += rng.standard_normal(m) * np.exp(-np.arange(m) / (0.004 * SR)) * rng.uniform(0.05, 0.25)
    return body * 3 + boom + crack + debris


def cannon(rng: np.random.Generator, pitch: float) -> np.ndarray:
    """Layers: transient crack, body boom, low-end rumble, mechanics (breech clank at 0.9 s)."""
    n = int(1.2 * SR)
    t = np.arange(n) / SR
    crack = rng.standard_normal(n) * np.exp(-t / 0.012) * 1.2
    boom = np.sin(2 * np.pi * 55 * pitch * t) * np.exp(-t / 0.35) * 1.5
    rumble = lowpass_fast(rng.standard_normal(n), 400) * np.exp(-t / 0.4) * 2
    breech = clank(n, 0.9, 1400 * pitch, 0.25, 0.05)
    return crack + boom + rumble + breech


def supersonic(rng: np.random.Generator) -> np.ndarray:
    """The crack of a round passing close to the listener: an N-wave plus a hiss, ahead of the report."""
    n = int(0.12 * SR)
    x = np.zeros(n)
    w = int(0.0015 * SR)
    x[:w] = np.linspace(1, -1, w)
    t = np.arange(n) / SR
    hiss = np.fft.irfft(np.fft.rfft(rng.standard_normal(n)) * (np.fft.rfftfreq(n, 1 / SR) > 3000), n) * np.exp(-t / 0.01) * 0.6
    return x + hiss


def footstep(surface: str, rng: np.random.Generator, pitch: float) -> np.ndarray:
    """One footfall per surface: dirt a low thud, grass a soft swish, gravel a crunch of grains, concrete a sharp tap."""
    n = int(0.18 * SR)
    t = np.arange(n) / SR
    if surface == "dirt":
        return lowpass_fast(rng.standard_normal(n), 900 * pitch) * np.exp(-t / 0.03) * 2 + np.sin(2 * np.pi * 110 * pitch * t) * np.exp(-t / 0.02)
    if surface == "grass":
        f = np.fft.rfftfreq(n, 1 / SR)
        sw = np.fft.irfft(np.fft.rfft(rng.standard_normal(n)) * ((f > 1500) & (f < 7000)), n)
        return sw * np.exp(-((t - 0.04) ** 2) / 0.0006) * 1.5
    if surface == "gravel":
        x = np.zeros(n)
        for _ in range(30):
            i = int(rng.uniform(0, 0.09) * SR)
            m = int(0.004 * SR)
            x[i:i + m] += rng.standard_normal(m) * np.exp(-np.arange(m) / (0.0008 * SR)) * rng.uniform(0.3, 1)
        return x
    tap = rng.standard_normal(n) * np.exp(-t / 0.004)
    ring = np.sin(2 * np.pi * 1900 * pitch * t) * np.exp(-t / 0.015) * 0.3
    return tap + ring


def squelch(era: str, rng: np.random.Generator) -> np.ndarray:
    """Radio squelch tail per era (BRIEF D7): WW2 heavy noise, Cold War a short burst, modern a clean digital chirp."""
    if era == "ww2":
        n = int(0.35 * SR)
        t = np.arange(n) / SR
        return lowpass_fast(rng.standard_normal(n), 3000) * np.exp(-t / 0.15)
    if era == "coldwar":
        n = int(0.14 * SR)
        t = np.arange(n) / SR
        return lowpass_fast(rng.standard_normal(n), 4000) * np.exp(-t / 0.05)
    n = int(0.07 * SR)
    t = np.arange(n) / SR
    return np.sin(2 * np.pi * (1800 + 9000 * t) * t) * np.exp(-t / 0.03) * 0.6


def ambience(kind: str, rng: np.random.Generator) -> np.ndarray:
    """8 s seamless beds: noise is filtered circularly (so it loops), and events stay clear of the loop point."""
    n = 8 * SR
    t = np.arange(n) / SR
    def band(lo: float, hi: float) -> np.ndarray:
        f = np.fft.rfftfreq(n, 1 / SR)
        return np.fft.irfft(np.fft.rfft(rng.standard_normal(n)) * ((f >= lo) & (f < hi)), n)
    def events(count: int, make, length_s: float) -> np.ndarray:
        x = np.zeros(n)
        m = int(length_s * SR)
        for _ in range(count):
            # events stay 60 ms clear of the loop point, so the seam is the circular bed alone
            i = int(rng.uniform(0.06 * SR, n - m - 0.06 * SR))
            x[i:i + m] += make(np.arange(m) / SR)
        return x
    if kind == "wind":
        mod = 0.6 + 0.4 * np.sin(2 * np.pi * t / 8) * np.sin(2 * np.pi * 3 * t / 8)
        return band(80, 900) * mod * 3
    if kind == "birds":
        return band(200, 2000) * 0.3 + events(10, lambda tt: np.sin(2 * np.pi * (3000 + 1500 * np.sin(2 * np.pi * 18 * tt)) * tt) * np.exp(-tt / 0.06) * 0.5, 0.25)
    if kind == "insects":
        pulse = (np.sin(2 * np.pi * 4500 * t) * (np.sin(2 * np.pi * 24 * t) > 0.3)) * 0.25
        return band(150, 1500) * 0.3 + pulse
    if kind == "rain":
        return band(500, 12000) * 1.5 + events(400, lambda tt: np.sin(2 * np.pi * 4000 * tt) * np.exp(-tt / 0.003) * 0.3, 0.02)
    if kind == "fire":
        return band(100, 1200) * 0.8 + events(120, lambda tt: rng.standard_normal(len(tt)) * np.exp(-tt / 0.004), 0.03)
    # distant artillery rumble: a low bed with sparse far booms
    return band(20, 200) * 1.5 + events(3, lambda tt: np.sin(2 * np.pi * 40 * tt) * np.exp(-tt / 0.6), 2.0)


def engine(kind: str, rng: np.random.Generator) -> np.ndarray:
    """4 s loop; every component completes an integer number of cycles, so the end meets the start."""
    n = 4 * SR
    t = np.arange(n) / SR
    if kind == "diesel":
        f = 31.0  # 124 firing pulses in 4 s
        pulses = sum(np.sin(2 * np.pi * f * k * t) / k for k in range(1, 9))
        noise = np.fft.irfft(np.fft.rfft(rng.standard_normal(n)) * (np.fft.rfftfreq(n, 1 / SR) < 900), n)
        x = pulses * 0.8 + noise * 3
    else:
        whine = np.sin(2 * np.pi * 2400 * t) * 0.25 + np.sin(2 * np.pi * 4800 * t) * 0.08
        rush = np.fft.irfft(np.fft.rfft(rng.standard_normal(n)) * (np.fft.rfftfreq(n, 1 / SR) < 6000), n)
        x = whine + rush * 2
    return x  # FFT-filtered noise is circular, so it loops seamlessly too


DESIGN = {
    "rifle": ("rifle and carbine report", ["transient crack", "body thump", "mechanics: bolt clicks", "tail by distance"], 2, 8),
    "mg": ("machine-gun burst (5 rounds)", ["transient cracks", "body thumps", "mechanics: bolt clicks", "tail by distance"], 2, 6),
    "cannon": ("tank and anti-tank gun report", ["transient crack", "body boom", "low end rumble", "mechanics: breech clank", "tail by distance"], 1, 4),
    "explosion": ("shell, bomb and vehicle explosions", ["transient click", "body roar", "low end boom", "debris grains"], 1, 4),
    "crack": ("supersonic crack of a round passing near the listener", ["N-wave", "hiss"], 1, 4),
    "squelch": ("radio squelch tail per era", ["noise or chirp"], 2, 1),
    "ambience": ("ambience beds", ["filtered noise bed", "wrapped events"], 4, 4),
    "engine": ("vehicle engine loops", ["firing pulses or turbine whine", "broadband noise"], 3, 6),
    "ui": ("interface click", ["tone blip"], 1, 2),
    "footstep": ("infantry footfalls per surface (dirt, grass, gravel, concrete)", ["surface layer (thud, swish, grains or tap)"], 4, 12),
}


def write_design(files: list[dict]) -> None:
    """BRIEF D7: one design entry per sound family in audio/design/<id>.yaml."""
    import yaml
    d = ROOT / "audio" / "design"
    d.mkdir(parents=True, exist_ok=True)
    for fam, (purpose, layers, priority, max_inst) in DESIGN.items():
        mine = [f for f in files if f["family"] == fam]
        entry = {
            "id": fam, "purpose": purpose, "references": "none: procedural synthesis from first principles (D020); no look-only listening references used",
            "layers": layers, "distance_bands": sorted({f["band"] for f in mine if "band" in f}) or ["single"],
            "variants": max([f.get("variant", 1) for f in mine] or [1]), "loop_points": "whole file (seamless)" if any(f.get("loop") for f in mine) else "none",
            "priority": priority, "max_concurrent": max_inst, "files": [f["path"] for f in mine], "generator": "scripts/generate_sfx.py",
        }
        (d / f"{fam}.yaml").write_text(yaml.safe_dump(entry, sort_keys=False))


def save(x: np.ndarray, path: Path, loop: bool = False) -> dict:
    x = x / (np.max(np.abs(x)) + 1e-9) * PEAK
    if not loop:
        fade = int(0.001 * SR)  # 1 ms fade-in: no onset click from a non-zero first sample
        x = x.copy()
        x[:fade] *= np.linspace(0, 1, fade)
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((x * 32767).astype("<i2").tobytes())
    return {"path": str(path.relative_to(ROOT)), "duration_s": round(len(x) / SR, 3)}


def main() -> int:
    files = []
    for v in range(4):
        rng = np.random.default_rng(1000 + v)
        pitch = 1 + (v - 1.5) * 0.06
        dry = shot(rng, pitch, 18 + 4 * v)
        burst = np.zeros(int(0.6 * SR))
        for k in range(5):
            s0 = int(k * 0.1 * SR)
            one = shot(np.random.default_rng(2000 + 10 * v + k), pitch * (1 + 0.02 * k), 16 + 3 * v)
            burst[s0:s0 + len(one)] += one[: len(burst) - s0]
        gun = cannon(rng, pitch)
        for band in ("near", "mid", "far"):
            files.append({"family": "rifle", "name": f"rifle_{band}_v{v + 1}", "band": band, "variant": v + 1, **save(distance(dry, band, np.random.default_rng(3000 + v)), OUT / "rifle" / f"rifle_{band}_v{v + 1}.wav")})
            files.append({"family": "mg", "name": f"mg_{band}_v{v + 1}", "band": band, "variant": v + 1, **save(distance(burst, band, np.random.default_rng(4000 + v)), OUT / "mg" / f"mg_{band}_v{v + 1}.wav")})
            files.append({"family": "cannon", "name": f"cannon_{band}_v{v + 1}", "band": band, "variant": v + 1, **save(distance(gun, band, np.random.default_rng(5000 + v)), OUT / "cannon" / f"cannon_{band}_v{v + 1}.wav")})
        for size in ("small", "medium", "large"):
            files.append({"family": "explosion", "name": f"explosion_{size}_v{v + 1}", "size": size, "variant": v + 1,
                          **save(explosion(np.random.default_rng(6000 + 10 * v), size, pitch), OUT / "explosion" / f"explosion_{size}_v{v + 1}.wav")})
    for v in range(4):
        files.append({"family": "crack", "name": f"crack_v{v + 1}", "variant": v + 1, **save(supersonic(np.random.default_rng(7500 + v)), OUT / "crack" / f"crack_v{v + 1}.wav")})
    for surface in ("dirt", "grass", "gravel", "concrete"):
        for v in range(4):
            files.append({"family": "footstep", "name": f"footstep_{surface}_v{v + 1}", "surface": surface, "variant": v + 1,
                          **save(footstep(surface, np.random.default_rng(7800 + 10 * v), 1 + (v - 1.5) * 0.05), OUT / "footstep" / f"footstep_{surface}_v{v + 1}.wav")})
    for era in ("ww2", "coldwar", "modern"):
        files.append({"family": "squelch", "name": f"squelch_{era}", **save(squelch(era, np.random.default_rng(7600)), OUT / "squelch" / f"squelch_{era}.wav")})
    for kind in ("wind", "birds", "insects", "rain", "fire", "rumble"):
        files.append({"family": "ambience", "name": f"ambience_{kind}", "loop": True, **save(ambience(kind, np.random.default_rng(7700)), OUT / "ambience" / f"ambience_{kind}.wav", loop=True)})
    for kind in ("diesel", "turbine"):
        files.append({"family": "engine", "name": f"engine_{kind}", "loop": True, **save(engine(kind, np.random.default_rng(7000)), OUT / "engine" / f"engine_{kind}.wav", loop=True)})
    click = np.zeros(int(0.05 * SR))
    click[:240] = np.sin(2 * np.pi * 2000 * np.arange(240) / SR) * np.exp(-np.arange(240) / 60)
    files.append({"family": "ui", "name": "ui_click", **save(click, OUT / "ui" / "ui_click.wav")})
    write_design(files)
    (ROOT / "audio" / "sfx_manifest.json").write_text(json.dumps({"version": 1, "generator": "scripts/generate_sfx.py (numpy synthesis, D068)", "files": files}, indent=1) + "\n")
    print(f"sfx: {len(files)} files")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
