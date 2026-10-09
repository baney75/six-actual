using SixActual.Sim.State.Battle;

namespace SixActual.Sim.Battle;

/// <summary>
/// SUP-03 engineer support, delivered through the off-map support system: each effect works on a strip of
/// terrain cells centred on the aim point, `length_m` along the heading (the sim's convention, 0 = east,
/// counter-clockwise) and `width_m` across it.
///   bridge    a gap-crossing span (AVLB, ribbon bridge, MTU-20): water/blocked cells become passable road
///   fortify   a dug trench line (ACE, BAT-M, PZM-2): heavy cover for infantry
///   obstacle  an anti-tank ditch: stops vehicles, infantry cross
///   breach    a cleared lane (MICLIC, UR-77): mines and obstacles removed
/// </summary>
public static class Engineering
{
    public static void Apply(WorldState w, int player, int type, int x, int y, int headingDeg, int lengthM, int widthM)
    {
        var t = w.Terrain;
        var rad = Fp.FromRaw((long)headingDeg * Trig.TwoPi.Raw / 360);
        Trig.SinCos(rad, out var sin, out var cos);
        var reach = (lengthM / 2) + widthM;
        for (var cy = Math.Max(0, (y - reach) / t.CellM); cy <= Math.Min(t.Cells - 1, (y + reach) / t.CellM); cy++)
        {
            for (var cx = Math.Max(0, (x - reach) / t.CellM); cx <= Math.Min(t.Cells - 1, (x + reach) / t.CellM); cx++)
            {
                long dx = (cx * t.CellM) + (t.CellM / 2) - x;
                long dy = (cy * t.CellM) + (t.CellM / 2) - y;
                var along = ((dx * cos.Raw) + (dy * sin.Raw)) >> Fp.FractionalBits;
                var across = ((-dx * sin.Raw) + (dy * cos.Raw)) >> Fp.FractionalBits;
                if (Math.Abs(along) * 2 > lengthM || Math.Abs(across) * 2 > widthM)
                {
                    continue;
                }

                var i = t.Index(cx, cy);
                switch (type)
                {
                    case SupportDef.Bridge:
                        if (t.BuildingAt[i] == 0)
                        {
                            t.Blocked[i] = 0;
                            t.Road[i] = 100;
                        }

                        t.Obstacle[i] = 0;
                        break;
                    case SupportDef.Fortify:
                        t.Fort[i] = 100;
                        break;
                    case SupportDef.ObstacleBelt:
                        t.Obstacle[i] = 100;
                        break;
                    case SupportDef.Breach:
                        t.MineDensity[i] = 0;
                        t.MineTeam[i] = 0;
                        t.Obstacle[i] = 0;
                        break;
                }
            }
        }

        t.Version++;
        w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.EngineerWork, A = player, B = type, C = x, Value = y });
    }
}
