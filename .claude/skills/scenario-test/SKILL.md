---
name: scenario-test
description: Author a G4 scenario (verify/scenarios/<name>.json) with map, units, orders, seed(s), duration and outcome assertions, run by sim/Headless --scenario. Use for any requirement whose verifier column says G4.
---
# Scenario test (G4)

Scenarios live in `verify/` and are protected. Each new scenario needs a DECISIONS.md entry and its own `VERIFY:` commit. Create `.claude/verify-unlock` with the reason first.

1. Name it after the requirement family: `los_*`, `armor_*`, `collapse_*`, `front_*`, `net_*`, `net_fly_*`.
2. Contents: era, map or terrain fixture, units by data id (never inline stats), the orders as a command list with ticks, seeds, duration in ticks, and assertions.
3. Assertions test outcomes, ranges or statistics from sim state or the event log. Statistical scenarios run 50 or more seeds and assert on a confidence interval, not a single run.
4. Write the scenario first and run `dotnet run --project sim/Headless/SixActual.Headless.csproj -c Release -- --scenario verify/scenarios/<name>.json`. It must fail for the stated reason before you implement. Record that failing output in PROGRESS.md.
5. Never mock the sim, read the seed in product code, or detect the harness.

*`--scenario` and the scenario schema are not built yet. They are the first Phase 1 task (they unblock SIM-04 to SIM-10).*
