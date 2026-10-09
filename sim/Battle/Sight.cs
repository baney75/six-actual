using SixActual.Sim.State.Battle;

namespace SixActual.Sim.Battle;

/// <summary>SIM-04 line of sight and SIM-05 detection over the heightfield, forest canopy and smoke.</summary>
public static class Sight
{
    public static int MetresBetween(in UnitState a, in UnitState b)
    {
        var dx = (a.X.Raw - b.X.Raw) >> Fp.FractionalBits;
        var dy = (a.Y.Raw - b.Y.Raw) >> Fp.FractionalBits;
        return IntSqrt((dx * dx) + (dy * dy));
    }

    public static int GroundDm(TerrainGrid t, Fp x, Fp y)
    {
        var cx = t.CellOf(x);
        var cy = t.CellOf(y);
        return t.InCell(cx, cy) ? t.HeightDm[t.Index(cx, cy)] : 0;
    }

    public static int ForestAt(TerrainGrid t, Fp x, Fp y)
    {
        var cx = t.CellOf(x);
        var cy = t.CellOf(y);
        return t.InCell(cx, cy) ? t.Forest[t.Index(cx, cy)] : 0;
    }

    public static bool HedgeAt(TerrainGrid t, Fp x, Fp y)
    {
        var cx = t.CellOf(x);
        var cy = t.CellOf(y);
        return t.InCell(cx, cy) && t.Hedge.Length > 0 && t.Hedge[t.Index(cx, cy)] > 0;
    }

    /// <summary>True when nothing blocks a ray from the observer's eye to the target's aim point.</summary>
    public static bool HasLine(WorldState w, BattleRules r, in UnitState from, in UnitState to)
    {
        var t = w.Terrain;
        long fromZ = GroundDm(t, from.X, from.Y) + r.EyeHeightDm + ToDm(from.Z);
        long toZ = GroundDm(t, to.X, to.Y) + r.TargetHeightDm + ToDm(to.Z);
        var dist = MetresBetween(from, to);
        var samples = Math.Max(1, dist / r.LosStepM);
        long canopy = 0;
        var smoke = AnySmoke(w);
        var hedges = t.Hedge.Length > 0;
        for (var i = 1; i < samples; i++)
        {
            var x = from.X + Fp.FromRaw((to.X.Raw - from.X.Raw) * i / samples);
            var y = from.Y + Fp.FromRaw((to.Y.Raw - from.Y.Raw) * i / samples);
            var line = fromZ + ((toZ - fromZ) * i / samples);

            // One cell lookup per sample (G6, D072); same tests as GroundDm, HedgeAt and ForestAt.
            var cx = t.CellOf(x);
            var cy = t.CellOf(y);
            var inside = t.InCell(cx, cy);
            var idx = inside ? t.Index(cx, cy) : 0;
            var ground = inside ? t.HeightDm[idx] : 0;
            if (ground > line)
            {
                return false;
            }

            if (inside && hedges && t.Hedge[idx] > 0 && line < ground + r.HedgeHeightDm)
            {
                return false;
            }

            var forest = inside ? t.Forest[idx] : 0;
            if (forest > 0 && line < ground + r.ForestHeightDm)
            {
                canopy += forest * (long)r.LosStepM;
                if (canopy >= r.ForestBlockPctM)
                {
                    return false;
                }
            }

            if (smoke && InSmoke(w, r, x, y, line - ground))
            {
                return false;
            }
        }

        return !Structures.Blocks(w, r, from.X, from.Y, fromZ, to.X, to.Y, toZ);
    }

    /// <summary>True when any smoke screen is active this tick (otherwise HasLine skips the per-sample smoke test).</summary>
    static bool AnySmoke(WorldState w)
    {
        for (var i = 0; i < w.Smoke.Length; i++)
        {
            ref readonly var s = ref w.Smoke[i];
            if (s.Active != 0 && s.StartTick <= w.Tick && s.ExpiresTick > w.Tick)
            {
                return true;
            }
        }

        return false;
    }

    static bool InSmoke(WorldState w, BattleRules r, Fp x, Fp y, long heightAboveGroundDm)
    {
        if (heightAboveGroundDm >= r.SmokeHeightDm)
        {
            return false;
        }

        for (var i = 0; i < w.Smoke.Length; i++)
        {
            ref readonly var s = ref w.Smoke[i];
            if (s.Active == 0 || s.StartTick > w.Tick || s.ExpiresTick <= w.Tick)
            {
                continue;
            }

            var dx = (x.Raw - s.X.Raw) >> Fp.FractionalBits;
            var dy = (y.Raw - s.Y.Raw) >> Fp.FractionalBits;
            if ((dx * dx) + (dy * dy) <= (long)s.RadiusM * s.RadiusM)
            {
                return true;
            }
        }

        return false;
    }

    static bool Lit(WorldState w, Fp x, Fp y)
    {
        foreach (var f in w.Flares)
        {
            if (f.Active == 0 || f.StartTick > w.Tick || f.ExpiresTick <= w.Tick)
            {
                continue;
            }

            long dx = (x.Raw - f.X.Raw) >> Fp.FractionalBits;
            long dy = (y.Raw - f.Y.Raw) >> Fp.FractionalBits;
            if ((dx * dx) + (dy * dy) <= (long)f.RadiusM * f.RadiusM)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Effective detection range of observer against target, in metres.</summary>
    static readonly int NightWeather = BattleRules.WeatherIndex("night");

    /// <summary>An upper bound on <see cref="DetectRange"/> for an observer: optics at the highest percentage the rules allow
    /// (BattleRules.MaxOpticsPct) plus the moving-target penalty (the most stealth can fall below zero), or the firing-reveal range.
    /// Pairs farther apart than this can never be detected, so Spot skips them without the full computation.</summary>
    static long MaxReach(WorldState w, BattleRules r, in UnitState observer) =>
        Math.Max(((long)w.Defs[observer.Def].OpticsM * r.MaxOpticsPct / 100) + r.StealthMovingPenalty, r.FiringRevealM);

    public static int DetectRange(WorldState w, BattleRules r, in UnitState observer, in UnitState target)
    {
        // Illumination restores daylight range for targets inside a flare's light (night, D8).
        var pct = r.WeatherPct(w.Weather);
        if (w.Weather == NightWeather && Lit(w, target.X, target.Y))
        {
            pct = 100;
        }

        // MEC-07 time of day darkens on top of the weather (illumination restores daylight inside its light).
        if (w.TimeOfDay >= 0 && w.TimeOfDay < r.TodOpticsPct.Length && !(w.TimeOfDay == BattleRules.TodNight && Lit(w, target.X, target.Y)))
        {
            pct = pct * r.TodOpticsPct[w.TimeOfDay] / 100;
        }

        var dark = w.TimeOfDay == BattleRules.TodNight;
        if (w.Defs[observer.Def].Thermal != 0 && ((w.Weather >= 0 && w.Weather < r.ThermalIgnores.Length && r.ThermalIgnores[w.Weather]) || dark))
        {
            pct = Math.Max(pct, r.ThermalOpticsPct);
        }

        var optics = w.Defs[observer.Def].OpticsM * pct / 100;
        if ((observer.Flags & Combat.FlagOptics) != 0)
        {
            optics /= 2;
        }

        var stealth = w.Defs[target.Def].Stealth;
        if (ForestAt(w.Terrain, target.X, target.Y) > 0)
        {
            stealth += r.StealthForestBonus;
        }

        if (target.MovedThisTick != 0)
        {
            stealth -= r.StealthMovingPenalty;
        }

        var range = optics - stealth;
        if (target.RevealUntilTick > w.Tick && range < r.FiringRevealM)
        {
            range = r.FiringRevealM;
        }

        return range < 0 ? 0 : range;
    }

    /// <summary>
    /// Recompute SpottedBy masks (a staggered share of targets per tick) and log changes. Own units are always
    /// known to their player.
    /// </summary>
    /// <summary>Player slots for per-player masks and lists: every player in the world (at least 8, at most 31, the
    /// width of the int SpottedBy mask).</summary>
    public static int PlayerSlots(WorldState w)
    {
        var n = Math.Max(8, w.Players.Length);
        foreach (var u in w.Units)
        {
            n = Math.Max(n, u.Player + 1);
        }

        return Math.Min(n, 31);
    }

    public static void Spot(WorldState w, BattleRules r)
    {
        var units = w.Units;
        var slots = PlayerSlots(w);
        var interval = Math.Max(1, r.SpotIntervalTicks);
        for (var j = 0; j < units.Length; j++)
        {
            // Staggered: a target is re-evaluated every `interval` ticks (all of them on tick 0).
            if (w.Tick != 0 && ((units[j].Id + w.Tick) % interval) != 0 && units[j].Alive != 0)
            {
                continue;
            }

            var before = units[j].SpottedBy;
            var mask = 0;
            if (units[j].Alive != 0)
            {
                for (var i = 0; i < units.Length; i++)
                {
                    // Teammates do not spot each other as enemies (N-vs-N skirmish, D073).
                    if (units[i].Alive == 0 || units[i].Player == units[j].Player || (mask & (1 << units[i].Player)) != 0
                        || Frontline.TeamOf(w, units[i].Player) == Frontline.TeamOf(w, units[j].Player))
                    {
                        continue;
                    }

                    long ddx = (units[i].X.Raw - units[j].X.Raw) >> Fp.FractionalBits;
                    long ddy = (units[i].Y.Raw - units[j].Y.Raw) >> Fp.FractionalBits;
                    var d2 = (ddx * ddx) + (ddy * ddy);
                    var bound = MaxReach(w, r, units[i]);
                    if (d2 > bound * bound)
                    {
                        continue; // beyond any possible detection range: same result as the full test, much cheaper
                    }

                    long reach = DetectRange(w, r, units[i], units[j]);
                    if (d2 <= reach * reach && HasLine(w, r, units[i], units[j]))
                    {
                        mask |= 1 << units[i].Player;
                    }
                }
            }

            if (units[j].Alive != 0 && w.Reveals.Count > 0)
            {
                // Recon flights and AWACS (MEC-03 support) reveal what is inside their footprint.
                for (var p = 0; p < slots; p++)
                {
                    if (p != units[j].Player && (mask & (1 << p)) == 0 && Frontline.TeamOf(w, p) != Frontline.TeamOf(w, units[j].Player) && Support.Revealed(w, r, p, units[j]))
                    {
                        mask |= 1 << p;
                    }
                }
            }

            units[j].SpottedBy = mask;
            var gained = mask & ~before;
            var lost = before & ~mask;
            for (var p = 0; p < slots; p++)
            {
                if ((gained & (1 << p)) != 0)
                {
                    w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Spotted, A = p, B = units[j].Id });
                }

                if ((lost & (1 << p)) != 0)
                {
                    w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Lost, A = p, B = units[j].Id });
                }
            }
        }
    }

    /// <summary>What player p is allowed to know: own units plus units it currently detects (SIM-05).</summary>
    public static bool Visible(WorldState w, int player, int unitIndex) =>
        w.Units[unitIndex].Player == player || (w.Units[unitIndex].SpottedBy & (1 << player)) != 0;

    static long ToDm(Fp z) => (z.Raw * 10) >> Fp.FractionalBits;

    public static int IntSqrt(long v)
    {
        if (v <= 0)
        {
            return 0;
        }

        // Newton from 2^ceil(bits/2), which is >= sqrt(v), so the iteration decreases monotonically to floor(sqrt).
        var bits = 64 - System.Numerics.BitOperations.LeadingZeroCount((ulong)v);
        var x = 1L << ((bits + 1) / 2);
        var y = (x + (v / x)) / 2;
        while (y < x)
        {
            x = y;
            y = (x + (v / x)) / 2;
        }

        return (int)x;
    }
}
