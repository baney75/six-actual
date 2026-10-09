using SixActual.Sim.State.Battle;

namespace SixActual.Sim.Battle;

/// <summary>
/// SIM-06 armor facing and penetration, SIM-07 criticals, and direct fire. Every unit's mounts pick the most
/// effective visible target in range, roll to hit from the weapon's accuracy table, then resolve the facing
/// armor (KE or CE by weapon type) against penetration at range. Indirect weapons fire only through fire
/// missions (SUP-01), not here.
/// </summary>
public static class Combat
{
    public const int FlagTracked = 1;

    public const int FlagOptics = 2;

    public const int FlagEngine = 4;

    public const int FlagTurret = 8;

    public const int FlagBail = 16;

    public const int FlagFire = 32;

    public static readonly int[] CritFlags = [FlagTracked, FlagOptics, FlagEngine, FlagTurret, FlagBail, FlagFire];

    public const int RoeFree = 0;

    public const int RoeReturn = 1;

    public const int RoeHold = 2;

    /// <summary>Return-fire units engage for this long after being shot at.</summary>
    public const int ReturnFireWindowTicks = 200;

    public const int FaceFront = 0;

    public const int FaceSide = 1;

    public const int FaceRear = 2;

    public const int FaceTop = 3;

    /// <summary>Ticks a unit waits before rescanning when no weapon had anything to shoot.</summary>
    public const int IdleScanTicks = 10;

    public static void Step(WorldState w, BattleRules r)
    {
        for (var i = 0; i < w.MountReload.Length; i++)
        {
            if (w.MountReload[i] > 0)
            {
                w.MountReload[i]--;
            }
        }

        // Destroyed ground vehicles this tick: wrecks give cover (MEC-04). Per world, so parallel matches never share it.
        w.Wrecks.Clear();
        for (var i = 0; i < w.Units.Length; i++)
        {
            if (w.Units[i].Alive == 0 && !w.Defs[w.Units[i].Def].IsInfantry && !w.Defs[w.Units[i].Def].IsAir)
            {
                w.Wrecks.Add(i);
            }
        }

        var spotted = SpottedLists(w);
        var los = new LosCache(w.Units.Length);
        for (var s = 0; s < w.Units.Length; s++)
        {
            if (!CanFight(w.Units[s]) || w.Units[s].Roe == RoeHold || w.Units[s].Retreating != 0
                || (w.Units[s].Roe == RoeReturn && (w.Units[s].LastHitByTick == 0 || w.Tick - w.Units[s].LastHitByTick > ReturnFireWindowTicks))
                || w.Units[s].NextScanTick > w.Tick)
            {
                continue;
            }

            var def = w.Defs[w.Units[s].Def];
            var anyReady = false;
            var fired = false;
            los.Begin(s);
            for (var m = 0; m < def.Weapons.Length; m++)
            {
                var slot = w.Units[s].MountStart + m;
                if (w.MountAmmo[slot] <= 0 || w.MountReload[slot] > 0)
                {
                    continue;
                }

                var weapon = w.Weapons[def.Weapons[m].Weapon];
                if (weapon.Indirect)
                {
                    continue;
                }

                anyReady = true;
                var target = PickTarget(w, r, s, weapon, spotted[Math.Clamp(w.Units[s].Player, 0, spotted.Length - 1)], los);
                if (target < 0)
                {
                    continue;
                }

                Fire(w, r, s, slot, weapon, target);
                fired = true;
                if (!CanFight(w.Units[s]))
                {
                    break;
                }
            }

            if (anyReady && !fired)
            {
                w.Units[s].NextScanTick = w.Tick + IdleScanTicks;
            }
        }

        Burn(w, r);
    }

    /// <summary>Per player: indices of living units of the other team that player currently detects. Teammates are
    /// never targets (an N-vs-N skirmish has several players per team, D073).</summary>
    static List<int>[] SpottedLists(WorldState w)
    {
        var n = Sight.PlayerSlots(w);
        var lists = new List<int>[n];
        for (var p = 0; p < n; p++)
        {
            lists[p] = [];
        }

        for (var t = 0; t < w.Units.Length; t++)
        {
            ref readonly var u = ref w.Units[t];
            if (u.Alive == 0 || u.MoraleState == 4 || u.SpottedBy == 0)
            {
                continue;
            }

            var team = Frontline.TeamOf(w, u.Player);
            for (var p = 0; p < n; p++)
            {
                if ((u.SpottedBy & (1 << p)) != 0 && u.Player != p && Frontline.TeamOf(w, p) != team)
                {
                    lists[p].Add(t);
                }
            }
        }

        return lists;
    }

    /// <summary>Line-of-sight results for the current shooter, shared by all its mounts this tick.</summary>
    sealed class LosCache(int units)
    {
        readonly int[] stamp = new int[units];

        readonly bool[] value = new bool[units];

        int shooter = -1;

        int gen;

        public void Begin(int s)
        {
            shooter = s;
            gen++;
        }

        public bool Line(WorldState w, BattleRules r, int target)
        {
            if (target < stamp.Length && stamp[target] == gen)
            {
                return value[target];
            }

            var v = Sight.HasLine(w, r, w.Units[shooter], w.Units[target]);
            if (target < stamp.Length)
            {
                stamp[target] = gen;
                value[target] = v;
            }

            return v;
        }
    }

    public static bool CanFight(in UnitState u) =>
        u.Alive != 0 && (u.Flags & FlagBail) == 0 && u.MoraleState < 3;

    /// <summary>
    /// Best target for this weapon: highest expected effect per metre among the player's detected enemies in
    /// range and in the weapon's domain. Line of sight is checked only from the best candidate down.
    /// </summary>
    static int PickTarget(WorldState w, BattleRules r, int shooter, WeaponDef weapon, List<int> candidates, LosCache los)
    {
        ref readonly var s = ref w.Units[shooter];
        Span<long> scores = candidates.Count <= 256 ? stackalloc long[candidates.Count] : new long[candidates.Count];
        var any = false;
        for (var k = 0; k < candidates.Count; k++)
        {
            scores[k] = 0;
            ref readonly var u = ref w.Units[candidates[k]];
            if (u.Alive == 0)
            {
                continue;
            }

            var air = u.Z.Raw > Fp.FromInt(r.AirAltitudeM).Raw;
            if (air ? !weapon.HitsAir : !weapon.HitsGround)
            {
                continue;
            }

            // FLY-06: anti-radiation missiles home only on emitting radars (radar-directed air defence).
            if (weapon.Type == "arm" && !EmitsRadar(w, u))
            {
                continue;
            }

            var range = Sight.MetresBetween(s, u);
            if (range > weapon.RangeMaxM || range < weapon.RangeMinM)
            {
                continue;
            }

            if ((s.Flags & FlagTurret) != 0 && !InArc(s.Heading, s, u, r.TurretJamHalfDeg))
            {
                continue;
            }

            var tdef = w.Defs[u.Def];
            var armor = Armor(tdef, Facing(r, u, s.X, s.Y, air || IsAirborne(r, s) || weapon.TopAttack), weapon.ChemicalEnergy);
            var pen = WeaponDef.Interp(weapon.Pen, range);

            // Expected effect: penetrating hits are worth damage; anything else only suppresses.
            long effect = pen > armor ? weapon.Damage + weapon.BlastDamage : (tdef.IsInfantry || armor == 0 ? weapon.Damage : weapon.Suppression / 4);
            if (effect <= 0)
            {
                continue;
            }

            scores[k] = effect * 100_000 / (range + 50);

            // CMD-04 priority targets: the shooter's seat prefers one target class.
            if (s.Seat >= 0 && s.Seat < w.Seats.Length && w.Seats[s.Seat].PriorityKind != 0 && w.Seats[s.Seat].PriorityKind == tdef.Kind)
            {
                scores[k] = scores[k] * r.Command.PriorityTargetPct / 100;
            }
            any = true;
        }

        while (any)
        {
            // Highest score first; ties go to the lower unit index (deterministic).
            var bestK = -1;
            for (var k = 0; k < candidates.Count; k++)
            {
                if (scores[k] > 0 && (bestK < 0 || scores[k] > scores[bestK]))
                {
                    bestK = k;
                }
            }

            if (bestK < 0)
            {
                break;
            }

            if (los.Line(w, r, candidates[bestK]))
            {
                return candidates[bestK];
            }

            scores[bestK] = 0;
        }

        return -1;
    }

    static void Fire(WorldState w, BattleRules r, int shooter, int slot, WeaponDef weapon, int target)
    {
        ref var s = ref w.Units[shooter];
        w.MountAmmo[slot] = Math.Max(0, w.MountAmmo[slot] - weapon.Salvo);
        w.MountReload[slot] = weapon.ReloadTicks;
        s.RevealUntilTick = w.Tick + r.RevealOnFireTicks;
        var mount = slot - s.MountStart;
        if (w.Units[target].Flown != 0 && weapon.Type is "aaa" or "sam" or "aam")
        {
            // FLY-06: the pilot's RWR / launch warning.
            w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.ThreatWarning, A = w.Units[target].Id, B = s.Id, Value = weapon.Type == "aaa" ? 1 : 2 });
        }

        // Value = shots actually taken (the salvo stops when the target dies), so hit rates per shot are exact (MEC-08).
        var firedAt = w.Events.Count;
        w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Fired, A = s.Id, B = w.Units[target].Id, Value = 0, C = mount });

        for (var shot = 0; shot < weapon.Salvo; shot++)
        {
            if (w.Units[target].Alive == 0)
            {
                return;
            }

            var ev = w.Events[firedAt];
            ev.Value = shot + 1;
            w.Events[firedAt] = ev;

            var range = Sight.MetresBetween(s, w.Units[target]);
            var chance = HitChance(w, r, s, w.Units[target], weapon, range);
            if ((int)(BattleWorld.NextRandom(w) % 100UL) >= chance)
            {
                Suppress(w, r, target, weapon.Suppression / 2);
                w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Miss, A = s.Id, B = w.Units[target].Id, C = mount });
                continue;
            }

            Resolve(w, r, shooter, target, weapon, range, mount);
        }
    }

    public static int HitChance(WorldState w, BattleRules r, in UnitState s, in UnitState t, WeaponDef weapon, int range)
    {
        long chance = WeaponDef.Interp(weapon.Accuracy, range);
        chance = chance * w.Defs[t.Def].SizePct / 100;
        if (s.MovedThisTick != 0)
        {
            chance = chance * r.ShooterMovingPct / 100;
        }

        if (t.MovedThisTick != 0)
        {
            chance = chance * r.TargetMovingPct / 100;
        }

        chance = chance * r.MoraleAccuracyPct[Math.Min(s.MoraleState, 3)] / 100;
        chance = chance * r.VetAccuracyPct[Math.Clamp(s.Veterancy, 0, 3)] / 100;
        chance = chance * CoverPct(w, r, s, t) / 100;
        chance = chance * (100 + Command.PrepBonusPct(w, r, s.Seat)) / 100;
        if (s.Flown != 0)
        {
            chance = chance * (100 + r.DirectControlHitPct) / 100;
        }

        chance = chance * ElectronicWarfare.HitPct(w, r, s, t, weapon) / 100;
        if (w.Weather >= 0 && w.Weather < r.WeatherAccuracyPct.Length
            && !(w.Defs[s.Def].Thermal != 0 && w.Weather < r.ThermalIgnores.Length && r.ThermalIgnores[w.Weather]))
        {
            chance = chance * r.WeatherAccuracyPct[w.Weather] / 100;
        }

        if (w.TimeOfDay >= 0 && w.TimeOfDay < r.TodAccuracyPct.Length && !(w.Defs[s.Def].Thermal != 0 && w.TimeOfDay == BattleRules.TodNight))
        {
            chance = chance * r.TodAccuracyPct[w.TimeOfDay] / 100;
        }

        return (int)Math.Clamp(chance, 0, 100);
    }

    /// <summary>MEC-04 directional cover: hit-chance multiplier for target t against shooter s. Heavy inside a standing
    /// building; light inside forest, behind a hedge or wreck on the shooter's side (within cover_object_m), or for
    /// infantry in a crater. Fire from aircraft only respects heavy cover; airborne targets get none.</summary>
    /// <summary>SUP-03: infantry in a field fortification cell (trench line) has heavy cover.</summary>
    public static bool InFortification(WorldState w, in UnitState t)
    {
        if (!w.Defs[t.Def].IsInfantry || w.Terrain.Fort.Length == 0)
        {
            return false;
        }

        var cx = w.Terrain.CellOf(t.X);
        var cy = w.Terrain.CellOf(t.Y);
        return w.Terrain.InCell(cx, cy) && w.Terrain.Fort[w.Terrain.Index(cx, cy)] > 0;
    }

    public static int CoverPct(WorldState w, BattleRules r, in UnitState s, in UnitState t)
    {
        if (IsAirborne(r, t) || r.CoverObjectM <= 0)
        {
            return 100;
        }

        if (InBuilding(w, r, t) || InFortification(w, t))
        {
            return r.CoverHeavyPct;
        }

        if (IsAirborne(r, s))
        {
            return 100;
        }

        if (Sight.ForestAt(w.Terrain, t.X, t.Y) > 50)
        {
            return r.CoverLightPct;
        }

        long tx = t.X.Raw >> Fp.FractionalBits;
        long ty = t.Y.Raw >> Fp.FractionalBits;
        long dx = (s.X.Raw >> Fp.FractionalBits) - tx;
        long dy = (s.Y.Raw >> Fp.FractionalBits) - ty;
        var len = Math.Max(1L, (long)Sight.IntSqrt((dx * dx) + (dy * dy)));
        var probe = Math.Min(r.CoverObjectM * 2 / 3, (int)len);
        if (Sight.HedgeAt(w.Terrain, Fp.FromInt((int)(tx + (dx * probe / len))), Fp.FromInt((int)(ty + (dy * probe / len)))))
        {
            return r.CoverLightPct;
        }

        long reach = r.CoverObjectM;
        foreach (var j in w.Wrecks)
        {
            long wx = (w.Units[j].X.Raw >> Fp.FractionalBits) - tx;
            long wy = (w.Units[j].Y.Raw >> Fp.FractionalBits) - ty;
            var d2 = (wx * wx) + (wy * wy);
            var dot = (wx * dx) + (wy * dy);
            // On the shooter's side, within 60 deg of the line of fire: cos > 0.5 <=> 2 dot > |w| |d|.
            if (d2 <= reach * reach && dot > 0 && 2 * dot > Sight.IntSqrt(d2) * len)
            {
                return r.CoverLightPct;
            }
        }

        if (w.Defs[t.Def].IsInfantry)
        {
            for (var c = 0; c < Math.Min(w.CraterCount, w.CraterX.Length); c++)
            {
                long cx = w.CraterX[c] - tx;
                long cy = w.CraterY[c] - ty;
                if ((cx * cx) + (cy * cy) <= reach * reach)
                {
                    return r.CoverLightPct;
                }
            }
        }

        return 100;
    }

    public static bool InBuilding(WorldState w, BattleRules r, in UnitState t)
    {
        var x = (int)(t.X.Raw >> Fp.FractionalBits);
        var y = (int)(t.Y.Raw >> Fp.FractionalBits);
        foreach (var b in w.Buildings)
        {
            var ix = (x - b.X) / Math.Max(1, r.ChunkM);
            var iy = (y - b.Y) / Math.Max(1, r.ChunkM);
            if (x >= b.X && y >= b.Y && ix < b.Nx && iy < b.Ny && Structures.Exists(w, b, ix, iy, 0))
            {
                return true;
            }
        }

        return false;
    }

    static void Resolve(WorldState w, BattleRules r, int shooter, int target, WeaponDef weapon, int range, int mount)
    {
        ref var t = ref w.Units[target];
        t.LastAttacker = shooter;
        var tdef = w.Defs[t.Def];
        var top = IsAirborne(r, t) || IsAirborne(r, w.Units[shooter]) || weapon.TopAttack;
        if (tdef.IsInfantry)
        {
            HitInfantry(w, r, shooter, target, weapon.Damage);
        }
        else
        {
            var face = Facing(r, t, w.Units[shooter].X, w.Units[shooter].Y, top);
            var armor = Armor(tdef, face, weapon.ChemicalEnergy);
            var pen = WeaponDef.Interp(weapon.Pen, range);
            if (pen > armor)
            {
                t.Hp -= weapon.Damage;
                Suppress(w, r, target, weapon.Suppression + r.CasualtySuppression);
                w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Hit, A = w.Units[shooter].Id, B = t.Id, Value = (face * 10000) + Math.Min(9999, pen - armor), C = mount });
                var critChance = r.CritBasePct + ((pen - armor) * r.CritPer100MmPct / 100);
                if ((int)(BattleWorld.NextRandom(w) % 100UL) < critChance)
                {
                    Critical(w, r, target, RollCrit(w, r));
                }
            }
            else
            {
                Suppress(w, r, target, r.NonpenSuppression);
                w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.NonPen, A = w.Units[shooter].Id, B = t.Id, Value = (face * 10000) + Math.Min(9999, armor - pen), C = mount });
                if ((int)(BattleWorld.NextRandom(w) % 100UL) < r.NonpenMinorCritPct)
                {
                    Critical(w, r, target, (BattleWorld.NextRandom(w) & 1) == 0 ? 0 : 1);
                }
            }

            KillIfDead(w, r, shooter, target);
        }

        if (weapon.BlastDamage > 0 && weapon.BlastRadiusM > 0)
        {
            var x = (int)(w.Units[target].X.Raw >> Fp.FractionalBits);
            var y = (int)(w.Units[target].Y.Raw >> Fp.FractionalBits);
            Blast(w, r, shooter, x, y, weapon.BlastRadiusM, weapon.BlastDamage, weapon.Suppression, target);
            if (weapon.Type == "incendiary")
            {
                Ignite(w, x, y, weapon.BlastRadiusM);
            }
        }
    }

    /// <summary>FLY-06: incendiaries set every ground vehicle in the burst on fire (the SIM-07 fire critical).</summary>
    public static void Ignite(WorldState w, int x, int y, int radiusM)
    {
        for (var i = 0; i < w.Units.Length; i++)
        {
            ref var u = ref w.Units[i];
            var d = w.Defs[u.Def];
            if (u.Alive == 0 || d.IsInfantry || d.IsAir)
            {
                continue;
            }

            long dx = (u.X.Raw >> Fp.FractionalBits) - x;
            long dy = (u.Y.Raw >> Fp.FractionalBits) - y;
            if ((dx * dx) + (dy * dy) < (long)radiusM * radiusM)
            {
                u.Flags |= FlagFire;
            }
        }
    }

    /// <summary>True for a unit carrying a radar-directed weapon (type aaa: ZSU-23-4 Gun Dish, M163 ranging radar).</summary>
    static bool EmitsRadar(WorldState w, in UnitState u)
    {
        foreach (var m in w.Defs[u.Def].Weapons)
        {
            if (w.Weapons[m.Weapon].Type == "aaa")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>HE effect: units within radius take falloff damage (armor halves it per 10 mm top/side, infantry full), structures take it too.</summary>
    public static void Blast(WorldState w, BattleRules r, int shooter, int x, int y, int radiusM, int damage, int suppression, int skip)
    {
        // SUP-03: ammunition dumps in the blast take its damage; at 0 hp the stock is lost.
        for (var k = 0; k < w.Dumps.Length; k++)
        {
            ref var d = ref w.Dumps[k];
            long ddx = d.X - x;
            long ddy = d.Y - y;
            var dd = Sight.IntSqrt((ddx * ddx) + (ddy * ddy));
            if (d.Hp <= 0 || dd >= radiusM)
            {
                continue;
            }

            d.Hp -= (int)((long)damage * (radiusM - dd) / radiusM);
            if (d.Hp <= 0)
            {
                d.Hp = 0;
                d.SupplyLeft = 0;
                w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.DumpDestroyed, A = k, B = d.Team });
            }
        }

        for (var u = 0; u < w.Units.Length; u++)
        {
            if (u == skip || w.Units[u].Alive == 0)
            {
                continue;
            }

            long dx = (w.Units[u].X.Raw >> Fp.FractionalBits) - x;
            long dy = (w.Units[u].Y.Raw >> Fp.FractionalBits) - y;
            var d = Sight.IntSqrt((dx * dx) + (dy * dy));
            if (d >= radiusM)
            {
                continue;
            }

            var dealt = (int)((long)damage * (radiusM - d) / radiusM);
            var tdef = w.Defs[w.Units[u].Def];
            Suppress(w, r, u, suppression * (radiusM - d) / radiusM);
            if (tdef.IsInfantry)
            {
                HitInfantry(w, r, shooter, u, dealt);
            }
            else
            {
                // Blast and fragments against armor: what exceeds the thinnest (top/side) plate gets through.
                var armor = Math.Min(tdef.ArmorTop, tdef.ArmorSideKe);
                if (dealt > armor)
                {
                    w.Units[u].Hp -= dealt - armor;
                    KillIfDead(w, r, shooter, u);
                }
            }
        }

        Structures.Blast(w, r, x, y, 15, radiusM, damage);
    }

    /// <summary>Infantry squads lose whole members: damage / member hp, remainder rolled.</summary>
    static void HitInfantry(WorldState w, BattleRules r, int shooter, int target, int damage)
    {
        ref var t = ref w.Units[target];
        var tdef = w.Defs[t.Def];
        var memberHp = Math.Max(1, tdef.Hp / Math.Max(1, tdef.Members));
        var losses = damage / memberHp;
        if ((int)(BattleWorld.NextRandom(w) % (ulong)memberHp) < damage % memberHp)
        {
            losses++;
        }

        if (losses <= 0)
        {
            Suppress(w, r, target, r.NonpenSuppression);
            return;
        }

        losses = Math.Min(losses, t.Members);
        t.Members -= losses;
        t.Hp = t.Members * memberHp;
        Suppress(w, r, target, r.CasualtySuppression * losses);
        w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Casualty, A = shooter >= 0 ? w.Units[shooter].Id : -1, B = t.Id, Value = losses });
        KillIfDead(w, r, shooter, target);
    }

    /// <summary>SUP-01: a top-attack strike from a drone or loitering munition: if `pen` beats the top armour the target
    /// takes `damage` and may suffer a critical; the burst suppresses either way.</summary>
    public static void TopStrike(WorldState w, BattleRules r, int target, int pen, int damage, int suppression)
    {
        ref var t = ref w.Units[target];
        var d = w.Defs[t.Def];
        Suppress(w, r, target, suppression);
        w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Impact, A = (int)(t.X.Raw >> Fp.FractionalBits), B = (int)(t.Y.Raw >> Fp.FractionalBits), C = -1, Value = Fires.KindHe });
        if (pen <= d.ArmorTop)
        {
            return;
        }

        t.Hp -= damage;
        w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Hit, A = -1, B = t.Id, Value = (3 * 10000) + Math.Min(9999, pen - d.ArmorTop), C = -1 });
        if ((int)(BattleWorld.NextRandom(w) % 100UL) < r.CritBasePct)
        {
            Critical(w, r, target, RollCrit(w, r));
        }

        KillIfDead(w, r, -1, target);
    }

    /// <summary>SUP-03: a mine strike on an infantry unit (no shooter).</summary>
    public static void MineCasualties(WorldState w, BattleRules r, int target, int damage) => HitInfantry(w, r, -1, target, damage);

    public static void KillIfDead(WorldState w, BattleRules r, int shooter, int target)
    {
        ref var t = ref w.Units[target];
        if (t.Alive == 0 || (t.Hp > 0 && (t.Flags & FlagBail) == 0))
        {
            return;
        }

        if (shooter < 0 && t.LastAttacker >= 0 && t.LastAttacker < w.Units.Length)
        {
            shooter = t.LastAttacker;
        }

        t.Alive = 0;
        if (shooter >= 0)
        {
            ref var k = ref w.Units[shooter];
            k.Kills++;
            if (k.Veterancy < 3 && k.Veterancy + 1 < r.VetKills.Length && k.Kills >= r.VetKills[k.Veterancy + 1])
            {
                k.Veterancy++;
                w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Promoted, A = k.Id, Value = k.Veterancy });
            }
        }

        w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Killed, A = shooter >= 0 ? w.Units[shooter].Id : -1, B = t.Id });
        Economy.OnKill(w, r, shooter, target);
    }

    static int RollCrit(WorldState w, BattleRules r)
    {
        var total = 0;
        foreach (var weight in r.CritWeights)
        {
            total += weight;
        }

        var roll = (int)(BattleWorld.NextRandom(w) % (ulong)Math.Max(1, total));
        for (var i = 0; i < r.CritWeights.Length; i++)
        {
            roll -= r.CritWeights[i];
            if (roll < 0)
            {
                return i;
            }
        }

        return 0;
    }

    static void Critical(WorldState w, BattleRules r, int target, int crit)
    {
        ref var t = ref w.Units[target];
        t.Flags |= CritFlags[crit];
        w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Critical, A = t.Id, Value = CritFlags[crit] });
        if (CritFlags[crit] == FlagBail)
        {
            KillIfDead(w, r, -1, target);
        }
    }

    static void Burn(WorldState w, BattleRules r)
    {
        for (var i = 0; i < w.Units.Length; i++)
        {
            if (w.Units[i].Alive != 0 && (w.Units[i].Flags & FlagFire) != 0)
            {
                w.Units[i].Hp -= r.FireHpPerTick;
                KillIfDead(w, r, -1, i);
            }
        }
    }

    public static void Suppress(WorldState w, BattleRules r, int target, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        ref var t = ref w.Units[target];
        t.LastHitByTick = w.Tick;
        t.Suppression = Math.Min(r.SuppressionMax, t.Suppression + amount);
    }

    public static bool IsAirborne(BattleRules r, in UnitState u) => u.Z.Raw > Fp.FromInt(r.AirAltitudeM).Raw;

    /// <summary>Which face of the target a shot from (fromX, fromY) strikes; `top` when the target or the shooter is airborne.</summary>
    public static int Facing(BattleRules r, in UnitState target, Fp fromX, Fp fromY, bool top)
    {
        if (top)
        {
            return FaceTop;
        }

        var toShooter = Movement.HeadingOf(fromX - target.X, fromY - target.Y);
        var off = AngleDiff(toShooter, target.Heading);
        var arc = r.ArcFrontHalfDeg * 65536 / 360;
        return off <= arc ? FaceFront : off >= 32768 - arc ? FaceRear : FaceSide;
    }

    public static int Armor(BattleUnitDef d, int face, bool chemical) => face switch
    {
        FaceFront => chemical ? d.ArmorFrontCe : d.ArmorFrontKe,
        FaceSide => chemical ? d.ArmorSideCe : d.ArmorSideKe,
        FaceRear => d.ArmorRear,
        _ => d.ArmorTop,
    };

    static bool InArc(int heading, in UnitState from, in UnitState to, int halfDeg)
    {
        var bearing = Movement.HeadingOf(to.X - from.X, to.Y - from.Y);
        return AngleDiff(bearing, heading) <= halfDeg * 65536 / 360;
    }

    /// <summary>Absolute difference of two binary angles, 0..32768.</summary>
    public static int AngleDiff(int a, int b)
    {
        var d = (a - b) & 0xFFFF;
        return d > 32768 ? 65536 - d : d;
    }
}
