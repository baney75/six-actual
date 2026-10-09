using SixActual.Sim.State.Battle;

namespace SixActual.Sim.Battle;

/// <summary>
/// SIM-08 suppression and morale: normal, shaken, pinned, routed, surrendered. Suppression decays each tick,
/// faster near a friendly command post. Going up the ladder is immediate; coming back down needs
/// suppression to drop recover_margin below the threshold. Pinned units stop; routed units fall back home
/// and surrender if a detected enemy stays close for long enough.
/// </summary>
public static class Morale
{
    public const int Normal = 0;

    public const int Shaken = 1;

    public const int Pinned = 2;

    public const int Routed = 3;

    public const int Surrendered = 4;

    public static void Step(WorldState w, BattleRules r)
    {
        for (var i = 0; i < w.Units.Length; i++)
        {
            ref var u = ref w.Units[i];
            if (u.Alive == 0 || u.MoraleState == Surrendered || w.Defs[u.Def].IsAir)
            {
                continue;
            }

            // Cut off or standing in enemy ground: suppression does not come off (MEC-02, C1-2).
            var decay = Frontline.IsEncircled(w, i) || !Frontline.InFriendlyTerritory(w, r, i)
                ? 0
                : r.DecayPerTick + (NearHq(w, r, i) ? r.HqDecayBonus : 0);
            u.Suppression = Math.Max(0, u.Suppression - decay);

            var next = Ladder(r, u.MoraleState, u.Suppression);
            if (next != u.MoraleState)
            {
                Enter(w, ref u, next);
            }

            if (u.MoraleState == Routed && w.Tick - u.RoutedSinceTick >= r.SurrenderAfterTicks && EnemyClose(w, r, i))
            {
                u.MoraleState = Surrendered;
                u.Alive = 0;
                u.HasGoal = 0;
                w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Surrendered, A = u.Id });
            }
        }
    }

    static int Ladder(BattleRules r, int current, int sup)
    {
        int[] at = [0, r.ShakenAt, r.PinnedAt, r.RoutedAt];
        var up = sup >= r.RoutedAt ? Routed : sup >= r.PinnedAt ? Pinned : sup >= r.ShakenAt ? Shaken : Normal;
        if (up >= current)
        {
            return up;
        }

        // Step down one rung at a time, only once well below the current rung's threshold.
        return sup < at[current] - r.RecoverMargin ? current - 1 : current;
    }

    static void Enter(WorldState w, ref UnitState u, int state)
    {
        var was = u.MoraleState;
        u.MoraleState = state;
        w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.MoraleChange, A = u.Id, B = was, Value = state });
        if (state == Routed)
        {
            u.RoutedSinceTick = w.Tick;
            u.HasGoal = 1;
            u.GoalX = Fp.FromInt(u.HomeX);
            u.GoalY = Fp.FromInt(u.HomeY);
            u.PathStep = -1;
        }
        else if (state == Pinned && was < Pinned)
        {
            u.HasGoal = 0;
        }
    }

    static bool NearHq(WorldState w, BattleRules r, int i)
    {
        var r2 = (long)r.HqRadiusM * r.HqRadiusM;
        for (var j = 0; j < w.Units.Length; j++)
        {
            if (j == i || w.Units[j].Alive == 0 || w.Units[j].IsHq == 0 || w.Units[j].Player != w.Units[i].Player)
            {
                continue;
            }

            long dx = (w.Units[j].X.Raw - w.Units[i].X.Raw) >> Fp.FractionalBits;
            long dy = (w.Units[j].Y.Raw - w.Units[i].Y.Raw) >> Fp.FractionalBits;
            if ((dx * dx) + (dy * dy) <= r2)
            {
                return true;
            }
        }

        return false;
    }

    static bool EnemyClose(WorldState w, BattleRules r, int i)
    {
        var me = w.Units[i];
        for (var j = 0; j < w.Units.Length; j++)
        {
            var e = w.Units[j];
            if (e.Alive == 0 || e.Player == me.Player || (e.SpottedBy & (1 << me.Player)) == 0)
            {
                continue;
            }

            if (Sight.MetresBetween(me, e) <= r.SurrenderEnemyM)
            {
                return true;
            }
        }

        return false;
    }
}
