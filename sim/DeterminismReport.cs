namespace SixActual.Sim;

public static class DeterminismReport
{
    public static readonly int[] Ticks = [100, 1000, 12000];

    public static readonly ulong[] Seeds = [1, 2, 3];

    public static readonly int[] ThreadCounts = [1, 8];

    public const int Entities = 32;

    public static Order[] SampleOrders() =>
    [
        new() { Tick = 3, EntityId = 2, Vx = Fp.FromInt(1), Vy = Fp.FromInt(-1) },
        new() { Tick = 40, EntityId = 2, Vx = Fp.Zero, Vy = Fp.FromRaw(128) },
        new() { Tick = 40, EntityId = 9999, Vx = Fp.One, Vy = Fp.One },
    ];

    public static IEnumerable<string> Lines()
    {
        foreach (var seed in Seeds)
        {
            foreach (var threads in ThreadCounts)
            {
                foreach (var tick in Ticks)
                {
                    var hash = Hashing.Hash(Simulation.Run(seed, Entities, tick, threads, ReadOnlySpan<Order>.Empty));
                    yield return $"seed={seed} threads={threads} tick={tick} hash={hash}";
                }
            }
        }

        var orders = SampleOrders();
        var encoded = ReplayLog.Write(9, 16, orders);
        var decoded = ReplayLog.Read(encoded);
        var direct = Hashing.Hash(Simulation.Run(9, 16, 80, 1, orders));
        var replayed = Hashing.Hash(Simulation.Run(decoded.Seed, decoded.EntityCount, 80, 8, decoded.Orders));
        if (!string.Equals(direct, replayed, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Replay diverged from the recorded inputs.");
        }

        yield return $"replay=ok hash={direct}";

        var lockstep = Simulation.LockstepHash(8, 4, Entities, 1000, 8, ReadOnlySpan<Order>.Empty);
        yield return $"lockstep_clients=8 seed=4 tick=1000 hash={lockstep}";
    }

    public static string SelfTest()
    {
        Order[] orders =
        [
            new() { Tick = 0, EntityId = 0, Vx = Fp.FromInt(2), Vy = Fp.Zero },
            new() { Tick = 50, EntityId = 8, Vx = Fp.FromInt(-1), Vy = Fp.FromInt(1) },
        ];
        return Hashing.Hash(Simulation.Run(1, 16, 200, 1, orders));
    }
}
