using SixActual.Sim.State.Battle;

namespace SixActual.Sim.Battle;

/// <summary>
/// SIM-09: ammunition per mount and fuel per unit; logistics vehicles resupply friends in radius from their
/// supply points; aircraft go home on Winchester or Bingo, spend the turnaround off-map, and return full.
/// </summary>
public static class Supply
{
    public static void Step(WorldState w, BattleRules r)
    {
        Dumps(w, r);
        for (var i = 0; i < w.Units.Length; i++)
        {
            ref var u = ref w.Units[i];
            if (u.Alive == 0)
            {
                continue;
            }

            var def = w.Defs[u.Def];
            if (def.IsAir)
            {
                Aircraft(w, r, i);
            }

            // Supply helicopters deliver while halted (landed or hovering at the drop point).
            if (def.Supply > 0 && w.Units[i].SupplyLeft > 0 && (!def.IsAir || (w.Units[i].HasGoal == 0 && w.Units[i].OffMapUntil == 0)))
            {
                Resupply(w, r, i);
            }

            if (def.Medevac != 0 && w.Units[i].HasGoal == 0 && w.Units[i].OffMapUntil == 0)
            {
                Medevac(w, r, i);
            }

            if (def.Repair != 0 && u.MovedThisTick == 0)
            {
                Repair(w, r, i);
            }
        }
    }

    /// <summary>SUP-03: each standing dump reloads its team's trucks in supply radius and resupplies every other unit
    /// of its team in radius (ammunition, one round per mount per ammo interval; fuel), drawing on its own stock.</summary>
    static void Dumps(WorldState w, BattleRules r)
    {
        var r2 = (long)r.SupplyRadiusM * r.SupplyRadiusM;
        var ammoTick = w.Tick % r.AmmoIntervalTicks == 0;
        for (var k = 0; k < w.Dumps.Length; k++)
        {
            ref var d = ref w.Dumps[k];
            for (var j = 0; j < w.Units.Length && d.Hp > 0 && d.SupplyLeft > 0; j++)
            {
                ref var f = ref w.Units[j];
                var fdef = w.Defs[f.Def];
                if (f.Alive == 0 || fdef.IsAir || Frontline.TeamOf(w, f.Player) != d.Team)
                {
                    continue;
                }

                long dx = (f.X.Raw >> Fp.FractionalBits) - d.X;
                long dy = (f.Y.Raw >> Fp.FractionalBits) - d.Y;
                if ((dx * dx) + (dy * dy) > r2)
                {
                    continue;
                }

                if (fdef.Supply > 0)
                {
                    var add = Math.Min(Math.Min(r.DumpRefillPerTick, fdef.Supply - f.SupplyLeft), d.SupplyLeft);
                    f.SupplyLeft += add;
                    d.SupplyLeft -= add;
                    continue;
                }

                if (ammoTick)
                {
                    for (var m = 0; m < fdef.Weapons.Length && d.SupplyLeft > 0; m++)
                    {
                        if (w.MountAmmo[f.MountStart + m] < fdef.Weapons[m].Ammo)
                        {
                            w.MountAmmo[f.MountStart + m]++;
                            d.SupplyLeft--;
                        }
                    }
                }

                if (f.Fuel < fdef.Fuel && d.SupplyLeft > 0)
                {
                    var add = Math.Min(r.FuelPerTick, fdef.Fuel - f.Fuel);
                    f.Fuel += add;
                    if ((f.Fuel % r.FuelPerSupplyPoint) < add)
                    {
                        d.SupplyLeft--;
                    }
                }
            }
        }
    }

    /// <summary>SUP-03: a halted medevac aircraft returns one lightly wounded soldier per medevac_ticks to each friendly
    /// squad below strength in supply radius that has not been hit in the last medevac_ticks.</summary>
    static void Medevac(WorldState w, BattleRules r, int heli)
    {
        var r2 = (long)r.SupplyRadiusM * r.SupplyRadiusM;
        for (var j = 0; j < w.Units.Length; j++)
        {
            ref var f = ref w.Units[j];
            var fdef = w.Defs[f.Def];
            if (f.Alive == 0 || !fdef.IsInfantry || f.Player != w.Units[heli].Player || f.Members >= fdef.Members
                || (f.LastHitByTick > 0 && w.Tick - f.LastHitByTick < r.MedevacTicks))
            {
                continue;
            }

            long dx = (f.X.Raw - w.Units[heli].X.Raw) >> Fp.FractionalBits;
            long dy = (f.Y.Raw - w.Units[heli].Y.Raw) >> Fp.FractionalBits;
            if ((dx * dx) + (dy * dy) > r2)
            {
                continue;
            }

            // RepairTicks also serves as the squad's medevac progress (infantry is never repaired by a recovery vehicle).
            if (++f.RepairTicks >= r.MedevacTicks)
            {
                f.RepairTicks = 0;
                f.Members++;
                var memberHp = Math.Max(1, fdef.Hp / Math.Max(1, fdef.Members));
                f.Hp = Math.Min(fdef.Hp, f.Members * memberHp);
                w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Medevaced, A = f.Id, B = w.Units[heli].Id, Value = f.Members });
            }
        }
    }

    static readonly int[] Repairable = [Combat.FlagTracked, Combat.FlagEngine, Combat.FlagOptics, Combat.FlagTurret];

    /// <summary>SUP-03: a halted recovery vehicle repairs every damaged friendly ground vehicle in supply radius: 1 hp
    /// per repair_interval_ticks, and one critical (track, engine, optics, turret) per repair_crit_ticks. A burning or
    /// bailed-out vehicle cannot be repaired. A withdrawn vehicle back at full health with no criticals returns to the
    /// fight.</summary>
    static void Repair(WorldState w, BattleRules r, int arv)
    {
        var r2 = (long)r.SupplyRadiusM * r.SupplyRadiusM;
        var hpTick = w.Tick % Math.Max(1, r.RepairIntervalTicks) == 0;
        for (var j = 0; j < w.Units.Length; j++)
        {
            ref var f = ref w.Units[j];
            var fdef = w.Defs[f.Def];
            if (j == arv || f.Alive == 0 || f.Player != w.Units[arv].Player || fdef.IsAir || fdef.IsInfantry
                || (f.Flags & (Combat.FlagFire | Combat.FlagBail)) != 0)
            {
                continue;
            }

            long dx = (f.X.Raw - w.Units[arv].X.Raw) >> Fp.FractionalBits;
            long dy = (f.Y.Raw - w.Units[arv].Y.Raw) >> Fp.FractionalBits;
            if ((dx * dx) + (dy * dy) > r2)
            {
                continue;
            }

            var crit = 0;
            foreach (var flag in Repairable)
            {
                if ((f.Flags & flag) != 0)
                {
                    crit = flag;
                    break;
                }
            }

            if (crit != 0 && ++f.RepairTicks >= r.RepairCritTicks)
            {
                f.Flags &= ~crit;
                f.RepairTicks = 0;
                w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Repaired, A = f.Id, B = w.Units[arv].Id, Value = crit });
            }

            if (hpTick && f.Hp < fdef.Hp)
            {
                f.Hp++;
                if (f.Hp == fdef.Hp)
                {
                    w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Repaired, A = f.Id, B = w.Units[arv].Id, Value = 0 });
                }
            }

            if (f.Withdrawing != 0 && f.Hp == fdef.Hp && (f.Flags & (Combat.FlagTracked | Combat.FlagEngine | Combat.FlagOptics | Combat.FlagTurret)) == 0)
            {
                f.Withdrawing = 0;
                f.WasDamaged = 0;
            }
        }
    }

    /// <summary>Charge fuel for distance moved (called by Movement); a vehicle with fuel use and no fuel stops.</summary>
    public static void Burn(WorldState w, ref UnitState u, BattleUnitDef def, long metresRaw)
    {
        if (def.FuelPerKm <= 0)
        {
            return;
        }

        // Millimetres, so sub-metre moves per tick are not truncated away.
        u.MetresSinceFuel += (int)((metresRaw * 1000) >> Fp.FractionalBits);
        while (u.MetresSinceFuel >= 1_000_000)
        {
            u.MetresSinceFuel -= 1_000_000;
            u.Fuel = Math.Max(0, u.Fuel - def.FuelPerKm);
        }

        if (u.Fuel == 0)
        {
            u.HasGoal = 0;
            w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.OutOfFuel, A = u.Id });
        }
    }

    static void Resupply(WorldState w, BattleRules r, int truck)
    {
        var r2 = (long)r.SupplyRadiusM * r.SupplyRadiusM;
        var ammoTick = w.Tick % r.AmmoIntervalTicks == 0;
        for (var j = 0; j < w.Units.Length && w.Units[truck].SupplyLeft > 0; j++)
        {
            ref var f = ref w.Units[j];
            if (j == truck || f.Alive == 0 || f.Player != w.Units[truck].Player || w.Defs[f.Def].IsAir || w.Defs[f.Def].Supply > 0
                || Frontline.IsEncircled(w, j))
            {
                continue;
            }

            long dx = (f.X.Raw - w.Units[truck].X.Raw) >> Fp.FractionalBits;
            long dy = (f.Y.Raw - w.Units[truck].Y.Raw) >> Fp.FractionalBits;
            if ((dx * dx) + (dy * dy) > r2)
            {
                continue;
            }

            var fdef = w.Defs[f.Def];
            var gave = false;
            if (ammoTick)
            {
                for (var m = 0; m < fdef.Weapons.Length && w.Units[truck].SupplyLeft > 0; m++)
                {
                    var slot = f.MountStart + m;
                    if (w.MountAmmo[slot] < fdef.Weapons[m].Ammo)
                    {
                        w.MountAmmo[slot]++;
                        w.Units[truck].SupplyLeft--;
                        Economy.OnSupplyDelivered(w, r, w.Units[truck].Player, 1);
                        gave = true;
                    }
                }
            }

            if (f.Fuel < fdef.Fuel && w.Units[truck].SupplyLeft > 0)
            {
                var add = Math.Min(r.FuelPerTick, fdef.Fuel - f.Fuel);
                f.Fuel += add;
                if ((f.Fuel % r.FuelPerSupplyPoint) < add)
                {
                    w.Units[truck].SupplyLeft--;
                    Economy.OnSupplyDelivered(w, r, w.Units[truck].Player, 1);
                }

                gave = true;
            }

            if (gave && w.Tick % r.AmmoIntervalTicks == 0)
            {
                w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Resupplied, A = w.Units[truck].Id, B = f.Id });
            }
        }
    }

    static void Aircraft(WorldState w, BattleRules r, int i)
    {
        ref var u = ref w.Units[i];
        var def = w.Defs[u.Def];
        if (u.OffMapUntil > 0)
        {
            if (w.Tick < u.OffMapUntil)
            {
                return;
            }

            // Back from the airbase: full load, on station at the home edge.
            u.OffMapUntil = 0;
            u.SortieDone = 0;
            u.Fuel = def.Fuel;
            u.SupplyLeft = def.Supply;
            for (var m = 0; m < def.Weapons.Length; m++)
            {
                w.MountAmmo[u.MountStart + m] = def.Weapons[m].Ammo;
            }

            w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Returned, A = u.Id });
            return;
        }

        if (u.Flown != 0)
        {
            // A human is flying it (FLY-01): Flight decides when control returns; the AI pilot waits.
            return;
        }

        var bingo = def.Fuel * r.BingoFuelPct / 100;
        var winchester = def.Weapons.Length > 0;
        for (var m = 0; m < def.Weapons.Length; m++)
        {
            if (w.MountAmmo[u.MountStart + m] > 0)
            {
                winchester = false;
            }
        }

        if ((u.Fuel <= bingo || winchester || u.SortieDone != 0) && u.HasGoal == 0 && AtHome(u))
        {
            u.OffMapUntil = w.Tick + r.AircraftTurnaroundTicks;
            u.Z = Fp.Zero;
            return;
        }

        if ((u.Fuel <= bingo || winchester || u.SortieDone != 0) && !(u.GoalX.Raw == Fp.FromInt(u.HomeX).Raw && u.GoalY.Raw == Fp.FromInt(u.HomeY).Raw && u.HasGoal != 0))
        {
            u.HasGoal = 1;
            u.GoalX = Fp.FromInt(u.HomeX);
            u.GoalY = Fp.FromInt(u.HomeY);
            w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Rtb, A = u.Id, Value = u.Fuel <= bingo ? 1 : winchester ? 2 : 3 });
        }
    }

    static bool AtHome(in UnitState u) =>
        (u.X.Raw >> Fp.FractionalBits) == u.HomeX && (u.Y.Raw >> Fp.FractionalBits) == u.HomeY;
}
