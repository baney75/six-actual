using SixActual.Sim.State.Battle;

namespace SixActual.Sim.Battle;

/// <summary>
/// FLY-01..03: Take Control of an aircraft, automatic and manual return of control, and the arcade-sim flight
/// model for player-flown aircraft. Integer arithmetic only (mm, mm/s, permille, binary-angle headings).
///
/// Flight model per 20 Hz tick, from each aircraft's sourced envelope (vmax at low level, stall speed, climb
/// rate, G limit):
///   thrust  a_max * throttle%, with a_max chosen so that at the best-climb speed (55% of vmax) the specific excess
///           power equals g * climb rate, and drag a_max * (v / vmax)^2 balances full throttle at vmax;
///   energy  dv = thrust - drag - g sin(gamma) - turn bleed: climbing trades speed, diving gains it;
///   turn    rate capped by the G limit, omega_max = g sqrt(G^2 - 1) / v, scaled by the turn stick; hard turns bleed
///           speed (up to turn_bleed_pct of a_max at full stick);
///   stall   below the stall speed the nose drops (flight path to stall_sink_pm) whatever the stick says, until the
///           aircraft is 10% above stall speed again; StallWarning on entry;
///   ground  reaching zero altitude destroys the aircraft (the pilot ejects; control returns).
/// </summary>
public static class Flight
{
    public const int ReasonBingo = 1;

    public const int ReasonWinchester = 2;

    public const int ReasonEjected = 3;

    public const int ReasonLanded = 4;

    public const int ReasonManual = 5;

    const int GravityMmS2 = 9807;

    const int MaxClimbSinPm = 500;      // stick full back: 30 deg flight path

    const int ClimbSlewPmPerS = 300;    // flight path change rate, permille of sine per second

    const int StallSinkPm = -250;       // nose drops to ~ -15 deg in a stall

    const int TurnBleedPct = 40;        // full turn stick bleeds 40% of max thrust acceleration

    const int LandedAltitudeM = 50;     // below this over its home strip, a flown aircraft counts as landed

    const int LandingRadiusM = 600;     // ... within this distance of its home point

    /// <summary>The player takes control of one of their on-map aircraft; their XO takes command of the commanders
    /// the same tick and announces it (FLY-01).</summary>
    public static void TakeControl(WorldState w, int unit, int player)
    {
        ref var u = ref w.Units[unit];
        var def = w.Defs[u.Def];
        if (u.Alive == 0 || !def.IsAir || u.OffMapUntil > 0 || u.Player != player || u.Flown != 0)
        {
            return;
        }

        // One aircraft at a time: taking another hands the current one back first.
        for (var k = 0; k < w.Units.Length; k++)
        {
            if (k != unit && w.Units[k].Flown == player + 1)
            {
                ReturnControl(w, k, ReasonManual);
            }
        }

        EnsurePlayers(w, player + 1);
        w.XoRestore[player] = w.XoActive[player];
        w.XoActive[player] = 1;
        u.Flown = player + 1;
        u.HasGoal = 0;
        u.Throttle = 80;
        u.StickTurn = 0;
        u.StickClimb = 0;
        u.AirspeedMmS = Math.Max(def.SpeedMmS, Vstall(def) * 12 / 10);
        u.SinGammaPm = 0;
        u.Stalled = 0;
        w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.XoTookCommand, A = player, B = u.Id });
    }

    /// <summary>Hands a flown aircraft back to its AI pilot and the commanders back to the player (FLY-02).</summary>
    public static void ReturnControl(WorldState w, int unit, int reason)
    {
        ref var u = ref w.Units[unit];
        if (u.Flown == 0)
        {
            return;
        }

        var player = u.Flown - 1;
        u.Flown = 0;
        u.Throttle = 0;
        u.StickTurn = 0;
        u.StickClimb = 0;
        EnsurePlayers(w, player + 1);
        w.XoActive[player] = w.XoRestore[player];
        w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.ControlReturned, A = player, B = u.Id, Value = reason });
    }

    public static void Step(WorldState w, BattleRules r)
    {
        for (var i = 0; i < w.Units.Length; i++)
        {
            ref var u = ref w.Units[i];
            if (u.Flown == 0)
            {
                continue;
            }

            var def = w.Defs[u.Def];
            if (u.Alive == 0)
            {
                ReturnControl(w, i, ReasonEjected);
                continue;
            }

            if (u.OffMapUntil > 0)
            {
                ReturnControl(w, i, ReasonLanded);
                continue;
            }

            var bingo = def.Fuel * r.BingoFuelPct / 100;
            if (def.Fuel > 0 && u.Fuel <= bingo)
            {
                ReturnControl(w, i, ReasonBingo);
                continue;
            }

            if (Winchester(w, i))
            {
                ReturnControl(w, i, ReasonWinchester);
                continue;
            }

            Fly(w, r, i);
            if (u.Alive == 0)
            {
                ReturnControl(w, i, ReasonEjected);
                continue;
            }

            long hx = (u.X.Raw >> Fp.FractionalBits) - u.HomeX;
            long hy = (u.Y.Raw >> Fp.FractionalBits) - u.HomeY;
            if ((hx * hx) + (hy * hy) <= (long)LandingRadiusM * LandingRadiusM
                && u.Z.Raw < Fp.FromInt(LandedAltitudeM).Raw && u.AirspeedMmS < Vstall(def) * 15 / 10)
            {
                // Down on its home strip at landing speed: it taxis off to rearm and refuel.
                u.X = Fp.FromInt(u.HomeX);
                u.Y = Fp.FromInt(u.HomeY);
                u.Z = Fp.Zero;
                u.OffMapUntil = w.Tick + r.AircraftTurnaroundTicks;
                ReturnControl(w, i, ReasonLanded);
            }
        }
    }

    static bool Winchester(WorldState w, int i)
    {
        var def = w.Defs[w.Units[i].Def];
        if (def.Weapons.Length == 0)
        {
            return false;
        }

        for (var m = 0; m < def.Weapons.Length; m++)
        {
            if (w.MountAmmo[w.Units[i].MountStart + m] > 0)
            {
                return false;
            }
        }

        return true;
    }

    public static int Vmax(BattleUnitDef d) => d.VmaxMmS > 0 ? d.VmaxMmS : d.SpeedMmS;

    public static int Vstall(BattleUnitDef d) => d.VstallMmS > 0 ? d.VstallMmS : Vmax(d) * 30 / 100;

    public static int Climb(BattleUnitDef d) => d.ClimbMmS > 0 ? d.ClimbMmS : Vmax(d) / 10;

    public static int GLimitX10(BattleUnitDef d) => d.GLimitX10 > 0 ? d.GLimitX10 : 60;

    /// <summary>Maximum thrust acceleration (mm/s^2): g * climb / (vc * (1 - (vc/vmax)^2)) with vc = 0.55 vmax.</summary>
    public static long AMax(BattleUnitDef d)
    {
        long vc = Vmax(d) * 55L / 100;
        return GravityMmS2 * (long)Climb(d) * 10_000 / Math.Max(1, vc * 6975);
    }

    /// <summary>Sustained turn-rate ceiling at speed v, in binary-angle units (65536 = 360 deg) per second.</summary>
    public static long MaxTurnPerS(BattleUnitDef d, long v)
    {
        var g10 = (long)GLimitX10(d);
        var root10 = Sight.IntSqrt((g10 * g10) - 100); // 10 * sqrt(G^2 - 1)
        // omega [rad/s] = g * sqrt(G^2-1) / v ; binary angle = omega * 65536 / (2 pi)
        return (long)GravityMmS2 * root10 * 65536 * 1000 / Math.Max(1L, 10 * v * 6283);
    }

    static void Fly(WorldState w, BattleRules r, int i)
    {
        ref var u = ref w.Units[i];
        var def = w.Defs[u.Def];
        var hz = r.TickHz;
        long v = u.AirspeedMmS;
        long vmax = Vmax(def);
        var vstall = Vstall(def);
        var amax = AMax(def);

        // Stall state and flight path.
        if (v < vstall)
        {
            if (u.Stalled == 0)
            {
                u.Stalled = 1;
                w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.StallWarning, A = u.Id });
            }
        }
        else if (v > vstall * 11L / 10)
        {
            u.Stalled = 0;
        }

        var targetSin = u.Stalled != 0 ? StallSinkPm : u.StickClimb * MaxClimbSinPm / 100;
        var slew = ClimbSlewPmPerS / hz;
        u.SinGammaPm += Math.Clamp(targetSin - u.SinGammaPm, -Math.Max(1, slew), Math.Max(1, slew));

        // Energy: thrust - drag - gravity along the path - turn bleed.
        long thrust = amax * u.Throttle / 100;
        long drag = amax * v / vmax * v / vmax;
        long grav = (long)GravityMmS2 * u.SinGammaPm / 1000;
        long bleed = amax * TurnBleedPct / 100 * Math.Abs(u.StickTurn) / 100;
        v += (thrust - drag - grav - bleed) / hz;
        v = Math.Max(0, v);
        u.AirspeedMmS = (int)v;

        // Turn: rate limited by the G limit at this speed.
        var turn = MaxTurnPerS(def, Math.Max(v, vstall)) * u.StickTurn / 100 / hz;
        u.Heading = (int)((u.Heading + turn) & 0xFFFF);

        // Move: horizontal component along the heading, vertical along the flight path.
        long cosG = Sight.IntSqrt(1_000_000L - ((long)u.SinGammaPm * u.SinGammaPm)); // permille
        long horiz = v * cosG / 1000;                    // mm/s
        var rad = Fp.FromRaw((long)u.Heading * Trig.TwoPi.Raw / 65536);
        Trig.SinCos(rad, out var sin, out var cos);
        var stepRaw = horiz * Fp.OneRaw / 1000 / hz;     // metres this tick, Q48.16
        u.X = Fp.FromRaw(Math.Clamp(u.X.Raw + (stepRaw * cos.Raw >> Fp.FractionalBits), Fp.FromInt(1).Raw, Fp.FromInt(w.Terrain.SizeM - 2).Raw));
        u.Y = Fp.FromRaw(Math.Clamp(u.Y.Raw + (stepRaw * sin.Raw >> Fp.FractionalBits), Fp.FromInt(1).Raw, Fp.FromInt(w.Terrain.SizeM - 2).Raw));
        u.Z = Fp.FromRaw(u.Z.Raw + (v * u.SinGammaPm / 1000 * Fp.OneRaw / 1000 / hz));
        u.MovedThisTick = 1;
        Supply.Burn(w, ref u, def, Math.Abs(stepRaw));

        if (u.Z.Raw <= 0)
        {
            // Flown into the ground: the aircraft is lost, the pilot ejects.
            u.Z = Fp.Zero;
            u.Hp = 0;
            u.Alive = 0;
            w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Killed, A = -1, B = u.Id });
        }
    }

    static void EnsurePlayers(WorldState w, int n)
    {
        if (w.XoActive.Length < n)
        {
            Array.Resize(ref w.XoActive, n);
        }

        if (w.XoRestore.Length < n)
        {
            Array.Resize(ref w.XoRestore, n);
        }
    }
}
