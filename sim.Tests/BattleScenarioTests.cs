using SixActual.Headless;
using SixActual.Sim.Battle;

namespace SixActual.Sim.Tests;

/// <summary>Runs every G4 scenario through the real battle sim inside the test process, so coverage sees it.</summary>
public class BattleScenarioTests
{
    static readonly string Root = FindRepoRoot();

    static readonly BattleData Defs = BattleData.Load(Root);

    static readonly BattleRules Rules = BattleRules.Load(Path.Combine(Root, "data", "rules.json"));

    /// <summary>Battle scenarios. Grand-strategy scenarios run on their own runner in
    /// <see cref="Grand_strategy_scenarios_pass"/>. Network scenarios (8 lockstep clients, minutes each) run headless in
    /// G4; <see cref="Lockstep_short_match_stays_in_sync_under_loss"/> keeps the lockstep code under test here (D058).
    /// LAN scenarios (real UDP, D076) also run in G4, and LanTests covers the same code here.</summary>
    public static TheoryData<string> Files() => ScenarioFiles(f => !NetRunner.IsNetScenario(f) && !LanRunner.IsLanScenario(f) && !GsRunner.IsGsScenario(f));

    public static TheoryData<string> StrategicFiles() => ScenarioFiles(GsRunner.IsGsScenario);

    static TheoryData<string> ScenarioFiles(Func<string, bool> keep)
    {
        var data = new TheoryData<string>();
        foreach (var f in Directory.GetFiles(Path.Combine(Root, "verify", "scenarios"), "*.json").Order(StringComparer.Ordinal))
        {
            if (keep(f))
            {
                data.Add(Path.GetFileName(f));
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(StrategicFiles))]
    public void Grand_strategy_scenarios_pass(string file)
    {
        var path = Path.Combine(Root, "verify", "scenarios", file);
        Assert.True(GsRunner.RunScenario(path, Root, Root, Path.Combine(Root, "data", "rules.json"), Defs, Rules), file);
    }

    [Fact]
    public void Lockstep_short_match_stays_in_sync_under_loss()
    {
        var net = new SixActual.Sim.Net.NetConditions { LatencyMs = 80, JitterMs = 20, LossPct = 5, InputDelayTicks = 4, Seed = 3, FaultClient = -1 };
        var session = new SixActual.Sim.Net.LockstepSession(2, () => NetRunner.Skirmish(Defs, Rules, 3, 1, false), Rules, net) { HashIntervalTicks = 20 };
        session.Run(300, (client, w) => [], -1, -1, -1);
        Assert.Equal(0, session.Desyncs);
        Assert.True(session.HashChecks >= 10);
        Assert.True(session.PacketsLost > 0);
        Assert.Equal(WorldHash.Of(session.World(0)), WorldHash.Of(session.World(1)));
        Assert.Equal(WorldHash.Of(session.World(0)), session.Replay(300));
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void Scenario_checks_pass_on_their_first_seed(string file)
    {
        var s = Scenario.Load(Path.Combine(Root, "verify", "scenarios", file));
        var results = s.RunSeed(Defs, Rules, s.Seeds[0]);
        for (var i = 0; i < results.Length; i++)
        {
            if (s.Checks[i].CiLowerPct < 0)
            {
                Assert.True(results[i], $"{s.Id}: {s.Checks[i].Describe()}");
            }
        }
    }

    [Fact]
    public void Same_seed_and_orders_give_the_same_world_hash()
    {
        var s = Scenario.Load(Path.Combine(Root, "verify", "scenarios", "path_road_preference.json"));
        string Run()
        {
            var w = s.Build(Defs, Rules, 7);
            var orders = s.BuildOrders();
            for (var t = 0; t < 1500; t++)
            {
                BattleWorld.Step(w, Rules, orders);
            }

            return WorldHash.Of(w);
        }

        var first = Run();
        Assert.Equal(first, Run());
        var other = s.Build(Defs, Rules, 7);
        Assert.NotEqual(first, WorldHash.Of(other));
    }

    [Fact]
    public void Unknown_inputs_are_rejected()
    {
        Assert.Throws<InvalidDataException>(() => BattleRules.WeatherIndex("sandstorm"));
        var s = Scenario.Load(Path.Combine(Root, "verify", "scenarios", "los_open_ground.json"));
        Assert.Throws<InvalidDataException>(() => s.TagId("nobody"));
    }

    [Fact]
    public void Integer_square_root_is_exact()
    {
        foreach (var v in new long[] { 0, 1, 2, 3, 4, 15, 16, 17, 1_000_000, 999_999, long.MaxValue / 4 })
        {
            var r = (long)Sight.IntSqrt(v);
            Assert.True(r * r <= v && (r + 1) * (r + 1) > v, $"isqrt({v})={r}");
        }
    }

    [Theory]
    [InlineData(30, 50, 45, true)]   // 60%: Wilson 95% lower bound 0.462
    [InlineData(30, 50, 47, false)]
    [InlineData(50, 50, 92, true)]   // 100%: lower bound 0.929
    [InlineData(50, 50, 93, false)]
    [InlineData(0, 50, 1, false)]
    [InlineData(5, 0, 1, false)]
    public void Wilson_lower_bound_matches_reference_values(int k, int n, int pct, bool expected) =>
        Assert.Equal(expected, SixActual.Headless.ScenarioRunner.WilsonLowerAtLeast(k, n, pct));

    [Fact]
    public void Teammates_never_spot_or_fire_on_each_other_in_a_2v2()
    {
        // D073 regression: spotting and targeting once checked "other player" instead of "other team".
        var w = Skirmish.Create(Defs, Rules, 3, "us_1989_armored", "su_1989_motor_rifle", true, true, new Skirmish.Options { PerSide = 2 });
        for (var t = 0; t < 6000; t++)
        {
            BattleWorld.Step(w, Rules, ReadOnlySpan<UnitOrder>.Empty);
        }

        int TeamOfUnit(int id) => Frontline.TeamOf(w, w.Units[Array.FindIndex(w.Units, u => u.Id == id)].Player);
        var fired = w.Events.Where(e => e.Kind == SixActual.Sim.State.Battle.BattleEvent.Fired).ToList();
        Assert.NotEmpty(fired);
        Assert.DoesNotContain(fired, e => TeamOfUnit(e.A) == TeamOfUnit(e.B));
        Assert.DoesNotContain(w.Events, e => e.Kind == SixActual.Sim.State.Battle.BattleEvent.Spotted && Frontline.TeamOf(w, e.A) == TeamOfUnit(e.B));
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SixActual.slnx")))
        {
            dir = dir.Parent;
        }

        return dir!.FullName;
    }
}
