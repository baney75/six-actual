namespace SixActual.Sim;

public readonly struct Order
{
    public int Tick { get; init; }

    public int EntityId { get; init; }

    public Fp Vx { get; init; }

    public Fp Vy { get; init; }
}
