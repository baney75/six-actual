---
name: balance-pass
description: Run AI-vs-AI soak after a content wave and produce the balance report — win rates, cost-efficiency per card, unused cards, 2σ outliers, MEC-09/10 checks (W6). Use after any content wave or before a phase gate.
---
# Balance pass (W6)

1. `dotnet run --project sim/Headless/SixActual.Headless.csproj -c Release -- --soak N --out build/soak/<stamp>.csv`, with N ≥ 200 for any tuning decision. Wrap long runs in `systemd-inhibit --what=idle:sleep --why="six-actual soak"`.
2. `python3 scripts/analyze_soak.py build/soak/<stamp>.csv`. It checks the G5 thresholds from `verify/thresholds.toml`:
   - mirror and cross-faction win bands
   - stuck units and crashes
   - commander behavior metrics
   - MEC-09: AA kills ≤ 1.5× cost; single-class deck ≤ 55% against combined arms
   - MEC-10: median unspent bank < 30%
3. `scripts/balance/report.py` writes `build/reports/balance-<stamp>.html`: win rates per faction and archetype, kills value ÷ cost per card, unused cards, outliers above 2σ.
4. Adjust costs before stats, in the generators, never by hand. Re-run.

*`--soak`, analyze_soak.py and report.py are not built yet (G5, Phase 1).*
