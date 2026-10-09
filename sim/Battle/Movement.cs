using SixActual.Sim.State.Battle;

namespace SixActual.Sim.Battle;

/// <summary>SIM-12 ground movement along A* paths; terrain sets speed per movement class.</summary>
public static class Movement
{
    public static void Step(WorldState w, BattleRules r)
    {
        if (w.Paths.Length != w.Units.Length)
        {
            Array.Resize(ref w.Paths, w.Units.Length);
        }

        for (var i = 0; i < w.Units.Length; i++)
        {
            ref var u = ref w.Units[i];
            if (u.Alive == 0 || u.HasGoal == 0)
            {
                continue;
            }

            var def = w.Defs[u.Def];
            var mc = def.MoveClass;
            if ((u.Flags & Combat.FlagTracked) != 0)
            {
                // Immobilised: a thrown track cancels any move until repaired (SUP-03).
                u.HasGoal = 0;
                continue;
            }

            if ((u.MoraleState == Morale.Pinned && u.Retreating == 0) || u.MoraleState == Morale.Surrendered)
            {
                continue;
            }

            if (mc == Navigation.None)
            {
                if (u.OffMapUntil == 0)
                {
                    MoveStraight(w, ref u, def.SpeedMmS);
                    Supply.Burn(w, ref u, def, (long)def.SpeedMmS * Fp.OneRaw / 1000 / r.TickHz);
                }

                continue;
            }

            if (u.PathStep < 0 || w.Paths[i] is null)
            {
                Navigation.EnsureGrid(w, r);
                var path = Navigation.FindPath(w, mc, Navigation.NavIndex(w, u.X, u.Y), Navigation.NavIndex(w, u.GoalX, u.GoalY));
                if (path is null)
                {
                    u.HasGoal = 0;
                    u.NoPath = 1;
                    w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.NoPath, A = u.Id });
                    continue;
                }

                w.Paths[i] = path;
                u.PathStep = 0;
                u.NoPath = 0;
            }

            if (def.FuelPerKm > 0 && u.Fuel <= 0)
            {
                u.HasGoal = 0;
                continue;
            }

            var surface = Navigation.SurfaceAt(w.Terrain, u.X, u.Y);
            var speed = Mounted(w, r, u, def)
                ? (long)def.MountedSpeedMmS * r.SpeedPct[def.MountedMoveClass][surface] / 100
                : (long)def.SpeedMmS * r.SpeedPct[mc][surface] / 100;
            if ((u.Flags & Combat.FlagEngine) != 0)
            {
                speed /= 2;
            }

            var cellBefore = w.Terrain.CellOf(u.X) + (w.Terrain.CellOf(u.Y) * w.Terrain.Cells);
            var budget = speed * Fp.OneRaw / 1000 / r.TickHz;
            var full = budget;

            // Spend the whole tick's movement, carrying any remainder past each waypoint.
            while (budget > 0 && u.HasGoal != 0)
            {
                var waypoints = w.Paths[i];
                var final = u.PathStep >= waypoints.Length - 1;
                Fp tx, ty;
                if (final)
                {
                    tx = u.GoalX;
                    ty = u.GoalY;
                }
                else
                {
                    CellCentre(w, waypoints[u.PathStep], out tx, out ty);
                }

                budget = Advance(ref u, tx, ty, budget);
                if (budget < 0)
                {
                    break;
                }

                if (final)
                {
                    u.HasGoal = 0;
                    u.PathStep = -1;
                    w.Paths[i] = [];
                    w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Arrived, A = u.Id });
                }
                else
                {
                    u.PathStep++;
                }
            }

            u.MovedThisTick = 1;
            u.LastProgressTick = w.Tick;
            Supply.Burn(w, ref u, def, budget < 0 ? full : full - budget);
            var cellAfter = w.Terrain.CellOf(u.X) + (w.Terrain.CellOf(u.Y) * w.Terrain.Cells);
            if (cellAfter != cellBefore)
            {
                Mines.Enter(w, r, i);
            }
        }

        Retreats(w);
    }

    /// <summary>MEC-04: a retreating unit that has reached home is reinforced (full strength, normal morale, no
    /// suppression) and keeps its veterancy.</summary>
    static void Retreats(WorldState w)
    {
        for (var i = 0; i < w.Units.Length; i++)
        {
            ref var u = ref w.Units[i];
            if (u.Retreating == 0 || u.Alive == 0 || u.HasGoal != 0)
            {
                continue;
            }

            long dx = (u.X.Raw >> Fp.FractionalBits) - u.HomeX;
            long dy = (u.Y.Raw >> Fp.FractionalBits) - u.HomeY;
            if ((dx * dx) + (dy * dy) > 30L * 30)
            {
                continue;
            }

            var def = w.Defs[u.Def];
            u.Retreating = 0;
            u.Hp = def.Hp;
            u.Members = def.Members;
            u.Suppression = 0;
            u.MoraleState = Morale.Normal;
            w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Reinforced, A = u.Id });
        }
    }

    /// <summary>
    /// Moves toward (tx, ty) by up to budget (raw metres). Returns the unspent budget when the point is
    /// reached this tick (>= 0), or -1 when the whole budget was used short of it.
    /// </summary>
    static long Advance(ref UnitState u, Fp tx, Fp ty, long budget)
    {
        var dx = tx.Raw - u.X.Raw;
        var dy = ty.Raw - u.Y.Raw;
        long len = Sight.IntSqrt((dx * dx) + (dy * dy));
        if (len > 0)
        {
            u.Heading = HeadingOf(Fp.FromRaw(dx), Fp.FromRaw(dy));
        }

        if (len <= budget)
        {
            u.X = tx;
            u.Y = ty;
            return budget - len;
        }

        u.X = Fp.FromRaw(u.X.Raw + (dx * budget / len));
        u.Y = Fp.FromRaw(u.Y.Raw + (dy * budget / len));
        return -1;
    }

    static void MoveStraight(WorldState w, ref UnitState u, int speedMmS)
    {
        var stepRaw = (long)speedMmS * Fp.OneRaw / 1000 / 20;
        if (Advance(ref u, u.GoalX, u.GoalY, stepRaw) >= 0)
        {
            u.HasGoal = 0;
            w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Arrived, A = u.Id });
        }

        u.MovedThisTick = 1;
    }

    public static void CellCentre(WorldState w, int cell, out Fp x, out Fp y)
    {
        var half = w.NavCellM / 2;
        x = Fp.FromInt(((cell % w.NavCells) * w.NavCellM) + half);
        y = Fp.FromInt(((cell / w.NavCells) * w.NavCellM) + half);
    }

    /// <summary>Binary-angle heading (0..65535, 0 = east, counter-clockwise) of a vector.</summary>
    public static int HeadingOf(Fp dx, Fp dy)
    {
        var a = Trig.Atan2(dy, dx).Raw;
        if (a < 0)
        {
            a += Trig.TwoPi.Raw;
        }

        return (int)(a * 65536 / Trig.TwoPi.Raw) & 0xFFFF;
    }

    /// <summary>MEC-09: a carried infantry team rides its carrier unless it fired, was hit or was suppressed within
    /// the last dismount_ticks.</summary>
    public static bool Mounted(WorldState w, BattleRules r, in UnitState u, BattleUnitDef def)
    {
        if (def.MountedSpeedMmS <= 0 || u.Suppression > 0)
        {
            return false;
        }

        var firedAt = u.RevealUntilTick - r.RevealOnFireTicks;
        var recentFire = u.RevealUntilTick > 0 && w.Tick - firedAt < r.DismountTicks;
        var recentHit = u.LastHitByTick > 0 && w.Tick - u.LastHitByTick < r.DismountTicks;
        return !recentFire && !recentHit;
    }
}
