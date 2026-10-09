using SixActual.Sim.Battle;
using SixActual.Sim.State.Battle;

namespace SixActual.Sim.Net;

/// <summary>Simulated network conditions for a lockstep session (NET-01, NET-02, FLY-07).</summary>
public sealed class NetConditions
{
    public int LatencyMs { get; init; }

    public int JitterMs { get; init; }

    public int LossPct { get; init; }

    /// <summary>Inputs issued on tick t execute on tick t + InputDelayTicks on every client.</summary>
    public int InputDelayTicks { get; init; } = 4;

    public ulong Seed { get; init; } = 1;

    /// <summary>Test-only fault injection: this client silently drops the first non-empty input it executes, so the
    /// desync detector can be shown to work (-1 = none).</summary>
    public int FaultClient { get; init; } = -1;
}

/// <summary>
/// Deterministic lockstep over a simulated lossy network. Every client runs its own copy of the battle and, each
/// tick, sends its inputs for tick t + delay to every peer (an empty packet still confirms the tick). A packet
/// arrives after latency +/- jitter or is lost; unacknowledged packets are resent every tick until acknowledged.
/// A client advances tick T only when it holds every client's inputs for T, applying them sorted by (player,
/// sequence), so all clients execute identical inputs in identical order. State hashes are compared every
/// hash_interval ticks; any mismatch is a desync. Replays are the confirmed input log; a reconnecting client
/// rebuilds its state by re-simulating that log from the start and then rejoins the exchange (the others stall
/// while it is away: plain lockstep has no host substitution).
/// </summary>
public sealed class LockstepSession
{
    const int TickMs = 50;

    readonly NetConditions _net;
    readonly Func<WorldState> _build;
    readonly BattleRules _rules;
    readonly Client[] _clients;
    readonly List<Packet> _inFlight = [];
    readonly SortedDictionary<int, List<UnitOrder>> _log = [];
    ulong _rng;
    int _nowMs;

    sealed class Client
    {
        public WorldState World = null!;

        public int Seq;

        /// <summary>Inputs this client has received (or issued) per tick, per sender.</summary>
        public Dictionary<int, Dictionary<int, List<UnitOrder>>> Inputs = [];

        /// <summary>Packets sent and not yet acknowledged, by (peer, tick), with the time last sent.</summary>
        public Dictionary<(int Peer, int Tick), (List<UnitOrder> Orders, int SentMs)> Unacked = [];

        public int StallMs;

        public bool Connected = true;

        /// <summary>Highest tick whose inputs this client has sent.</summary>
        public int SentThrough = -1;

        public int LastHashedTick;
    }

    readonly struct Packet
    {
        public int From { get; init; }

        public int To { get; init; }

        public int Tick { get; init; }

        public bool Ack { get; init; }

        public int ArriveMs { get; init; }

        public UnitOrder[] Orders { get; init; }
    }

    public int Desyncs { get; private set; }

    public int HashChecks { get; private set; }

    public int MaxStallMs { get; private set; }

    public int PacketsLost { get; private set; }

    public int Reconnects { get; private set; }

    public int HashIntervalTicks { get; init; } = 20;

    public LockstepSession(int clients, Func<WorldState> build, BattleRules rules, NetConditions net)
    {
        _net = net;
        _build = build;
        _rules = rules;
        _rng = net.Seed == 0 ? 0x9E3779B97F4A7C15UL : net.Seed;
        _clients = Enumerable.Range(0, clients).Select(_ => new Client { World = build() }).ToArray();
    }

    public WorldState World(int client) => _clients[client].World;

    public IReadOnlyDictionary<int, List<UnitOrder>> Log => _log;

    ulong Next()
    {
        var x = _rng;
        x ^= x >> 12;
        x ^= x << 25;
        x ^= x >> 27;
        _rng = x;
        return x * 0x2545F4914F6CDD1DUL;
    }

    /// <summary>Runs until every connected client has simulated `ticks` ticks. `issue(client, world)` returns the
    /// inputs that client's player issues on its current tick (they execute InputDelayTicks later).</summary>
    public void Run(int ticks, Func<int, WorldState, IEnumerable<UnitOrder>> issue, int dropClient = -1, int dropAtTick = -1, int rejoinAfterMs = -1)
    {
        var droppedAtMs = -1;
        var guard = 0;
        while (_clients.Where(c => c.Connected).Any(c => c.World.Tick < ticks) || (dropClient >= 0 && !_clients[dropClient].Connected))
        {
            if (++guard > ticks * 400)
            {
                throw new InvalidOperationException("lockstep made no progress");
            }

            _nowMs += TickMs;
            for (var c = 0; c < _clients.Length; c++)
            {
                var cl = _clients[c];
                if (c == dropClient && cl.Connected && droppedAtMs < 0 && dropAtTick >= 0 && cl.World.Tick >= dropAtTick)
                {
                    cl.Connected = false;
                    droppedAtMs = _nowMs;
                    Reconnects++;
                }

                if (c == dropClient && !cl.Connected && rejoinAfterMs >= 0 && _nowMs - droppedAtMs >= rejoinAfterMs)
                {
                    Rejoin(c);
                }

                if (!cl.Connected)
                {
                    continue;
                }

                // Send this client's inputs for the next tick in its window (one tick of inputs per frame).
                var t = cl.World.Tick;
                var target = t + _net.InputDelayTicks;
                if (cl.SentThrough < target)
                {
                    var orders = cl.SentThrough < _net.InputDelayTicks - 1
                        ? []
                        : issue(c, cl.World).Select(o => Stamp(o, target, c, ref cl.Seq)).ToList();
                    for (var tick = cl.SentThrough + 1; tick <= target; tick++)
                    {
                        Broadcast(c, tick, tick == target ? orders : []);
                    }

                    cl.SentThrough = target;
                }

                Resend(c);
            }

            Deliver();
            foreach (var cl in _clients.Where(x => x.Connected))
            {
                if (cl.World.Tick < ticks && Ready(cl, cl.World.Tick))
                {
                    Step(cl, Array.IndexOf(_clients, cl));
                    cl.StallMs = 0;
                }
                else if (cl.World.Tick < ticks)
                {
                    cl.StallMs += TickMs;
                    MaxStallMs = Math.Max(MaxStallMs, cl.StallMs);
                }
            }

            CompareHashes();
        }
    }

    static UnitOrder Stamp(UnitOrder o, int tick, int client, ref int seq)
    {
        seq++;
        return new UnitOrder
        {
            Tick = tick,
            UnitId = o.UnitId,
            Kind = o.Kind,
            X = o.X,
            Y = o.Y,
            ZDm = o.ZDm,
            RadiusM = o.RadiusM,
            Damage = o.Damage,
            Player = client,
            Card = o.Card,
        };
    }

    void Broadcast(int from, int tick, List<UnitOrder> orders)
    {
        var cl = _clients[from];
        Inputs(cl, tick)[from] = orders;
        for (var to = 0; to < _clients.Length; to++)
        {
            if (to == from)
            {
                continue;
            }

            cl.Unacked[(to, tick)] = (orders, _nowMs);
            Send(from, to, tick, false, orders);
        }
    }

    /// <summary>Resends packets unacknowledged for a round trip (2 x (latency + jitter) + one frame).</summary>
    void Resend(int from)
    {
        var timeout = (2 * (_net.LatencyMs + _net.JitterMs)) + TickMs;
        var cl = _clients[from];
        foreach (var (key, (orders, sent)) in cl.Unacked.OrderBy(k => k.Key.Peer).ThenBy(k => k.Key.Tick).ToList())
        {
            if (_nowMs - sent >= timeout)
            {
                cl.Unacked[key] = (orders, _nowMs);
                Send(from, key.Peer, key.Tick, false, orders);
            }
        }
    }

    void Send(int from, int to, int tick, bool ack, List<UnitOrder> orders)
    {
        if ((int)(Next() % 100UL) < _net.LossPct)
        {
            PacketsLost++;
            return;
        }

        var jitter = _net.JitterMs == 0 ? 0 : (int)(Next() % (ulong)((2 * _net.JitterMs) + 1)) - _net.JitterMs;
        _inFlight.Add(new Packet { From = from, To = to, Tick = tick, Ack = ack, ArriveMs = _nowMs + Math.Max(0, _net.LatencyMs + jitter), Orders = [.. orders] });
    }

    void Deliver()
    {
        var arrived = _inFlight.Where(p => p.ArriveMs <= _nowMs).OrderBy(p => p.ArriveMs).ThenBy(p => p.From).ThenBy(p => p.To).ThenBy(p => p.Tick).ToList();
        _inFlight.RemoveAll(p => p.ArriveMs <= _nowMs);
        foreach (var p in arrived)
        {
            var to = _clients[p.To];
            if (!to.Connected)
            {
                continue;
            }

            if (p.Ack)
            {
                to.Unacked.Remove((p.From, p.Tick));
                continue;
            }

            Inputs(to, p.Tick)[p.From] = [.. p.Orders];
            Send(p.To, p.From, p.Tick, true, []);
        }
    }

    static Dictionary<int, List<UnitOrder>> Inputs(Client c, int tick)
    {
        if (!c.Inputs.TryGetValue(tick, out var d))
        {
            c.Inputs[tick] = d = [];
        }

        return d;
    }

    bool Ready(Client c, int tick)
    {
        if (tick < _net.InputDelayTicks)
        {
            return true;
        }

        return c.Inputs.TryGetValue(tick, out var d) && d.Count == _clients.Length;
    }

    bool _faultUsed;

    void Step(Client c, int index)
    {
        var tick = c.World.Tick;
        var orders = new List<UnitOrder>();
        if (c.Inputs.TryGetValue(tick, out var d))
        {
            foreach (var kv in d.OrderBy(k => k.Key))
            {
                orders.AddRange(kv.Value);
            }

            c.Inputs.Remove(tick);
        }

        if (!_log.ContainsKey(tick))
        {
            _log[tick] = orders;
        }

        if (index == _net.FaultClient && !_faultUsed && orders.Count > 0)
        {
            _faultUsed = true;
            BattleWorld.Step(c.World, _rules, orders.Skip(1).ToArray());
            return;
        }

        BattleWorld.Step(c.World, _rules, orders.ToArray());
    }

    readonly SortedDictionary<int, List<string>> _hashes = [];

    /// <summary>Each client records its state hash at every hash_interval tick it reaches; once every connected client
    /// has recorded a tick, the hashes are compared.</summary>
    void CompareHashes()
    {
        foreach (var c in _clients.Where(x => x.Connected))
        {
            var tick = c.World.Tick;
            if (tick == 0 || tick % HashIntervalTicks != 0 || c.LastHashedTick == tick)
            {
                continue;
            }

            c.LastHashedTick = tick;
            if (!_hashes.TryGetValue(tick, out var list))
            {
                _hashes[tick] = list = [];
            }

            list.Add(WorldHash.Of(c.World));
        }

        var connected = _clients.Count(x => x.Connected);
        foreach (var tick in _hashes.Keys.ToList())
        {
            if (_hashes[tick].Count < connected)
            {
                continue;
            }

            HashChecks++;
            if (_hashes[tick].Distinct().Count() > 1)
            {
                Desyncs++;
            }

            _hashes.Remove(tick);
        }
    }

    /// <summary>A dropped client comes back: it rebuilds its world from the confirmed input log, then resumes the
    /// exchange from the current tick.</summary>
    void Rejoin(int c)
    {
        var cl = _clients[c];
        var host = _clients.First(x => x.Connected);
        var w = _build();
        while (w.Tick < host.World.Tick)
        {
            var orders = _log.TryGetValue(w.Tick, out var o) ? o.ToArray() : [];
            BattleWorld.Step(w, _rules, orders);
        }

        cl.World = w;
        cl.LastHashedTick = w.Tick;
        cl.Inputs = host.Inputs.ToDictionary(k => k.Key, k => k.Value.ToDictionary(x => x.Key, x => x.Value.ToList()));
        cl.Unacked.Clear();
        cl.SentThrough = Math.Max(host.SentThrough - 1, w.Tick);
        cl.Connected = true;
        foreach (var other in _clients)
        {
            foreach (var key in other.Unacked.Keys.Where(k => k.Peer == c).ToList())
            {
                other.Unacked.Remove(key);
            }
        }

        // Resend the rejoining client's (empty) inputs for ticks the others still wait on.
        for (var tick = w.Tick; tick <= cl.SentThrough; tick++)
        {
            if (!host.Inputs.TryGetValue(tick, out var d) || !d.ContainsKey(c))
            {
                Broadcast(c, tick, []);
            }
        }
    }

    /// <summary>Replays the confirmed input log on a fresh world and returns its hash after `ticks` ticks.</summary>
    public string Replay(int ticks)
    {
        var w = _build();
        while (w.Tick < ticks)
        {
            BattleWorld.Step(w, _rules, _log.TryGetValue(w.Tick, out var o) ? o.ToArray() : []);
        }

        return WorldHash.Of(w);
    }
}
