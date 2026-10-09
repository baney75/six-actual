using System.Text.Json;

namespace SixActual.Sim.Battle;

/// <summary>A weapon system from data/weapons.json. All values are integers (mm, m, ticks, percent).</summary>
public sealed class WeaponDef
{
    public string Id { get; init; } = "";

    /// <summary>ke, heat, atgm, he, autocannon, smallarms, mg, mortar, gun_artillery, rocket_artillery, sam, aaa, aam, bomb.</summary>
    public string Type { get; init; } = "";

    public int RangeMinM { get; init; }

    public int RangeMaxM { get; init; }

    /// <summary>(range m, penetration mm RHA) points, ascending range; linear between, flat beyond the ends.</summary>
    public int[][] Pen { get; init; } = [];

    /// <summary>(range m, hit percent vs a stationary MBT-sized target) points.</summary>
    public int[][] Accuracy { get; init; } = [];

    public int ReloadTicks { get; init; }

    public int Salvo { get; init; } = 1;

    /// <summary>Damage on a penetrating hit (hp).</summary>
    public int Damage { get; init; }

    public int BlastDamage { get; init; }

    public int BlastRadiusM { get; init; }

    public int Suppression { get; init; }

    public bool HitsGround { get; init; } = true;

    public bool HitsAir { get; init; }

    /// <summary>ERA-01 / SUP-01: strikes the target's top armour (Javelin, TOW-2B, loitering munitions).</summary>
    public bool TopAttack { get; init; }

    /// <summary>Indirect fire: mean shell speed over the trajectory (m/s).</summary>
    public int TofMS { get; init; }

    /// <summary>Indirect fire: circular error probable in thousandths of range.</summary>
    public int CepPermille { get; init; }

    /// <summary>Shaped-charge weapons use the target's CE armour; everything else uses KE armour.</summary>
    public bool ChemicalEnergy => Type is "heat" or "atgm" or "rocket";

    public bool Indirect => Type is "mortar" or "gun_artillery" or "rocket_artillery";

    public static int Interp(int[][] table, int x)
    {
        if (table.Length == 0)
        {
            return 0;
        }

        if (x <= table[0][0])
        {
            return table[0][1];
        }

        for (var i = 1; i < table.Length; i++)
        {
            if (x <= table[i][0])
            {
                var x0 = table[i - 1][0];
                var y0 = table[i - 1][1];
                var x1 = table[i][0];
                var y1 = table[i][1];
                return y0 + ((y1 - y0) * (x - x0) / Math.Max(1, x1 - x0));
            }
        }

        return table[^1][1];
    }
}

/// <summary>One mounted weapon with its basic load.</summary>
public sealed class Mount
{
    public int Weapon { get; init; }

    public int Ammo { get; init; }
}

/// <summary>A unit type from data/roster.json (the Phase 1 data model). Integers only.</summary>
public sealed class BattleUnitDef
{
    public string Id { get; init; } = "";

    public string Side { get; init; } = "";

    /// <summary>The research role (roster "kind": tank, fighter, transport_helo, ...) for menus and cards. Not sim state;
    /// the sim works on <see cref="Kind"/>.</summary>
    public string Role { get; init; } = "";

    public int Year { get; init; }

    public int Kind { get; init; }

    public int MoveClass { get; init; }

    public int SpeedMmS { get; init; }

    public int Hp { get; init; }

    /// <summary>Infantry squad members (each is hp / members). 1 for vehicles.</summary>
    public int Members { get; init; } = 1;

    public int ArmorFrontKe { get; init; }

    public int ArmorFrontCe { get; init; }

    public int ArmorSideKe { get; init; }

    public int ArmorSideCe { get; init; }

    public int ArmorRear { get; init; }

    public int ArmorTop { get; init; }

    public int OpticsM { get; init; }

    public int Thermal { get; init; }

    public int Stealth { get; init; }

    /// <summary>Target size percent; 100 = MBT. Scales the shooter's hit chance.</summary>
    public int SizePct { get; init; } = 100;

    public int Fuel { get; init; }

    /// <summary>Fuel units burned per kilometre moved.</summary>
    public int FuelPerKm { get; init; }

    /// <summary>Supply points carried (logistics units).</summary>
    public int Supply { get; init; }

    /// <summary>SUP-03: 1 for a recovery vehicle that repairs friendly vehicles.</summary>
    public int Repair { get; init; }

    /// <summary>SUP-03: 1 for a casualty-evacuation aircraft that returns lightly wounded to infantry squads.</summary>
    public int Medevac { get; init; }

    public Mount[] Weapons { get; init; } = [];

    /// <summary>Front-moving weight; -1 = use the rules default for the kind (recon and AT teams set 0).</summary>
    public int FrontWeight { get; init; } = -1;

    /// <summary>Aircraft flight envelope (FLY-03): max level speed at low altitude, stall speed, climb rate (mm/s)
    /// and positive G limit x10. 0 = not an aircraft or not yet sourced (Flight derives fallbacks).</summary>
    public int VmaxMmS { get; init; }

    /// <summary>MEC-09: road speed of the carrier a mechanised or motorised infantry team rides (0: on foot only).</summary>
    public int MountedSpeedMmS { get; init; }

    /// <summary>Movement class index of that carrier (Rules.MoveClasses), for its terrain speed factors.</summary>
    public int MountedMoveClass { get; init; }

    public int VstallMmS { get; init; }

    public int ClimbMmS { get; init; }

    public int GLimitX10 { get; init; }

    public bool IsAir => Kind == UnitDef.KindAir;

    public bool IsInfantry => Kind == UnitDef.KindInfantry;

    /// <summary>Adapter for the Phase 0 placeholder catalog (data/units.json) until data/roster.json replaces it.</summary>
    public static (BattleUnitDef[] Units, WeaponDef[] Weapons) FromLegacy(UnitDef[] legacy)
    {
        var units = new BattleUnitDef[legacy.Length];
        var weapons = new WeaponDef[legacy.Length];
        for (var i = 0; i < legacy.Length; i++)
        {
            var d = legacy[i];
            weapons[i] = new WeaponDef
            {
                Id = d.Id + "_main",
                Type = d.Kind == UnitDef.KindArtillery ? "gun_artillery" : d.Kind == UnitDef.KindInfantry ? "smallarms" : "ke",
                RangeMaxM = d.RangeM,
                Pen = [[0, d.PenMm], [1000, Math.Max(0, d.PenMm - d.PenDropPerKm)]],
                Accuracy = [[0, 90], [Math.Max(1, d.RangeM), 40]],
                ReloadTicks = Math.Max(1, d.ReloadTicks),
                Damage = 30,
                Suppression = 10,
                HitsAir = d.Kind == UnitDef.KindAa,
            };
            units[i] = new BattleUnitDef
            {
                Id = d.Id,
                Year = d.Year,
                Kind = d.Kind,
                MoveClass = Navigation.ClassOf(d),
                SpeedMmS = d.SpeedMmS,
                Hp = d.Hp,
                ArmorFrontKe = d.ArmorFront,
                ArmorFrontCe = d.ArmorFront,
                ArmorSideKe = d.ArmorSide,
                ArmorSideCe = d.ArmorSide,
                ArmorRear = d.ArmorRear,
                ArmorTop = d.ArmorTop,
                OpticsM = d.OpticsM,
                Stealth = d.Stealth,
                Fuel = d.Fuel,
                Weapons = d.RangeM > 0 ? [new Mount { Weapon = i, Ammo = d.Ammo }] : [],
            };
        }

        return (units, weapons);
    }

    public static (BattleUnitDef[] Units, WeaponDef[] Weapons) Load(string rosterPath, string weaponsPath)
    {
        var weapons = new List<WeaponDef>();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        using (var doc = JsonDocument.Parse(File.ReadAllBytes(weaponsPath)))
        {
            foreach (var w in doc.RootElement.GetProperty("weapons").EnumerateArray())
            {
                index[w.GetProperty("id").GetString()!] = weapons.Count;
                weapons.Add(new WeaponDef
                {
                    Id = w.GetProperty("id").GetString()!,
                    Type = w.GetProperty("type").GetString()!,
                    RangeMinM = Int(w, "range_min_m"),
                    RangeMaxM = Int(w, "range_max_m"),
                    Pen = Table(w, "pen"),
                    Accuracy = Table(w, "accuracy"),
                    ReloadTicks = Int(w, "reload_ticks", 1),
                    Salvo = Int(w, "salvo", 1),
                    Damage = Int(w, "damage"),
                    BlastDamage = Int(w, "blast_damage"),
                    BlastRadiusM = Int(w, "blast_radius_m"),
                    Suppression = Int(w, "suppression"),
                    HitsGround = !w.TryGetProperty("hits_ground", out var g) || g.GetBoolean(),
                    TopAttack = w.TryGetProperty("top_attack", out var ta) && ta.GetBoolean(),
                    HitsAir = w.TryGetProperty("hits_air", out var a) && a.GetBoolean(),
                    TofMS = Int(w, "tof_m_s"),
                    CepPermille = Int(w, "cep_permille"),
                });
            }
        }

        var units = new List<BattleUnitDef>();
        using (var doc = JsonDocument.Parse(File.ReadAllBytes(rosterPath)))
        {
            foreach (var u in doc.RootElement.GetProperty("units").EnumerateArray())
            {
                var armor = u.GetProperty("armor");
                var mounts = new List<Mount>();
                foreach (var m in u.GetProperty("weapons").EnumerateArray())
                {
                    var wid = m.GetProperty("weapon").GetString()!;
                    if (!index.TryGetValue(wid, out var wi))
                    {
                        throw new InvalidDataException($"Unit {u.GetProperty("id").GetString()} mounts unknown weapon {wid}.");
                    }

                    mounts.Add(new Mount { Weapon = wi, Ammo = m.GetProperty("ammo").GetInt32() });
                }

                units.Add(new BattleUnitDef
                {
                    Id = u.GetProperty("id").GetString()!,
                    Side = u.GetProperty("side").GetString()!,
                    Year = Int(u, "year"),
                    Kind = UnitDef.ParseKind(u.GetProperty("sim_kind").GetString()!),
                    MoveClass = Array.IndexOf(BattleRules.MoveClasses, u.GetProperty("move_class").GetString()!),
                    SpeedMmS = Int(u, "speed_mm_s"),
                    Hp = Int(u, "hp"),
                    Members = Int(u, "members", 1),
                    ArmorFrontKe = Int(armor, "front_ke"),
                    ArmorFrontCe = Int(armor, "front_ce"),
                    ArmorSideKe = Int(armor, "side_ke"),
                    ArmorSideCe = Int(armor, "side_ce"),
                    ArmorRear = Int(armor, "rear"),
                    ArmorTop = Int(armor, "top"),
                    OpticsM = Int(u, "optics_m"),
                    Thermal = Int(u, "thermal"),
                    Stealth = BattleRules.NonNegative(Int(u, "stealth"), $"{u.GetProperty("id").GetString()}.stealth"),
                    SizePct = Int(u, "size_pct", 100),
                    Fuel = Int(u, "fuel"),
                    FuelPerKm = Int(u, "fuel_per_km"),
                    Supply = Int(u, "supply"),
                    Repair = u.TryGetProperty("repair", out var rp) ? rp.GetInt32() : 0,
                    Medevac = u.TryGetProperty("medevac", out var mv) ? mv.GetInt32() : 0,
                    FrontWeight = Int(u, "front_weight", -1),
                    Role = u.TryGetProperty("kind", out var role) ? role.GetString()! : "",
                    Weapons = mounts.ToArray(),
                    VmaxMmS = u.TryGetProperty("flight", out var fl) ? Int(fl, "vmax_mm_s") : 0,
                    MountedSpeedMmS = u.TryGetProperty("mounted_speed_mm_s", out var ms) ? ms.GetInt32() : 0,
                    MountedMoveClass = u.TryGetProperty("mounted_move_class", out var mmc) && mmc.GetString() is { Length: > 0 } mcs ? Array.IndexOf(BattleRules.MoveClasses, mcs) : 0,
                    VstallMmS = u.TryGetProperty("flight", out var fl2) ? Int(fl2, "vstall_mm_s") : 0,
                    ClimbMmS = u.TryGetProperty("flight", out var fl3) ? Int(fl3, "climb_mm_s") : 0,
                    GLimitX10 = u.TryGetProperty("flight", out var fl4) ? Int(fl4, "g_limit_x10") : 0,
                });
            }
        }

        return (units.ToArray(), weapons.ToArray());
    }

    public static int IndexOf(BattleUnitDef[] defs, string id)
    {
        for (var i = 0; i < defs.Length; i++)
        {
            if (string.Equals(defs[i].Id, id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        throw new InvalidDataException($"Missing unit {id}.");
    }

    static int Int(JsonElement e, string name, int fallback = 0) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : fallback;

    static int[][] Table(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var t))
        {
            return [];
        }

        return t.EnumerateArray().Select(p => new[] { p[0].GetInt32(), p[1].GetInt32() }).ToArray();
    }
}
