using System.Text.Json;
using SixActual.Sim.State.Battle;

namespace SixActual.Sim.Battle;

/// <summary>One off-map support option from data/support.json (SUP-02, SUP-05, MEC-03).</summary>
public sealed class SupportDef
{
    public const int Barrage = 1;

    public const int Smoke = 2;

    public const int Illum = 3;

    public const int StrikeRun = 4;

    public const int Sead = 5;

    public const int Reveal = 6;

    /// <summary>SUP-03: scatterable minefield of `density` within `radius_m`.</summary>
    public const int Minefield = 7;

    /// <summary>SUP-03 engineer support on a strip `length_m` x `width_m` along `heading_deg` (Engineering.cs).</summary>
    public const int Bridge = 8;

    public const int Fortify = 9;

    public const int ObstacleBelt = 10;

    public const int Breach = 11;

    /// <summary>SUP-04 electronic warfare zones (ElectronicWarfare.cs): `radius_m` for `duration_ticks`.</summary>
    public const int JamRadar = 12;

    public const int JamComms = 13;

    public const int Decoys = 14;

    /// <summary>GS-04: tactical nuclear strike (a blast of `blast_radius_m` / `blast_damage` at the aim point), refused
    /// unless the grand-strategy escalation has released tactical weapons (reason 7).</summary>
    public const int TacticalNuke = 15;

    /// <summary>SUP-01: laser-guided rounds within `cep_m`, refused without a designator in `designator_m` (reason 8).</summary>
    public const int Precision = 16;

    /// <summary>SUP-02: `count` units of roster `unit` land around the aim point within `scatter_m` (paradrop, air assault).</summary>
    public const int AirLanding = 17;

    /// <summary>SUP-02: a gunship orbits for `duration_ticks`, every `interval_ticks` hitting the nearest visible enemy within
    /// `radius_m` with a `blast_radius_m` / `blast_damage` burst.</summary>
    public const int Gunship = 18;

    /// <summary>SUP-02: a cruise missile: one precise blast at the aim point, no designator needed.</summary>
    public const int Cruise = 19;

    /// <summary>SUP-02: fighter escort zone (ElectronicWarfare.Escort) of `radius_m` for `duration_ticks`.</summary>
    public const int EscortZone = 20;

    /// <summary>SUP-01 modern: a loitering munition: `strikes` attacks, one every `interval_ticks`, each on the most
    /// valuable visible enemy vehicle within `radius_m`, top attack with `pen` / `blast_damage`.</summary>
    public const int Loitering = 21;

    /// <summary>SUP-01 modern: FPV drone strikes, like loitering munitions but each succeeds with fpv_success_pct
    /// (fpv_jammed_pct when the target is inside an enemy comms-jamming zone).</summary>
    public const int Fpv = 22;

    /// <summary>SUP-04 modern: GPS / GLONASS jamming zone of `radius_m` for `duration_ticks`.</summary>
    public const int JamGps = 23;

    public string Id { get; init; } = "";

    public string Side { get; init; } = "";

    public string Era { get; init; } = "";

    public int Type { get; init; }

    public int SpCost { get; init; }

    public int Charges { get; init; }

    public int RechargeTicks { get; init; }

    public int DelayTicks { get; init; }

    public int Weapon { get; init; } = -1;

    public int Rounds { get; init; }

    public int RangeM { get; init; }

    public int Impacts { get; init; }

    public int BlastRadiusM { get; init; }

    public int BlastDamage { get; init; }

    public int Suppression { get; init; }

    public int LengthM { get; init; }

    public int HeadingDeg { get; init; }

    public int IntervalTicks { get; init; } = 1;

    public int RadiusM { get; init; }

    public int DurationTicks { get; init; }

    public bool AirOnly { get; init; }

    public int Density { get; init; }

    public int WidthM { get; init; }

    public int CepM { get; init; }

    /// <summary>Roster unit index for air landings (-1 none), how many, and their scatter.</summary>
    public int LandUnit { get; init; } = -1;

    public int Count { get; init; }

    public int ScatterM { get; init; }

    /// <summary>Strike runs that set vehicles on fire (napalm).</summary>
    public bool Incendiary { get; init; }

    /// <summary>Satellite-guided precision rounds: no designator needed, but GPS jamming spoils them.</summary>
    public bool Gps { get; init; }

    public int Pen { get; init; }

    public int Strikes { get; init; }

    public int DesignatorM { get; init; }

    public static SupportDef[] Load(string path, WeaponDef[] weapons, BattleUnitDef[]? units = null)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
        var list = new List<SupportDef>();
        string[] types = ["", "barrage", "smoke", "illum", "strike_run", "sead", "reveal", "minefield", "bridge", "fortify", "obstacle", "breach", "jam_radar", "jam_comms", "decoy", "tactical_nuke", "precision", "air_landing", "gunship", "cruise", "escort", "loitering", "fpv", "jam_gps"];
        foreach (var o in doc.RootElement.GetProperty("options").EnumerateArray())
        {
            var p = o.GetProperty("params");
            int I(string n, int f = 0) => p.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : f;
            var wid = p.TryGetProperty("weapon", out var wv) ? wv.GetString()! : "";
            list.Add(new SupportDef
            {
                Id = o.GetProperty("id").GetString()!,
                Side = o.GetProperty("side").GetString()!,
                Era = o.GetProperty("era").GetString()!,
                Type = Array.IndexOf(types, o.GetProperty("type").GetString()!),
                SpCost = o.GetProperty("sp_cost").GetInt32(),
                Charges = o.GetProperty("charges").GetInt32(),
                RechargeTicks = o.GetProperty("recharge_ticks").GetInt32(),
                DelayTicks = o.GetProperty("delay_ticks").GetInt32(),
                Weapon = wid.Length > 0 ? Array.FindIndex(weapons, w => w.Id == wid) : -1,
                Rounds = I("rounds"),
                RangeM = I("range_m"),
                Impacts = I("impacts"),
                BlastRadiusM = I("blast_radius_m"),
                BlastDamage = I("blast_damage"),
                Suppression = I("suppression"),
                LengthM = I("length_m"),
                HeadingDeg = I("heading_deg"),
                IntervalTicks = Math.Max(1, I("interval_ticks", 1)),
                RadiusM = I("radius_m"),
                DurationTicks = I("duration_ticks"),
                AirOnly = p.TryGetProperty("air_only", out var ao) && ao.GetBoolean(),
                Density = I("density"),
                WidthM = I("width_m"),
                CepM = I("cep_m"),
                LandUnit = units != null && p.TryGetProperty("unit", out var lu) ? BattleUnitDef.IndexOf(units, lu.GetString()!) : -1,
                Count = I("count"),
                ScatterM = I("scatter_m"),
                Incendiary = p.TryGetProperty("incendiary", out var inc) && inc.GetBoolean(),
                Gps = p.TryGetProperty("gps", out var gps) && gps.GetBoolean(),
                Pen = I("pen"),
                Strikes = I("strikes"),
                DesignatorM = I("designator_m"),
            });
        }

        return list.ToArray();
    }
}

/// <summary>
/// Off-map support (MEC-03, SUP-02, SUP-05): a player spends Support Points on a catalogue option; each option has
/// charges that recharge, and a response delay before the effect arrives. Effects: artillery barrage, smoke and
/// illumination (off-map shells through Fires), air strike runs (blasts along a line), SEAD (an anti-radiation
/// strike on a radar-directed air-defence unit) and reveal (recon flights, AWACS).
/// </summary>
public static class Support
{
    /// <summary>Calls an option for a player at (x, y) while it has a charge left. Refused (SupportRefused, Value =
    /// reason) if the option is for the other side (1), the player is short of SP (2), every charge is spent (3), aircraft are grounded by the weather (6), tactical nuclear weapons are not released (7), no designator lases a precision target (8), or
    /// the option belongs to another era (5). Each spent charge recharges after recharge_ticks, one at a time.</summary>
    public static bool Order(WorldState w, BattleRules r, int player, int option, int x, int y)
    {
        var defs = w.SupportDefs;
        if (option < 0 || option >= defs.Length || player < 0 || player >= w.Players.Length)
        {
            return false;
        }

        EnsureState(w);
        var d = defs[option];
        var slot = (player * defs.Length) + option;
        var reason = 0;
        var side = PlayerSide(w, player);
        if (!string.Equals(d.Era, w.Era, StringComparison.Ordinal))
        {
            reason = 5;
        }
        else if (side.Length > 0 && !string.Equals(side, d.Side, StringComparison.Ordinal))
        {
            reason = 1;
        }
        else if (w.Players[player].Sp < d.SpCost)
        {
            reason = 2;
        }
        else if (w.AirGrounded != 0 && d.Type is SupportDef.StrikeRun or SupportDef.Sead)
        {
            // SIM-11: aircraft are grounded by the weather.
            reason = 6;
        }
        else if (d.Type == SupportDef.Precision && !d.Gps && !Designated(w, r, player, x, y, d.DesignatorM))
        {
            // SUP-01: no friendly unit is lasing the aim point.
            reason = 8;
        }
        else if (d.Type == SupportDef.TacticalNuke && w.NuclearRelease < 1)
        {
            // GS-04: not released by the escalation.
            reason = 7;
        }
        else if (w.SupportCharges[slot] <= 0)
        {
            // Every charge spent: the next comes back when its recharge timer runs out.
            reason = 3;
        }

        if (reason != 0)
        {
            w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.SupportRefused, A = player, B = option, Value = reason });
            return false;
        }

        w.Players[player].Sp -= d.SpCost;
        w.SupportCharges[slot]--;
        if (w.SupportReady[slot] == 0)
        {
            // The recharge timer runs for the first spent charge; later ones queue behind it (see Step).
            w.SupportReady[slot] = w.Tick + d.RechargeTicks;
        }
        w.PendingSupport.Add(new PendingSupport { Def = option, Player = player, X = x, Y = y, Arrive = w.Tick + d.DelayTicks, Done = 0 });
        w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.SupportCalled, A = player, B = option, C = w.Tick + d.DelayTicks });
        return true;
    }

    public static void Step(WorldState w, BattleRules r)
    {
        for (var k = 0; k < w.PendingSupport.Count; k++)
        {
            var p = w.PendingSupport[k];
            if (p.Done < 0 || w.Tick < p.Arrive)
            {
                continue;
            }

            var d = w.SupportDefs[p.Def];
            if (p.Done == 0)
            {
                w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.SupportArrived, A = p.Player, B = p.Def });
            }

            switch (d.Type)
            {
                case SupportDef.Precision:
                    if (d.Weapon >= 0)
                    {
                        // SUP-04: satellite guidance fails inside the enemy's GPS jamming.
                        var cep = d.Gps && ElectronicWarfare.InZone(w, ElectronicWarfare.GpsJam, Fp.FromInt(p.X), Fp.FromInt(p.Y), Frontline.TeamOf(w, p.Player), hostile: true)
                            ? d.CepM * r.GpsJamCepX
                            : d.CepM;
                        for (var n = 0; n < d.Rounds; n++)
                        {
                            Fires.Disperse(w, cep, out var px, out var py);
                            Fires.QueueShell(w, r, new Shell { Active = 1, LandTick = w.Tick + (n * 2), X = p.X + px, Y = p.Y + py, Weapon = d.Weapon, Kind = Fires.KindHe, Shooter = -1 });
                        }
                    }

                    p.Done = -1;
                    break;
                case SupportDef.Barrage:
                case SupportDef.Smoke:
                case SupportDef.Illum:
                    Shells(w, r, d, p.X, p.Y, d.Type == SupportDef.Smoke ? Fires.KindSmoke : d.Type == SupportDef.Illum ? Fires.KindIllum : Fires.KindHe);
                    p.Done = -1;
                    break;
                case SupportDef.StrikeRun:
                    if ((w.Tick - p.Arrive) % d.IntervalTicks == 0)
                    {
                        // Impact n of the stick, laid along the heading from the aim point.
                        var n = p.Done;
                        var along = d.Impacts > 1 ? (long)d.LengthM * n / (d.Impacts - 1) : 0;
                        var rad = Fp.FromRaw((long)d.HeadingDeg * Trig.TwoPi.Raw / 360);
                        Trig.SinCos(rad, out var sin, out var cos);
                        var ix = p.X + (int)((along * cos.Raw) >> Fp.FractionalBits);
                        var iy = p.Y + (int)((along * sin.Raw) >> Fp.FractionalBits);
                        Combat.Blast(w, r, -1, ix, iy, d.BlastRadiusM, d.BlastDamage, d.Suppression, -1);
                        if (d.Incendiary)
                        {
                            Combat.Ignite(w, ix, iy, d.BlastRadiusM);
                        }

                        w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Impact, A = ix, B = iy, C = -1, Value = Fires.KindHe });
                        p.Done = n + 1 >= d.Impacts ? -1 : n + 1;
                    }

                    break;
                case SupportDef.Sead:
                    SeadStrike(w, r, p.Player, p.X, p.Y, d.RadiusM);
                    p.Done = -1;
                    break;
                case SupportDef.Bridge:
                case SupportDef.Fortify:
                case SupportDef.ObstacleBelt:
                case SupportDef.Breach:
                    Engineering.Apply(w, p.Player, d.Type, p.X, p.Y, d.HeadingDeg, d.LengthM, d.WidthM);
                    p.Done = -1;
                    break;
                case SupportDef.JamRadar:
                case SupportDef.JamComms:
                case SupportDef.Decoys:
                    ElectronicWarfare.Open(w, Frontline.TeamOf(w, p.Player), d.Type - SupportDef.JamRadar + 1, p.X, p.Y, d.RadiusM, d.DurationTicks);
                    p.Done = -1;
                    break;
                case SupportDef.TacticalNuke:
                    Combat.Blast(w, r, -1, p.X, p.Y, d.BlastRadiusM, d.BlastDamage, d.Suppression, -1);
                    TerrainBuilder.AddCrater(w, r, p.X, p.Y);
                    p.Done = -1;
                    break;
                case SupportDef.AirLanding:
                    for (var n = 0; n < d.Count && d.LandUnit >= 0; n++)
                    {
                        Fires.Disperse(w, d.ScatterM, out var lx, out var ly);
                        var u = Economy.Spawn(w, d.LandUnit, p.Player, Math.Clamp(p.X + lx, 10, w.Terrain.SizeM - 10), Math.Clamp(p.Y + ly, 10, w.Terrain.SizeM - 10), 0);
                        w.Units[u].Roe = Combat.RoeFree;
                    }

                    p.Done = -1;
                    break;
                case SupportDef.Gunship:
                    if ((w.Tick - p.Arrive) % d.IntervalTicks == 0)
                    {
                        var target = NearestVisibleEnemy(w, p.Player, p.X, p.Y, d.RadiusM);
                        if (target >= 0)
                        {
                            var tx = (int)(w.Units[target].X.Raw >> Fp.FractionalBits);
                            var ty = (int)(w.Units[target].Y.Raw >> Fp.FractionalBits);
                            Combat.Blast(w, r, -1, tx, ty, d.BlastRadiusM, d.BlastDamage, d.Suppression, -1);
                            w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Impact, A = tx, B = ty, C = -1, Value = Fires.KindHe });
                        }
                    }

                    p.Done = w.Tick - p.Arrive >= d.DurationTicks ? -1 : p.Done + 1;
                    break;
                case SupportDef.Cruise:
                    Combat.Blast(w, r, -1, p.X, p.Y, d.BlastRadiusM, d.BlastDamage, d.Suppression, -1);
                    w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Impact, A = p.X, B = p.Y, C = -1, Value = Fires.KindHe });
                    p.Done = -1;
                    break;
                case SupportDef.EscortZone:
                    ElectronicWarfare.Open(w, Frontline.TeamOf(w, p.Player), ElectronicWarfare.Escort, p.X, p.Y, d.RadiusM, d.DurationTicks);
                    p.Done = -1;
                    break;
                case SupportDef.Loitering:
                case SupportDef.Fpv:
                    if ((w.Tick - p.Arrive) % Math.Max(1, d.IntervalTicks) == 0)
                    {
                        var tgt = MostValuableVisibleVehicle(w, p.Player, p.X, p.Y, d.RadiusM);
                        if (tgt >= 0)
                        {
                            var jammed = ElectronicWarfare.InZone(w, ElectronicWarfare.CommsJam, w.Units[tgt].X, w.Units[tgt].Y, Frontline.TeamOf(w, p.Player), hostile: true);
                            var chance = d.Type == SupportDef.Fpv ? (jammed ? r.FpvJammedPct : r.FpvSuccessPct) : 100;
                            if ((int)(BattleWorld.NextRandom(w) % 100UL) < chance)
                            {
                                Combat.TopStrike(w, r, tgt, d.Pen, d.BlastDamage, d.Suppression);
                            }
                        }

                        p.Done++;
                        if (p.Done >= d.Strikes)
                        {
                            p.Done = -1;
                        }
                    }

                    break;
                case SupportDef.JamGps:
                    ElectronicWarfare.Open(w, Frontline.TeamOf(w, p.Player), ElectronicWarfare.GpsJam, p.X, p.Y, d.RadiusM, d.DurationTicks);
                    p.Done = -1;
                    break;
                case SupportDef.Minefield:
                    Mines.Lay(w, Frontline.TeamOf(w, p.Player), p.X, p.Y, d.RadiusM, d.Density);
                    p.Done = -1;
                    break;
                case SupportDef.Reveal:
                    w.Reveals.Add(new Reveal { Player = p.Player, X = p.X, Y = p.Y, RadiusM = d.RadiusM, Until = w.Tick + d.DurationTicks, AirOnly = d.AirOnly ? 1 : 0 });
                    p.Done = -1;
                    break;
                default:
                    p.Done = -1;
                    break;
            }

            w.PendingSupport[k] = p;
        }

        // Recharge: a spent charge comes back once its timer runs out.
        if (w.SupportCharges.Length > 0)
        {
            for (var pl = 0; pl < w.Players.Length; pl++)
            {
                for (var o = 0; o < w.SupportDefs.Length; o++)
                {
                    var slot = (pl * w.SupportDefs.Length) + o;
                    if (w.SupportCharges[slot] < w.SupportDefs[o].Charges && w.SupportReady[slot] > 0 && w.Tick >= w.SupportReady[slot])
                    {
                        w.SupportCharges[slot]++;
                        w.SupportReady[slot] = w.SupportCharges[slot] < w.SupportDefs[o].Charges ? w.Tick + w.SupportDefs[o].RechargeTicks : 0;
                    }
                }
            }
        }
    }

    /// <summary>True if player sees target j through an active reveal (recon flight, AWACS).</summary>
    public static bool Revealed(WorldState w, BattleRules r, int player, in UnitState t)
    {
        foreach (var v in w.Reveals)
        {
            if (v.Player != player || w.Tick >= v.Until || (v.AirOnly != 0 && !Combat.IsAirborne(r, t)))
            {
                continue;
            }

            long dx = (t.X.Raw >> Fp.FractionalBits) - v.X;
            long dy = (t.Y.Raw >> Fp.FractionalBits) - v.Y;
            if ((dx * dx) + (dy * dy) <= (long)v.RadiusM * v.RadiusM)
            {
                return true;
            }
        }

        return false;
    }

    static void Shells(WorldState w, BattleRules r, SupportDef d, int x, int y, int kind)
    {
        if (d.Weapon < 0)
        {
            return;
        }

        var weapon = w.Weapons[d.Weapon];
        var cep = d.RangeM * weapon.CepPermille / 1000;
        for (var n = 0; n < d.Rounds; n++)
        {
            Fires.Disperse(w, cep, out var ox, out var oy);
            Fires.QueueShell(w, r, new Shell { Active = 1, LandTick = w.Tick + (n % 4), X = x + ox, Y = y + oy, Weapon = d.Weapon, Kind = kind, Shooter = -1 });
        }
    }

    static int MostValuableVisibleVehicle(WorldState w, int player, int x, int y, int radiusM)
    {
        var best = -1;
        var bestValue = -1;
        for (var j = 0; j < w.Units.Length; j++)
        {
            ref readonly var e = ref w.Units[j];
            var d = w.Defs[e.Def];
            if (e.Alive == 0 || Frontline.TeamOf(w, e.Player) == Frontline.TeamOf(w, player) || d.IsInfantry || d.IsAir || !Sight.Visible(w, player, j))
            {
                continue;
            }

            long dx = (e.X.Raw >> Fp.FractionalBits) - x;
            long dy = (e.Y.Raw >> Fp.FractionalBits) - y;
            var value = d.Hp + d.ArmorFrontKe;
            if ((dx * dx) + (dy * dy) <= (long)radiusM * radiusM && value > bestValue)
            {
                best = j;
                bestValue = value;
            }
        }

        return best;
    }

    static int NearestVisibleEnemy(WorldState w, int player, int x, int y, int radiusM)
    {
        var best = -1;
        var bestD = long.MaxValue;
        for (var j = 0; j < w.Units.Length; j++)
        {
            ref readonly var e = ref w.Units[j];
            if (e.Alive == 0 || e.Player == player || Frontline.TeamOf(w, e.Player) == Frontline.TeamOf(w, player) || !Sight.Visible(w, player, j) || w.Defs[e.Def].IsAir)
            {
                continue;
            }

            long dx = (e.X.Raw >> Fp.FractionalBits) - x;
            long dy = (e.Y.Raw >> Fp.FractionalBits) - y;
            var d2 = (dx * dx) + (dy * dy);
            if (d2 <= (long)radiusM * radiusM && d2 < bestD)
            {
                best = j;
                bestD = d2;
            }
        }

        return best;
    }

    /// <summary>SUP-01: true if a living unit of the player within `rangeM` of (x, y) has line of sight to it.</summary>
    static bool Designated(WorldState w, BattleRules r, int player, int x, int y, int rangeM)
    {
        var aim = new UnitState { X = Fp.FromInt(x), Y = Fp.FromInt(y), Def = 0 };
        for (var i = 0; i < w.Units.Length; i++)
        {
            ref readonly var u = ref w.Units[i];
            if (u.Alive == 0 || u.Player != player || w.Defs[u.Def].IsAir)
            {
                continue;
            }

            long dx = (u.X.Raw >> Fp.FractionalBits) - x;
            long dy = (u.Y.Raw >> Fp.FractionalBits) - y;
            if ((dx * dx) + (dy * dy) <= (long)rangeM * rangeM && Sight.HasLine(w, r, u, aim))
            {
                return true;
            }
        }

        return false;
    }

    static void SeadStrike(WorldState w, BattleRules r, int player, int x, int y, int radiusM)
    {
        var best = -1;
        var bestD = long.MaxValue;
        for (var j = 0; j < w.Units.Length; j++)
        {
            ref readonly var e = ref w.Units[j];
            if (e.Alive == 0 || e.Player == player || !RadarAirDefence(w, j))
            {
                continue;
            }

            long dx = (e.X.Raw >> Fp.FractionalBits) - x;
            long dy = (e.Y.Raw >> Fp.FractionalBits) - y;
            var d2 = (dx * dx) + (dy * dy);
            if (d2 <= (long)radiusM * radiusM && d2 < bestD)
            {
                bestD = d2;
                best = j;
            }
        }

        if (best >= 0)
        {
            ref var t = ref w.Units[best];
            t.Hp = 0;
            Combat.Suppress(w, r, best, 100);
            Combat.KillIfDead(w, r, -1, best);
        }
    }

    /// <summary>An air-defence unit with radar-directed guns (anti-air weapon of type "aaa").</summary>
    static bool RadarAirDefence(WorldState w, int j)
    {
        var def = w.Defs[w.Units[j].Def];
        if (def.Kind != UnitDef.KindAa)
        {
            return false;
        }

        foreach (var m in def.Weapons)
        {
            var wd = w.Weapons[m.Weapon];
            if (wd.HitsAir && string.Equals(wd.Type, "aaa", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static string PlayerSide(WorldState w, int player)
    {
        foreach (var u in w.Units)
        {
            if (u.Player == player)
            {
                return w.Defs[u.Def].Side;
            }
        }

        var deck = w.Players[player].Deck;
        if (deck >= 0 && deck < w.Decks.Length && w.Decks[deck].Cards.Length > 0)
        {
            return w.Defs[w.Decks[deck].Cards[0].Unit].Side;
        }

        return "";
    }

    static void EnsureState(WorldState w)
    {
        var n = w.Players.Length * w.SupportDefs.Length;
        if (w.SupportCharges.Length == n)
        {
            return;
        }

        w.SupportCharges = new int[n];
        w.SupportReady = new int[n];
        for (var p = 0; p < w.Players.Length; p++)
        {
            for (var o = 0; o < w.SupportDefs.Length; o++)
            {
                w.SupportCharges[(p * w.SupportDefs.Length) + o] = w.SupportDefs[o].Charges;
            }
        }
    }
}
