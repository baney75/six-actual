namespace SixActual.Sim;

public static class Trig
{
    public static readonly Fp Pi = Fp.FromRaw(205887);

    public static readonly Fp TwoPi = Fp.FromRaw(411774);

    static readonly int[] Atan = [51472, 30386, 16055, 8150, 4091, 2045, 1024, 512, 256, 128, 64, 32, 16, 8, 4, 2];

    public static void SinCos(Fp radians, out Fp sin, out Fp cos)
    {
        var z = Wrap(radians.Raw);
        var cosineSign = 1;
        var half = Pi.Raw / 2;
        if (z > half)
        {
            z = (int)(Pi.Raw - z);
            cosineSign = -1;
        }
        else if (z < -half)
        {
            z = (int)(-Pi.Raw - z);
            cosineSign = -1;
        }

        long x = 39797;
        long y = 0;
        for (var i = 0; i < Atan.Length; i++)
        {
            long nextX;
            long nextY;
            if (z >= 0)
            {
                nextX = x - (y >> i);
                nextY = y + (x >> i);
                z -= Atan[i];
            }
            else
            {
                nextX = x + (y >> i);
                nextY = y - (x >> i);
                z += Atan[i];
            }

            x = nextX;
            y = nextY;
        }

        sin = Fp.FromRaw(y);
        cos = Fp.FromRaw(x * cosineSign);
    }

    public static Fp Atan2(Fp y, Fp x)
    {
        if (x.Raw == 0 && y.Raw == 0)
        {
            return Fp.Zero;
        }

        long px = x.Raw < 0 ? -x.Raw : x.Raw;
        long py = y.Raw < 0 ? -y.Raw : y.Raw;
        var z = 0;
        for (var i = 0; i < Atan.Length; i++)
        {
            long nextX;
            long nextY;
            if (py >= 0)
            {
                nextX = px + (py >> i);
                nextY = py - (px >> i);
                z += Atan[i];
            }
            else
            {
                nextX = px - (py >> i);
                nextY = py + (px >> i);
                z -= Atan[i];
            }

            px = nextX;
            py = nextY;
        }

        if (x.Raw >= 0)
        {
            z = y.Raw < 0 ? -z : z;
        }
        else
        {
            z = y.Raw < 0 ? (int)(-Pi.Raw + z) : (int)(Pi.Raw - z);
        }

        return Fp.FromRaw(z);
    }

    public static void FacingVector(int facing, out Fp east, out Fp north)
    {
        var angle = Fp.FromRaw(TwoPi.Raw * (facing & 255) / 256);
        SinCos(angle, out north, out east);
    }

    static long Wrap(long raw)
    {
        var turn = TwoPi.Raw;
        raw %= turn;
        if (raw > Pi.Raw)
        {
            raw -= turn;
        }
        else if (raw < -Pi.Raw)
        {
            raw += turn;
        }

        return raw;
    }
}
