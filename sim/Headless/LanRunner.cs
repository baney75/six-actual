using System.Diagnostics;
using System.Text.Json;
using SixActual.Sim.Battle;
using SixActual.Sim.Net;
using SixActual.Sim.State.Battle;

namespace SixActual.Headless;

/// <summary>
/// NET-01 LAN play over real UDP sockets (D076). G4 scenarios of type "lan" run a host and its clients in this
/// process, each on its own localhost socket, through the real handshake and <see cref="LockstepPeer"/>, with
/// optional datagram loss. `--net-host` and `--net-bot` run one side each, so the game can be tested against them.
/// </summary>
public static class LanRunner
{
    public static bool IsLanScenario(string file)
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(file));
        return doc.RootElement.TryGetProperty("type", out var t) && t.GetString() == "lan";
    }

    sealed class Result
    {
        public List<LockstepPeer> Peers = [];
        public List<LanEndpoint> Endpoints = [];
        public long WallMs;
        public bool TimedOut;
        public NetSetup Setup = null!;
    }

    static Result Run(BattleData data, BattleRules rules, NetSetup proto, int ticks, int lossPct, string script, int timeoutS)
    {
        var r = new Result();
        var clock = Stopwatch.StartNew();
        using var host = new LanHost(0, proto.Peers, "host");
        var clients = Enumerable.Range(1, proto.Peers - 1).Select(i => new LanClient("127.0.0.1", host.Port, $"bot{i}", isAi: true)).ToList();
        host.LossPct = lossPct;
        clients.ForEach(c => c.LossPct = lossPct);
        r.Endpoints.Add(host);
        r.Endpoints.AddRange(clients);

        // Lobby: every client must be welcomed before the host starts.
        while (host.Players.Count < proto.Peers || clients.Any(c => c.Slot < 0))
        {
            host.Pump(clock.ElapsedMilliseconds);
            clients.ForEach(c => c.Pump(clock.ElapsedMilliseconds));
            if (clock.ElapsedMilliseconds > 10_000)
            {
                throw new InvalidOperationException("LAN lobby: clients were not welcomed in 10 s");
            }

            Thread.Sleep(1);
        }

        var setup = new NetSetup
        {
            Seed = proto.Seed,
            PerSide = proto.PerSide,
            Deck0 = proto.Deck0,
            Deck1 = proto.Deck1,
            StartDp = proto.StartDp,
            Map = proto.Map,
            MapSha = proto.Map is null ? null : NetSetup.MapHash(data.Maps.First(m => m.Id == proto.Map)),
            Peers = proto.Peers,
            Humans = proto.Humans,
            InputDelayTicks = proto.InputDelayTicks,
            Names = [.. host.Players],
        };
        r.Setup = setup;
        host.Start(setup.ToBytes());
        host.Peer = new LockstepPeer(0, setup.Peers, setup.Build(data, rules), rules, setup.InputDelayTicks);
        r.Peers.Add(host.Peer);
        var peers = new LockstepPeer?[setup.Peers];
        var scripted = Enumerable.Repeat(-1, setup.Peers).ToArray();
        peers[0] = host.Peer;
        while (true)
        {
            var now = clock.ElapsedMilliseconds;
            host.Pump(now);
            foreach (var c in clients)
            {
                c.Pump(now);
                if (c.Peer is null && c.Setup is not null)
                {
                    // Each client builds its own world from the bytes it received, not from the host's object.
                    var s = NetSetup.FromBytes(c.Setup);
                    c.Peer = new LockstepPeer(c.Slot, s.Peers, s.Build(data, rules), rules, s.InputDelayTicks);
                    peers[c.Slot] = c.Peer;
                }
            }

            foreach (var p in peers)
            {
                for (var k = 0; p is not null && k < 4 && p.World.Tick < ticks; k++)
                {
                    // Each tick's scripted orders are issued once, however many polls it takes to step it.
                    if (scripted[p.Self] != p.World.Tick)
                    {
                        scripted[p.Self] = p.World.Tick;
                        foreach (var o in NetRunner.Script(p.Self, p.World, script))
                        {
                            p.Issue(o);
                        }
                    }

                    if (!p.TryStep())
                    {
                        break;
                    }
                }
            }

            if (peers.All(p => p is not null && p.World.Tick >= ticks))
            {
                // Let the last hashes and acks cross before stopping.
                for (var i = 0; i < 50; i++)
                {
                    host.Pump(clock.ElapsedMilliseconds);
                    clients.ForEach(c => c.Pump(clock.ElapsedMilliseconds));
                    Thread.Sleep(1);
                }

                break;
            }

            if (clock.ElapsedMilliseconds > timeoutS * 1000L)
            {
                r.TimedOut = true;
                break;
            }
        }

        r.Peers = peers.Where(p => p is not null).Select(p => p!).ToList();
        r.WallMs = clock.ElapsedMilliseconds;
        clients.ForEach(c => c.Dispose());
        return r;
    }

    public static bool RunScenario(string file, BattleData data, BattleRules rules)
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(file));
        var e = doc.RootElement;
        var id = e.GetProperty("id").GetString()!;
        var req = e.GetProperty("requirement").GetString()!;
        var ticks = e.GetProperty("ticks").GetInt32();
        var proto = new NetSetup
        {
            Seed = e.GetProperty("seed").GetUInt64(),
            PerSide = e.GetProperty("players_per_side").GetInt32(),
            Peers = e.GetProperty("peers").GetInt32(),
            Deck0 = e.GetProperty("deck0").GetString()!,
            Deck1 = e.GetProperty("deck1").GetString()!,
            Map = e.TryGetProperty("map", out var m) ? m.GetString() : null,
            Humans = e.TryGetProperty("humans", out var hu) ? hu.EnumerateArray().Select(x => x.GetInt32()).ToArray() : [],
            InputDelayTicks = e.GetProperty("input_delay_ticks").GetInt32(),
        };
        var r = Run(data, rules, proto, ticks, e.GetProperty("loss_pct").GetInt32(), e.TryGetProperty("script", out var sc) ? sc.GetString()! : "moves", e.GetProperty("timeout_s").GetInt32());
        var finals = r.Peers.Select(p => WorldHash.Of(p.World)).Distinct().ToList();
        var host = r.Peers[0];
        var allOk = !r.TimedOut && r.Peers.Count == proto.Peers;
        Console.WriteLine($"{(allOk ? "pass" : "FAIL")} {id} [{req}] completed :: {r.Peers.Count(p => p.World.Tick >= ticks)}/{proto.Peers} peers reached tick {ticks} over UDP (slowest at {(r.Peers.Count == 0 ? 0 : r.Peers.Min(p => p.World.Tick))}) in {r.WallMs} ms, timed out {r.TimedOut}");
        foreach (var c in e.GetProperty("assert").EnumerateArray())
        {
            var value = c.TryGetProperty("value", out var v) ? v.GetInt32() : 0;
            bool ok;
            string detail;
            switch (c.GetProperty("check").GetString())
            {
                case "desyncs":
                    ok = host.Desyncs == value && finals.Count == 1 && r.Peers.All(p => p.World.Tick == ticks);
                    detail = $"{host.Desyncs} desyncs in {host.HashChecks} host hash checks; final hashes distinct {finals.Count}";
                    break;
                case "hash_checks_at_least":
                    ok = host.HashChecks >= value;
                    detail = $"{host.HashChecks} hash checks";
                    break;
                case "replay_matches":
                    var replays = r.Peers.Select(p => p.Replay(() => r.Setup.Build(data, rules))).Distinct().ToList();
                    ok = finals.Count == 1 && replays.Count == 1 && replays[0] == finals[0];
                    detail = $"replays {string.Join(",", replays.Select(x => x[..12]))} vs peers {finals[0][..12]}";
                    break;
                case "datagrams_dropped_at_least":
                    var dropped = r.Endpoints.Sum(x => x.Dropped);
                    ok = dropped >= value;
                    detail = $"{dropped} of {r.Endpoints.Sum(x => x.Sent)} datagrams dropped on purpose";
                    break;
                case "orders_exchanged_at_least":
                    var orders = host.Log.Values.Sum(l => l.Length);
                    var fromClients = host.Log.Values.Sum(l => l.Count(o => o.Player != 0));
                    ok = orders >= value && fromClients > 0;
                    detail = $"{orders} orders executed, {fromClients} from clients";
                    break;
                case "battle_progressed":
                    var w0 = host.World;
                    var dead = w0.Units.Count(u => u.Alive == 0);
                    ok = w0.Units.Length >= value && dead > 0;
                    detail = $"{w0.Units.Length} units deployed, {dead} destroyed";
                    break;
                default:
                    throw new InvalidDataException($"unknown lan check {c.GetProperty("check").GetString()}");
            }

            allOk &= ok;
            Console.WriteLine($"{(ok ? "pass" : "FAIL")} {id} [{req}] {c.GetProperty("check").GetString()} :: {detail}");
        }

        return allOk;
    }

    /// <summary>`--net-bot host port [name]`: joins a LAN game as an AI player and plays it to the end (or until the
    /// host goes quiet for 30 s). Used by the G13 LAN smoke test and for manual testing.</summary>
    public static int Bot(string[] args, BattleData data, BattleRules rules)
    {
        var host = args[0];
        var port = int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture);
        using var c = new LanClient(host, port, args.Length > 2 ? args[2] : "bot", isAi: true);
        var clock = Stopwatch.StartNew();
        var lastTick = -1;
        var lastProgressMs = 0L;
        while (true)
        {
            c.Pump(clock.ElapsedMilliseconds);
            if (c.Refused)
            {
                Console.WriteLine("bot: lobby full");
                return 1;
            }

            if (c.Peer is null && c.Setup is not null && c.RefuseReason is null)
            {
                var s = NetSetup.FromBytes(c.Setup);
                try
                {
                    c.Peer = new LockstepPeer(c.Slot, s.Peers, s.Build(data, rules), rules, s.InputDelayTicks);
                    Console.WriteLine($"bot: slot {c.Slot}, {s.PerSide}v{s.PerSide}, seed {s.Seed}, map {s.Map ?? "generated"}");
                }
                catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
                {
                    c.RefuseReason = ex.Message;
                    Console.WriteLine($"bot: refused setup: {ex.Message}");
                    var until = clock.ElapsedMilliseconds + 1500;
                    while (clock.ElapsedMilliseconds < until)
                    {
                        c.Pump(clock.ElapsedMilliseconds);
                        Thread.Sleep(5);
                    }

                    return 1;
                }
            }

            // Real time: one tick per 50 ms at most.
            while (c.Peer is not null && c.Peer.World.Tick * 50L < clock.ElapsedMilliseconds && c.Peer.TryStep())
            {
            }

            var tick = c.Peer?.World.Tick ?? -1;
            if (tick != lastTick)
            {
                lastTick = tick;
                lastProgressMs = clock.ElapsedMilliseconds;
                if (tick > 0 && tick % 200 == 0)
                {
                    Console.WriteLine($"bot: tick {tick} hash {WorldHash.Of(c.Peer!.World)[..12]}");
                }
            }

            // Before the battle starts the bot waits up to 10 minutes in the lobby; once running, 30 s without a tick ends it.
            if (c.Peer?.World.Winner >= 0 || clock.ElapsedMilliseconds - lastProgressMs > (c.Peer is null ? 600_000 : 30_000))
            {
                Console.WriteLine($"bot: stopped at tick {tick}");
                return 0;
            }

            Thread.Sleep(2);
        }
    }
}
