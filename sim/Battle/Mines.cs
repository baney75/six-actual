using SixActual.Sim.State.Battle;

namespace SixActual.Sim.Battle;

/// <summary>
/// SUP-03 minefields. A minefield is a terrain layer: density per cell (percent chance that a vehicle entering the
/// cell strikes a mine; a quarter of that for infantry, whose risk is the anti-personnel share) and the laying team,
/// whose own units know the lanes. A strike on a vehicle throws a track and deals mine_damage; on infantry it causes
/// casualties as a hit of mine_damage / 2. Each strike thins the cell by mine_deplete. Minefields come from map
/// features or from scatterable-mine support (Support type minefield).
/// </summary>
public static class Mines
{
    public static void Enter(WorldState w, BattleRules r, int unit)
    {
        var t = w.Terrain;
        if (t.MineDensity.Length == 0)
        {
            return;
        }

        ref var u = ref w.Units[unit];
        var def = w.Defs[u.Def];
        var cx = t.CellOf(u.X);
        var cy = t.CellOf(u.Y);
        if (def.IsAir || !t.InCell(cx, cy))
        {
            return;
        }

        var i = t.Index(cx, cy);
        var density = t.MineDensity[i];
        if (density == 0 || t.MineTeam[i] == Frontline.TeamOf(w, u.Player) + 1)
        {
            return;
        }

        var chance = def.IsInfantry ? density / 4 : density;
        if ((int)(BattleWorld.NextRandom(w) % 100UL) >= chance)
        {
            return;
        }

        t.MineDensity[i] = (byte)Math.Max(0, density - r.MineDepletePct);
        t.Version++;
        w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.MineStrike, A = u.Id, B = cx * t.CellM, C = cy * t.CellM, Value = t.MineDensity[i] });
        if (def.IsInfantry)
        {
            Combat.MineCasualties(w, r, unit, r.MineDamage / 2);
            return;
        }

        u.Flags |= Combat.FlagTracked;
        u.HasGoal = 0;
        u.Hp -= r.MineDamage;
        Combat.KillIfDead(w, r, -1, unit);
    }

    /// <summary>Lays a scatterable minefield of `density` in every cell within `radiusM` of (x, y) for `team`.</summary>
    public static void Lay(WorldState w, int team, int x, int y, int radiusM, int density)
    {
        var t = w.Terrain;
        var r2 = (long)radiusM * radiusM;
        for (var cy = Math.Max(0, (y - radiusM) / t.CellM); cy <= Math.Min(t.Cells - 1, (y + radiusM) / t.CellM); cy++)
        {
            for (var cx = Math.Max(0, (x - radiusM) / t.CellM); cx <= Math.Min(t.Cells - 1, (x + radiusM) / t.CellM); cx++)
            {
                long dx = (cx * t.CellM) + (t.CellM / 2) - x;
                long dy = (cy * t.CellM) + (t.CellM / 2) - y;
                if ((dx * dx) + (dy * dy) <= r2)
                {
                    var i = t.Index(cx, cy);
                    t.MineDensity[i] = (byte)Math.Max(t.MineDensity[i], density);
                    t.MineTeam[i] = (byte)(team + 1);
                }
            }
        }

        t.Version++;
    }
}
