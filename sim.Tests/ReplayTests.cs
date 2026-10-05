namespace SixActual.Sim.Tests;

public class ReplayTests
{
    [Fact]
    public void Recorded_inputs_reproduce_the_state_hash()
    {
        var orders = DeterminismReport.SampleOrders();
        var encoded = ReplayLog.Write(9, 16, orders);
        var decoded = ReplayLog.Read(encoded);
        var direct = Hashing.Hash(Simulation.Run(9, 16, 80, 1, orders));
        var replayed = Hashing.Hash(Simulation.Run(decoded.Seed, decoded.EntityCount, 80, 1, decoded.Orders));
        Assert.Equal(direct, replayed);
        Assert.Equal(orders.Length, decoded.Orders.Length);
    }

    [Fact]
    public void Unknown_replay_version_is_rejected()
    {
        var bytes = ReplayLog.Write(1, 1, ReadOnlySpan<Order>.Empty);
        bytes[0] = 9;
        Assert.Throws<InvalidDataException>(() => ReplayLog.Read(bytes));
    }

    [Fact]
    public void Negative_order_count_is_rejected()
    {
        var bytes = ReplayLog.Write(1, 1, ReadOnlySpan<Order>.Empty);
        bytes[16] = 0xFF;
        bytes[17] = 0xFF;
        bytes[18] = 0xFF;
        bytes[19] = 0xFF;
        Assert.Throws<InvalidDataException>(() => ReplayLog.Read(bytes));
    }
}
