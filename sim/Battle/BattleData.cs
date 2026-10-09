namespace SixActual.Sim.Battle;

/// <summary>The unit and weapon catalogs the battle sim runs on.</summary>
public sealed class BattleData
{
    public BattleUnitDef[] Units { get; init; } = [];

    public WeaponDef[] Weapons { get; init; } = [];

    /// <summary>Shipped decks (data/decks.json, later). Scenarios may add inline decks per player.</summary>
    public DeckDef[] Decks { get; init; } = [];

    /// <summary>Off-map support catalogue (data/support.json).</summary>
    public SupportDef[] Support { get; init; } = [];

    /// <summary>MEC-06 Field Requests task forces (data/field_requests.json).</summary>
    public FieldRequestDef[] FieldRequests { get; init; } = [];

    /// <summary>MEC-07 operation event deck (data/operation_events.json).</summary>
    public OperationEventDef[] OperationEvents { get; init; } = [];

    /// <summary>
    /// Loads data/roster.json + data/weapons.json when present; until the sourced roster lands it adapts the
    /// Phase 0 placeholder catalog data/units.json. Exactly one source is used, chosen by file presence.
    /// </summary>
    public static BattleData Load(string repoRoot)
    {
        var roster = Path.Combine(repoRoot, "data", "roster.json");
        var weapons = Path.Combine(repoRoot, "data", "weapons.json");
        var (units, weaps) = File.Exists(roster)
            ? BattleUnitDef.Load(roster, weapons)
            : BattleUnitDef.FromLegacy(UnitCatalog.Load(Path.Combine(repoRoot, "data", "units.json")));
        var decksPath = Path.Combine(repoRoot, "data", "decks.json");
        var decks = new List<DeckDef>();
        if (File.Exists(decksPath) && File.Exists(roster))
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(decksPath));
            string[] phases = ["A", "B", "C"];
            foreach (var d in doc.RootElement.GetProperty("decks").EnumerateArray())
            {
                decks.Add(new DeckDef
                {
                    Id = d.GetProperty("id").GetString()!,
                    Income = d.TryGetProperty("income", out var inc) ? inc.GetString()! : "balanced",
                    Era = d.TryGetProperty("era", out var era) ? era.GetString()! : "",
                    Side = d.TryGetProperty("side", out var side) ? side.GetString()! : "",
                    Nation = d.TryGetProperty("nation", out var nation) ? nation.GetString()! : "",
                    Cards = d.GetProperty("cards").EnumerateArray().Select(c => new CardDef
                    {
                        Unit = BattleUnitDef.IndexOf(units, c.GetProperty("unit").GetString()!),
                        Count = c.GetProperty("count").GetInt32(),
                        MaxCount = c.TryGetProperty("max_count", out var mx) ? mx.GetInt32() : c.GetProperty("count").GetInt32(),
                        Phase = Array.IndexOf(phases, c.GetProperty("phase").GetString()!),
                        Cost = c.GetProperty("cost").GetInt32(),
                    }).ToArray(),
                });
            }
        }

        var support = File.Exists(roster) ? SupportDef.Load(Path.Combine(repoRoot, "data", "support.json"), weaps, units) : [];
        var field = File.Exists(roster) ? FieldRequestDef.Load(Path.Combine(repoRoot, "data", "field_requests.json"), units) : [];
        var events = OperationEventDef.Load(Path.Combine(repoRoot, "data", "operation_events.json"));
        return new BattleData { Units = units, Weapons = weaps, Decks = decks.ToArray(), Support = support, FieldRequests = field, OperationEvents = events };
    }
}
