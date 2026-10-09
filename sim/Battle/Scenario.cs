using System.Text.Json;
using SixActual.Sim.State.Battle;

namespace SixActual.Sim.Battle;

/// <summary>
/// G4 scenario: terrain features, units by data id, timed orders, seeds, tick count and assertions.
/// Loading and running use integers only; statistics over seeds are the runner's job.
/// </summary>
public sealed class Scenario
{
    public string Id { get; init; } = "";

    public string Requirement { get; init; } = "";

    public string Description { get; init; } = "";

    public int Ticks { get; init; }

    public IReadOnlyList<ulong> Seeds { get; init; } = [];

    public JsonElement Terrain { get; init; }

    public IReadOnlyList<ScenarioUnit> Units { get; init; } = [];

    public IReadOnlyList<ScenarioOrder> Orders { get; init; } = [];

    public IReadOnlyList<ScenarioCheck> Checks { get; init; } = [];

    public string Weather { get; init; } = "clear";

    /// <summary>Default ROE for units that do not set one (non-combat scenarios use hold_fire).</summary>
    public int Roe { get; init; }

    public IReadOnlyList<JsonElement> Players { get; init; } = [];

    public IReadOnlyList<(int X, int Y, int Owner)> Objectives { get; init; } = [];

    public static Scenario Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = doc.RootElement.Clone();
        var seeds = new List<ulong>();
        var seedNode = root.GetProperty("seeds");
        if (seedNode.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in seedNode.EnumerateArray())
            {
                seeds.Add(s.GetUInt64());
            }
        }
        else
        {
            var start = seedNode.GetProperty("start").GetUInt64();
            var count = seedNode.GetProperty("count").GetInt32();
            for (var i = 0; i < count; i++)
            {
                seeds.Add(start + (ulong)i);
            }
        }

        var units = new List<ScenarioUnit>();
        foreach (var u in root.GetProperty("units").EnumerateArray())
        {
            units.Add(new ScenarioUnit
            {
                Tag = u.GetProperty("tag").GetString()!,
                UnitId = u.GetProperty("unit").GetString()!,
                Player = u.GetProperty("player").GetInt32(),
                X = u.GetProperty("x").GetInt32(),
                Y = u.GetProperty("y").GetInt32(),
                HeadingDeg = Opt(u, "heading_deg", 0),
                AltitudeM = Opt(u, "altitude_m", -1),
                HomeX = u.TryGetProperty("home_x", out var hx) ? hx.GetInt32() : null,
                HomeY = u.TryGetProperty("home_y", out var hy) ? hy.GetInt32() : null,
                Suppression = Opt(u, "suppression", 0),
                Cost = Opt(u, "cost", 0),
                Ammo = u.TryGetProperty("ammo", out var am) ? am.EnumerateArray().Select(x => x.GetInt32()).ToArray() : null,
                Flags = u.TryGetProperty("flags", out var fl) ? fl.EnumerateArray().Aggregate(0, (acc, f) => acc | Combat.CritFlags[Array.IndexOf(BattleRules.Crits, f.GetString()!)]) : 0,
                Fuel = u.TryGetProperty("fuel", out var fu) ? fu.GetInt32() : -1,
                Wreck = u.TryGetProperty("wreck", out var wk) && wk.GetBoolean(),
                HpPct = u.TryGetProperty("hp_pct", out var hpp) ? hpp.GetInt32() : 100,
                SupplyLeft = u.TryGetProperty("supply_left", out var sl) ? sl.GetInt32() : -1,
                Roe = u.TryGetProperty("roe", out var roe) ? RoeOf(roe.GetString()!) : -1,
                SeatName = u.TryGetProperty("seat", out var st) ? st.GetString()! : "",
            });
        }

        var orders = new List<ScenarioOrder>();
        if (root.TryGetProperty("orders", out var ordersNode))
        {
            foreach (var o in ordersNode.EnumerateArray())
            {
                orders.Add(new ScenarioOrder
                {
                    Tick = o.GetProperty("tick").GetInt32(),
                    Tag = o.TryGetProperty("tag", out var tg) ? tg.GetString()! : "",
                    Kind = o.GetProperty("kind").GetString()!,
                    X = Opt(o, "x", 0),
                    Y = Opt(o, "y", 0),
                    ZDm = Opt(o, "z_dm", 0),
                    Damage = o.TryGetProperty("rounds", out var rd) ? rd.GetInt32() : Opt(o, "damage", 0),
                    RadiusM = o.TryGetProperty("round", out var rk) ? rk.GetString() switch { "smoke" => Fires.KindSmoke, "illum" => Fires.KindIllum, _ => Fires.KindHe } : Opt(o, "radius_m", 0),
                    Player = Opt(o, "player", 0),
                    Card = Opt(o, "card", 0),
                    SeatName = o.TryGetProperty("seat", out var sn) ? sn.GetString()! : "",
                    Task = o.TryGetProperty("task", out var tk) ? tk.GetString()! : "",
                    X2 = Opt(o, "x2", 0),
                    Y2 = Opt(o, "y2", 0),
                    Deadline = Opt(o, "deadline", 0),
                    Field = o.TryGetProperty("field", out var fd) ? fd.GetString()! : "",
                    Value = Opt(o, "value", 0),
                    Request = Opt(o, "request", 0),
                    Priority = Opt(o, "priority", -1),
                    Option = o.TryGetProperty("option", out var op) ? op.GetString()! : "",
                });
            }
        }

        var checks = new List<ScenarioCheck>();
        foreach (var c in root.GetProperty("assert").EnumerateArray())
        {
            checks.Add(ScenarioCheck.Parse(c));
        }

        return new Scenario
        {
            Id = root.GetProperty("id").GetString()!,
            Requirement = root.GetProperty("requirement").GetString()!,
            Description = root.TryGetProperty("description", out var d) ? d.GetString()! : "",
            Ticks = root.GetProperty("ticks").GetInt32(),
            Era = root.TryGetProperty("era", out var era) ? era.GetString()! : "coldwar",
            FieldRequestsOn = root.TryGetProperty("field_requests", out var frq) && frq.GetBoolean(),
            TimeOfDay = root.TryGetProperty("time_of_day", out var tod) ? tod.GetString()! : "day",
            VictoryType = root.TryGetProperty("victory", out var vic) && vic.GetString() == "annihilation" ? 1 : 0,
            NuclearRelease = root.TryGetProperty("nuclear_release", out var nr) ? nr.GetInt32() : 0,
            PhaseBreaks = root.TryGetProperty("phase_breaks", out var pb)
                ? pb.EnumerateArray().Select(b => (b.GetProperty("at_tick").GetInt32(), b.TryGetProperty("outcome", out var oc) ? oc.GetInt32() : -2)).ToList()
                : [],
            Seeds = seeds,
            Terrain = root.GetProperty("terrain"),
            Units = units,
            Orders = orders,
            Checks = checks,
            Weather = root.TryGetProperty("weather", out var wx) ? wx.GetString()! : "clear",
            Roe = root.TryGetProperty("roe", out var sroe) ? RoeOf(sroe.GetString()!) : Combat.RoeFree,
            Players = root.TryGetProperty("players", out var pl) ? pl.EnumerateArray().ToList() : [],
            Objectives = root.TryGetProperty("objectives", out var ob)
                ? ob.EnumerateArray().Select(o => (o.GetProperty("x").GetInt32(), o.GetProperty("y").GetInt32(), o.TryGetProperty("owner", out var ow) ? ow.GetInt32() : -1)).ToList()
                : [],
        };
    }

    public static int RoeOf(string name) => name switch
    {
        "fire_at_will" => Combat.RoeFree,
        "return_fire" => Combat.RoeReturn,
        "hold_fire" => Combat.RoeHold,
        _ => throw new InvalidDataException($"Unknown ROE {name}."),
    };

    static int Opt(JsonElement e, string name, int fallback) =>
        e.TryGetProperty(name, out var v) ? v.GetInt32() : fallback;

    public int TagId(string tag)
    {
        for (var i = 0; i < Units.Count; i++)
        {
            if (Units[i].Tag == tag)
            {
                return i;
            }
        }

        throw new InvalidDataException($"Scenario {Id}: unknown unit tag {tag}.");
    }

    public WorldState Build(BattleData data, BattleRules rules, ulong seed)
    {
        supportIds = data.Support.Select(d => d.Id).ToArray();
        var w = new WorldState
        {
            Seed = seed,
            Rng = BattleWorld.MixSeed(seed),
            Defs = data.Units,
            Weapons = data.Weapons,
            SupportDefs = data.Support,
            FieldRequestDefs = data.FieldRequests,
            EventDefs = data.OperationEvents,
            TimeOfDay = Array.IndexOf(TimeNames, TimeOfDay),
            FieldRequestsOn = FieldRequestsOn ? 1 : 0,
            NuclearRelease = NuclearRelease,
            Era = Era,
            Weather = BattleRules.WeatherIndex(Weather),
            VictoryType = VictoryType,
            AirGrounded = rules.AirOps(BattleRules.WeatherIndex(Weather)) ? 0 : 1,
            Terrain = TerrainBuilder.Build(Terrain),
        };
        var defs = data.Units;
        var units = new UnitState[Units.Count];
        var ammo = new List<int>();
        for (var i = 0; i < Units.Count; i++)
        {
            var su = Units[i];
            var def = BattleUnitDef.IndexOf(defs, su.UnitId);
            var mountStart = ammo.Count;
            for (var m = 0; m < defs[def].Weapons.Length; m++)
            {
                ammo.Add(su.Ammo is not null && m < su.Ammo.Length ? su.Ammo[m] : defs[def].Weapons[m].Ammo);
            }

            units[i] = new UnitState
            {
                Id = i,
                Def = def,
                Player = su.Player,
                Alive = su.Wreck ? 0 : 1,
                X = Fp.FromInt(su.X),
                Y = Fp.FromInt(su.Y),
                // Without an explicit altitude, aircraft start airborne at the deploy altitude, like deployed ones.
                Z = Fp.FromInt(su.AltitudeM >= 0 ? su.AltitudeM : defs[def].IsAir ? Economy.DeployAltitudeM : 0),
                Heading = su.HeadingDeg * 65536 / 360,
                Hp = defs[def].Hp * su.HpPct / 100,
                Fuel = su.Fuel >= 0 ? su.Fuel : defs[def].Fuel,
                Flags = su.Flags,
                Morale = 100,
                PathStep = -1,
                LastAttacker = -1,
                Seat = -1,
                MountStart = mountStart,
                // An infantry unit's hp_pct is its share of members still standing.
                Members = defs[def].IsInfantry ? Math.Max(1, defs[def].Members * su.HpPct / 100) : defs[def].Members,
                HomeX = su.HomeX ?? su.X,
                HomeY = su.HomeY ?? su.Y,
                IsHq = defs[def].Kind == UnitDef.KindCommand ? 1 : 0,
                Suppression = su.Suppression,
                Roe = su.Roe >= 0 ? su.Roe : Roe,
                SupplyLeft = su.SupplyLeft >= 0 ? su.SupplyLeft : defs[def].Supply,
                Cost = su.Cost,
            };
        }

        w.Units = units;
        w.NextUnitId = units.Length;
        BuildPlayers(w, data, rules);
        Command.Init(w);
        for (var i = 0; i < Units.Count; i++)
        {
            if (Units[i].SeatName.Length > 0)
            {
                Command.Assign(w, i, Command.SeatIndex(Units[i].Player, Command.SeatRole(Units[i].SeatName)));
            }
        }

        w.Objectives = Objectives.Select(o => new Objective { X = o.X, Y = o.Y, Owner = o.Owner }).ToArray();
        for (var p = 0; p < Players.Count; p++)
        {
            if (Players[p].TryGetProperty("ai", out var ai) && ai.GetBoolean())
            {
                w.XoActive[p] = 1;
            }

            if (Players[p].TryGetProperty("aggression", out var ag))
            {
                w.Seats[Command.SeatIndex(p, Command.Xo)].Aggression = ag.GetInt32();
            }
        }
        w.MountAmmo = ammo.ToArray();
        w.MountReload = new int[ammo.Count];
        w.Paths = new int[units.Length][];
        TerrainBuilder.PlaceSmoke(w, Terrain);
        TerrainBuilder.PlaceCraters(w, rules, Terrain);
        TerrainBuilder.PlaceDumps(w, Terrain);
        Structures.Place(w, rules, Terrain);
        return w;
    }

    static readonly string[] PhaseNames = ["A", "B", "C"];

    void BuildPlayers(WorldState w, BattleData data, BattleRules rules)
    {
        var decks = new List<DeckDef>(data.Decks);
        var players = new PlayerState[Players.Count];
        var stock = new List<int>();
        for (var p = 0; p < Players.Count; p++)
        {
            var e = Players[p];
            var deckIndex = -1;
            if (e.TryGetProperty("deck", out var deckName) && deckName.ValueKind == JsonValueKind.String)
            {
                deckIndex = Array.FindIndex(data.Decks, d => d.Id == deckName.GetString());
                if (deckIndex < 0)
                {
                    throw new InvalidDataException($"Scenario {Id}: unknown deck {deckName.GetString()}.");
                }
            }
            else if (e.TryGetProperty("deck", out var deck))
            {
                var cards = new List<CardDef>();
                foreach (var c in deck.GetProperty("cards").EnumerateArray())
                {
                    cards.Add(new CardDef
                    {
                        Unit = BattleUnitDef.IndexOf(data.Units, c.GetProperty("unit").GetString()!),
                        Count = c.GetProperty("count").GetInt32(),
                        Phase = Array.IndexOf(PhaseNames, c.GetProperty("phase").GetString()!),
                        Cost = c.GetProperty("cost").GetInt32(),
                    });
                }

                deckIndex = decks.Count;
                decks.Add(new DeckDef { Id = $"{Id}_p{p}", Cards = cards.ToArray() });
            }

            players[p] = new PlayerState
            {
                Dp = e.TryGetProperty("start_dp", out var sdp) ? sdp.GetInt32() : rules.StartDp,
                Sp = e.TryGetProperty("start_sp", out var ssp) ? ssp.GetInt32() : 0,
                Profile = BattleRules.ProfileIndex(e.TryGetProperty("income", out var inc) ? inc.GetString()! : deckIndex >= 0 ? decks[deckIndex].Income : "balanced"),
                SpawnX = e.TryGetProperty("spawn_x", out var sx) ? sx.GetInt32() : 0,
                SpawnY = e.TryGetProperty("spawn_y", out var sy) ? sy.GetInt32() : 0,
                Deck = deckIndex,
                StockStart = stock.Count,
                Team = e.TryGetProperty("team", out var tm) ? tm.GetInt32() : p & 1,
            };
            if (deckIndex >= 0)
            {
                foreach (var c in decks[deckIndex].Cards)
                {
                    stock.Add(c.Count);
                }
            }
        }

        w.Decks = decks.ToArray();
        w.Players = players;
        w.CardStock = stock.ToArray();
    }

    string[] supportIds = [];

    /// <summary>Battle era (scenario "era", default "coldwar").</summary>
    public string Era { get; init; } = "coldwar";

    /// <summary>GS-04: nuclear level released by the grand-strategy escalation for this battle.</summary>
    public int NuclearRelease { get; init; }

    /// <summary>MEC-07: starting time of day (dawn, day, dusk, night).</summary>
    public string TimeOfDay { get; init; } = "day";

    /// <summary>MODE-01: scenario "victory" ("conquest" default, "annihilation").</summary>
    public int VictoryType { get; init; }

    static readonly string[] TimeNames = ["dawn", "day", "dusk", "night"];

    /// <summary>MEC-07: operation phase breaks: at the tick, Operation.Advance with the forced winner team (or the
    /// battle's winner when "outcome" is left out).</summary>
    public List<(int Tick, int Outcome)> PhaseBreaks { get; init; } = [];

    /// <summary>MEC-06: scenario "field_requests" (skirmish toggle; default off).</summary>
    public bool FieldRequestsOn { get; init; }

    public UnitOrder[] BuildOrders()
    {
        var list = new UnitOrder[Orders.Count];
        for (var i = 0; i < Orders.Count; i++)
        {
            var o = Orders[i];
            var seatIndex = o.SeatName.Length > 0 ? Command.SeatIndex(o.Player, Command.SeatRole(o.SeatName)) : -1;
            if (o.Kind == "task")
            {
                list[i] = new UnitOrder { Tick = o.Tick, Kind = UnitOrder.QueueTask, Card = seatIndex, Damage = Command.TaskKind(o.Task), X = o.X, Y = o.Y, ZDm = o.X2, RadiusM = o.Y2, Player = o.Deadline, UnitId = -1 };
                continue;
            }

            if (o.Kind == "intent")
            {
                var field = Array.IndexOf(new[] { "aggression", "casualty_tolerance", "roe", "approval", "priority" }, o.Field);
                list[i] = new UnitOrder { Tick = o.Tick, Kind = UnitOrder.SetIntent, Card = seatIndex, Damage = field, X = o.Value, UnitId = -1 };
                continue;
            }

            if (o.Kind is "approve" or "deny")
            {
                list[i] = new UnitOrder { Tick = o.Tick, Kind = UnitOrder.Decide, Card = o.Request, X = o.Kind == "approve" ? 1 : 0, Y = o.Priority, UnitId = -1 };
                continue;
            }

            if (o.Kind == "field_request")
            {
                list[i] = new UnitOrder { Tick = o.Tick, Kind = UnitOrder.FieldRequest, Player = o.Player, Card = o.Value, UnitId = -1 };
                continue;
            }

            if (o.Kind == "support")
            {
                var opt = Array.IndexOf(supportIds, o.Option);
                if (opt < 0)
                {
                    throw new InvalidDataException($"Scenario {Id}: unknown support option {o.Option}.");
                }

                list[i] = new UnitOrder { Tick = o.Tick, Kind = UnitOrder.Support, Player = o.Player, Card = opt, X = o.X, Y = o.Y, UnitId = -1 };
                continue;
            }

            if (o.Kind == "assign")
            {
                list[i] = new UnitOrder { Tick = o.Tick, Kind = UnitOrder.AssignSeat, UnitId = TagId(o.Tag), Card = seatIndex };
                continue;
            }

            list[i] = new UnitOrder
            {
                Tick = o.Tick,
                UnitId = o.Kind is "blast" or "deploy" ? -1 : TagId(o.Tag),
                Kind = o.Kind switch
                {
                    "move" => UnitOrder.Move,
                    "stop" => UnitOrder.Stop,
                    "blast" => UnitOrder.Blast,
                    "deploy" => UnitOrder.Deploy,
                    "fire_mission" => UnitOrder.FireMission,
                    "take_control" => UnitOrder.TakeControl,
                    "release" => UnitOrder.Release,
                    "stick" => UnitOrder.Stick,
                    "retreat" => UnitOrder.Retreat,
                    _ => throw new InvalidDataException($"Scenario {Id}: unknown order {o.Kind}."),
                },
                X = o.X,
                Y = o.Y,
                ZDm = o.ZDm,
                RadiusM = o.RadiusM,
                Damage = o.Damage,
                Player = o.Player,
                Card = o.Card,
            };
        }

        return list;
    }

    /// <summary>Runs one seed and returns one pass flag per check, in order.</summary>
    public bool[] RunSeed(BattleData data, BattleRules rules, ulong seed)
    {
        var w = Build(data, rules, seed);
        var orders = BuildOrders();
        var results = new bool[Checks.Count];
        var done = new bool[Checks.Count];
        for (var tick = 0; tick <= Ticks; tick++)
        {
            for (var c = 0; c < Checks.Count; c++)
            {
                if (!done[c] && Checks[c].AtTick == w.Tick)
                {
                    results[c] = Checks[c].Evaluate(this, w, rules);
                    done[c] = true;
                }
            }

            foreach (var (at, outcome) in PhaseBreaks)
            {
                if (at == w.Tick)
                {
                    Operation.Advance(w, rules, outcome >= -1 ? outcome : (w.Winner is 0 or 1 ? Frontline.TeamOf(w, w.Winner) : -1));
                }
            }

            if (tick < Ticks)
            {
                BattleWorld.Step(w, rules, orders);
            }
        }

        for (var c = 0; c < Checks.Count; c++)
        {
            if (!done[c])
            {
                throw new InvalidDataException($"Scenario {Id}: check {c} at tick {Checks[c].AtTick} is past the end ({Ticks}).");
            }
        }

        return results;
    }
}

public sealed class ScenarioUnit
{
    public string Tag { get; init; } = "";

    public string UnitId { get; init; } = "";

    public int Player { get; init; }

    public int X { get; init; }

    public int Y { get; init; }

    public int HeadingDeg { get; init; }

    public int AltitudeM { get; init; }

    public int? HomeX { get; init; }

    public int? HomeY { get; init; }

    public int Suppression { get; init; }

    public int Roe { get; init; } = -1;

    public int Cost { get; init; }

    /// <summary>Optional starting rounds per mount (test fixture to isolate one weapon).</summary>
    public int[]? Ammo { get; init; }

    /// <summary>Starting critical flags (fixture for testing their effects).</summary>
    public int Flags { get; init; }

    /// <summary>Starting fuel; -1 = full.</summary>
    public int Fuel { get; init; } = -1;

    /// <summary>MEC-04: spawn already destroyed (a wreck).</summary>
    public bool Wreck { get; init; }

    /// <summary>Starting hit points as a percentage of full (fixture for damaged units).</summary>
    public int HpPct { get; init; } = 100;

    /// <summary>SUP-03: a logistics vehicle's supply points at the start (-1 = full).</summary>
    public int SupplyLeft { get; init; } = -1;

    /// <summary>Commander seat name (e.g. "Army1"); empty = player-controlled.</summary>
    public string SeatName { get; init; } = "";
}

public sealed class ScenarioOrder
{
    public int Tick { get; init; }

    public string Tag { get; init; } = "";

    public string Kind { get; init; } = "";

    public int X { get; init; }

    public int Y { get; init; }

    public int ZDm { get; init; }

    public int RadiusM { get; init; }

    public int Damage { get; init; }

    public int Player { get; init; }

    public int Card { get; init; }

    public string SeatName { get; init; } = "";

    public string Task { get; init; } = "";

    public int X2 { get; init; }

    public int Y2 { get; init; }

    public int Deadline { get; init; }

    public string Field { get; init; } = "";

    public int Value { get; init; }

    public int Request { get; init; }

    /// <summary>approve/deny: new priority for the request (lower is served first); -1 keeps it.</summary>
    public int Priority { get; init; } = -1;

    /// <summary>support: catalogue option id (data/support.json).</summary>
    public string Option { get; init; } = "";
}

/// <summary>
/// One assertion. Per-seed it yields pass/fail. Across seeds the runner requires either every seed
/// (`all`) or a pass rate whose 95% Wilson lower bound is at least `ci_lower_pct` (statistical).
/// </summary>
public sealed class ScenarioCheck
{
    public int AtTick { get; init; }

    public string Kind { get; init; } = "";

    public string Unit { get; init; } = "";

    public string Other { get; init; } = "";

    /// <summary>Commander seat name for seat checks (with Player).</summary>
    public string Seat { get; init; } = "";

    public int Player { get; init; }

    public int Value { get; init; }

    public bool Expect { get; init; } = true;

    public int CiLowerPct { get; init; } = -1;

    public int X { get; init; }

    public int Y { get; init; }

    public int Mount { get; init; }

    public string Outcome { get; init; } = "";

    public int Face { get; init; } = -1;

    static readonly string[] Faces = ["front", "side", "rear", "top"];

    /// <summary>Aggregate checks sum their counts over every seed and are judged once by the runner.</summary>
    public bool IsAggregate => Kind == "hit_rate_parity_pct";

    /// <summary>Aggregate counts across seeds: shots and misses of Unit, then of Other.</summary>
    public long[] Agg { get; } = new long[4];

    /// <summary>MEC-08: both shooters fired at least X shots on the mount in total, and Unit's per-shot hit rate is
    /// within Value percent (relative) of Other's.</summary>
    public bool AggregatePass(out string detail)
    {
        long sa = Agg[0], ma = Agg[1], sb = Agg[2], mb = Agg[3];
        long ha = sa - ma, hb = sb - mb;
        var enough = sa >= X && sb >= X;
        // |ha/sa - hb/sb| <= Value% * hb/sb  <=>  |ha*sb - hb*sa| * 100 <= Value * hb * sa
        var close = Math.Abs((ha * sb) - (hb * sa)) * 100 <= Value * hb * sa;
        detail = $"{Unit} {ha}/{sa} hits, {Other} {hb}/{sb} hits, min shots {X}, tolerance {Value}%";
        return enough && hb > 0 && close == Expect;
    }

    public static ScenarioCheck Parse(JsonElement c) => new()
    {
        AtTick = c.GetProperty("at_tick").GetInt32(),
        Kind = c.GetProperty("check").GetString()!,
        Unit = c.TryGetProperty("unit", out var u) ? u.GetString()! : "",
        Other = c.TryGetProperty("other", out var o) ? o.GetString()! : "",
        Seat = c.TryGetProperty("seat", out var sk) ? sk.GetString()! : "",
        Player = c.TryGetProperty("player", out var p) ? p.GetInt32() : 0,
        Value = c.TryGetProperty("value", out var v) ? v.GetInt32() : 0,
        Expect = !c.TryGetProperty("expect", out var e) || e.GetBoolean(),
        CiLowerPct = c.TryGetProperty("ci_lower_pct", out var ci) ? ci.GetInt32() : -1,
        X = c.TryGetProperty("x", out var x) ? x.GetInt32() : 0,
        Y = c.TryGetProperty("y", out var y) ? y.GetInt32() : 0,
        Mount = c.TryGetProperty("mount", out var mt) ? mt.GetInt32() : 0,
        Outcome = c.TryGetProperty("outcome", out var oc) ? oc.GetString()! : "",
        Face = c.TryGetProperty("face", out var fc) ? Array.IndexOf(Faces, fc.GetString()!) : -1,
    };

    public string Describe() =>
        $"t={AtTick} {Kind}({Unit}{(Other.Length > 0 ? "," + Other : "")}{(Kind == "hits" ? $",m{Mount},{Outcome}{(Face >= 0 ? "," + Faces[Face] : "")}" : "")}{(Kind == "near" ? $",{X},{Y}" : "")}{(Kind is "spotted" or "visible_to" ? ",p" + Player : "")}{(Value != 0 ? "," + Value : "")}) expect {Expect}";

    bool Tally(WorldState w, int a, int b)
    {
        int ida = w.Units[a].Id, idb = w.Units[b].Id;
        foreach (var e in w.Events)
        {
            if (e.C != Mount || (e.A != ida && e.A != idb))
            {
                continue;
            }

            var k = e.A == ida ? 0 : 2;
            if (e.Kind == BattleEvent.Fired)
            {
                Agg[k] += e.Value;
            }
            else if (e.Kind == BattleEvent.Miss)
            {
                Agg[k + 1]++;
            }
        }

        return Expect;
    }

    public bool Evaluate(Scenario s, WorldState w, BattleRules r)
    {
        _ = r;
        return EvaluateCore(s, w, r);
    }

    bool EvaluateCore(Scenario s, WorldState w, BattleRules r)
    {
        var observed = Kind switch
        {
            "spotted" => Sight.Visible(w, Player, s.TagId(Unit)) && w.Units[s.TagId(Unit)].Player != Player,
            "line" => Sight.HasLine(w, r, w.Units[s.TagId(Unit)], w.Units[s.TagId(Other)]),
            "alive" => w.Units[s.TagId(Unit)].Alive != 0,
            "within_m" => WithinM(w, s.TagId(Unit), s.TagId(Other), Value),
            "event_spotted_count_at_least" => CountEvents(w, BattleEvent.Spotted, Player) >= Value,
            "event_lost_count_at_least" => CountEvents(w, BattleEvent.Lost, Player) >= Value,
            "near" => Near(w.Units[s.TagId(Unit)], X, Y, Value),
            "on_road" => Navigation.SurfaceAt(w.Terrain, w.Units[s.TagId(Unit)].X, w.Units[s.TagId(Unit)].Y) == 1,
            "no_path" => w.Units[s.TagId(Unit)].NoPath != 0,
            "has_goal" => w.Units[s.TagId(Unit)].HasGoal != 0,
            "chunks_standing_at_most" => CountChunks(w, Value) <= Value,
            "chunks_standing_at_least" => CountChunks(w, Value) >= Value,
            "chunk_standing" => ChunkStanding(w, Player, X, Y, Value),
            "hp_at_most" => w.Units[s.TagId(Unit)].Hp <= Value,
            "rubble_at" => Rubble(w, X, Y) >= Value,
            "dp_at_least" => w.Players[Player].Dp >= Value,
            "dp_at_most" => w.Players[Player].Dp <= Value,
            "sp_at_least" => w.Players[Player].Sp >= Value,
            "sp_at_most" => w.Players[Player].Sp <= Value,
            "phase_is" => w.Phase == Value,
            "time_of_day" => w.TimeOfDay == Value,
            "winner" => w.Winner == Value,
            "threat_warning" => w.Events.Any(e => e.Kind == BattleEvent.ThreatWarning && e.A == w.Units[s.TagId(Unit)].Id && e.Value == Value),
            "operation_phase" => w.OperationPhase == Value,
            "operation_events_drawn" => w.OperationEvents.Count == Value,
            "spawn_y" => w.Players.Length > Player && w.Players[Player].SpawnY == Value,
            "crater_count_at_least" => w.CraterCount >= Value,
            "in_fortification" => Combat.InFortification(w, w.Units[s.TagId(Unit)]),
            "units_of_player_at_least" => CountUnits(w, Player) >= Value,
            "units_of_player_at_most" => CountUnits(w, Player) <= Value,
            "morale_state" => w.Units[s.TagId(Unit)].MoraleState == Value,
            "flag_set" => (w.Units[s.TagId(Unit)].Flags & Value) != 0,
            "members_at_most" => w.Units[s.TagId(Unit)].Members <= Value,
            "members_at_least" => w.Units[s.TagId(Unit)].Members >= Value,
            "supply_left_at_least" => w.Units[s.TagId(Unit)].SupplyLeft >= Value,
            "dump_alive" => Value < w.Dumps.Length && w.Dumps[Value].Hp > 0,
            "ammo_at_least" => w.MountAmmo[w.Units[s.TagId(Unit)].MountStart] >= Value,
            "ammo_at_most" => w.MountAmmo[w.Units[s.TagId(Unit)].MountStart] <= Value,
            "fuel_at_least" => w.Units[s.TagId(Unit)].Fuel >= Value,
            "fuel_at_most" => w.Units[s.TagId(Unit)].Fuel <= Value,
            "off_map" => w.Units[s.TagId(Unit)].OffMapUntil > 0,
            "hits" => Hits(w, s.TagId(Unit), s.TagId(Other), Mount, Outcome, Face),
            "field_offers_open" => (w.FieldOffers.Length > Player * FieldRequests.Offers ? Enumerable.Range(0, FieldRequests.Offers).Count(o => w.FieldOffers[(Player * FieldRequests.Offers) + o] >= 0) : 0) == Value,
            "field_offers_distinct" => w.FieldOffers.Length > Player * FieldRequests.Offers && Enumerable.Range(0, FieldRequests.Offers).Select(o => w.FieldOffers[(Player * FieldRequests.Offers) + o]).Where(x => x >= 0).Distinct().Count() == FieldRequests.Offers
                && Enumerable.Range(0, FieldRequests.Offers).All(o => w.FieldRequestDefs[w.FieldOffers[(Player * FieldRequests.Offers) + o]].Phase == w.Phase && w.FieldRequestDefs[w.FieldOffers[(Player * FieldRequests.Offers) + o]].Side == Support.PlayerSide(w, Player)),
            "field_taken_units" => w.Events.Where(e => e.Kind == BattleEvent.FieldTaken && e.A == Player).Sum(e => e.Value) == Value,
            "hit_rate_parity_pct" => Tally(w, s.TagId(Unit), s.TagId(Other)),
            "morale_at_least" => w.Units[s.TagId(Unit)].MoraleState >= Value,
            "encircled" => Frontline.IsEncircled(w, s.TagId(Unit)),
            "territory_at" => w.Territory.Length > 0 && TerritoryAt(w, X, Y) == Value,
            "suppression_at_least" => w.Units[s.TagId(Unit)].Suppression >= Value,
            "events_at_least" => CountKind(w, Value) >= Player,
            "impacts_cep_pct" => CepPct(w, X, Y) >= Player && CepPct(w, X, Y) <= Value,
            "first_impact_after" => FirstImpact(w) >= Value,
            "first_impact_before" => FirstImpact(w) >= 0 && FirstImpact(w) <= Value,
            "impact_count_at_least" => CountKind(w, BattleEvent.Impact) >= Value,
            "battery_located_by" => Located(w, s.TagId(Unit), Player),
            "objective_owner" => w.Objectives.Length > X && w.Objectives[X].Owner == Value,
            "winner_is" => w.Winner == Value,
            "withdrawing" => w.Units[s.TagId(Unit)].Withdrawing != 0,
            "assault_with_fires" => Assaults(w, withFires: true) >= Value,
            "assault_without_fires" => Assaults(w, withFires: false) >= Value,
            "request_pending" => w.Requests.Any(q => q.Id == Value && q.Status == 0),
            "request_status" => w.Requests.Any(q => q.Id == X && q.Status == Value),
            "seat_of" => w.Units[s.TagId(Unit)].Seat == Value,
            "task_staging" => w.Tasks.Count > Mount && Math.Abs(w.Tasks[Mount].StagingX - X) <= Value && Math.Abs(w.Tasks[Mount].StagingY - Y) <= Value,
            "in_forest" => Sight.ForestAt(w.Terrain, w.Units[s.TagId(Unit)].X, w.Units[s.TagId(Unit)].Y) > 50,
            "roe_is" => w.Units[s.TagId(Unit)].Roe == Value,
            "fired_any" => w.Events.Any(e => e.Kind == BattleEvent.Fired && e.A == w.Units[s.TagId(Unit)].Id),
            "hit_chance_between" => ChanceOf(w, r, s.TagId(Unit), s.TagId(Other), Mount) is var hc && hc >= X && hc <= Value,
            "veterancy_at_least" => w.Units[s.TagId(Unit)].Veterancy >= Value,
            "retreating" => w.Units[s.TagId(Unit)].Retreating != 0,
            "support_charges" => SupportCharges(w, Player, Seat) == Value,
            "support_refused" => w.Events.Any(e => e.Kind == BattleEvent.SupportRefused && e.A == Player && e.Value == Value),
            "task_prep_at_least" => w.Tasks.Count > Mount && w.Tasks[Mount].Prep >= Value * 100,
            "task_prep_at_most" => w.Tasks.Count > Mount && w.Tasks[Mount].Prep <= Value * 100,
            "in_building" => Combat.InBuilding(w, r, w.Units[s.TagId(Unit)]),
            "fired_since" => w.Events.Any(e => e.Kind == BattleEvent.Fired && e.A == w.Units[s.TagId(Unit)].Id && e.Tick >= X),
            "xo_active" => w.XoActive.Length > Player && w.XoActive[Player] != 0,
            "flown" => w.Units[s.TagId(Unit)].Flown != 0,
            "airspeed_at_least" => w.Units[s.TagId(Unit)].AirspeedMmS >= Value,
            "airspeed_at_most" => w.Units[s.TagId(Unit)].AirspeedMmS <= Value,
            "altitude_at_least" => w.Units[s.TagId(Unit)].Z.Raw >= Fp.FromInt(Value).Raw,
            "altitude_at_most" => w.Units[s.TagId(Unit)].Z.Raw <= Fp.FromInt(Value).Raw,
            "vertical_speed_between" => (long)w.Units[s.TagId(Unit)].AirspeedMmS * w.Units[s.TagId(Unit)].SinGammaPm / 1000 is var vz && vz >= X && vz <= Value,
            "heading_near" => Math.Abs((short)(w.Units[s.TagId(Unit)].Heading - X)) <= Value,
            "control_returned" => w.Events.Any(e => e.Kind == BattleEvent.ControlReturned && e.B == w.Units[s.TagId(Unit)].Id && e.Value == Value),
            "first_kill_kind" => FirstKillKind(w, w.Units[s.TagId(Unit)].Id) == Value,
            "seat_withdrawals_at_least" => SeatWithdrawals(w, Command.SeatIndex(Player, Command.SeatRole(Seat))) >= Value,
            _ => throw new InvalidDataException($"Unknown check {Kind}."),
        };
        return observed == Expect;
    }

    /// <summary>
    /// At least one hit from shooter's mount on target, every hit has the expected outcome ("pen" or "nonpen"),
    /// and (when given) every hit struck the expected face.
    /// </summary>
    static bool Hits(WorldState w, int shooter, int target, int mount, string outcome, int face)
    {
        var sid = w.Units[shooter].Id;
        var tid = w.Units[target].Id;
        var n = 0;
        foreach (var e in w.Events)
        {
            if ((e.Kind != BattleEvent.Hit && e.Kind != BattleEvent.NonPen) || e.A != sid || e.B != tid || e.C != mount)
            {
                continue;
            }

            n++;
            var pen = e.Kind == BattleEvent.Hit;
            // outcome "any": only counts hits.
            if ((outcome == "pen" && !pen) || (outcome == "nonpen" && pen) || (face >= 0 && e.Value / 10000 != face))
            {
                return false;
            }
        }

        return n > 0;
    }

    /// <summary>Median radial miss of all impacts around (x, y), in metres.</summary>
    static int MedianMiss(WorldState w, int x, int y)
    {
        var d = new List<int>();
        foreach (var e in w.Events)
        {
            if (e.Kind == BattleEvent.Impact)
            {
                long dx = e.A - x;
                long dy = e.B - y;
                d.Add(Sight.IntSqrt((dx * dx) + (dy * dy)));
            }
        }

        if (d.Count == 0)
        {
            return -1;
        }

        d.Sort();
        return d[d.Count / 2];
    }

    /// <summary>Median miss as a percentage of the expected CEP stored in check field `Mount` (metres).</summary>
    int CepPct(WorldState w, int x, int y) => Mount <= 0 ? -1 : MedianMiss(w, x, y) * 100 / Mount;

    /// <summary>Combat hit chance (%) of shooter's mount against target at their current range, cover included.</summary>
    static int ChanceOf(WorldState w, BattleRules r, int shooter, int target, int mount)
    {
        var def = w.Defs[w.Units[shooter].Def];
        var weapon = w.Weapons[def.Weapons[mount].Weapon];
        return Combat.HitChance(w, r, w.Units[shooter], w.Units[target], weapon, Sight.MetresBetween(w.Units[shooter], w.Units[target]));
    }

    /// <summary>Charges left of a support option (by id, in the check's "seat" field) for a player.</summary>
    static int SupportCharges(WorldState w, int player, string option)
    {
        var o = Array.FindIndex(w.SupportDefs, d => d.Id == option);
        if (o < 0)
        {
            return -1;
        }

        var slot = (player * w.SupportDefs.Length) + o;
        return slot < w.SupportCharges.Length ? w.SupportCharges[slot] : w.SupportDefs[o].Charges;
    }

    /// <summary>UnitDef kind of the first unit the shooter killed; -1 if none.</summary>
    static int FirstKillKind(WorldState w, int shooterId)
    {
        foreach (var e in w.Events)
        {
            if (e.Kind == BattleEvent.Killed && e.A == shooterId)
            {
                for (var i = 0; i < w.Units.Length; i++)
                {
                    if (w.Units[i].Id == e.B)
                    {
                        return w.Defs[w.Units[i].Def].Kind;
                    }
                }
            }
        }

        return -1;
    }

    /// <summary>Withdraw events for units currently assigned to the seat.</summary>
    static int SeatWithdrawals(WorldState w, int seat)
    {
        var n = 0;
        foreach (var e in w.Events)
        {
            if (e.Kind != BattleEvent.Withdraw)
            {
                continue;
            }

            for (var i = 0; i < w.Units.Length; i++)
            {
                if (w.Units[i].Id == e.A && w.Units[i].Seat == seat)
                {
                    n++;
                }
            }
        }

        return n;
    }

    static int Assaults(WorldState w, bool withFires)
    {
        var n = 0;
        foreach (var e in w.Events)
        {
            if (e.Kind == BattleEvent.AssaultStarted && (e.Value != 0) == withFires)
            {
                n++;
            }
        }

        return n;
    }

    static bool Located(WorldState w, int battery, int team)
    {
        foreach (var f in w.Fixes)
        {
            if (f.Battery == battery && f.Team == team)
            {
                return true;
            }
        }

        return false;
    }

    static int FirstImpact(WorldState w)
    {
        foreach (var e in w.Events)
        {
            if (e.Kind == BattleEvent.Impact)
            {
                return e.Tick;
            }
        }

        return -1;
    }

    static int TerritoryAt(WorldState w, int x, int y)
    {
        var n = w.FrontCells;
        var cell = w.Terrain.SizeM / Math.Max(1, n);
        return w.Territory[(Math.Clamp(y / cell, 0, n - 1) * n) + Math.Clamp(x / cell, 0, n - 1)];
    }

    /// <summary>Events of a kind (value) logged so far; used as events_at_least with player = minimum count.</summary>
    static int CountKind(WorldState w, int kind)
    {
        var n = 0;
        foreach (var e in w.Events)
        {
            n += e.Kind == kind ? 1 : 0;
        }

        return n;
    }

    static int CountUnits(WorldState w, int player)
    {
        var n = 0;
        foreach (var u in w.Units)
        {
            n += u.Alive != 0 && u.Player == player ? 1 : 0;
        }

        return n;
    }

    /// <summary>Standing chunks across all buildings (the argument is unused; kept for a uniform call shape).</summary>
    static int CountChunks(WorldState w, int unused)
    {
        _ = unused;
        var n = 0;
        foreach (var hp in w.ChunkHp)
        {
            n += hp > 0 ? 1 : 0;
        }

        return n;
    }

    /// <summary>Building index in `player`, chunk ix/iy in x/y, floor in value.</summary>
    static bool ChunkStanding(WorldState w, int building, int ix, int iy, int floor) =>
        Structures.Exists(w, w.Buildings[building], ix, iy, floor);

    static int Rubble(WorldState w, int x, int y)
    {
        var t = w.Terrain;
        var cx = x / t.CellM;
        var cy = y / t.CellM;
        return t.InCell(cx, cy) ? t.RubbleDm[t.Index(cx, cy)] : 0;
    }

    static bool Near(in UnitState u, int x, int y, int metres)
    {
        long dx = (u.X.Raw >> Fp.FractionalBits) - x;
        long dy = (u.Y.Raw >> Fp.FractionalBits) - y;
        return (dx * dx) + (dy * dy) <= (long)metres * metres;
    }

    static bool WithinM(WorldState w, int a, int b, int metres) => Sight.MetresBetween(w.Units[a], w.Units[b]) <= metres;

    static int CountEvents(WorldState w, int kind, int player)
    {
        var n = 0;
        foreach (var e in w.Events)
        {
            if (e.Kind == kind && e.A == player)
            {
                n++;
            }
        }

        return n;
    }
}
