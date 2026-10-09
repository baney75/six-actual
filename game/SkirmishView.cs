using System.Collections.Generic;
using System.Linq;
using Godot;
using SixActual.Sim;
using SixActual.Sim.Battle;
using SixActual.Sim.State.Battle;

/// <summary>
/// MODE-01 skirmish on the Phase-1 battle sim (sim/Battle): an 8 km generated map, the player's deck against an AI
/// XO. Deployment through the card bar, Take Control of an aircraft (T) and return (G), time controls, then the
/// debrief. Visuals are placeholders (boxes and discs) until the art pipeline delivers models.
/// </summary>
public partial class SkirmishView : Node3D
{
    /// <summary>Time compression for G13 scripts and fast-forward: sim ticks per rendered frame; 0 = real time.</summary>
    public static int TicksPerFrame;

    /// <summary>The live battle view (G13 helpers need a unit's screen position).</summary>
    public static SkirmishView? Current;

    bool _logSeats;
    Radio _radio = null!;
    MusicDirector _music = null!;
    SfxPlayer _sfx = null!;
    AmbienceDirector _ambience = null!;
    Vector3? _lastAlert;
    int _supportArmed = -1;
    bool _fireArmed;
    Button _fireButton = null!;
    int[] _objOwner = [];
    readonly List<(MeshInstance3D Mesh, double Left)> _pings = [];
    bool _fogDue;

    WorldState _w = null!;
    BattleRules _rules = null!;
    readonly List<UnitOrder> _pending = [];
    readonly Dictionary<int, MeshInstance3D> _views = [];
    Camera3D _camera = null!;
    Label _status = null!;
    HBoxContainer _cards = null!;
    readonly List<Button> _cardButtons = [];
    VBoxContainer _offers = null!;
    int _eventIndex;
    double _accum;
    bool _paused;
    bool _over;
    int _flown = -1;

    bool _slow;
    bool _stickLogged;
    int _paintSeat = -2;
    Vector2 _paintFrom;
    bool _painting;
    int _selected = -1;
    readonly List<Button> _seatButtons = [];
    Label _subtitle = null!;
    double _subtitleLeft;

    public override void _Ready()
    {
        Current = this;
        _rules = GameData.Rules;
        var opt = new Skirmish.Options
        {
            PerSide = GameSession.PerSide,
            StartDp = GameSession.StartDp,
            Income = GameSession.Income,
            VictoryType = GameSession.VictoryType,
            TimeOfDay = GameSession.TimeOfDay,
            Weather = GameSession.Weather,
            FieldRequests = GameSession.FieldRequests,
            StartSp = GameSession.StartSp,
        };
        _w = Skirmish.Create(GameData.Data, _rules, GameSession.Seed, GameSession.PlayerDeck, GameSession.EnemyDeck, xo0: false, xo1: true, opt, DeckBuilderScreen.Load());
        BuildScene();
        BuildHud();
        _objOwner = _w.Objectives.Select(o => o.Owner).ToArray();
        var seats = new List<(string, object)>();
        for (var r = 1; r < Command.SeatNames.Length; r++)
        {
            var st = _w.Seats[Command.SeatIndex(0, r)];
            seats.Add((Radio.Speakers[r], $"{_radio.CommanderName(Radio.Speakers[r])}, {Radio.Trait(st.Aggression, st.CasualtyTolerance)}"));
        }

        UxLog.Event("commanders", [.. seats, ("xo", _radio.CommanderName("xo"))]);
        UxLog.Event("screen", ("name", "battle"));
        // Options as the sim world holds them (read back, not echoed from the menu).
        UxLog.Event("battle_started", ("deck", GameSession.PlayerDeck), ("enemy", GameSession.EnemyDeck), ("seed", (long)GameSession.Seed), ("palette", GameSettings.Palettes[GameSettings.Palette]),
            ("players", _w.Players.Length), ("team0", _w.Players.Count(p => p.Team == 0)), ("start_dp", _w.Players[0].Dp), ("income", BattleRules.IncomeProfiles[_w.Players[0].Profile]),
            ("victory", _w.VictoryType == 1 ? "annihilation" : "conquest"), ("time_of_day", new[] { "dawn", "day", "dusk", "night" }[_w.TimeOfDay]), ("weather", BattleRules.WeatherNames[_w.Weather]), ("air_grounded", _w.AirGrounded),
            ("ai_xos", _w.XoActive.Count(x => x != 0)));
    }

    void BuildScene()
    {
        _camera = new Camera3D { Current = true, Far = 20000, Position = new Vector3(4000, 1400, -900) };
        AddChild(_camera);
        _camera.LookAt(new Vector3(4000, 0, 1800), Vector3.Up);
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-50, 30, 0) });
        // Time of day and weather tint the placeholder scene: darker sky at dusk and night, distance fog in fog/rain/snow.
        var light = new[] { 0.7f, 1f, 0.6f, 0.18f }[_w.TimeOfDay];
        var env = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(0.55f, 0.62f, 0.7f) * light };
        var wx = BattleRules.WeatherNames[_w.Weather];
        env.FogEnabled = wx is "fog" or "rain" or "snow";
        env.FogDensity = wx == "fog" ? 0.004f : 0.001f;
        AddChild(new WorldEnvironment { Environment = env });
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(8000, 8000) },
            Position = new Vector3(4000, 0, 4000),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.28f, 0.34f, 0.2f) },
        });
        foreach (var o in _w.Objectives)
        {
            AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 60, BottomRadius = 60, Height = 2 },
                Position = new Vector3(o.X, 1, o.Y),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.9f, 0.8f) },
            });
        }
    }

    void BuildHud()
    {
        var layer = new CanvasLayer();
        AddChild(layer);
        var top = new VBoxContainer { Position = new Vector2(12, 8) };
        layer.AddChild(top);
        _status = new Label { Name = "Status" };
        top.AddChild(_status);
        var row = new HBoxContainer();
        top.AddChild(row);
        Ui.Button(row, "TakeControl", "Take control (T)", TakeControl);
        Ui.Button(row, "ReturnControl", "Return (G)", ReturnControl);
        Ui.Button(row, "Pause", "Pause", () => { _paused = !_paused; UxLog.Event("pause", ("paused", _paused)); });
        Ui.Button(row, "Faster", "Speed x8", () => TicksPerFrame = TicksPerFrame == 0 ? 8 : 0);
        Ui.Button(row, "JumpToAlert", "Latest alert (Home)", JumpToAlert);
        Ui.Button(row, "Surrender", "Surrender", () => { UxLog.Event("surrendered", ("tick", _w.Tick)); Finish(surrendered: true); });
        // CMD-01/02 commander panel: arm a seat, then drag a rectangle on the map to assign every friendly ground unit
        // inside it (sector paint). "Player" takes units back under direct control.
        var seats = new VBoxContainer { Name = "Seats", Position = new Vector2(1080, 90) };
        layer.AddChild(seats);
        seats.AddChild(new Label { Text = "Commanders (paint a sector)" });
        for (var role = 1; role < Command.SeatNames.Length; role++)
        {
            var seat = Command.SeatIndex(0, role);
            _seatButtons.Add(Ui.Button(seats, $"Paint_{Command.SeatNames[role]}", Command.SeatNames[role], () => ArmPaint(seat)));
        }

        _seatButtons.Add(Ui.Button(seats, "Paint_Player", "Player", () => ArmPaint(-1)));
        _sfx = new SfxPlayer { Name = "Sfx" };
        AddChild(_sfx);
        _ambience = new AmbienceDirector { Name = "Ambience" };
        AddChild(_ambience);
        _ambience.Setup(_w.TimeOfDay, BattleRules.WeatherNames[_w.Weather]);
        _music = new MusicDirector { Name = "Music" };
        AddChild(_music);
        _music.Setup(GameSession.Era);
        _radio = new Radio { Name = "Radio" };
        AddChild(_radio);
        _radio.Setup(GameSession.Era, layer);
        // MEC-03 support panel: the off-map options for the player's era and side, plus fire missions for a selected battery.
        var support = new VBoxContainer { Name = "Support", Position = new Vector2(1080, 330) };
        layer.AddChild(support);
        support.AddChild(new Label { Text = "Support (SP)" });
        var side = _w.Decks[_w.Players[0].Deck].Side;
        var shown = 0;
        for (var i = 0; i < _w.SupportDefs.Length && shown < 6; i++)
        {
            var d = _w.SupportDefs[i];
            if (d.Era == _w.Era && d.Side == side)
            {
                var option = i;
                Ui.Button(support, $"Support_{d.Id}", $"{d.Id} ({d.SpCost})", () =>
                {
                    _supportArmed = option;
                    _fireArmed = false;
                    UxLog.Event("support_armed", ("option", _w.SupportDefs[option].Id), ("sp", _w.Players[0].Sp), ("cost", _w.SupportDefs[option].SpCost));
                });
                shown++;
            }
        }

        _fireButton = Ui.Button(support, "FireMission", "Fire mission (selected battery)", () =>
        {
            if (_selected >= 0 && _w.Defs[_w.Units[_selected].Def].Kind == SixActual.Sim.UnitDef.KindArtillery)
            {
                _fireArmed = true;
                _supportArmed = -1;
                UxLog.Event("fire_armed", ("unit", _w.Units[_selected].Id));
            }
        });
        if (GameSession.Tutorial is { } tut)
        {
            var t = new Tutorial { Name = "Tutorial" };
            AddChild(t);
            t.Start(tut, layer);
        }

        _subtitle = new Label { Name = "Subtitle", Position = new Vector2(380, 520), Visible = false };
        _subtitle.AddThemeFontSizeOverride("font_size", 22);
        layer.AddChild(_subtitle);
        _offers = new VBoxContainer { Name = "Offers", Position = new Vector2(900, 8), Visible = false };
        layer.AddChild(_offers);
        layer.AddChild(new Label { Text = "PLACEHOLDER VISUALS", Position = new Vector2(560, 40) });
        // Layout containers ignore the mouse so clicks between buttons reach the map (only the buttons catch clicks).
        // The card bar is one scrolling row along the bottom edge, so the battlefield stays clickable. Layout containers
        // ignore the mouse; only the buttons catch clicks.
        var cardScroll = new ScrollContainer { Name = "CardBar", Position = new Vector2(12, 560), CustomMinimumSize = new Vector2(1056, 150), VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        layer.AddChild(cardScroll);
        _cards = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        cardScroll.AddChild(_cards);
        var deck = _w.Decks[_w.Players[0].Deck];
        for (var c = 0; c < deck.Cards.Length; c++)
        {
            var card = c;
            var def = _w.Defs[deck.Cards[c].Unit];
            var info = CardInfo.Of(def, _w.Weapons);
            var panel = new VBoxContainer { Name = $"CardPanel_{c}", CustomMinimumSize = new Vector2(150, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
            _cards.AddChild(panel);
            var b = Ui.Button(panel, $"Card_{c}_{KindName(def.Kind)}_{(def.IsAir ? "air" : "ground")}", string.Empty, () => _pending.Add(new UnitOrder { Kind = UnitOrder.Deploy, Player = 0, Card = card }));
            _cardButtons.Add(b);
            panel.AddChild(new Label { Name = $"CardRole_{c}", Text = $"{info.Icon} {info.Role}" });
            panel.AddChild(new ProgressBar { Name = $"CardRange_{c}", MaxValue = CardInfo.RangeScaleM, Value = info.RangeM, ShowPercentage = false, CustomMinimumSize = new Vector2(140, 6), MouseFilter = Control.MouseFilterEnum.Ignore });
            b.TooltipText = $"{info.Role}: effective range {info.RangeM} m. {info.UseFor}";
            panel.AddChild(new Label { Name = $"CardUse_{c}", Text = info.UseFor, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(150, 0) });
            UxLog.Event("card_shown", ("card", c), ("unit", def.Id), ("icon", info.Icon), ("role", info.Role), ("range_m", info.RangeM), ("use_for", info.UseFor));
        }
    }

    void ShowOffers(int[] offers)
    {
        foreach (var c in _offers.GetChildren())
        {
            c.QueueFree();
        }

        _offers.AddChild(new Label { Text = "FIELD REQUEST: choose one task force" });
        for (var i = 0; i < offers.Length; i++)
        {
            if (offers[i] < 0 || offers[i] >= _w.FieldRequestDefs.Length)
            {
                continue;
            }

            var choice = i;
            var tf = _w.FieldRequestDefs[offers[i]];
            var units = string.Join(", ", tf.Units.Select(u => $"{u.Count}x {_w.Defs[u.Unit].Id}"));
            Ui.Button(_offers, $"Offer_{i}", $"{tf.Id}: {units}", () => _pending.Add(new UnitOrder { Kind = UnitOrder.FieldRequest, Player = 0, Card = choice }));
        }

        _offers.Visible = true;
    }

    /// <summary>Moves the camera to the latest map ping (an RTS "jump to alert").</summary>
    void JumpToAlert()
    {
        if (_lastAlert is not { } a || _flown >= 0)
        {
            return;
        }

        _camera.Position = a + new Vector3(0, 450, -600);
        _camera.LookAt(a, Vector3.Up);
        UxLog.Event("camera_jump", ("x", (int)a.X), ("y", (int)a.Z), ("tick", _w.Tick));
    }

    void ArmPaint(int seat)
    {
        _paintSeat = seat;
        UxLog.Event("paint_armed", ("seat", seat < 0 ? "Player" : Command.SeatNames[seat % Command.SeatsPerPlayer]));
    }

    /// <summary>Ground point under a screen position (the y = 0 plane); rays above the horizon are clamped 12 km out.</summary>
    Vector3 Ground(Vector2 screen)
    {
        var from = _camera.ProjectRayOrigin(screen);
        var dir = _camera.ProjectRayNormal(screen);
        var t = dir.Y < -1e-4f ? -from.Y / dir.Y : 12000;
        return from + (dir * t);
    }

    void Paint(Vector2 a, Vector2 b)
    {
        var ga = Ground(a);
        var gb = Ground(b);
        float x0 = Mathf.Min(ga.X, gb.X), x1 = Mathf.Max(ga.X, gb.X), z0 = Mathf.Min(ga.Z, gb.Z), z1 = Mathf.Max(ga.Z, gb.Z);
        var n = 0;
        for (var i = 0; i < _w.Units.Length; i++)
        {
            ref readonly var u = ref _w.Units[i];
            var x = ToFloat(u.X);
            var z = ToFloat(u.Y);
            if (u.Player == 0 && u.Alive != 0 && !_w.Defs[u.Def].IsAir && x >= x0 && x <= x1 && z >= z0 && z <= z1)
            {
                _pending.Add(new UnitOrder { Kind = UnitOrder.AssignSeat, UnitId = u.Id, Card = _paintSeat });
                n++;
            }
        }

        _logSeats = true;
        UxLog.Event("sector_painted", ("seat", _paintSeat < 0 ? "Player" : Command.SeatNames[_paintSeat % Command.SeatsPerPlayer]), ("units", n), ("tick", _w.Tick));
        _paintSeat = -2;
    }

    void Select(Vector2 screen)
    {
        var g = Ground(screen);
        var best = -1;
        var bestD = 40f * 40f;
        for (var i = 0; i < _w.Units.Length; i++)
        {
            ref readonly var u = ref _w.Units[i];
            var d = new Vector2(ToFloat(u.X) - g.X, ToFloat(u.Y) - g.Z).LengthSquared();
            if (u.Player == 0 && u.Alive != 0 && d < bestD)
            {
                best = i;
                bestD = d;
            }
        }

        _selected = best;
        if (best >= 0)
        {
            UxLog.Event("selected", ("unit", _w.Units[best].Id), ("seat", _w.Units[best].Seat));
        }
    }

    void MoveSelected(Vector2 screen)
    {
        if (_selected < 0 || _w.Units[_selected].Alive == 0)
        {
            return;
        }

        var g = Ground(screen);
        _pending.Add(new UnitOrder { Kind = UnitOrder.Move, UnitId = _w.Units[_selected].Id, X = (int)g.X, Y = (int)g.Z });
        UxLog.Event("move_ordered", ("unit", _w.Units[_selected].Id), ("seat", _w.Units[_selected].Seat), ("x", (int)g.X), ("y", (int)g.Z));
    }

    void TakeControl()
    {
        var air = System.Array.FindIndex(_w.Units, u => u.Player == 0 && u.Alive != 0 && _w.Defs[u.Def].IsAir);
        if (air < 0)
        {
            UxLog.Event("take_control_refused", ("reason", "no aircraft"));
            return;
        }

        _pending.Add(new UnitOrder { Kind = UnitOrder.TakeControl, UnitId = _w.Units[air].Id, Player = 0 });
        UxLog.Event("take_control_ordered", ("unit", _w.Units[air].Id), ("tick", _w.Tick));
    }

    void ReturnControl()
    {
        if (_flown >= 0)
        {
            _pending.Add(new UnitOrder { Kind = UnitOrder.Release, UnitId = _flown, Player = 0 });
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } lb && _paintSeat != -2)
        {
            if (lb.Pressed)
            {
                _painting = true;
                _paintFrom = lb.Position;
            }
            else if (_painting)
            {
                _painting = false;
                Paint(_paintFrom, lb.Position);
            }

            return;
        }

        if (e is InputEventMouseButton { Pressed: true } mb && _flown < 0)
        {
            if (mb.ButtonIndex == MouseButton.Left && (_supportArmed >= 0 || _fireArmed))
            {
                var g = Ground(mb.Position);
                if (_supportArmed >= 0)
                {
                    _pending.Add(new UnitOrder { Kind = UnitOrder.Support, Player = 0, Card = _supportArmed, X = (int)g.X, Y = (int)g.Z });
                }
                else if (_selected >= 0)
                {
                    _pending.Add(new UnitOrder { Kind = UnitOrder.FireMission, UnitId = _w.Units[_selected].Id, X = (int)g.X, Y = (int)g.Z, Damage = 6, RadiusM = 0 });
                }

                UxLog.Event("target_clicked", ("x", (int)g.X), ("y", (int)g.Z), ("support", _supportArmed >= 0), ("fire", _fireArmed));
                _supportArmed = -1;
                _fireArmed = false;
                return;
            }

            if (mb.ButtonIndex == MouseButton.Left)
            {
                Select(mb.Position);
            }
            else if (mb.ButtonIndex == MouseButton.Right)
            {
                MoveSelected(mb.Position);
            }

            return;
        }

        if (e.IsActionPressed("take_control"))
        {
            TakeControl();
        }
        else if (e.IsActionPressed("return_control"))
        {
            ReturnControl();
        }
        else if (e.IsActionPressed("pause"))
        {
            _paused = !_paused;
            UxLog.Event("pause", ("paused", _paused));
        }
        else if (e is InputEventKey { Pressed: true, Keycode: Key.Home })
        {
            JumpToAlert();
        }
        else if (e.IsActionPressed("slow_time"))
        {
            _slow = !_slow;
            UxLog.Event("slow_time", ("slow", _slow));
        }
    }

    public override void _Process(double delta)
    {
        if (!_over && !_paused)
        {
            var steps = TicksPerFrame;
            if (steps == 0)
            {
                // UX-02 slow time (single player): half speed.
                _accum += _slow ? delta / 2 : delta;
                steps = (int)(_accum / 0.05);
                _accum -= steps * 0.05;
            }

            for (var i = 0; i < steps && !_over; i++)
            {
                if (_w.Tick % 600 == 300)
                {
                    _fogDue = true;
                }

                FlyStick();
                // The sim applies an order only on the tick it names, so pending input is stamped with the current tick.
                BattleWorld.Step(_w, _rules, _pending.Select(o => o with { Tick = _w.Tick }).ToArray());
                _pending.Clear();
                ReadEvents();
                if (_logSeats)
                {
                    _logSeats = false;
                    LogSeats();
                }
            }
        }

        _radio.Pump(_w.Tick);
        // BRIEF D7: the listener sits at the camera's ground focus point (the battlefield under the screen centre).
        _sfx.Listener = Ground(GetViewport().GetVisibleRect().Size / 2);
        _sfx.RealTime = TicksPerFrame == 0;
        // Fractional sim time: at real-time speed the part of a tick already elapsed counts, so sounds start within a
        // frame of their arrival time instead of on 50 ms tick boundaries.
        _sfx.Pump(_w, _rules, _w.Tick + (TicksPerFrame == 0 ? _accum / 0.05 : 0));
        _ambience.Pump(_w, _sfx.Listener, _music.Intensity);
        _music.Duck(_radio.Busy(_w.Tick), _w.Tick);
        _music.Pump(_w);
        SyncViews();
        if (_fogDue)
        {
            // Sampled after the views sync, so "shown" is what this frame draws for the current tick.
            _fogDue = false;
            LogFog();
        }

        Refresh();
        for (var i = _pings.Count - 1; i >= 0; i--)
        {
            var (mesh, left) = _pings[i];
            left -= delta;
            if (left <= 0)
            {
                mesh.QueueFree();
                _pings.RemoveAt(i);
            }
            else
            {
                _pings[i] = (mesh, left);
            }
        }

        if (_subtitleLeft > 0 && (_subtitleLeft -= delta) <= 0)
        {
            _subtitle.Visible = false;
        }
    }

    /// <summary>A flown aircraft holds course and throttle unless the player steers (FLY-04 input mapping is next).</summary>
    void FlyStick()
    {
        if (_flown < 0 || _w.Tick % 10 != 0)
        {
            return;
        }

        // FLY-04: keys, gamepad or HOTAS stick through the input actions; with no stick input, mouse-aim steers toward
        // the pointer (offset from the screen centre).
        var turn = Input.GetAxis("fly_left", "fly_right");
        var climb = Input.GetAxis("fly_down", "fly_up");
        if (turn == 0 && climb == 0 && Input.IsMouseButtonPressed(MouseButton.Right))
        {
            var size = GetViewport().GetVisibleRect().Size;
            var off = (GetViewport().GetMousePosition() - (size / 2)) / (size / 2);
            turn = Mathf.Clamp(off.X, -1, 1);
            climb = Mathf.Clamp(-off.Y, -1, 1);
        }

        var source = "none";
        if (turn != 0 || climb != 0)
        {
            var stick = Input.GetAxis("fly_left", "fly_right") != 0 || Input.GetAxis("fly_down", "fly_up") != 0;
            var keys = new[] { "fly_left", "fly_right", "fly_up", "fly_down" }.Any(a => Input.IsKeyPressed(Controls.KeyOf(a)) || Input.IsPhysicalKeyPressed(Controls.KeyOf(a)));
            source = !stick ? "mouse" : keys ? "keys" : "joypad";
        }

        _pending.Add(new UnitOrder { Kind = UnitOrder.Stick, UnitId = _flown, X = 80, Y = (int)(turn * 100), ZDm = (int)(climb * 100) });
        if (!_stickLogged && source != "none")
        {
            _stickLogged = true;
            UxLog.Event("stick_input", ("source", source), ("turn", (int)(turn * 100)), ("climb", (int)(climb * 100)), ("tick", _w.Tick));
        }
    }

    void ReadEvents()
    {
        for (; _eventIndex < _w.Events.Count; _eventIndex++)
        {
            var e = _w.Events[_eventIndex];
            SoundFor(e);
            switch (e.Kind)
            {
                case BattleEvent.Deployed when e.A == 0:
                    var u = System.Array.FindIndex(_w.Units, x => x.Id == e.B);
                    if (u < 0)
                    {
                        break;
                    }

                    UxLog.Event("deployed", ("unit", _w.Defs[_w.Units[u].Def].Id), ("tick", _w.Tick));
                    if (_w.Defs[_w.Units[u].Def].IsAir)
                    {
                        UxLog.Event("deployed_air", ("unit", _w.Defs[_w.Units[u].Def].Id), ("tick", _w.Tick));
                    }

                    break;
                case BattleEvent.DeployRefused when e.A == 0:
                    UxLog.Event("deploy_refused", ("card", e.B), ("tick", _w.Tick));
                    break;
                case BattleEvent.PhaseChange:
                    _radio.Report("xo", "phase_change", _w.Tick);
                    UxLog.Event("phase", ("phase", "ABC"[System.Math.Clamp(e.Value, 0, 2)].ToString()), ("tick", _w.Tick));
                    break;
                case BattleEvent.XoTookCommand when e.A == 0:
                    _radio.Report("xo", "take_control", _w.Tick);
                    _flown = e.B;
                    _stickLogged = false;
                    Say("XO", "I have the battle. Your aircraft, sir.");
                    UxLog.Event("take_control", ("unit", e.B), ("xo_active", _w.XoActive[0]), ("tick", _w.Tick));
                    break;
                case BattleEvent.ControlReturned when e.A == 0:
                    _radio.Report("xo", "return_control", _w.Tick);
                    _flown = -1;
                    Say("XO", "Back with you. Battle handed over.");
                    UxLog.Event("control_returned", ("unit", e.B), ("reason", e.Value), ("tick", _w.Tick));
                    break;
                case BattleEvent.FieldOffered when e.A == 0:
                    ShowOffers([e.B, e.C, e.Value]);
                    UxLog.Event("field_offered", ("offers", new[] { e.B, e.C, e.Value }.Where(x => x >= 0 && x < _w.FieldRequestDefs.Length).Select(x => _w.FieldRequestDefs[x].Id).ToArray()), ("tick", _w.Tick));
                    break;
                case BattleEvent.FieldTaken when e.A == 0 && e.B >= 0 && e.B < _w.FieldRequestDefs.Length:
                    _offers.Visible = false;
                    UxLog.Event("field_taken", ("task_force", _w.FieldRequestDefs[e.B].Id), ("units", e.Value), ("xo_active", _w.XoActive[0]), ("tick", _w.Tick));
                    break;
                case BattleEvent.GameOver:
                    Finish();
                    break;
                case BattleEvent.SupportCalled when e.A == 0:
                    UxLog.Event("support_called", ("option", _w.SupportDefs[e.B].Id), ("arrives_tick", e.C), ("tick", _w.Tick));
                    break;
                case BattleEvent.SupportArrived when e.A == 0:
                    UxLog.Event("support_arrived", ("option", _w.SupportDefs[e.B].Id), ("tick", _w.Tick));
                    break;
                case BattleEvent.SupportRefused when e.A == 0:
                    UxLog.Event("support_refused", ("option", e.B >= 0 && e.B < _w.SupportDefs.Length ? _w.SupportDefs[e.B].Id : "?"), ("reason", e.Value), ("tick", _w.Tick));
                    break;
                case BattleEvent.MissionStarted when System.Array.FindIndex(_w.Units, x => x.Id == e.A) is var bi && bi >= 0 && _w.Units[bi].Player == 0:
                    UxLog.Event("fire_mission", ("unit", _w.Defs[_w.Units[bi].Def].Id), ("x", e.B), ("y", e.C), ("rounds", e.Value), ("tick", _w.Tick));
                    RadioFor(e);
                    break;
                default:
                    RadioFor(e);
                    break;
            }
        }
    }

    /// <summary>CMD-07: the commander who would report a sim event on player 0's net, and where to ping the map.</summary>
    void RadioFor(BattleEvent e)
    {
        string? SeatSpeaker(int seat) => seat >= 0 && seat / Command.SeatsPerPlayer == 0 ? Radio.Speakers[seat % Command.SeatsPerPlayer] : null;
        int UnitIndex(int id) => System.Array.FindIndex(_w.Units, u => u.Id == id);
        switch (e.Kind)
        {
            case BattleEvent.AssaultStarted:
                Say2(SeatSpeaker(e.A), "assault_started");
                break;
            case BattleEvent.Escalation:
                Say2(SeatSpeaker(e.A), "escalation");
                break;
            case BattleEvent.RequestMade:
                Say2(SeatSpeaker(e.B), "request");
                break;
            case BattleEvent.Withdraw:
                Say2(SeatSpeaker(e.B), "withdraw");
                break;
            case BattleEvent.MissionStarted:
                {
                    var u = UnitIndex(e.A);
                    if (u >= 0 && _w.Units[u].Player == 0)
                    {
                        var sp = SeatSpeaker(_w.Units[u].Seat);
                        Say2(sp is "arty1" or "arty2" ? sp : "arty1", "fire_mission", e.B, e.C);
                    }

                    break;
                }

            case BattleEvent.BatteryLocated when e.B == 0:
                Say2("arty1", "battery_located", e.C, e.Value);
                break;
            case BattleEvent.CasOnStation:
                {
                    var a = UnitIndex(e.B);
                    if (a >= 0 && _w.Units[a].Player == 0)
                    {
                        var sp = SeatSpeaker(_w.Units[a].Seat);
                        Say2(sp is "air1" or "air2" ? sp : "air1", "cas_on_station", (int)ToFloat(_w.Units[a].X), (int)ToFloat(_w.Units[a].Y));
                    }

                    break;
                }

            case BattleEvent.Resupplied:
                {
                    var t = UnitIndex(e.A);
                    if (t >= 0 && _w.Units[t].Player == 0)
                    {
                        Say2("logistics", "resupply");
                    }

                    break;
                }

            case BattleEvent.ObjectiveTaken:
                {
                    var k = e.A;
                    if (k >= 0 && k < _objOwner.Length)
                    {
                        var o = _w.Objectives[k];
                        if (e.B == 0)
                        {
                            Say2("xo", "objective_taken", o.X, o.Y);
                        }
                        else if (_objOwner[k] == 0)
                        {
                            Say2("xo", "objective_lost", o.X, o.Y);
                        }

                        _objOwner[k] = e.B;
                    }

                    break;
                }

            case BattleEvent.Spotted when e.A == 0:
                {
                    var target = UnitIndex(e.B);
                    if (target < 0)
                    {
                        break;
                    }

                    var best = -1;
                    long bestD = long.MaxValue;
                    for (var i = 0; i < _w.Units.Length; i++)
                    {
                        ref readonly var u = ref _w.Units[i];
                        if (u.Player == 0 && u.Alive != 0 && !_w.Defs[u.Def].IsAir)
                        {
                            var dx = (u.X.Raw - _w.Units[target].X.Raw) >> 16;
                            var dy = (u.Y.Raw - _w.Units[target].Y.Raw) >> 16;
                            if ((dx * dx) + (dy * dy) < bestD)
                            {
                                bestD = (dx * dx) + (dy * dy);
                                best = i;
                            }
                        }
                    }

                    var sp = best >= 0 ? SeatSpeaker(_w.Units[best].Seat) : null;
                    if (sp is "army1" or "army2" or "armor")
                    {
                        Say2(sp, "contact", (int)ToFloat(_w.Units[target].X), (int)ToFloat(_w.Units[target].Y));
                    }

                    break;
                }
        }
    }

    void Say2(string? speaker, string trigger, int x = -1, int y = -1)
    {
        if (speaker is null)
        {
            return;
        }

        if (_radio.Report(speaker, trigger, _w.Tick) && x >= 0)
        {
            var ping = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 40, BottomRadius = 40, Height = 1 },
                Position = new Vector3(x, 3, y),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.95f, 0.3f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
            };
            AddChild(ping);
            _pings.Add((ping, 3));
            _lastAlert = new Vector3(x, 0, y);
            UxLog.Event("map_ping", ("speaker", speaker), ("trigger", trigger), ("x", x), ("y", y), ("tick", _w.Tick));
        }
    }

    /// <summary>A radio line: shown as a subtitle when subtitles are on (UX-02). Voice lines arrive with AUD-01.</summary>
    void Say(string who, string line)
    {
        if (!GameSettings.Subtitles)
        {
            return;
        }

        _subtitle.Text = $"{who}: {line}";
        _subtitle.Visible = true;
        _subtitleLeft = 4;
        UxLog.Event("subtitle", ("speaker", who), ("text", line), ("tick", _w.Tick));
    }

    public override void _ExitTree()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    void Finish(bool surrendered = false)
    {
        if (_over)
        {
            return;
        }

        _over = true;
        GameSession.Winner = surrendered ? 1 : _w.Winner;
        GameSession.Score = [_w.Score[0], _w.Score[1]];
        GameSession.Deployed = [_w.Units.Count(u => u.Player == 0), _w.Units.Count(u => u.Player == 1)];
        GameSession.Kills = [0, 0];
        foreach (var e in _w.Events.Where(e => e.Kind == BattleEvent.Killed))
        {
            var victim = System.Array.FindIndex(_w.Units, x => x.Id == e.B);
            if (victim >= 0)
            {
                GameSession.Kills[1 - (_w.Units[victim].Player & 1)]++;
            }
        }

        GameSession.Ticks = _w.Tick;
        UxLog.Event("battle_over", ("winner", _w.Winner), ("score0", _w.Score[0]), ("score1", _w.Score[1]), ("tick", _w.Tick), ("hash", WorldHash.Of(_w)[..16]));
        TicksPerFrame = 0;
        GameSession.Tutorial = null;
        GameSession.StartSp = 0;
        Ui.Go(this, new DebriefScreen());
    }

    void SyncViews()
    {
        for (var i = 0; i < _w.Units.Length; i++)
        {
            ref readonly var u = ref _w.Units[i];
            if (!_views.TryGetValue(u.Id, out var view))
            {
                var def = _w.Defs[u.Def];
                var size = def.IsAir ? new Vector3(14, 4, 14) : def.Members > 1 ? new Vector3(6, 2, 6) : new Vector3(8, 3, 10);
                view = new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = size },
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = u.Player == 0 ? GameSettings.SideColors[GameSettings.Palette].Friend : GameSettings.SideColors[GameSettings.Palette].Enemy },
                };
                AddChild(view);
                _views[u.Id] = view;
            }

            view.Visible = u.Alive != 0 && (u.Player == 0 || Sight.Visible(_w, 0, i));
            view.Position = new Vector3(ToFloat(u.X), System.Math.Max(1.5f, ToFloat(u.Z)), ToFloat(u.Y));
        }

        if (_flown >= 0)
        {
            var i = System.Array.FindIndex(_w.Units, x => x.Id == _flown);
            if (i >= 0)
            {
                var target = new Vector3(ToFloat(_w.Units[i].X), ToFloat(_w.Units[i].Z), ToFloat(_w.Units[i].Y));
                _camera.Position = _camera.Position.Lerp(target + new Vector3(0, 120, -260), 0.1f);
                _camera.LookAt(target, Vector3.Up);
            }
        }
    }

    /// <summary>AUD-03: the sound a sim event makes at its source (weapon family by weapon type; impacts and vehicle
    /// kills explode), heard from the camera's ground point.</summary>
    void SoundFor(BattleEvent e)
    {
        switch (e.Kind)
        {
            case BattleEvent.Fired:
                {
                    var u = System.Array.FindIndex(_w.Units, x => x.Id == e.A);
                    if (u < 0)
                    {
                        return;
                    }

                    var def = _w.Defs[_w.Units[u].Def];
                    var type = e.C >= 0 && e.C < def.Weapons.Length ? _w.Weapons[def.Weapons[e.C].Weapon].Type : "smallarms";
                    var family = type switch { "smallarms" => "rifle", "mg" or "autocannon" or "aaa" => "mg", _ => "cannon" };
                    _sfx.Play(_w, _rules, family, ToFloat(_w.Units[u].X), ToFloat(_w.Units[u].Y), _w.Units[u], _w.Tick);
                    var tgt = System.Array.FindIndex(_w.Units, x => x.Id == e.B);
                    if (family is "rifle" or "mg" && tgt >= 0)
                    {
                        _sfx.PassBy(new Vector2(ToFloat(_w.Units[u].X), ToFloat(_w.Units[u].Y)), new Vector2(ToFloat(_w.Units[tgt].X), ToFloat(_w.Units[tgt].Y)), _w.Tick);
                    }
                    break;
                }

            case BattleEvent.Killed:
                {
                    var t = System.Array.FindIndex(_w.Units, x => x.Id == e.B);
                    if (t >= 0 && _w.Defs[_w.Units[t].Def].Members <= 1)
                    {
                        var size = _w.Defs[_w.Units[t].Def].Kind == SixActual.Sim.UnitDef.KindTank ? "explosion_large" : "explosion_medium";
                        _sfx.Play(_w, _rules, size, ToFloat(_w.Units[t].X), ToFloat(_w.Units[t].Y), _w.Units[t], _w.Tick);
                    }

                    break;
                }

            case BattleEvent.Impact when _w.Units.Length > 0:
                _sfx.Play(_w, _rules, "explosion_small", e.A, e.B, _w.Units[0] with { X = SixActual.Sim.Fp.FromInt(e.A), Y = SixActual.Sim.Fp.FromInt(e.B) }, _w.Tick);
                break;
        }
    }

    /// <summary>Seat counts read back from the sim after an assignment (CMD-01/02).</summary>
    void LogSeats()
    {
        var fields = new List<(string, object)>();
        for (var r = 1; r < Command.SeatNames.Length; r++)
        {
            var seat = Command.SeatIndex(0, r);
            fields.Add((Command.SeatNames[r], _w.Units.Count(u => u.Player == 0 && u.Alive != 0 && u.Seat == seat)));
        }

        fields.Add(("Player", _w.Units.Count(u => u.Player == 0 && u.Alive != 0 && u.Seat == -1)));
        fields.Add(("tick", _w.Tick));
        UxLog.Event("seats", [.. fields]);
    }

    /// <summary>Screen position of a living friendly ground unit (for recorded clicks), or null.</summary>
    public Vector2? FriendlyScreenPos()
    {
        UxLog.Event("ux_camera", ("pos", _camera.GlobalPosition.ToString()), ("current", _camera.Current), ("viewport", GetViewport().GetVisibleRect().Size.ToString()));
        for (var i = 0; i < _w.Units.Length; i++)
        {
            ref readonly var u = ref _w.Units[i];
            if (u.Player == 0 && u.Alive != 0 && !_w.Defs[u.Def].IsAir)
            {
                var p = new Vector3(ToFloat(u.X), 1.5f, ToFloat(u.Y));
                if (!_camera.IsPositionBehind(p))
                {
                    return _camera.UnprojectPosition(p);
                }
            }
        }

        return null;
    }

    /// <summary>SIM-05 sample: the view draws exactly the enemies the sim says the player's side has spotted.</summary>
    void LogFog()
    {
        int alive = 0, spotted = 0, shown = 0;
        for (var i = 0; i < _w.Units.Length; i++)
        {
            ref readonly var u = ref _w.Units[i];
            if (u.Player == 0 || u.Alive == 0)
            {
                continue;
            }

            alive++;
            spotted += Sight.Visible(_w, 0, i) ? 1 : 0;
            shown += _views.TryGetValue(u.Id, out var v) && v.Visible ? 1 : 0;
        }

        _sfx.LogStats(_w.Tick);
        UxLog.Event("fog", ("enemy_alive", alive), ("spotted", spotted), ("shown", shown), ("hidden", alive - shown), ("tick", _w.Tick));
    }

    void Refresh()
    {
        for (var r = 1; r < Command.SeatNames.Length && r - 1 < _seatButtons.Count; r++)
        {
            var seat = Command.SeatIndex(0, r);
            _seatButtons[r - 1].Text = $"{Command.SeatNames[r]}: {_w.Units.Count(u => u.Player == 0 && u.Alive != 0 && u.Seat == seat)}";
        }

        if (_seatButtons.Count >= Command.SeatNames.Length)
        {
            _seatButtons[^1].Text = $"Player: {_w.Units.Count(u => u.Player == 0 && u.Alive != 0 && u.Seat == -1)}";
        }

        var p = _w.Players[0];
        _fireButton.Disabled = _selected < 0 || _w.Units[_selected].Alive == 0 || _w.Defs[_w.Units[_selected].Def].Kind != SixActual.Sim.UnitDef.KindArtillery;
        _status.Text = $"SP {p.Sp}   Phase {"ABC"[System.Math.Clamp(_w.Phase, 0, 2)]}   DP {p.Dp}   Score {_w.Score[0]} : {_w.Score[1]}   "
            + $"{(_w.XoActive[0] != 0 ? "XO has the battle" : "You have the battle")}{(_flown >= 0 ? "   FLYING" : string.Empty)}{(_paused ? "   PAUSED" : string.Empty)}   {_w.Tick / 20 / 60}:{_w.Tick / 20 % 60:00}";
        var deck = _w.Decks[p.Deck];
        for (var c = 0; c < deck.Cards.Length && c < _cardButtons.Count; c++)
        {
            var card = deck.Cards[c];
            var stock = _w.CardStock[p.StockStart + c];
            var b = _cardButtons[c];
            b.Text = $"{_w.Defs[card.Unit].Id}\n{card.Cost} DP  x{stock}  {"ABC"[card.Phase]}";
            b.Disabled = _over || stock <= 0 || card.Phase > _w.Phase || card.Cost > p.Dp;
        }
    }

    static string KindName(int kind) => kind switch
    {
        SixActual.Sim.UnitDef.KindInfantry => "infantry",
        SixActual.Sim.UnitDef.KindTank => "tank",
        SixActual.Sim.UnitDef.KindIfv => "ifv",
        SixActual.Sim.UnitDef.KindArtillery => "artillery",
        SixActual.Sim.UnitDef.KindAa => "aa",
        SixActual.Sim.UnitDef.KindAir => "air",
        SixActual.Sim.UnitDef.KindSupply => "supply",
        SixActual.Sim.UnitDef.KindCommand => "command",
        _ => "other",
    };

    /// <summary>View-side conversion of a Q48.16 position to Godot units (metres). Floats never flow back into the sim.</summary>
    static float ToFloat(Fp v) => v.Raw / 65536f;
}
