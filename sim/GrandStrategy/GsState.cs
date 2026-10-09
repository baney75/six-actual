using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SixActual.Sim.GrandStrategy;

public sealed class GsProvince
{
    public int Owner { get; set; }

    public int Industry { get; set; }

    public int Manpower { get; set; }

    /// <summary>GS-04: fallout zone until this turn (exclusive).</summary>
    public int FalloutUntil { get; set; }
}

public sealed class GsFaction
{
    public int Side { get; set; }

    public bool AtWar { get; set; }

    public int Production { get; set; }

    public int ManpowerPool { get; set; }

    public int Tech { get; set; }

    public int Research { get; set; }

    /// <summary>GS-03: faction receiving lend-lease from this one (-1 none).</summary>
    public int LendLeaseTo { get; set; } = -1;

    /// <summary>GS-03: province this faction's war goal names (-1 none).</summary>
    public int WarGoal { get; set; } = -1;

    /// <summary>GS-04: AI owes a retaliation strike (1) after being struck.</summary>
    public int OwesRetaliation { get; set; }
}

public sealed class GsArmy
{
    public int Id { get; set; }

    public int Faction { get; set; }

    public int Province { get; set; }

    public int Strength { get; set; }

    public int Commander { get; set; } = -1;

    /// <summary>Province the army is ordered toward (-1 none); it moves one province a turn.</summary>
    public int Target { get; set; } = -1;
}

/// <summary>CMD-08: a commander with persistent XP and wounds; a wounded one is replaced (0 XP) until he returns.</summary>
public sealed class GsCommander
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public int Faction { get; set; }

    public int Xp { get; set; }

    public int WoundedUntil { get; set; }

    public int Battles { get; set; }
}

/// <summary>MEC-13 bookkeeping: a province attacked on Turn, and whether a reserve was in reach and arrived.</summary>
public sealed class GsAttack
{
    public int Turn { get; set; }

    public int Province { get; set; }

    public int Faction { get; set; }

    public bool ReserveInReach { get; set; }

    /// <summary>The defender still held the province after the battle (only then can it be reinforced).</summary>
    public bool Held { get; set; }

    /// <summary>The reserve army the AI sent (-1 none yet).</summary>
    public int Reinforcer { get; set; } = -1;

    public int ArrivedTurn { get; set; } = -1;
}

/// <summary>The whole grand-strategy state: deterministic, integer-only, hashable and saved/loaded as JSON (CMD-08).</summary>
public sealed class GsState
{
    public const int WinnerNone = -1;

    public const int WinnerStalemate = 100;

    public const int WinnerExchange = 101;

    public string Era { get; set; } = "";

    public ulong Rng { get; set; }

    public int Turn { get; set; }

    public int Winner { get; set; } = WinnerNone;

    public int Tension { get; set; }

    /// <summary>Highest nuclear level released: 0 none, 1 tactical, 2 theater, 3 strategic.</summary>
    public int NuclearLevel { get; set; }

    public int CeasefireUntil { get; set; }

    public int NextArmyId { get; set; }

    public GsProvince[] Provinces { get; set; } = [];

    public GsFaction[] Factions { get; set; } = [];

    public List<GsArmy> Armies { get; set; } = [];

    public List<GsCommander> Commanders { get; set; } = [];

    public List<GsAttack> Attacks { get; set; } = [];

    public int Battles { get; set; }

    public int Captures { get; set; }

    public int RtsBattles { get; set; }

    /// <summary>Per-turn weather index (BattleRules.WeatherNames) for battles fought this turn.</summary>
    public int Weather { get; set; }

    static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public string Save() => JsonSerializer.Serialize(this, Json);

    public static GsState Load(string json) => JsonSerializer.Deserialize<GsState>(json, Json)!;

    public string Hash() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Save()))).ToLowerInvariant();
}
