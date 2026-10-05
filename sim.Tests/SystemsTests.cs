namespace SixActual.Sim.Tests;

public class SystemsTests
{
    [Fact]
    public void Fog_hides_an_enemy_until_it_is_spotted()
    {
        var state = BattleSim.CreateDuel(600, 10, hill: true);
        Assert.False(BattleSim.SeenBy(state, 0, 1));
        state.Units[1].Spotted = 1;
        Assert.True(BattleSim.SeenBy(state, 0, 1));
        Assert.True(BattleSim.SeenBy(state, 1, 1));
    }

    [Fact]
    public void Fog_shortens_detection_against_the_clear_case()
    {
        var clear = BattleSim.CreateDuel(600, 10, hill: false);
        clear.Defs[0].OpticsM = 180;
        clear.Weather = 0;
        var fog = BattleSim.CreateDuel(600, 10, hill: false);
        fog.Defs[0].OpticsM = 180;
        fog.Weather = 2;
        BattleSim.Step(clear, ReadOnlySpan<BattleOrder>.Empty);
        BattleSim.Step(fog, ReadOnlySpan<BattleOrder>.Empty);
        Assert.True(BattleSim.SeenBy(clear, 0, 1));
        Assert.False(BattleSim.SeenBy(fog, 0, 1));
    }

    [Fact]
    public void Overmatch_sets_a_critical_flag()
    {
        var state = BattleSim.CreateDuel(armorFront: 20, pen: 400, hill: false, fromSide: true);
        state.Aggression0 = 0;
        state.Aggression1 = 0;
        BattleSim.Run(state, 4, ReadOnlySpan<BattleOrder>.Empty);
        Assert.True(state.Units[1].Flags != 0);
    }

    [Fact]
    public void Suppression_breaks_morale_and_the_unit_bails()
    {
        var state = BattleSim.CreateDuel(600, 10, hill: true);
        state.Units[0].Ammo = 0;
        state.Units[1].Ammo = 0;
        state.Units[1].Suppression = 90;
        state.Units[1].Morale = 1;
        BattleSim.Step(state, ReadOnlySpan<BattleOrder>.Empty);
        Assert.Equal(0, state.Units[1].Alive);
        Assert.NotEqual(0, state.Units[1].Flags & SixActual.Sim.State.BattleState.FlagBail);
    }

    [Fact]
    public void Artillery_impacts_after_the_delay()
    {
        var state = BattleSim.CreateArtillery();
        var before = state.Units[1].Hp;
        BattleSim.Run(state, 10, ReadOnlySpan<BattleOrder>.Empty);
        Assert.Equal(before, state.Units[1].Hp);
        BattleSim.Run(state, 40, ReadOnlySpan<BattleOrder>.Empty);
        Assert.True(state.Units[1].Hp < before);
    }

    [Fact]
    public void The_staff_has_nine_seats()
    {
        Assert.Equal(9, BattleSim.CommanderSeats.Length);
        Assert.Equal("XO", BattleSim.CommanderSeats[0]);
        Assert.Equal("Logistics", BattleSim.CommanderSeats[8]);
    }

    [Fact]
    public void Climbing_burns_more_fuel_than_level_flight()
    {
        var climbing = BattleSim.CreateFlight(fuel: 400, ammo: 20, destroy: false);
        climbing.Units[0].Z = Fp.Zero;
        climbing.Units[0].HasGoal = 1;
        var level = BattleSim.CreateFlight(fuel: 400, ammo: 20, destroy: false);
        level.Units[0].Z = Fp.FromInt(200);
        level.Units[0].HasGoal = 1;
        BattleSim.Run(climbing, 8, ReadOnlySpan<BattleOrder>.Empty);
        BattleSim.Run(level, 8, ReadOnlySpan<BattleOrder>.Empty);
        Assert.True(climbing.Units[0].Fuel < level.Units[0].Fuel);
    }

    [Fact]
    public void A_wall_is_left_behind()
    {
        var state = BattleSim.CreateWall();
        BattleSim.Run(state, 500, ReadOnlySpan<BattleOrder>.Empty);
        Assert.True(state.Units[0].X.Raw > Fp.FromInt(140).Raw);
    }
}
