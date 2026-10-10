using System.Linq;
using Godot;
using SixActual.Sim.Net;

/// <summary>NET-01 / UX-01 lobby. A local lobby against AI slots, plus LAN / direct-IP play (D076): host a game on
/// UDP port 47610 (or `--lan-port N`), or join one by address. Steam lobbies replace the transport later. The host's
/// battle settings (decks, players per side, map, seed) go to every client in the Setup message.</summary>
public partial class LobbyScreen : Node
{
    int _ai;
    bool _ready;
    Label _slots = null!;
    Label _lan = null!;
    LineEdit _address = null!;
    int _seenPlayers = 1;
    bool _joinedLogged;

    static int Port()
    {
        var args = OS.GetCmdlineUserArgs();
        var i = System.Array.IndexOf(args, "--lan-port");
        return i >= 0 && i + 1 < args.Length ? int.Parse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture) : Lan.DefaultPort;
    }

    public override void _Ready()
    {
        var box = Ui.Screen(this, "LOBBY", "Local lobby against AI, or LAN / direct-IP play. You are slot 1.");
        _slots = new Label();
        box.AddChild(_slots);
        Ui.Button(box, "AddAi", "Add AI opponent", () => { _ai++; UxLog.Event("lobby_slot_added", ("slots", 1 + _ai)); Refresh(); });
        Ui.Button(box, "Ready", "Ready", () => { _ready = true; UxLog.Event("lobby_ready"); Refresh(); });
        Ui.Button(box, "StartLobby", "Start", () =>
        {
            if (!_ready || _ai == 0)
            {
                UxLog.Event("lobby_start_refused", ("ready", _ready), ("ai", _ai));
                return;
            }

            UxLog.Event("lobby_started", ("players", 1 + _ai));
            GameSession.Origin = "lobby";
            Ui.Go(this, new DeckScreen());
        });

        box.AddChild(new Label { Text = "LAN / direct IP" });
        _lan = new Label { Name = "LanStatus" };
        box.AddChild(_lan);
        Ui.Button(box, "HostLan", "Host LAN game", HostLan);
        _address = new LineEdit { Name = "JoinAddress", Text = "127.0.0.1", CustomMinimumSize = new Vector2(240, 0) };
        box.AddChild(_address);
        Ui.Button(box, "JoinLan", "Join by IP", JoinLan);
        Ui.Button(box, "StartLan", "Start LAN battle", StartLan);
        Ui.Button(box, "Back", "Back", () =>
        {
            GameSession.EndLan();
            Ui.Go(this, new Main());
        });
        Refresh();
        UxLog.Event("screen", ("name", "lobby"));
        UxLog.Event("lobby_created");
    }

    void HostLan()
    {
        if (GameSession.LanHost is not null || GameSession.LanClient is not null)
        {
            return;
        }

        try
        {
            GameSession.LanHost = new LanHost(Port(), 10, "Host");
            UxLog.Event("lan_hosting", ("port", GameSession.LanHost.Port));
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            UxLog.Event("lan_host_failed", ("error", ex.SocketErrorCode.ToString()));
        }

        Refresh();
    }

    void JoinLan()
    {
        if (GameSession.LanHost is not null || GameSession.LanClient is not null)
        {
            return;
        }

        try
        {
            GameSession.LanClient = new LanClient(_address.Text.Trim(), Port(), "Player");
            UxLog.Event("lan_joining", ("address", _address.Text.Trim()), ("port", Port()));
        }
        catch (System.Exception ex) when (ex is System.Net.Sockets.SocketException or System.FormatException or System.ArgumentException or System.InvalidOperationException)
        {
            UxLog.Event("lan_join_failed", ("error", ex.GetType().Name));
        }

        Refresh();
    }

    void StartLan()
    {
        var host = GameSession.LanHost;
        if (host is null || host.Players.Count < 2 || host.Started)
        {
            UxLog.Event("lan_start_refused", ("hosting", host is not null), ("players", host?.Players.Count ?? 0));
            return;
        }

        var n = host.Players.Count;

        // Every peer builds the battle from shipped data only: a deck-builder deck exists on this machine alone.
        if (!GameData.Data.Decks.Any(d => d.Id == GameSession.PlayerDeck) || !GameData.Data.Decks.Any(d => d.Id == GameSession.EnemyDeck))
        {
            UxLog.Event("lan_start_refused", ("hosting", true), ("players", n), ("reason", "custom decks are not supported online yet"));
            _lan.Text = "Custom decks are not supported online yet. Pick a shipped deck in Skirmish first.";
            return;
        }

        var map = GameSession.Map is null ? null : GameData.Data.Maps.FirstOrDefault(m => m.Id == GameSession.Map);
        var setup = new NetSetup
        {
            Seed = GameSession.Seed,
            PerSide = System.Math.Max(GameSession.PerSide, (n + 1) / 2),
            Deck0 = GameSession.PlayerDeck,
            Deck1 = GameSession.EnemyDeck,
            StartDp = GameSession.StartDp,
            Map = map?.Id,
            MapSha = map is null ? null : NetSetup.MapHash(map),
            VictoryType = GameSession.VictoryType,
            TimeOfDay = GameSession.TimeOfDay,
            Weather = GameSession.Weather,
            Peers = n,
            Humans = Enumerable.Range(0, n).Where(i => !host.IsAi[i]).ToArray(),
            Names = [.. host.Players],
        };
        // Build first: if this machine cannot, the lobby stays open instead of sending a battle nobody can start.
        SixActual.Sim.State.Battle.WorldState world;
        try
        {
            world = setup.Build(GameData.Data, GameData.Rules);
        }
        catch (System.Exception ex) when (ex is System.IO.InvalidDataException or System.ArgumentException)
        {
            UxLog.Event("lan_start_refused", ("hosting", true), ("players", n), ("reason", ex.Message));
            _lan.Text = $"Cannot start: {ex.Message}";
            return;
        }

        host.Start(setup.ToBytes());
        Begin(new LockstepPeer(0, n, world, GameData.Rules, setup.InputDelayTicks), setup);
    }

    void Begin(LockstepPeer peer, NetSetup setup)
    {
        GameSession.Peer = peer;
        if (GameSession.LanHost is not null)
        {
            GameSession.LanHost.Peer = peer;
        }

        if (GameSession.LanClient is not null)
        {
            GameSession.LanClient.Peer = peer;
        }

        GameSession.Origin = "lobby";
        GameSession.Map = setup.Map;
        UxLog.Event("lan_battle_starting", ("slot", peer.Self), ("peers", setup.Peers), ("per_side", setup.PerSide), ("map", setup.Map ?? "generated"), ("humans", setup.Humans.Length));
        Ui.Go(this, new SkirmishView());
    }

    public override void _Process(double delta)
    {
        var now = (long)Time.GetTicksMsec();
        if (GameSession.LanHost is { } host && GameSession.Peer is null)
        {
            host.Pump(now);
            while (_seenPlayers < host.Players.Count)
            {
                UxLog.Event("lobby_peer_joined", ("name", host.Players[_seenPlayers]), ("slot", _seenPlayers), ("players", host.Players.Count));
                _seenPlayers++;
                Refresh();
            }
        }

        if (GameSession.LanClient is { } client && GameSession.Peer is null)
        {
            client.Pump(now);
            if (client.Slot >= 0 && !_joinedLogged)
            {
                _joinedLogged = true;
                UxLog.Event("lan_joined", ("slot", client.Slot));
                Refresh();
            }

            if (client.Setup is not null && client.RefuseReason is null)
            {
                try
                {
                    var s = NetSetup.FromBytes(client.Setup);
                    Begin(new LockstepPeer(client.Slot, s.Peers, s.Build(GameData.Data, GameData.Rules), GameData.Rules, s.InputDelayTicks), s);
                }
                catch (System.Exception ex) when (ex is System.IO.InvalidDataException or System.ArgumentException or System.Text.Json.JsonException)
                {
                    // Tell the host (it stops waiting) and the player.
                    client.RefuseReason = ex.Message;
                    UxLog.Event("lan_setup_refused", ("reason", ex.Message));
                    _lan.Text = $"Cannot join this battle: {ex.Message}";
                }
            }
        }
    }

    void Refresh()
    {
        _slots.Text = $"Slot 1: you{(_ready ? " (ready)" : string.Empty)}\n" + string.Join("\n", Enumerable.Range(0, _ai).Select(i => $"Slot {i + 2}: AI"));
        _lan.Text = GameSession.LanHost is { } h ? $"Hosting on UDP port {h.Port}. Players: {string.Join(", ", h.Players)}"
            : GameSession.LanClient is { } c ? (c.Slot >= 0 ? $"Joined as slot {c.Slot + 1}; waiting for the host to start." : c.Refused ? "The game is full." : "Joining...")
            : "Not connected.";
    }
}
