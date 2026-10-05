namespace SixActual.Sim;

/// <summary>
/// Deterministic xorshift64* generator. Seed zero maps to a fixed nonzero state.
/// </summary>
public struct XorShift64
{
    const ulong ZeroSeed = 0x9E3779B97F4A7C15UL;
    const ulong Multiplier = 0x2545F4914F6CDD1DUL;

    ulong _state;

    public XorShift64(ulong seed)
    {
        _state = seed == 0 ? ZeroSeed : seed;
    }

    public ulong NextULong()
    {
        ulong x = _state;
        x ^= x >> 12;
        x ^= x << 25;
        x ^= x >> 27;
        _state = x;
        return x * Multiplier;
    }
}
