using SixActual.Sim.State;

namespace SixActual.Sim;

public static class Simulation
{
    public const int TickHz = 20;

    public static readonly Fp TickDuration = Fp.One / Fp.FromInt(TickHz);

    public const int MaxEntities = 100_000;

    public static MatchState Create(ulong seed, int entityCount)
    {
        if (entityCount < 0 || entityCount > MaxEntities)
        {
            throw new ArgumentOutOfRangeException(nameof(entityCount));
        }

        var rng = new XorShift64(seed);
        var entities = new EntityState[entityCount];
        for (var i = 0; i < entityCount; i++)
        {
            entities[i] = new EntityState
            {
                Id = i,
                X = Fp.FromInt((int)(rng.NextULong() % 8000) - 4000),
                Y = Fp.FromInt((int)(rng.NextULong() % 8000) - 4000),
                Vx = Fp.FromRaw((long)(rng.NextULong() % 2048) - 1024),
                Vy = Fp.FromRaw((long)(rng.NextULong() % 2048) - 1024),
            };
        }

        return new MatchState
        {
            Tick = 0,
            Seed = seed,
            Entities = entities,
        };
    }

    public static void Step(MatchState state, ReadOnlySpan<Order> orders, int threads)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (threads < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(threads));
        }

        for (var i = 0; i < orders.Length; i++)
        {
            if (orders[i].Tick == state.Tick)
            {
                Apply(state, orders[i]);
            }
        }

        Integrate(state, threads);
        state.Tick++;
    }

    public static MatchState Run(ulong seed, int entityCount, int ticks, int threads, ReadOnlySpan<Order> orders)
    {
        if (ticks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticks));
        }

        var state = Create(seed, entityCount);
        for (var i = 0; i < ticks; i++)
        {
            Step(state, orders, threads);
        }

        return state;
    }

    public static string LockstepHash(int clients, ulong seed, int entityCount, int ticks, int threads, ReadOnlySpan<Order> orders)
    {
        if (clients < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(clients));
        }

        string? agreed = null;
        for (var client = 0; client < clients; client++)
        {
            var hash = Hashing.Hash(Run(seed, entityCount, ticks, threads, orders));
            if (agreed is null)
            {
                agreed = hash;
            }
            else if (!string.Equals(agreed, hash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Lockstep desync on client {client}.");
            }
        }

        return agreed ?? string.Empty;
    }

    static void Apply(MatchState state, Order order)
    {
        var index = order.EntityId;
        if ((uint)index >= (uint)state.Entities.Length)
        {
            return;
        }

        state.Entities[index].Vx = order.Vx;
        state.Entities[index].Vy = order.Vy;
    }

    static void Integrate(MatchState state, int threads)
    {
        var entities = state.Entities;
        if (threads == 1 || entities.Length < 2)
        {
            for (var i = 0; i < entities.Length; i++)
            {
                IntegrateOne(ref entities[i]);
            }

            return;
        }

        Parallel.For(
            0,
            entities.Length,
            new ParallelOptions { MaxDegreeOfParallelism = threads },
            i => IntegrateOne(ref entities[i]));
    }

    static void IntegrateOne(ref EntityState entity)
    {
        entity.X += entity.Vx;
        entity.Y += entity.Vy;
    }
}
