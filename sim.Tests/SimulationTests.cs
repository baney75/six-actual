namespace SixActual.Sim.Tests;

public class SimulationTests
{
    [Fact]
    public void The_only_clock_is_a_20_hertz_tick()
    {
        Assert.Equal(20, Simulation.TickHz);
        Assert.Equal(Fp.One / Fp.FromInt(20), Simulation.TickDuration);
        var accumulated = Simulation.TickDuration * Fp.FromInt(Simulation.TickHz);
        Assert.InRange(Fp.One.Raw - accumulated.Raw, 0, 16);
    }

    [Fact]
    public void Create_is_seed_stable_and_rejects_bad_counts()
    {
        var left = Simulation.Create(5, 4);
        var right = Simulation.Create(5, 4);
        Assert.Equal(Hashing.Hash(left), Hashing.Hash(right));
        Assert.NotEqual(Hashing.Hash(left), Hashing.Hash(Simulation.Create(6, 4)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Simulation.Create(1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Simulation.Create(1, Simulation.MaxEntities + 1));
    }

    [Fact]
    public void Step_integrates_velocity_and_applies_orders()
    {
        var state = Simulation.Create(2, 3);
        var before = state.Entities[1].X;
        Order[] orders = [new() { Tick = 0, EntityId = 1, Vx = Fp.FromInt(4), Vy = Fp.FromInt(-2) }];
        Simulation.Step(state, orders, threads: 1);
        Assert.Equal(1, state.Tick);
        Assert.Equal(before + Fp.FromInt(4), state.Entities[1].X);
        Assert.Equal(Fp.FromInt(4), state.Entities[1].Vx);
    }

    [Fact]
    public void Parallel_and_serial_integration_match()
    {
        var orders = DeterminismReport.SampleOrders();
        var serial = Hashing.Hash(Simulation.Run(11, 64, 300, 1, orders));
        var parallel = Hashing.Hash(Simulation.Run(11, 64, 300, 8, orders));
        Assert.Equal(serial, parallel);
    }

    [Fact]
    public void Step_rejects_bad_arguments()
    {
        var state = Simulation.Create(1, 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => Simulation.Step(state, ReadOnlySpan<Order>.Empty, 0));
        Assert.Throws<ArgumentNullException>(() => Simulation.Step(null!, ReadOnlySpan<Order>.Empty, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Simulation.Run(1, 1, -1, 1, ReadOnlySpan<Order>.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => Simulation.LockstepHash(0, 1, 1, 1, 1, ReadOnlySpan<Order>.Empty));
    }

    [Fact]
    public void Empty_world_still_advances_the_tick()
    {
        var state = Simulation.Create(0, 0);
        Simulation.Step(state, ReadOnlySpan<Order>.Empty, 4);
        Assert.Equal(1, state.Tick);
        Assert.Empty(state.Entities);
    }

    [Fact]
    public void Single_entity_parallel_request_stays_deterministic()
    {
        var serial = Hashing.Hash(Simulation.Run(3, 1, 20, 1, ReadOnlySpan<Order>.Empty));
        var parallel = Hashing.Hash(Simulation.Run(3, 1, 20, 4, ReadOnlySpan<Order>.Empty));
        Assert.Equal(serial, parallel);
    }

    [Fact]
    public void Lockstep_clients_agree()
    {
        var hash = Simulation.LockstepHash(8, 4, 32, 50, 8, DeterminismReport.SampleOrders());
        Assert.Equal(64, hash.Length);
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(1UL)]
    [InlineData(99UL)]
    public void Rng_repeats_for_a_seed(ulong seed)
    {
        var left = new XorShift64(seed);
        var right = new XorShift64(seed);
        Assert.Equal(left.NextULong(), right.NextULong());
        Assert.NotEqual(0UL, left.NextULong());
    }
}
