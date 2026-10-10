using System.Text.Json;
using SixActual.Sim;
using SixActual.Sim.Battle;
using SixActual.Sim.Net;
using SixActual.Sim.State.Battle;

namespace SixActual.Headless;

/// <summary>
/// G4 network scenarios (type "net"; NET-01, NET-02, FLY-07): an N-vs-N skirmish (8 km map, Cold War decks, AI
/// commanders under each player) played by one lockstep client per player over a simulated network. Players also
/// send scripted inputs: unit moves, and for "fly" a Take Control of the player's A-10 followed by stick inputs.
/// </summary>
public static class NetRunner
{
    public static bool IsNetScenario(string file)
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(file));
        return doc.RootElement.TryGetProperty("type", out var t) && t.GetString() == "net";
    }

    /// <summary>An N-vs-N skirmish world: even players NATO from the south, odd players Pact from the north.</summary>
    public static WorldState Skirmish(BattleData data, BattleRules rules, ulong seed, int perSide, bool aircraft)
    {
        var spec = Bench.Terrain(seed);
        var w = new WorldState { Seed = seed, Rng = BattleWorld.MixSeed(seed), Defs = data.Units, Weapons = data.Weapons, SupportDefs = data.Support, Terrain = TerrainBuilder.Build(spec) };
        Structures.Place(w, rules, spec);
        w.Decks = data.Decks;
        var nato = Array.FindIndex(data.Decks, d => d.Id == "us_1989_armored");
        var pact = Array.FindIndex(data.Decks, d => d.Id == "su_1989_motor_rifle");
        var n = perSide * 2;
        var stock = new List<int>();
        w.Players = new PlayerState[n];
        for (var p = 0; p < n; p++)
        {
            var deck = p % 2 == 0 ? nato : pact;
            w.Players[p] = new PlayerState
            {
                Dp = 1000,
                Profile = BattleRules.ProfileIndex(data.Decks[deck].Income),
                SpawnX = 1000 + ((p / 2) * 6000 / Math.Max(1, perSide)),
                SpawnY = p % 2 == 0 ? 600 : 7400,
                Deck = deck,
                StockStart = stock.Count,
                Team = p % 2,
            };
            foreach (var c in data.Decks[deck].Cards)
            {
                stock.Add(c.Count);
            }
        }

        w.CardStock = stock.ToArray();
        w.Objectives = [new Objective { X = 2000, Y = 4000, Owner = -1 }, new Objective { X = 4000, Y = 4000, Owner = -1 }, new Objective { X = 6000, Y = 4000, Owner = -1 }];
        Command.Init(w);
        for (var p = 0; p < n; p++)
        {
            w.XoActive[p] = 1;
        }

        if (aircraft)
        {
            var a10 = BattleUnitDef.IndexOf(data.Units, "a10a_thunderbolt");
            Economy.Spawn(w, a10, 0, 2000, 1500, 0);
        }

        return w;
    }

    public static bool RunScenario(string file, BattleData data, BattleRules rules)
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(file));
        var e = doc.RootElement;
        var id = e.GetProperty("id").GetString()!;
        var req = e.GetProperty("requirement").GetString()!;
        int Int(string n, int f) => e.TryGetProperty(n, out var v) ? v.GetInt32() : f;
        var perSide = Int("players_per_side", 4);
        var ticks = Int("ticks", 2400);
        var seed = (ulong)Int("seed", 1);
        var script = e.TryGetProperty("script", out var sc) ? sc.GetString()! : "moves";
        var net = new NetConditions
        {
            LatencyMs = Int("latency_ms", 50),
            JitterMs = Int("jitter_ms", 0),
            LossPct = Int("loss_pct", 0),
            InputDelayTicks = Int("input_delay_ticks", 4),
            Seed = seed,
            FaultClient = Int("fault_client", -1),
        };
        var fly = script == "fly";
        var session = new LockstepSession(perSide * 2, () => Skirmish(data, rules, seed, perSide, fly), rules, net) { HashIntervalTicks = Int("hash_interval_ticks", 20) };
        var drop = e.TryGetProperty("drop", out var d) ? d : default;
        session.Run(ticks, (client, w) => Script(client, w, script),
            drop.ValueKind == JsonValueKind.Object ? drop.GetProperty("client").GetInt32() : -1,
            drop.ValueKind == JsonValueKind.Object ? drop.GetProperty("at_tick").GetInt32() : -1,
            drop.ValueKind == JsonValueKind.Object ? drop.GetProperty("rejoin_after_ms").GetInt32() : -1);
        var finals = Enumerable.Range(0, perSide * 2).Select(c => WorldHash.Of(session.World(c))).Distinct().ToList();
        var allOk = true;
        foreach (var c in e.GetProperty("assert").EnumerateArray())
        {
            var value = c.TryGetProperty("value", out var v) ? v.GetInt32() : 0;
            bool ok;
            string detail;
            switch (c.GetProperty("check").GetString())
            {
                case "desyncs":
                    ok = session.Desyncs == value && finals.Count == 1;
                    detail = $"{session.Desyncs} desyncs in {session.HashChecks} hash checks; final hashes distinct {finals.Count}";
                    break;
                case "desyncs_at_least":
                    ok = session.Desyncs >= value;
                    detail = $"{session.Desyncs} desyncs in {session.HashChecks} hash checks (fault injected on client {net.FaultClient})";
                    break;
                case "hash_checks_at_least":
                    ok = session.HashChecks >= value;
                    detail = $"{session.HashChecks} hash checks";
                    break;
                case "replay_matches":
                    var replay = session.Replay(ticks);
                    ok = finals.Count == 1 && replay == finals[0];
                    detail = $"replay {replay[..12]} vs clients {finals[0][..12]}";
                    break;
                case "max_stall_ms_at_most":
                    ok = session.MaxStallMs <= value;
                    detail = $"max stall {session.MaxStallMs} ms, packets lost {session.PacketsLost}";
                    break;
                case "reconnects":
                    ok = session.Reconnects == value && finals.Count == 1;
                    detail = $"{session.Reconnects} reconnects, final hashes distinct {finals.Count}";
                    break;
                case "packets_lost_at_least":
                    ok = session.PacketsLost >= value;
                    detail = $"{session.PacketsLost} packets lost";
                    break;
                case "flown":
                    var flownEverywhere = Enumerable.Range(0, perSide * 2).All(k => session.World(k).Events.Any(ev => ev.Kind == BattleEvent.XoTookCommand && ev.A == 0));
                    var sticks = session.Log.Values.Sum(l => l.Count(o => o.Kind == UnitOrder.Stick));
                    ok = flownEverywhere && sticks >= value;
                    detail = $"take control seen on every client {flownEverywhere}; {sticks} stick inputs exchanged";
                    break;
                case "battle_progressed":
                    var w0 = session.World(0);
                    var deployed = w0.Units.Length;
                    var dead = w0.Units.Count(u => u.Alive == 0);
                    ok = deployed >= value && dead > 0;
                    detail = $"{deployed} units deployed, {dead} destroyed, scores {string.Join("/", w0.Score)}";
                    break;
                default:
                    throw new InvalidDataException($"unknown net check {c.GetProperty("check").GetString()}");
            }

            allOk &= ok;
            Console.WriteLine($"{(ok ? "pass" : "FAIL")} {id} [{req}] {c.GetProperty("check").GetString()} :: {detail}");
        }

        return allOk;
    }

    /// <summary>Scripted player inputs, decided from the client's own (identical) world: every 10 s each player
    /// sends its lowest-index living ground unit to a point that cycles with time; "fly" also has player 0 take
    /// control of its A-10 at 2 s and work the stick every second.</summary>
    internal static IEnumerable<UnitOrder> Script(int client, WorldState w, string script)
    {
        var t = w.Tick;
        if (t % 200 == 17)
        {
            for (var i = 0; i < w.Units.Length; i++)
            {
                if (w.Units[i].Player == client && w.Units[i].Alive != 0 && !w.Defs[w.Units[i].Def].IsAir && w.Units[i].Flown == 0)
                {
                    yield return new UnitOrder { Kind = UnitOrder.Move, UnitId = w.Units[i].Id, X = 1000 + ((t / 200 * 1300) % 6000), Y = 2500 + ((client * 700) % 3000) };
                    break;
                }
            }
        }

        if (script != "fly" || client != 0)
        {
            yield break;
        }

        var a10 = Array.FindIndex(w.Units, u => u.Player == 0 && u.Alive != 0 && w.Defs[u.Def].IsAir);
        if (a10 < 0)
        {
            yield break;
        }

        if (t == 40)
        {
            yield return new UnitOrder { Kind = UnitOrder.TakeControl, UnitId = w.Units[a10].Id };
        }
        else if (t > 40 && t % 20 == 0)
        {
            yield return new UnitOrder { Kind = UnitOrder.Stick, UnitId = w.Units[a10].Id, X = 70 + (t % 30), Y = ((t / 20) % 7) - 3, ZDm = ((t / 20) % 5) - 2 };
        }
    }
}
