namespace SixActual.Sim.State;

public sealed class MatchState
{
    public int Tick;

    public ulong Seed;

    public EntityState[] Entities = [];
}
