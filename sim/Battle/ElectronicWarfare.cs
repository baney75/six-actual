using SixActual.Sim.State.Battle;

namespace SixActual.Sim.Battle;

/// <summary>
/// SUP-04 electronic warfare (Cold War slice), delivered as off-map support: a zone of `radius_m` around the aim
/// point for `duration_ticks`, owned by the calling player's team.
///   radar jamming   enemy radar-directed guns (weapon type aaa) firing from inside the zone: hit chance x jam_radar_pct
///   comms jamming   enemy batteries inside the zone need jam_comms_delay_ticks more to start a fire mission
///   decoys          enemy missiles (sam, aam) fired at the owner's aircraft inside the zone: hit chance x decoy_pct
/// GPS degradation belongs to the modern era and arrives with its guided munitions.
/// </summary>
public static class ElectronicWarfare
{
    public const int RadarJam = 1;

    public const int CommsJam = 2;

    public const int Decoy = 3;

    /// <summary>SUP-02 fighter escort: enemy air-to-air missiles at the owner's aircraft in the zone x escort_pct.</summary>
    public const int Escort = 4;

    /// <summary>SUP-04 GPS / GLONASS jamming: satellite-guided rounds landing in the zone lose their accuracy.</summary>
    public const int GpsJam = 5;

    public static void Open(WorldState w, int team, int kind, int x, int y, int radiusM, int durationTicks) =>
        w.EwZones.Add(new EwZone { Team = team, Kind = kind, X = x, Y = y, RadiusM = radiusM, Until = w.Tick + durationTicks });

    /// <summary>True if (x, y) lies in an active zone of `kind` owned by a team other than `team` (hostile) or by
    /// `team` itself (friendly), as asked.</summary>
    public static bool InZone(WorldState w, int kind, Fp x, Fp y, int team, bool hostile)
    {
        foreach (var z in w.EwZones)
        {
            if (z.Kind != kind || z.Until <= w.Tick || (z.Team != team) != hostile)
            {
                continue;
            }

            long dx = (x.Raw >> Fp.FractionalBits) - z.X;
            long dy = (y.Raw >> Fp.FractionalBits) - z.Y;
            if ((dx * dx) + (dy * dy) <= (long)z.RadiusM * z.RadiusM)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Hit-chance multiplier (percent) from EW for this shot.</summary>
    public static int HitPct(WorldState w, BattleRules r, in UnitState s, in UnitState t, WeaponDef weapon)
    {
        if (w.EwZones.Count == 0)
        {
            return 100;
        }

        var pct = 100;
        if (weapon.Type == "aaa" && InZone(w, RadarJam, s.X, s.Y, Frontline.TeamOf(w, s.Player), hostile: true))
        {
            pct = pct * r.JamRadarPct / 100;
        }

        if (weapon.Type is "sam" or "aam" && w.Defs[t.Def].IsAir && InZone(w, Decoy, t.X, t.Y, Frontline.TeamOf(w, t.Player), hostile: false))
        {
            pct = pct * r.DecoyPct / 100;
        }

        if (weapon.Type == "aam" && w.Defs[t.Def].IsAir && InZone(w, Escort, t.X, t.Y, Frontline.TeamOf(w, t.Player), hostile: false))
        {
            pct = pct * r.EscortPct / 100;
        }

        return pct;
    }
}
