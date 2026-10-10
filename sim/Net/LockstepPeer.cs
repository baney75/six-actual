using System.Buffers.Binary;
using System.Text;
using SixActual.Sim.Battle;
using SixActual.Sim.State.Battle;

namespace SixActual.Sim.Net;

/// <summary>
/// One player's side of a real-network lockstep battle (NET-01 LAN / direct IP, D076). Star topology: peer 0 is the
/// host and relays every player's inputs to every client; clients talk only to the host.
///
/// Each peer seals its own inputs for tick t + InputDelay (an empty tick still seals). Input packets carry a window
/// of consecutive ticks for one player; the receiver acknowledges the highest contiguous tick it holds per player,
/// and every packet carries everything after that mark (at most 32 ticks), so the next packet repairs a lost one;
/// with nothing new to send, the window is resent every <see cref="ResendMs"/>. A peer
/// steps tick T once it holds every player's inputs for T, applied in player order, the same order on every peer.
/// Every <see cref="HashIntervalTicks"/> ticks clients send their state hash to the host, which compares it with
/// its own and counts mismatches as desyncs. The confirmed input log replays to the same state (NET-01 replays).
/// The peer has no clock or socket of its own: the caller passes the time and moves the bytes (see INetLink).
/// </summary>
public sealed class LockstepPeer
{
    public const byte MsgInputs = 4;
    public const byte MsgAck = 5;
    public const byte MsgHash = 6;
    const int MaxWindow = 32;

    readonly BattleRules _rules;
    readonly int _players;
    readonly int _delay;

    /// <summary>inputs[player][tick]; ticks below the delay are implicitly empty.</summary>
    readonly Dictionary<int, UnitOrder[]>[] _inputs;

    /// <summary>Highest tick held contiguously per player (from the delay up).</summary>
    readonly int[] _have;

    /// <summary>Host: per client, per player, the highest tick the client acknowledged. Client: [0][self] only.</summary>
    readonly int[][] _acked;

    readonly long[] _lastSentMs;
    readonly List<UnitOrder> _pending = [];
    readonly SortedDictionary<int, UnitOrder[]> _log = [];
    readonly Dictionary<int, string> _hostHashes = [];
    readonly Dictionary<int, List<(int Peer, string Hash)>> _clientHashes = [];
    readonly HashSet<(int Tick, int Peer)> _hashSeen = [];
    readonly List<(int To, byte[] Data)> _out = [];
    readonly int[] _pruned;
    readonly int[][] _sentThrough;
    readonly int[][] _ackSent;
    int _sealed;
    int _seq;

    public LockstepPeer(int self, int players, WorldState world, BattleRules rules, int inputDelayTicks = 4)
    {
        Self = self;
        _players = players;
        World = world;
        _rules = rules;
        _delay = inputDelayTicks;
        _inputs = Enumerable.Range(0, players).Select(_ => new Dictionary<int, UnitOrder[]>()).ToArray();
        _have = Enumerable.Repeat(inputDelayTicks - 1, players).ToArray();
        _acked = Enumerable.Range(0, players).Select(_ => Enumerable.Repeat(inputDelayTicks - 1, players).ToArray()).ToArray();
        _lastSentMs = Enumerable.Repeat(long.MinValue / 2, players).ToArray();
        _sealed = inputDelayTicks - 1;
        _pruned = Enumerable.Repeat(inputDelayTicks, players).ToArray();
        _sentThrough = Enumerable.Range(0, players).Select(_ => Enumerable.Repeat(inputDelayTicks - 1, players).ToArray()).ToArray();
        _ackSent = Enumerable.Range(0, players).Select(_ => new int[players]).ToArray();
    }

    public int Self { get; }

    public bool IsHost => Self == 0;

    public WorldState World { get; private set; }

    public int ResendMs { get; init; } = 20;

    public int HashIntervalTicks { get; init; } = 20;

    public int Desyncs { get; private set; }

    public int HashChecks { get; private set; }

    /// <summary>Ticks this peer could not step because a peer's inputs were missing (a stall, not an error).</summary>
    public int StalledPolls { get; private set; }

    public IReadOnlyDictionary<int, UnitOrder[]> Log => _log;

    /// <summary>Queues a local order; it executes InputDelay ticks after the tick it is sealed on.</summary>
    public void Issue(UnitOrder order) => _pending.Add(order);

    /// <summary>Highest tick for which this peer holds <paramref name="player"/>'s inputs contiguously.</summary>
    public int HeldThrough(int player) => _have[player];

    /// <summary>True when every player's inputs for the current tick are here.</summary>
    public bool Ready
    {
        get
        {
            var t = World.Tick;
            return t < _delay || _have.All(h => h >= t);
        }
    }

    /// <summary>Seals local inputs up to the current tick + delay, steps one tick if ready, and returns whether it did.</summary>
    public bool TryStep()
    {
        Seal();
        if (!Ready)
        {
            StalledPolls++;
            return false;
        }

        var t = World.Tick;
        var orders = new List<UnitOrder>();
        if (t >= _delay)
        {
            for (var p = 0; p < _players; p++)
            {
                orders.AddRange(_inputs[p][t]);
            }
        }

        // Orders a player may not give are dropped here, identically on every peer (NetOrders).
        var arr = orders.Where(o => NetOrders.Valid(World, o)).ToArray();
        _log[t] = arr;
        BattleWorld.Step(World, _rules, arr);
        Prune();
        if (World.Tick % HashIntervalTicks == 0)
        {
            var h = WorldHash.Of(World);
            if (IsHost)
            {
                _hostHashes[World.Tick] = h;
                CompareHashes(World.Tick);
                PruneHashes();
            }
            else
            {
                var bytes = Encoding.ASCII.GetBytes(h);
                var msg = new byte[5 + bytes.Length];
                msg[0] = MsgHash;
                BinaryPrimitives.WriteInt32LittleEndian(msg.AsSpan(1), World.Tick);
                bytes.CopyTo(msg, 5);
                _out.Add((0, msg));
            }
        }

        return true;
    }

    /// <summary>Drops inputs that are both executed and acknowledged by every peer this one sends them to.</summary>
    void Prune()
    {
        for (var p = 0; p < _players; p++)
        {
            var limit = World.Tick - 1;
            if (IsHost)
            {
                for (var c = 1; c < _players; c++)
                {
                    if (c != p)
                    {
                        limit = Math.Min(limit, _acked[c][p]);
                    }
                }
            }
            else if (p == Self)
            {
                limit = Math.Min(limit, _acked[0][Self]);
            }

            while (_pruned[p] <= limit)
            {
                _inputs[p].Remove(_pruned[p]);
                _pruned[p]++;
            }
        }
    }

    void Seal()
    {
        var target = World.Tick + _delay;
        while (_sealed < target)
        {
            _sealed++;
            var orders = _sealed == target ? _pending.Select(o => Stamp(o, _sealed)).ToArray() : [];
            if (_sealed == target)
            {
                _pending.Clear();
            }

            Accept(Self, _sealed, orders);
        }
    }

    UnitOrder Stamp(UnitOrder o, int tick)
    {
        _seq++;
        return new UnitOrder { Tick = tick, UnitId = o.UnitId, Kind = o.Kind, X = o.X, Y = o.Y, ZDm = o.ZDm, RadiusM = o.RadiusM, Damage = o.Damage, Player = Self, Card = o.Card };
    }

    void Accept(int player, int tick, UnitOrder[] orders)
    {
        if (tick <= _have[player] && _inputs[player].ContainsKey(tick))
        {
            return;
        }

        // Honest peers are never more than two input delays apart; anything further ahead is junk that would only grow memory.
        if (tick < _delay || tick < World.Tick || tick > World.Tick + (2 * _delay) + MaxWindow)
        {
            return;
        }

        _inputs[player][tick] = orders;
        while (_inputs[player].ContainsKey(_have[player] + 1))
        {
            _have[player]++;
        }
    }

    /// <summary>Packets due now: (destination peer, bytes). Clients send their own window and acks to the host; the
    /// host sends each client every other player's window and its acks.</summary>
    public List<(int To, byte[] Data)> Poll(long nowMs)
    {
        Seal();
        var due = new List<(int, byte[])>(_out);
        _out.Clear();
        var targets = IsHost ? Enumerable.Range(1, _players - 1) : [0];
        foreach (var to in targets)
        {
            // New inputs go out at once; everything unacknowledged is resent every ResendMs. Acks ride along.
            var resend = nowMs - _lastSentMs[to] >= ResendMs;
            var fresh = false;
            for (var p = 0; p < _players; p++)
            {
                fresh |= p != to && (IsHost || p == Self) && _have[p] > _sentThrough[to][p];
            }

            fresh |= !_ackSent[to].AsSpan().SequenceEqual(_have);
            if (!resend && !fresh)
            {
                continue;
            }

            if (resend)
            {
                _lastSentMs[to] = nowMs;
            }

            for (var p = 0; p < _players; p++)
            {
                if (p == to || (!IsHost && p != Self))
                {
                    continue;
                }

                // Always from the last acknowledged tick: the next packet repairs a lost one without waiting.
                var from = _acked[to][p] + 1;
                var through = Math.Min(_have[p], from + MaxWindow - 1);
                if (through >= from)
                {
                    due.Add((to, EncodeInputs(p, from, through)));
                    _sentThrough[to][p] = Math.Max(_sentThrough[to][p], through);
                }
            }

            due.Add((to, EncodeAck()));
            _have.CopyTo(_ackSent[to], 0);
        }

        return due;
    }

    byte[] EncodeInputs(int player, int from, int through)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(MsgInputs);
        bw.Write((byte)player);
        bw.Write(from);
        bw.Write((byte)(through - from + 1));
        for (var t = from; t <= through; t++)
        {
            var orders = _inputs[player][t];
            bw.Write((ushort)orders.Length);
            foreach (var o in orders)
            {
                bw.Write(o.Tick);
                bw.Write(o.UnitId);
                bw.Write(o.Kind);
                bw.Write(o.X);
                bw.Write(o.Y);
                bw.Write(o.ZDm);
                bw.Write(o.RadiusM);
                bw.Write(o.Damage);
                bw.Write(o.Player);
                bw.Write(o.Card);
            }
        }

        return ms.ToArray();
    }

    byte[] EncodeAck()
    {
        var msg = new byte[2 + (4 * _players)];
        msg[0] = MsgAck;
        msg[1] = (byte)_players;
        for (var p = 0; p < _players; p++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(msg.AsSpan(2 + (4 * p)), _have[p]);
        }

        return msg;
    }

    /// <summary>Handles one packet from peer <paramref name="from"/>. Malformed packets are dropped, not thrown.</summary>
    public void Receive(int from, ReadOnlySpan<byte> data)
    {
        if (data.Length < 2 || from < 0 || from >= _players || (!IsHost && from != 0) || (IsHost && from == 0))
        {
            return;
        }

        try
        {
            switch (data[0])
            {
                case MsgInputs:
                    ReceiveInputs(from, data.ToArray());
                    break;
                case MsgAck when data[1] == _players && data.Length == 2 + (4 * _players):
                    for (var p = 0; p < _players; p++)
                    {
                        // A peer cannot hold more than this peer sent it; a forged high ack would skip resends forever.
                        var v = Math.Min(BinaryPrimitives.ReadInt32LittleEndian(data[(2 + (4 * p))..]), Math.Min(_sentThrough[from][p], _have[p]));
                        _acked[from][p] = Math.Max(_acked[from][p], v);
                    }

                    break;
                case MsgHash when IsHost && data.Length > 5 && data.Length <= 133:
                    var tick = BinaryPrimitives.ReadInt32LittleEndian(data[1..]);
                    if (tick % HashIntervalTicks != 0 || tick > World.Tick + (2 * _delay) + MaxWindow || tick < World.Tick - (HashIntervalTicks * 50) || !_hashSeen.Add((tick, from)))
                    {
                        break;
                    }

                    if (!_clientHashes.TryGetValue(tick, out var list))
                    {
                        _clientHashes[tick] = list = [];
                    }

                    list.Add((from, Encoding.ASCII.GetString(data[5..])));
                    CompareHashes(tick);
                    break;
            }
        }
        catch (EndOfStreamException)
        {
        }
    }

    void ReceiveInputs(int from, byte[] data)
    {
        using var br = new BinaryReader(new MemoryStream(data));
        br.ReadByte();
        var player = br.ReadByte();

        // A client may only speak for itself; the host relays everyone.
        if (player >= _players || (IsHost && player != from) || player == Self)
        {
            return;
        }

        var start = br.ReadInt32();
        var count = br.ReadByte();
        for (var i = 0; i < count; i++)
        {
            var n = br.ReadUInt16();
            var orders = new UnitOrder[n];
            for (var k = 0; k < n; k++)
            {
                orders[k] = new UnitOrder
                {
                    Tick = br.ReadInt32(),
                    UnitId = br.ReadInt32(),
                    Kind = br.ReadInt32(),
                    X = br.ReadInt32(),
                    Y = br.ReadInt32(),
                    ZDm = br.ReadInt32(),
                    RadiusM = br.ReadInt32(),
                    Damage = br.ReadInt32(),
                    Player = br.ReadInt32(),
                    Card = br.ReadInt32(),
                };
            }

            // Orders stamped for another tick or player are stripped as forged, but the tick itself is always accepted:
            // dropping it would stall every peer. Forbidden kinds and other players' units are screened at execution
            // time by NetOrders, identically everywhere.
            Accept(player, start + i, orders.Where(o => o.Tick == start + i && o.Player == player).ToArray());
        }
    }

    void CompareHashes(int tick)
    {
        if (!_hostHashes.TryGetValue(tick, out var mine) || !_clientHashes.TryGetValue(tick, out var theirs))
        {
            return;
        }

        foreach (var (_, h) in theirs)
        {
            HashChecks++;
            if (h != mine)
            {
                Desyncs++;
            }
        }

        theirs.Clear();
    }

    /// <summary>Forgets hash bookkeeping older than 50 hash intervals (late hashes for those ticks are ignored).</summary>
    void PruneHashes()
    {
        var old = World.Tick - (HashIntervalTicks * 50);
        if (old <= 0 || World.Tick % HashIntervalTicks != 0)
        {
            return;
        }

        foreach (var t in _hostHashes.Keys.Where(t => t < old).ToList())
        {
            _hostHashes.Remove(t);
        }

        foreach (var t in _clientHashes.Keys.Where(t => t < old).ToList())
        {
            _clientHashes.Remove(t);
        }

        _hashSeen.RemoveWhere(k => k.Tick < old);
    }

    /// <summary>Replays the confirmed input log from a fresh world and returns the hash at this peer's tick.</summary>
    public string Replay(Func<WorldState> build)
    {
        var w = build();
        while (w.Tick < World.Tick)
        {
            BattleWorld.Step(w, _rules, _log.TryGetValue(w.Tick, out var o) ? o : []);
        }

        return WorldHash.Of(w);
    }
}
