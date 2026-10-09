using System.Diagnostics;
using System.Text.Json;
using SixActual.Sim;
using SixActual.Sim.Battle;
using SixActual.Sim.State.Battle;

namespace SixActual.Headless;

/// <summary>
/// SIM-13 / G6 headless tick benchmark: two forces on a generated 8x8 km map advance into contact.
/// Entities = soldiers (squad members) + vehicles (D008). Prints tick p50/p95/p99/max in milliseconds.
/// </summary>
public static class Bench
{
    public static int Run(string[] args)
    {
        var entities = args.Length > 0 ? int.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture) : 2000;
        var ticks = args.Length > 1 ? int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 1200;
        var root = ScenarioRunner.FindRootPublic();
        var data = BattleData.Load(root);
        var rules = BattleRules.Load(Path.Combine(root, "data", "rules.json"));
        var w = BuildWorld(data, rules, entities, out var orders);
        var times = new long[ticks];
        var sys = new long[8];
        string[] names = ["economy+orders", "structures", "movement", "spot", "combat", "morale", "supply"];
        var sw = new Stopwatch();
        var part = new Stopwatch();
        void Time(int k, Action a)
        {
            part.Restart();
            a();
            sys[k] += part.ElapsedTicks;
        }

        for (var t = 0; t < ticks; t++)
        {
            sw.Restart();

            // Same order as BattleWorld.Step, timed per system.
            BattleWorld.BeginTick(w);
            Time(0, () => { Economy.Step(w, rules); BattleWorld.ApplyOrders(w, rules, orders); });
            Time(1, () => Structures.Step(w, rules));
            Time(2, () => Movement.Step(w, rules));
            Time(3, () => Sight.Spot(w, rules));
            Time(4, () => Combat.Step(w, rules));
            Time(5, () => Morale.Step(w, rules));
            Time(6, () => Supply.Step(w, rules));
            w.Tick++;
            times[t] = sw.ElapsedTicks;
        }

        Console.WriteLine(string.Join(" ", names.Select((nm, k) => $"{nm}={sys[k] * 1000.0 / Stopwatch.Frequency / ticks:0.000}ms/tick")));

        Array.Sort(times);
        string Ms(long v) => (v * 1000.0 / Stopwatch.Frequency).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
        var alive = w.Units.Count(u => u.Alive != 0);
        var kills = w.Events.Count(e => e.Kind == BattleEvent.Killed);
        var shots = w.Events.Count(e => e.Kind == BattleEvent.Fired);
        Console.WriteLine($"bench entities={entities} units={w.Units.Length} alive_end={alive} shots={shots} kills={kills} ticks={ticks} p50_ms={Ms(times[ticks / 2])} p95_ms={Ms(times[ticks * 95 / 100])} p99_ms={Ms(times[ticks * 99 / 100])} max_ms={Ms(times[^1])} hash={WorldHash.Of(w)[..16]}");
        return 0;
    }

    /// <summary>The skirmish terrain (moved to <see cref="Skirmish.Terrain"/> so the game can build the same map).</summary>
    public static System.Text.Json.JsonElement Terrain(ulong seed) => Skirmish.Terrain(seed);

    static WorldState BuildWorld(BattleData data, BattleRules rules, int entities, out UnitOrder[] orders)
    {
        // Deterministic terrain: rolling hills, forest blocks, a road grid and a few villages.
        var rng = new XorShift64(42);
        int Next(int n) => (int)(rng.NextULong() % (ulong)n);
        var features = new List<object>();
        for (var i = 0; i < 40; i++)
        {
            features.Add(new { type = "hill", x = Next(8000), y = Next(8000), radius_m = 300 + Next(900), height_dm = 100 + Next(500) });
        }

        for (var i = 0; i < 60; i++)
        {
            features.Add(new { type = "forest", x = Next(7600), y = Next(7600), w = 100 + Next(400), h = 100 + Next(400), density = 50 + Next(50) });
        }

        for (var k = 1; k < 4; k++)
        {
            features.Add(new { type = "road", x = 0, y = (k * 2000) - 8, w = 8000, h = 16 });
            features.Add(new { type = "road", x = (k * 2000) - 8, y = 0, w = 16, h = 8000 });
        }

        for (var v = 0; v < 6; v++)
        {
            var vx = 1000 + Next(6000);
            var vy = 1000 + Next(6000);
            for (var b = 0; b < 8; b++)
            {
                features.Add(new { type = "building", x = vx + ((b % 4) * 24), y = vy + ((b / 4) * 24), nx = 4, ny = 4, floors = 2, material = "brick" });
            }
        }

        var spec = JsonSerializer.SerializeToElement(new { size_m = 8000, cell_m = 8, features });
        var w = new WorldState
        {
            Seed = 42,
            Rng = BattleWorld.MixSeed(42),
            Defs = data.Units,
            Weapons = data.Weapons,
            Terrain = TerrainBuilder.Build(spec),
        };
        Structures.Place(w, rules, spec);

        // Force mix by entity share: infantry squads carry most entities.
        string[][] mix =
        [
            ["m1a1_abrams", "m2a2_bradley", "us_mech_rifle_squad", "us_mech_rifle_squad", "m109a3_sph", "m977_hemtt"],
            ["t80bv", "bmp2", "sov_motor_rifle_squad", "sov_motor_rifle_squad", "2s3_akatsiya", "ural4320"],
        ];
        var list = new List<(int Def, int Player, int X, int Y)>();
        var count = 0;
        var n = 0;
        while (count < entities)
        {
            var player = n % 2;
            var id = mix[player][(n / 2) % mix[player].Length];
            var def = BattleUnitDef.IndexOf(data.Units, id);
            var x = 400 + Next(7200);
            var y = player == 0 ? 2000 + Next(800) : 4800 + Next(800);
            list.Add((def, player, x, y));
            count += Math.Max(1, data.Units[def].Members);
            n++;
        }

        w.Units = [];
        w.Paths = [];
        foreach (var (def, player, x, y) in list)
        {
            Economy.Spawn(w, def, player, x, y, 0);
        }

        var ord = new List<UnitOrder>();
        for (var i = 0; i < w.Units.Length; i++)
        {
            var targetY = w.Units[i].Player == 0 ? 5600 : 2000;
            ord.Add(new UnitOrder { Tick = 1 + (i % 20), UnitId = w.Units[i].Id, Kind = UnitOrder.Move, X = (int)(w.Units[i].X.Raw >> Fp.FractionalBits), Y = targetY });
        }

        orders = ord.ToArray();
        return w;
    }
}
