using SixActual.Sim;
using SixActual.Sim.Battle;

namespace SixActual.Headless;

/// <summary>G4: runs scenario files over their seeds and applies the all-seeds or Wilson-bound rule per check.</summary>
public static class ScenarioRunner
{
    public static int Run(string[] targets)
    {
        var root = FindRoot();
        var dataAt = Array.IndexOf(targets, "--data-root");
        var dataRoot = root;
        if (dataAt >= 0)
        {
            // Mutation testing: load roster/weapons from an altered copy.
            dataRoot = targets[dataAt + 1];
            targets = targets.Where((_, i) => i != dataAt && i != dataAt + 1).ToArray();
        }

        var defs = BattleData.Load(dataRoot);
        var rulesPath = Path.Combine(root, "data", "rules.json");
        var rulesAt = Array.IndexOf(targets, "--rules");
        if (rulesAt >= 0)
        {
            // Mutation testing: run the suite against altered rules to prove checks can fail.
            rulesPath = targets[rulesAt + 1];
            targets = targets.Where((_, i) => i != rulesAt && i != rulesAt + 1).ToArray();
        }

        var rules = BattleRules.Load(rulesPath);
        var traceAt = Array.IndexOf(targets, "--trace");
        if (traceAt >= 0)
        {
            var tag = targets[traceAt + 1];
            targets = targets.Where((_, i) => i != traceAt && i != traceAt + 1).ToArray();
            foreach (var file in targets)
            {
                Trace(Scenario.Load(file), defs, rules, tag);
            }

            return 0;
        }
        var files = new List<string>();
        foreach (var t in targets.Length == 0 ? [Path.Combine(root, "verify", "scenarios")] : targets)
        {
            if (Directory.Exists(t))
            {
                files.AddRange(Directory.GetFiles(t, "*.json").Order(StringComparer.Ordinal));
            }
            else
            {
                files.Add(t);
            }
        }

        var failed = 0;
        foreach (var file in files)
        {
            if (NetRunner.IsNetScenario(file))
            {
                // NET-01, NET-02, FLY-07: lockstep clients over a simulated network.
                failed += NetRunner.RunScenario(file, defs, rules) ? 0 : 1;
                continue;
            }

            if (GsRunner.IsGsScenario(file))
            {
                // GS-01..05, CMD-08: grand-strategy scenarios run on the strategic sim.
                failed += GsRunner.RunScenario(file, root, dataRoot, rulesPath, defs, rules) ? 0 : 1;
                continue;
            }

            var s = Scenario.Load(file);
            if (s.Checks.Count == 0 || s.Seeds.Count == 0)
            {
                // A scenario with nothing to check, or no seed to run, would pass vacuously (D073 review).
                Console.WriteLine($"FAIL {s.Id} [{s.Requirement}] scenario has {s.Checks.Count} checks and {s.Seeds.Count} seeds; both must be > 0");
                failed++;
                continue;
            }

            var passes = new int[s.Checks.Count];
            foreach (var seed in s.Seeds)
            {
                var r = s.RunSeed(defs, rules, seed);
                for (var c = 0; c < r.Length; c++)
                {
                    passes[c] += r[c] ? 1 : 0;
                }
            }

            var ok = true;
            for (var c = 0; c < s.Checks.Count; c++)
            {
                var check = s.Checks[c];
                var n = s.Seeds.Count;
                bool pass;
                string detail;
                if (check.IsAggregate)
                {
                    pass = check.AggregatePass(out detail);
                }
                else if (check.CiLowerPct >= 0)
                {
                    pass = WilsonLowerAtLeast(passes[c], n, check.CiLowerPct);
                    detail = $"{passes[c]}/{n} seeds, 95% Wilson lower bound >= {check.CiLowerPct}%: {(pass ? "yes" : "no")}";
                }
                else
                {
                    pass = passes[c] == n;
                    detail = $"{passes[c]}/{n} seeds";
                }

                Console.WriteLine($"{(pass ? "pass" : "FAIL")} {s.Id} [{s.Requirement}] {check.Describe()} :: {detail}");
                ok &= pass;
            }

            failed += ok ? 0 : 1;
        }

        Console.WriteLine($"scenarios={files.Count} failed={failed}");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>
    /// Exact integer test that the 95% Wilson lower bound of k/n is at least pct%.
    /// L >= c iff p >= c and (p - c)^2 >= z^2 c (1 - c) / n, with z^2 = 3.8415.
    /// </summary>
    public static bool WilsonLowerAtLeast(int k, int n, int pct)
    {
        if (n == 0)
        {
            return false;
        }

        long gap = (100L * k) - ((long)pct * n);
        if (gap < 0)
        {
            return false;
        }

        return checked(10_000L * gap * gap) >= checked(38_415L * pct * (100 - pct) * n);
    }

    /// <summary>Debug aid: prints one unit's state every 100 ticks for the scenario's first seed.</summary>
    static void Trace(Scenario s, BattleData defs, BattleRules rules, string tag)
    {
        var w = s.Build(defs, rules, s.Seeds[0]);
        var orders = s.BuildOrders();
        var id = s.TagId(tag);
        for (var t = 0; t <= s.Ticks; t++)
        {
            if (t % 100 == 0)
            {
                var u = w.Units[id];
                Console.WriteLine($"t={t} x={u.X.Raw >> 16} y={u.Y.Raw >> 16} surface={Navigation.SurfaceAt(w.Terrain, u.X, u.Y)} goal={u.HasGoal} step={u.PathStep}/{w.Paths[id]?.Length ?? -1} hp={u.Hp} alive={u.Alive} ammo0={(w.MountAmmo.Length > u.MountStart ? w.MountAmmo[u.MountStart] : -1)} sup={u.Suppression} mission={(id < w.Missions.Length ? w.Missions[id].Active : 0)} impacts={w.Events.Count(e => e.Kind == SixActual.Sim.State.Battle.BattleEvent.Impact)} hdg={u.Heading} z={u.Z.Raw >> 16} v={u.AirspeedMmS} flown={u.Flown} turn={u.StickTurn} thr={u.Throttle}");
            }

            if (t < s.Ticks)
            {
                BattleWorld.Step(w, rules, orders);
            }
        }
    }

    public static string FindRootPublic() => FindRoot();

    static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SixActual.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }
}
