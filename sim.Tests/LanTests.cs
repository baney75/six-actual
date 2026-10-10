using System.Diagnostics;
using SixActual.Sim.Battle;
using SixActual.Sim.Net;

namespace SixActual.Sim.Tests;

/// <summary>NET-01 LAN play: real UDP sockets on localhost, the real handshake, and <see cref="LockstepPeer"/>.</summary>
public class LanTests
{
    static readonly string Root = FindRepoRoot();

    static readonly BattleData Defs = BattleData.Load(Root);

    static readonly BattleRules Rules = BattleRules.Load(Path.Combine(Root, "data", "rules.json"));

    static NetSetup Setup(int peers) => new() { Seed = 5, PerSide = 2, Deck0 = "us_1989_armored", Deck1 = "su_1989_motor_rifle", Peers = peers, Humans = [0] };

    [Fact]
    public void Three_peers_over_lossy_udp_stay_in_sync_and_replay()
    {
        using var host = new LanHost(0, 3, "host") { LossPct = 10 };
        var clients = new[] { new LanClient("127.0.0.1", host.Port, "a") { LossPct = 10 }, new LanClient("127.0.0.1", host.Port, "b") { LossPct = 10 } };
        var clock = Stopwatch.StartNew();
        while (host.Players.Count < 3)
        {
            host.Pump(clock.ElapsedMilliseconds);
            Array.ForEach(clients, c => c.Pump(clock.ElapsedMilliseconds));
            Assert.True(clock.ElapsedMilliseconds < 10_000, "lobby timed out");
            Thread.Sleep(1);
        }

        Assert.Equal(["host", "a", "b"], host.Players);
        var setup = Setup(3);
        host.Start(setup.ToBytes());
        host.Peer = new LockstepPeer(0, 3, setup.Build(Defs, Rules), Rules) { HashIntervalTicks = 20 };
        const int ticks = 600;
        while (host.Peer.World.Tick < ticks || clients.Any(c => c.Peer is null || c.Peer.World.Tick < ticks))
        {
            var now = clock.ElapsedMilliseconds;
            host.Pump(now);
            foreach (var c in clients)
            {
                c.Pump(now);
                if (c.Peer is null && c.Setup is not null)
                {
                    var s = NetSetup.FromBytes(c.Setup);
                    c.Peer = new LockstepPeer(c.Slot, s.Peers, s.Build(Defs, Rules), Rules) { HashIntervalTicks = 20 };
                }
            }

            foreach (var p in clients.Select(c => c.Peer).Prepend(host.Peer))
            {
                if (p is null || p.World.Tick >= ticks)
                {
                    continue;
                }

                if (p.World.Tick % 50 == 7)
                {
                    var mine = p.World.Units.FirstOrDefault(u => u.Player == p.Self && u.Alive != 0);
                    if (mine.Alive != 0)
                    {
                        p.Issue(new UnitOrder { Kind = UnitOrder.Move, UnitId = mine.Id, X = 4000, Y = 4000 + (p.Self * 100) });
                    }
                }

                p.TryStep();
            }

            Assert.True(clock.ElapsedMilliseconds < 60_000, $"stalled at host tick {host.Peer.World.Tick}");
        }

        for (var i = 0; i < 40; i++)
        {
            host.Pump(clock.ElapsedMilliseconds);
            Array.ForEach(clients, c => c.Pump(clock.ElapsedMilliseconds));
            Thread.Sleep(1);
        }

        var hashes = clients.Select(c => WorldHash.Of(c.Peer!.World)).Append(WorldHash.Of(host.Peer.World)).Distinct().ToList();
        Assert.Single(hashes);
        Assert.Equal(0, host.Peer.Desyncs);
        Assert.True(host.Peer.HashChecks >= 40, $"{host.Peer.HashChecks} hash checks");
        Assert.True(host.Dropped + clients.Sum(c => c.Dropped) > 0, "loss injection never dropped a datagram");
        Assert.Contains(host.Peer.Log.Values.SelectMany(o => o), o => o.Player == 2 && o.Kind == UnitOrder.Move);
        Assert.Equal(hashes[0], clients[1].Peer!.Replay(() => setup.Build(Defs, Rules)));
        Array.ForEach(clients, c => c.Dispose());
    }

    [Fact]
    public void A_peer_ignores_inputs_forged_for_another_player_and_malformed_packets()
    {
        var setup = Setup(3);
        var host = new LockstepPeer(0, 3, setup.Build(Defs, Rules), Rules);
        var liar = new LockstepPeer(1, 3, setup.Build(Defs, Rules), Rules);
        liar.TryStep();
        var genuine = liar.Poll(0).First(p => p.Data[0] == LockstepPeer.MsgInputs).Data;

        // The genuine packet is accepted for player 1; the same bytes relabelled as player 2 must not be.
        host.Receive(1, genuine);
        var forged = (byte[])genuine.Clone();
        forged[1] = 2;
        host.Receive(1, forged);
        host.Receive(1, new byte[] { LockstepPeer.MsgInputs, 1, 9 });
        host.Receive(2, new byte[] { LockstepPeer.MsgAck, 3, 0 });
        host.Receive(7, genuine);
        Assert.True(host.HeldThrough(1) >= 4, $"genuine inputs not accepted: {host.HeldThrough(1)}");
        Assert.Equal(3, host.HeldThrough(2));
        for (var i = 0; i < 4; i++)
        {
            Assert.True(host.TryStep());
        }

        Assert.False(host.TryStep());
        Assert.Equal(4, host.World.Tick);
    }

    [Fact]
    public void A_forged_ack_cannot_stop_the_host_resending()
    {
        var setup = Setup(2);
        var host = new LockstepPeer(0, 2, setup.Build(Defs, Rules), Rules);
        host.TryStep();
        var ack = new byte[2 + 8];
        ack[0] = LockstepPeer.MsgAck;
        ack[1] = 2;
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(ack.AsSpan(2), int.MaxValue);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(ack.AsSpan(6), int.MaxValue);
        host.Receive(1, ack);
        var sent = host.Poll(1000);
        Assert.Contains(sent, p => p.Data[0] == LockstepPeer.MsgInputs);
    }

    [Fact]
    public void Orders_a_player_may_not_give_are_dropped_on_every_peer()
    {
        // Both sides AI-commanded, so the host has units of its own for the client to try to steal.
        var setup = new NetSetup { Seed = 5, PerSide = 1, Deck0 = "us_1989_armored", Deck1 = "su_1989_motor_rifle", Peers = 2 };
        var host = new LockstepPeer(0, 2, setup.Build(Defs, Rules), Rules) { HashIntervalTicks = 20 };
        var client = new LockstepPeer(1, 2, setup.Build(Defs, Rules), Rules) { HashIntervalTicks = 20 };
        long now = 0;
        var guard = 0;
        while (host.World.Tick < 400 || client.World.Tick < 400)
        {
            Assert.True(++guard < 20_000, $"stalled: host {host.World.Tick} (holds {host.HeldThrough(0)}/{host.HeldThrough(1)}), client {client.World.Tick} (holds {client.HeldThrough(0)}/{client.HeldThrough(1)})");
            now += 50;
            if (client.World.Tick == 300)
            {
                var hostUnit = client.World.Units.First(u => u.Player == 0 && u.Alive != 0);
                client.Issue(new UnitOrder { Kind = UnitOrder.SetIntent, Card = 100000, Damage = 0, X = 50 });
                client.Issue(new UnitOrder { Kind = UnitOrder.Move, UnitId = hostUnit.Id, X = 10, Y = 10 });
                client.Issue(new UnitOrder { Kind = UnitOrder.Blast, X = 4000, Y = 4000, RadiusM = 500, Damage = 9999 });
                client.Issue(new UnitOrder { Kind = UnitOrder.Support, Card = -5, X = 4000, Y = 4000 });
            }

            foreach (var (_, d) in host.Poll(now))
            {
                client.Receive(0, d);
            }

            foreach (var (_, d) in client.Poll(now))
            {
                host.Receive(1, d);
            }

            host.TryStep();
            client.TryStep();
        }

        Assert.Equal(WorldHash.Of(host.World), WorldHash.Of(client.World));
        Assert.Equal(0, host.Desyncs);
        Assert.DoesNotContain(host.Log.Values.SelectMany(x => x), o => o.Player == 1 && (o.Kind is UnitOrder.SetIntent or UnitOrder.Move or UnitOrder.Blast or UnitOrder.Support));
    }

    [Fact]
    public void A_client_whose_welcome_was_lost_takes_the_setup_only_after_it_has_a_slot()
    {
        using var host = new LanHost(0, 2, "host") { LossPct = 100 };
        using var client = new LanClient("127.0.0.1", host.Port, "late");
        var clock = Stopwatch.StartNew();
        while (host.Players.Count < 2)
        {
            host.Pump(clock.ElapsedMilliseconds);
            client.Pump(clock.ElapsedMilliseconds);
            Assert.True(clock.ElapsedMilliseconds < 5000);
            Thread.Sleep(1);
        }

        // The Welcome was dropped; the host starts anyway, and the Setup now gets through.
        Assert.Equal(-1, client.Slot);
        host.LossPct = 0;
        host.Start(Setup(2).ToBytes());
        while (client.Setup is null)
        {
            host.Pump(clock.ElapsedMilliseconds);
            client.Pump(clock.ElapsedMilliseconds);
            Assert.True(clock.ElapsedMilliseconds < 10_000, "setup never arrived");
            Thread.Sleep(1);
        }

        Assert.Equal(1, client.Slot);
    }

    [Fact]
    public void A_map_missing_or_different_on_a_peer_fails_with_a_reason()
    {
        var fulda = Defs.Maps.First(m => m.Id == "fulda");
        var good = new NetSetup { Seed = 5, PerSide = 1, Deck0 = "us_1989_armored", Deck1 = "su_1989_motor_rifle", Peers = 2, Map = "fulda", MapSha = NetSetup.MapHash(fulda) };
        Assert.NotNull(good.Build(Defs, Rules));
        var different = new NetSetup { Seed = 5, PerSide = 1, Deck0 = "us_1989_armored", Deck1 = "su_1989_motor_rifle", Peers = 2, Map = "fulda", MapSha = "00" };
        Assert.Contains("differs", Assert.Throws<InvalidDataException>(() => different.Build(Defs, Rules)).Message);
        var missing = new NetSetup { Seed = 5, PerSide = 1, Deck0 = "us_1989_armored", Deck1 = "su_1989_motor_rifle", Peers = 2, Map = "atlantis" };
        Assert.Contains("not installed", Assert.Throws<InvalidDataException>(() => missing.Build(Defs, Rules)).Message);
    }

    [Fact]
    public void The_host_detects_a_client_whose_state_diverges()
    {
        var setup = Setup(2);
        var host = new LockstepPeer(0, 2, setup.Build(Defs, Rules), Rules) { HashIntervalTicks = 20 };
        var client = new LockstepPeer(1, 2, setup.Build(Defs, Rules), Rules) { HashIntervalTicks = 20 };
        long now = 0;
        void Exchange()
        {
            now += 100;
            foreach (var (_, d) in host.Poll(now))
            {
                client.Receive(0, d);
            }

            foreach (var (_, d) in client.Poll(now))
            {
                host.Receive(1, d);
            }
        }

        var guard = 0;
        while (host.World.Tick < 200 || client.World.Tick < 200)
        {
            Assert.True(++guard < 20_000, "stalled");
            Exchange();
            host.TryStep();
            client.TryStep();
        }

        Exchange();
        Assert.Equal(0, host.Desyncs);
        Assert.True(host.HashChecks >= 9);
        client.World.Players[1].Dp += 1;
        while (host.World.Tick < 300 || client.World.Tick < 300)
        {
            Exchange();
            host.TryStep();
            client.TryStep();
        }

        Exchange();
        Assert.True(host.Desyncs >= 4, $"{host.Desyncs} desyncs after corrupting the client");
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
