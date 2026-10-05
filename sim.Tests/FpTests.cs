namespace SixActual.Sim.Tests;

public class FpTests
{
    [Fact]
    public void Integer_arithmetic_matches_scaled_raw_values()
    {
        Assert.Equal(Fp.FromInt(6), Fp.FromInt(2) * Fp.FromInt(3));
        Assert.Equal(Fp.FromInt(-6), Fp.FromInt(-2) * Fp.FromInt(3));
        Assert.Equal(Fp.FromInt(5), Fp.FromInt(2) + Fp.FromInt(3));
        Assert.Equal(Fp.FromInt(-1), Fp.FromInt(2) - Fp.FromInt(3));
        Assert.Equal(Fp.FromInt(-4), -Fp.FromInt(4));
        Assert.Equal(Fp.One, Fp.FromInt(1) / Fp.FromInt(1));
        Assert.Equal(Fp.FromRaw(Fp.OneRaw / 2), Fp.One / Fp.FromInt(2));
    }

    [Fact]
    public void Sub_raw_product_shifts_away()
    {
        Assert.Equal(Fp.Zero, Fp.FromRaw(1) * Fp.FromRaw(1));
    }

    [Fact]
    public void Division_by_zero_throws()
    {
        Assert.Throws<DivideByZeroException>(() => Fp.One / Fp.Zero);
    }

    [Fact]
    public void Overflow_throws()
    {
        Assert.Throws<OverflowException>(() => Fp.FromRaw(long.MaxValue) + Fp.One);
    }

    [Fact]
    public void Equality_and_text_follow_the_raw_value()
    {
        var value = Fp.FromInt(3);
        Assert.True(value.Equals(Fp.FromInt(3)));
        Assert.True(value.Equals((object)Fp.FromInt(3)));
        Assert.False(value.Equals(Fp.FromInt(4)));
        Assert.False(value.Equals("3"));
        Assert.Equal(value.GetHashCode(), Fp.FromInt(3).GetHashCode());
        Assert.Equal(value.Raw.ToString(System.Globalization.CultureInfo.InvariantCulture), value.ToString());
        Assert.True(value == Fp.FromInt(3));
        Assert.True(value != Fp.FromInt(4));
    }
}
