using SixActual.Sim.State.Battle;

namespace SixActual.Sim.Battle;

/// <summary>
/// SUP-01 indirect fire. A fire mission names a battery, a target and a round count. The battery lays its
/// guns (setup_ticks), then fires one round per reload; each round lands after range / tof_m_s seconds at
/// a dispersion whose median miss equals the weapon's CEP at that range. HE rounds blast units and
/// structures; smoke rounds raise a screen.
/// </summary>
public static class Fires
{
    public const int KindHe = 0;

    public const int KindSmoke = 1;

    public const int KindIllum = 2;

    /// <summary>Starts a mission; returns false (and logs MissionRefused) if the battery cannot fire on the target.</summary>
    public static bool Order(WorldState w, BattleRules r, int battery, int x, int y, int rounds, int kind)
    {
        if (w.Missions.Length != w.Units.Length)
        {
            Array.Resize(ref w.Missions, w.Units.Length);
        }

        ref readonly var u = ref w.Units[battery];
        var mount = IndirectMount(w, battery);
        if (u.Alive == 0 || mount < 0 || u.MoraleState >= Morale.Routed)
        {
            Refuse(w, battery);
            return false;
        }

        var weapon = w.Weapons[w.Defs[u.Def].Weapons[mount].Weapon];
        long dx = (u.X.Raw >> Fp.FractionalBits) - x;
        long dy = (u.Y.Raw >> Fp.FractionalBits) - y;
        var range = Sight.IntSqrt((dx * dx) + (dy * dy));
        if (range < weapon.RangeMinM || range > weapon.RangeMaxM)
        {
            Refuse(w, battery);
            return false;
        }

        // A battery halts to fire (it cannot fire on the move): a mission cancels any relocation in progress.
        w.Units[battery].HasGoal = 0;
        w.Missions[battery] = new FireMission
        {
            Active = 1,
            Battery = battery,
            TargetX = x,
            TargetY = y,
            RoundsLeft = rounds,
            Kind = kind,
            NextShotTick = w.Tick + r.FireSetupTicks
                + (ElectronicWarfare.InZone(w, ElectronicWarfare.CommsJam, u.X, u.Y, Frontline.TeamOf(w, u.Player), hostile: true) ? r.JamCommsDelayTicks : 0),
            Mount = mount,
        };
        w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.MissionStarted, A = u.Id, B = x, C = y, Value = rounds });
        return true;
    }

    static void Refuse(WorldState w, int battery) =>
        w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.MissionRefused, A = w.Units[battery].Id });

    public static int IndirectMount(WorldState w, int unit)
    {
        var d = w.Defs[w.Units[unit].Def];
        for (var m = 0; m < d.Weapons.Length; m++)
        {
            if (w.Weapons[d.Weapons[m].Weapon].Indirect)
            {
                return m;
            }
        }

        return -1;
    }

    public static void Step(WorldState w, BattleRules r)
    {
        for (var b = 0; b < w.Missions.Length; b++)
        {
            ref var m = ref w.Missions[b];
            if (m.Active == 0)
            {
                continue;
            }

            ref var u = ref w.Units[b];
            var slot = u.MountStart + m.Mount;
            if (u.Alive == 0 || m.RoundsLeft <= 0 || w.MountAmmo[slot] <= 0 || u.MovedThisTick != 0)
            {
                // A battery that moves breaks off the mission.
                m.Active = 0;
                continue;
            }

            if (w.Tick < m.NextShotTick)
            {
                continue;
            }

            var wi = w.Defs[u.Def].Weapons[m.Mount].Weapon;
            var weapon = w.Weapons[wi];
            var salvo = Math.Min(Math.Min(weapon.Salvo, m.RoundsLeft), w.MountAmmo[slot]);
            long dx = (u.X.Raw >> Fp.FractionalBits) - m.TargetX;
            long dy = (u.Y.Raw >> Fp.FractionalBits) - m.TargetY;
            var range = Sight.IntSqrt((dx * dx) + (dy * dy));
            var flightTicks = (int)((long)range * r.TickHz / Math.Max(1, weapon.TofMS));
            var cep = range * weapon.CepPermille / 1000;
            for (var k = 0; k < salvo; k++)
            {
                Disperse(w, cep, out var ox, out var oy);
                AddShell(w, r, new Shell { Active = 1, LandTick = w.Tick + flightTicks, X = m.TargetX + ox, Y = m.TargetY + oy, Weapon = wi, Kind = m.Kind, Shooter = b });
            }

            w.MountAmmo[slot] -= salvo;
            m.RoundsLeft -= salvo;
            w.CbPending.Add((w.Tick + r.CbLocateTicks, b, (int)(u.X.Raw >> Fp.FractionalBits), (int)(u.Y.Raw >> Fp.FractionalBits)));
            m.NextShotTick = w.Tick + weapon.ReloadTicks;
            u.RevealUntilTick = w.Tick + r.RevealOnFireTicks;
            if (m.RoundsLeft <= 0)
            {
                m.Active = 0;
            }
        }

        Locate(w, r);
        for (var i = 0; i < w.Shells.Length; i++)
        {
            ref var s = ref w.Shells[i];
            if (s.Active == 0 || s.LandTick > w.Tick)
            {
                continue;
            }

            s.Active = 0;
            Land(w, r, s);
        }
    }

    static void Place(SmokeCloud[] slots, in SmokeCloud c, int tick)
    {
        for (var i = 0; i < slots.Length; i++)
        {
            if (slots[i].Active == 0 || slots[i].ExpiresTick <= tick)
            {
                slots[i] = c;
                return;
            }
        }
    }

    /// <summary>
    /// Counter-battery: cb_locate_ticks after a battery fires, the opposing team gets a fix on the firing
    /// position (with cb_error_m of error) that its artillery can engage.
    /// </summary>
    static void Locate(WorldState w, BattleRules r)
    {
        for (var i = w.CbPending.Count - 1; i >= 0; i--)
        {
            var (due, battery, x, y) = w.CbPending[i];
            if (due > w.Tick)
            {
                continue;
            }

            w.CbPending.RemoveAt(i);
            var team = 1 - Frontline.TeamOf(w, w.Units[battery].Player);
            Disperse(w, r.CbErrorM, out var ex, out var ey);
            var fix = new BatteryFix { Active = 1, Team = team, Battery = battery, X = x + ex, Y = y + ey, Tick = w.Tick };
            w.Fixes.Add(fix);
            w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.BatteryLocated, A = w.Units[battery].Id, B = team, C = fix.X, Value = fix.Y });
        }
    }

    /// <summary>Queues an off-map round (MEC-03 support barrages).</summary>
    public static void QueueShell(WorldState w, BattleRules r, in Shell shell) => AddShell(w, r, shell);

    static void AddShell(WorldState w, BattleRules r, in Shell shell)
    {
        for (var i = 0; i < w.Shells.Length; i++)
        {
            if (w.Shells[i].Active == 0)
            {
                w.Shells[i] = shell;
                return;
            }
        }

        if (w.Shells.Length < r.MaxPendingRounds)
        {
            Array.Resize(ref w.Shells, Math.Min(r.MaxPendingRounds, Math.Max(16, w.Shells.Length * 2)));
            AddShell(w, r, shell);
        }
    }

    static void Land(WorldState w, BattleRules r, in Shell s)
    {
        var weapon = w.Weapons[s.Weapon];
        w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Impact, A = s.X, B = s.Y, C = s.Weapon, Value = s.Kind });
        if (s.Kind == KindHe && weapon.BlastRadiusM >= r.CraterMinBlastM && r.CraterMax > 0)
        {
            // MEC-04: heavy HE leaves a crater that infantry can use as cover.
            TerrainBuilder.AddCrater(w, r, s.X, s.Y);
        }

        if (s.Kind == KindIllum)
        {
            Place(w.Flares, new SmokeCloud { Active = 1, X = Fp.FromInt(s.X), Y = Fp.FromInt(s.Y), RadiusM = r.IllumRadiusM, StartTick = w.Tick, ExpiresTick = w.Tick + r.IllumTicks }, w.Tick);
            return;
        }

        if (s.Kind == KindSmoke)
        {
            for (var i = 0; i < w.Smoke.Length; i++)
            {
                if (w.Smoke[i].Active == 0 || w.Smoke[i].ExpiresTick <= w.Tick)
                {
                    w.Smoke[i] = new SmokeCloud { Active = 1, X = Fp.FromInt(s.X), Y = Fp.FromInt(s.Y), RadiusM = r.SmokeRadiusM, StartTick = w.Tick, ExpiresTick = w.Tick + r.SmokeTicks };
                    return;
                }
            }

            return;
        }

        Combat.Blast(w, r, s.Shooter, s.X, s.Y, weapon.BlastRadiusM, weapon.BlastDamage, weapon.Suppression, -1);
    }

    /// <summary>
    /// Circular normal miss with median radius exactly `cep`: r = cep * sqrt(log2(1/U)), angle uniform.
    /// Integer-only: log2 in Q16 fixed point, sqrt by Sight.IntSqrt.
    /// </summary>
    public static void Disperse(WorldState w, int cep, out int ox, out int oy)
    {
        var u = (uint)(BattleWorld.NextRandom(w) >> 32);
        if (u == 0)
        {
            u = 1;
        }

        var log2Q16 = Log2InvQ16(u);
        var radius = (int)((long)cep * Sight.IntSqrt(log2Q16) / 256);
        var angle = (int)(BattleWorld.NextRandom(w) & 0xFFFF);
        Trig.SinCos(Fp.FromRaw(Trig.TwoPi.Raw * angle / 65536), out var sin, out var cos);
        ox = (int)(((long)radius * cos.Raw) >> Fp.FractionalBits);
        oy = (int)(((long)radius * sin.Raw) >> Fp.FractionalBits);
    }

    /// <summary>log2(2^32 / u) in Q16 fixed point, for u in [1, 2^32).</summary>
    public static long Log2InvQ16(uint u)
    {
        // log2(2^32/u) = 32 - log2(u). Integer part from the bit length; fraction by repeated squaring.
        var lz = System.Numerics.BitOperations.LeadingZeroCount(u);
        var intPart = 31 - lz;
        ulong x = (ulong)u << lz; // normalise to [2^31, 2^32)
        long frac = 0;
        for (var bit = 15; bit >= 0; bit--)
        {
            x = (x * x) >> 31;
            if (x >= (1UL << 32))
            {
                x >>= 1;
                frac |= 1L << bit;
            }
        }

        var log2uQ16 = ((long)intPart << 16) | frac;
        return (32L << 16) - log2uQ16;
    }
}
