using SixActual.Sim.State;

namespace SixActual.Sim.Tests;

public class BattleTests
{
    [Fact]
    public void Sine_and_cosine_match_known_angles()
    {
        Trig.SinCos(Fp.Zero, out var sin0, out var cos0);
        Assert.InRange(Math.Abs(sin0.Raw), 0, 30);
        Assert.InRange(cos0.Raw, 65000, 66000);
        Trig.SinCos(Fp.FromRaw(Trig.Pi.Raw / 2), out var sin90, out var cos90);
        Assert.InRange(sin90.Raw, 64000, 67000);
        Assert.InRange(Math.Abs(cos90.Raw), 0, 1500);
        var east = Trig.Atan2(Fp.Zero, Fp.One);
        Assert.InRange(Math.Abs(east.Raw), 0, 800);
    }

    [Fact]
    public void Front_armor_stops_a_weak_gun_and_the_side_does_not()
    {
        var front = BattleSim.CreateDuel(armorFront: 600, pen: 100, hill: false);
        var side = BattleSim.CreateDuel(armorFront: 600, pen: 100, hill: false, fromSide: true);
        front.Aggression0 = 0;
        front.Aggression1 = 0;
        side.Aggression0 = 0;
        side.Aggression1 = 0;
        BattleSim.Run(front, 8, ReadOnlySpan<BattleOrder>.Empty);
        BattleSim.Run(side, 8, ReadOnlySpan<BattleOrder>.Empty);
        Assert.Equal(80, front.Units[1].Hp);
        Assert.True(side.Units[1].Hp < 80);
    }

    [Fact]
    public void A_hill_blocks_line_of_sight()
    {
        var open = BattleSim.CreateDuel(40, 200, hill: false);
        var blocked = BattleSim.CreateDuel(40, 200, hill: true);
        BattleSim.Run(open, 6, ReadOnlySpan<BattleOrder>.Empty);
        BattleSim.Run(blocked, 6, ReadOnlySpan<BattleOrder>.Empty);
        Assert.True(open.Units[1].Hp < 80);
        Assert.Equal(80, blocked.Units[1].Hp);
    }

    [Fact]
    public void Penetration_falls_with_range()
    {
        var def = new UnitDef { PenMm = 200, PenDropPerKm = 50 };
        Assert.Equal(200, BattleSim.Penetration(def, 0));
        Assert.Equal(150, BattleSim.Penetration(def, 1000));
        Assert.Equal(0, BattleSim.Penetration(def, 5000));
    }

    [Fact]
    public void A_wall_does_not_stop_the_unit_forever()
    {
        var state = BattleSim.CreateWall();
        BattleSim.Run(state, 500, ReadOnlySpan<BattleOrder>.Empty);
        var x = (int)(state.Units[0].X.Raw >> Fp.FractionalBits);
        Assert.True(x > 140, $"stuck at {x},{(int)(state.Units[0].Y.Raw >> Fp.FractionalBits)}");
    }

    [Fact]
    public void Supply_refills_a_nearby_unit()
    {
        var state = BattleSim.CreateSupply();
        BattleSim.Run(state, 10, ReadOnlySpan<BattleOrder>.Empty);
        Assert.True(state.Units[0].Ammo > 0);
        Assert.True(state.Units[0].Fuel > 5);
    }

    [Fact]
    public void Take_control_gives_the_battle_to_the_xo()
    {
        var state = BattleSim.CreateFlight(fuel: 500, ammo: 20, destroy: false);
        BattleOrder[] orders = [new() { Tick = 0, UnitId = 0, Kind = BattleOrder.TakeControl }];
        BattleSim.Step(state, orders);
        Assert.Equal(1, state.Units[0].Controlled);
        Assert.Equal(1, state.XoCommand);
    }

    [Fact]
    public void Bingo_fuel_sends_the_aircraft_home_and_releases_control()
    {
        var state = BattleSim.CreateFlight(fuel: 10, ammo: 20, destroy: false);
        state.Units[0].Controlled = 1;
        BattleSim.Step(state, ReadOnlySpan<BattleOrder>.Empty);
        Assert.Equal(0, state.Units[0].Controlled);
        Assert.Equal(state.Units[0].HomeX, (int)(state.Units[0].GoalX.Raw >> Fp.FractionalBits));
    }

    [Fact]
    public void Empty_ammo_is_winchester()
    {
        var state = BattleSim.CreateFlight(fuel: 500, ammo: 0, destroy: false);
        state.Units[0].Controlled = 1;
        BattleSim.Step(state, ReadOnlySpan<BattleOrder>.Empty);
        Assert.Equal(0, state.Units[0].Controlled);
    }

    [Fact]
    public void Manual_release_returns_the_aircraft()
    {
        var state = BattleSim.CreateFlight(fuel: 500, ammo: 20, destroy: false);
        BattleOrder[] take = [new() { Tick = 0, UnitId = 0, Kind = BattleOrder.TakeControl }];
        BattleSim.Step(state, take);
        state.Units[0].Fuel = 500;
        state.Units[0].Controlled = 1;
        BattleOrder[] release = [new() { Tick = 1, UnitId = 0, Kind = BattleOrder.Release }];
        BattleSim.Step(state, release);
        Assert.Equal(0, state.Units[0].Controlled);
        Assert.Equal(0, state.XoCommand);
    }

    [Fact]
    public void Low_aggression_holds_and_high_aggression_advances()
    {
        var calm = BattleSim.CreateDuel(600, 10, hill: true);
        calm.Aggression0 = 0;
        calm.Units[0].HomeX = 40;
        calm.Units[0].HomeY = 200;
        calm.Defs[0].SpeedMmS = 10000;
        var bold = BattleSim.CreateDuel(600, 10, hill: true);
        bold.Aggression0 = 100;
        bold.Defs[0].SpeedMmS = 10000;
        BattleSim.Run(calm, 40, ReadOnlySpan<BattleOrder>.Empty);
        BattleSim.Run(bold, 40, ReadOnlySpan<BattleOrder>.Empty);
        Assert.True(bold.Units[0].X.Raw > calm.Units[0].X.Raw);
    }

    [Fact]
    public void Identical_battles_hash_the_same()
    {
        var left = BattleSim.CreateDuel(80, 120, hill: false);
        var right = BattleSim.CreateDuel(80, 120, hill: false);
        BattleSim.Run(left, 30, ReadOnlySpan<BattleOrder>.Empty);
        BattleSim.Run(right, 30, ReadOnlySpan<BattleOrder>.Empty);
        Assert.Equal(BattleHash.Hash(left), BattleHash.Hash(right));
    }

    [Fact]
    public void Catalog_loads_every_briefed_era()
    {
        var path = Path.Combine(FindRoot(), "data", "units.json");
        var defs = UnitCatalog.Load(path);
        Assert.Contains(defs, def => def.Id == "m1_abrams");
        Assert.Contains(defs, def => def.Id == "tiger");
        Assert.Contains(defs, def => def.Id == "f35");
        Assert.Equal(defs.Length, defs.Select(def => def.Id).Distinct().Count());
        var battle = BattleSim.CreateFulda(defs);
        var once = BattleHash.Hash(CopyRun(defs, 50));
        var twice = BattleHash.Hash(CopyRun(defs, 50));
        Assert.Equal(once, twice);
        Assert.True(battle.Units.Length > 10);
    }

    static BattleState CopyRun(UnitDef[] defs, int ticks)
    {
        var state = BattleSim.CreateFulda(defs);
        BattleSim.Run(state, ticks, ReadOnlySpan<BattleOrder>.Empty);
        return state;
    }

    static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "verify", "thresholds.toml")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("repo");
    }
}
