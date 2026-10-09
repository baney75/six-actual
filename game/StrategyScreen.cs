using System.Collections.Generic;
using System.Linq;
using Godot;
using SixActual.Sim.GrandStrategy;

/// <summary>
/// GS-01 grand strategy on the GS sim (sim/GrandStrategy): one theater map per era with provinces (owner, terrain,
/// industry, manpower), factions (production, manpower pool, research), armies led by commanders, and supply (the sim's
/// attrition rule). The player commands the first at-war faction of the first side; the AI plays every other faction.
/// Battles auto-resolve (GS-02 RTS battles run headless). Save and load use the sim's JSON state (CMD-08).
/// </summary>
public partial class StrategyScreen : Node
{
    static readonly string[] Eras = ["coldwar", "ww2", "modern"];
    const string SavePath = "user://gs_save.json";
    GsTheater _t = null!;
    GsRules _r = null!;
    GsState _s = null!;
    int _player;
    int _selectedArmy = -1;
    readonly List<GsOrder> _orders = [];
    MapCanvas _map = null!;
    Label _info = null!;
    Label _faction = null!;
    VBoxContainer _armies = null!;

    public override void _Ready()
    {
        var box = Ui.Screen(this, "GRAND STRATEGY");
        var era = new OptionButton { Name = "GsEra" };
        foreach (var e in Eras)
        {
            era.AddItem(e);
        }

        box.AddChild(era);
        var row = new HBoxContainer();
        box.AddChild(row);
        Ui.Button(row, "EndTurn", "End turn", EndTurn);
        Ui.Button(row, "Build", "Build an army (capital)", () => Queue(new GsOrder { Kind = GsOrder.Build, Faction = _player, Army = -1, Value = System.Math.Max(1, _s.Factions[_player].Production) }, "build"));
        Ui.Button(row, "Research", "Fund research", () => Queue(new GsOrder { Kind = GsOrder.Research, Faction = _player, Value = System.Math.Max(1, _s.Factions[_player].Production / 2) }, "research"));
        Ui.Button(row, "SaveGs", "Save", Save);
        Ui.Button(row, "LoadGs", "Load", Load);
        Ui.Button(row, "Back", "Menu", () => Ui.Go(this, new Main()));
        _faction = new Label { Name = "GsFaction" };
        box.AddChild(_faction);
        var body = new HBoxContainer();
        box.AddChild(body);
        _map = new MapCanvas { Owner2 = this, CustomMinimumSize = new Vector2(760, 520) };
        body.AddChild(_map);
        var side = new VBoxContainer { CustomMinimumSize = new Vector2(380, 0) };
        body.AddChild(side);
        _info = new Label { Name = "GsProvince", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(370, 0) };
        side.AddChild(_info);
        side.AddChild(new Label { Text = "Your armies (strength, commander)" });
        _armies = new VBoxContainer();
        side.AddChild(_armies);
        era.ItemSelected += i => Start(Eras[(int)i]);
        Start("coldwar");
        UxLog.Event("screen", ("name", "strategy"));
    }

    void Start(string era)
    {
        var dir = System.IO.Path.Combine(ProjectSettings.GlobalizePath("user://simdata"), "data");
        _ = GameData.Data; // copies the sim data to user:// on first use
        _t = GsTheater.Load(System.IO.Path.Combine(dir, "theaters.json")).First(x => x.Era == era);
        _r = GsRules.Load(System.IO.Path.Combine(dir, "rules.json"));
        _s = GsSim.Start(_t, _r, 7);
        _player = System.Array.FindIndex(_t.Factions, f => f.AtWar && f.Side == _t.Sides[0]);
        _orders.Clear();
        _selectedArmy = -1;
        _map.Build();
        Refresh();
        var first = _s.Armies.FirstOrDefault(a => a.Faction == _player);
        var from = first is null ? "" : _t.Provinces[first.Province].Id;
        var to = first is null ? "" : _t.Provinces[_t.Provinces[first.Province].Neighbours[0]].Id;
        UxLog.Event("gs_started", ("era", era), ("faction", _t.Factions[_player].Id), ("provinces", _t.Provinces.Length), ("armies", _s.Armies.Count(a => a.Faction == _player)),
            ("commanders", _s.Commanders.Count(c => c.Faction == _player)), ("first_army_in", from), ("neighbour", to));
    }

    void Queue(GsOrder o, string kind)
    {
        _orders.Add(o);
        UxLog.Event("gs_order", ("kind", kind), ("faction", _t.Factions[_player].Id), ("value", o.Value), ("turn", _s.Turn));
    }

    public void ProvincePressed(int p)
    {
        var mine = _s.Armies.Where(a => a.Faction == _player && a.Province == p).OrderByDescending(a => a.Strength).FirstOrDefault();
        if (_selectedArmy >= 0 && (mine is null || mine.Id == _selectedArmy))
        {
            if (mine is null || mine.Id != _selectedArmy)
            {
                Queue(new GsOrder { Kind = GsOrder.Move, Faction = _player, Army = _selectedArmy, Province = p }, "move");
                _selectedArmy = -1;
            }
        }
        else if (mine is not null)
        {
            _selectedArmy = mine.Id;
            UxLog.Event("gs_army_selected", ("army", mine.Id), ("province", _t.Provinces[p].Id), ("strength", mine.Strength));
        }

        var pr = _t.Provinces[p];
        var st = _s.Provinces[p];
        _info.Text = $"{pr.Name} ({pr.Id})\nOwner {_t.Factions[st.Owner].Id}  terrain {GsRules.Terrains[pr.Terrain]}\nIndustry {st.Industry}  manpower {st.Manpower}"
            + (st.FalloutUntil > _s.Turn ? $"\nFallout until turn {st.FalloutUntil}" : string.Empty)
            + "\nArmies: " + string.Join(", ", _s.Armies.Where(a => a.Province == p).Select(a => $"{_t.Factions[a.Faction].Id} {a.Strength}"));
        Refresh();
    }

    void EndTurn()
    {
        var battles = _s.Battles;
        var ai = _t.Factions.Select((_, i) => i != _player).ToArray();
        var orders = _orders.Select(o => new GsOrder { Turn = _s.Turn, Kind = o.Kind, Faction = o.Faction, Army = o.Army, Province = o.Province, Target = o.Target, Value = o.Value }).ToList();
        GsSim.EndTurn(_t, _r, _s, orders, ai);
        _orders.Clear();
        _selectedArmy = -1;
        Refresh();
        var f = _s.Factions[_player];
        UxLog.Event("gs_turn", ("turn", _s.Turn), ("owned", _s.Provinces.Count(p => _t.SideOf(p.Owner) == _t.SideOf(_player))), ("armies", _s.Armies.Count(a => a.Faction == _player)),
            ("strength", _s.Armies.Where(a => a.Faction == _player).Sum(a => a.Strength)), ("production", f.Production), ("manpower", f.ManpowerPool), ("tech", f.Tech), ("research", f.Research),
            ("battles", _s.Battles - battles), ("winner", _s.Winner));
    }

    static string Hash(string text)
    {
        var h = 14695981039346656037UL;
        foreach (var c in text)
        {
            h = (h ^ c) * 1099511628211UL;
        }

        return h.ToString("x16");
    }

    void Save()
    {
        var json = _s.Save();
        using var f = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Write);
        if (f is null)
        {
            UxLog.Event("save_failed", ("file", SavePath), ("error", Godot.FileAccess.GetOpenError().ToString()));
            return;
        }

        f.StoreString(json);
        UxLog.Event("gs_saved", ("turn", _s.Turn), ("hash", Hash(json)));
    }

    void Load()
    {
        var text = Godot.FileAccess.GetFileAsString(SavePath);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        _s = GsState.Load(text);
        _orders.Clear();
        Refresh();
        UxLog.Event("gs_loaded", ("turn", _s.Turn), ("hash", Hash(_s.Save())));
    }

    void Refresh()
    {
        var f = _s.Factions[_player];
        _faction.Text = $"{_t.Factions[_player].Id.ToUpperInvariant()}  turn {_s.Turn}   production {f.Production}   manpower {f.ManpowerPool}   tech {f.Tech} (research {f.Research})   tension {_s.Tension}   orders queued {_orders.Count}"
            + (_s.Winner != GsState.WinnerNone ? $"   WAR OVER: {(_s.Winner < _t.Sides.Length ? _t.Sides[_s.Winner] : "settlement")}" : string.Empty);
        foreach (var c in _armies.GetChildren())
        {
            c.QueueFree();
        }

        foreach (var a in _s.Armies.Where(a => a.Faction == _player))
        {
            var cmd = _s.Commanders.FirstOrDefault(c => c.Id == a.Commander);
            _armies.AddChild(new Label { Text = $"{_t.Provinces[a.Province].Name}: {a.Strength}  {(cmd is null ? "no commander" : $"{cmd.Name} (XP {cmd.Xp})")}{(a.Id == _selectedArmy ? "  [selected]" : string.Empty)}" });
        }

        _map.QueueRedraw();
    }

    /// <summary>The theater map: provinces as discs in owner colors, neighbour links, army strength labels. Each
    /// province is a named button (Prov_&lt;id&gt;) so input scripts can press it.</summary>
    public partial class MapCanvas : Control
    {
        public StrategyScreen Owner2 = null!;

        static Vector2 At(GsProvinceDef p) => new((p.X * 0.72f) + 20, ((p.Y - 200) * 0.68f) + 10);

        public void Build()
        {
            foreach (var c in GetChildren())
            {
                c.QueueFree();
            }

            var t = Owner2._t;
            for (var i = 0; i < t.Provinces.Length; i++)
            {
                var p = i;
                var b = new Button { Name = $"Prov_{t.Provinces[i].Id}", Text = t.Provinces[i].Name, Flat = true, Position = At(t.Provinces[i]) + new Vector2(-40, 14), Size = new Vector2(80, 20) };
                b.AddThemeFontSizeOverride("font_size", 11);
                b.Pressed += () => Owner2.ProvincePressed(p);
                AddChild(b);
            }
        }

        public override void _Draw()
        {
            var t = Owner2._t;
            var s = Owner2._s;
            DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.12f, 0.15f, 0.18f));
            foreach (var p in t.Provinces)
            {
                foreach (var n in p.Neighbours)
                {
                    DrawLine(At(p), At(t.Provinces[n]), new Color(0.4f, 0.45f, 0.5f), 1.5f);
                }
            }

            var colors = GameSettings.SideColors[GameSettings.Palette];
            for (var i = 0; i < t.Provinces.Length; i++)
            {
                var side = t.SideOf(s.Provinces[i].Owner);
                var c = side == t.SideOf(Owner2._player) ? colors.Friend : side >= 0 ? colors.Enemy : new Color(0.6f, 0.6f, 0.6f);
                DrawCircle(At(t.Provinces[i]), 14, c);
                var strength = s.Armies.Where(a => a.Province == i).Sum(a => a.Strength);
                if (strength > 0)
                {
                    DrawString(ThemeDB.FallbackFont, At(t.Provinces[i]) + new Vector2(-10, 5), strength.ToString(System.Globalization.CultureInfo.InvariantCulture), HorizontalAlignment.Left, -1, 12, Colors.White);
                }
            }
        }
    }
}
