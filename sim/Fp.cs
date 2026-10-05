using System.Globalization;

namespace SixActual.Sim;

/// <summary>
/// Signed fixed-point value, Q48.16. Arithmetic is checked and uses a 128-bit intermediate.
/// </summary>
public readonly struct Fp : IEquatable<Fp>
{
    public const int FractionalBits = 16;
    public const long OneRaw = 1L << FractionalBits;

    public static Fp Zero => default;

    public static Fp One => new(OneRaw);

    public long Raw { get; }

    public Fp(long raw)
    {
        Raw = raw;
    }

    public static Fp FromRaw(long raw) => new(raw);

    public static Fp FromInt(int value) => new(checked((long)value << FractionalBits));

    public static Fp operator +(Fp left, Fp right) => new(checked(left.Raw + right.Raw));

    public static Fp operator -(Fp left, Fp right) => new(checked(left.Raw - right.Raw));

    public static Fp operator -(Fp value) => new(checked(-value.Raw));

    public static Fp operator *(Fp left, Fp right)
    {
        var wide = (Int128)left.Raw * right.Raw;
        return new(checked((long)(wide >> FractionalBits)));
    }

    public static Fp operator /(Fp left, Fp right)
    {
        if (right.Raw == 0)
        {
            throw new DivideByZeroException("Fixed-point division by zero.");
        }

        var wide = ((Int128)left.Raw << FractionalBits) / right.Raw;
        return new(checked((long)wide));
    }

    public bool Equals(Fp other) => Raw == other.Raw;

    public override bool Equals(object? obj) => obj is Fp other && Equals(other);

    public override int GetHashCode() => Raw.GetHashCode();

    public override string ToString() => Raw.ToString(CultureInfo.InvariantCulture);

    public static bool operator ==(Fp left, Fp right) => left.Equals(right);

    public static bool operator !=(Fp left, Fp right) => !left.Equals(right);
}
