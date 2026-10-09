using System.Text.Json;
using SixActual.Sim.State.Battle;

namespace SixActual.Sim.Battle;

/// <summary>One Field Requests task force from data/field_requests.json (MEC-06).</summary>
public sealed class FieldRequestDef
{
    public string Id { get; init; } = "";

    public string Side { get; init; } = "";

    public string Era { get; init; } = "";

    /// <summary>Phase index (0 A, 1 B, 2 C) at whose start it is offered.</summary>
    public int Phase { get; init; }

    /// <summary>Roster unit index, count and card cost per entry.</summary>
    public (int Unit, int Count, int Cost)[] Units { get; init; } = [];

    public static FieldRequestDef[] Load(string path, BattleUnitDef[] units)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
        string[] phases = ["A", "B", "C"];
        return doc.RootElement.GetProperty("task_forces").EnumerateArray().Select(t => new FieldRequestDef
        {
            Id = t.GetProperty("id").GetString()!,
            Side = t.GetProperty("side").GetString()!,
            Era = t.GetProperty("era").GetString()!,
            Phase = Array.IndexOf(phases, t.GetProperty("phase").GetString()!),
            Units = t.GetProperty("units").EnumerateArray()
                .Select(u => (BattleUnitDef.IndexOf(units, u.GetProperty("unit").GetString()!), u.GetProperty("count").GetInt32(), u.GetProperty("cost").GetInt32()))
                .ToArray(),
        }).ToArray();
    }
}

/// <summary>
/// MEC-06 Field Requests: at each phase change, every player of a match with Field Requests on is offered three
/// different task forces of their side, era and the new phase, drawn with the battle RNG. Taking one (order
/// FieldRequest, choice 0-2) brings its units in at the player's entry point, free of deployment points and in
/// addition to the deck. A player whose XO is running the battle has it chosen after field_request_auto_ticks: the
/// XO takes the offer with the most value. An offer not taken lapses at the next phase change.
/// </summary>
public static class FieldRequests
{
    public const int Offers = 3;

    public static void OnPhaseChange(WorldState w)
    {
        if (w.FieldRequestsOn == 0 || w.FieldRequestDefs.Length == 0)
        {
            return;
        }

        EnsureState(w);
        for (var p = 0; p < w.Players.Length; p++)
        {
            var side = Support.PlayerSide(w, p);
            var pool = new List<int>();
            for (var k = 0; k < w.FieldRequestDefs.Length; k++)
            {
                var d = w.FieldRequestDefs[k];
                if (d.Phase == w.Phase && string.Equals(d.Side, side, StringComparison.Ordinal) && string.Equals(d.Era, w.Era, StringComparison.Ordinal))
                {
                    pool.Add(k);
                }
            }

            for (var o = 0; o < Offers; o++)
            {
                if (pool.Count == 0)
                {
                    w.FieldOffers[(p * Offers) + o] = -1;
                    continue;
                }

                var pick = (int)(BattleWorld.NextRandom(w) % (ulong)pool.Count);
                w.FieldOffers[(p * Offers) + o] = pool[pick];
                pool.RemoveAt(pick);
            }

            w.FieldOfferTick[p] = w.Tick;
            w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.FieldOffered, A = p, B = w.FieldOffers[p * Offers], C = w.FieldOffers[(p * Offers) + 1], Value = w.FieldOffers[(p * Offers) + 2] });
        }
    }

    /// <summary>The player takes offer `choice` (0-2); false if there is no open offer in that slot.</summary>
    public static bool Take(WorldState w, int player, int choice)
    {
        if (w.FieldRequestsOn == 0 || player < 0 || player >= w.Players.Length || choice < 0 || choice >= Offers)
        {
            return false;
        }

        EnsureState(w);
        var tf = w.FieldOffers[(player * Offers) + choice];
        if (tf < 0)
        {
            return false;
        }

        var spawned = 0;
        var pl = w.Players[player];
        foreach (var (unit, count, cost) in w.FieldRequestDefs[tf].Units)
        {
            for (var n = 0; n < count; n++)
            {
                // Spread the arrivals 20 m apart across the entry point.
                Economy.Spawn(w, unit, player, pl.SpawnX + ((spawned % 5) * 20) - 40, pl.SpawnY + (spawned / 5 * 20), cost);
                spawned++;
            }
        }

        for (var o = 0; o < Offers; o++)
        {
            w.FieldOffers[(player * Offers) + o] = -1;
        }

        w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.FieldTaken, A = player, B = tf, Value = spawned });
        return true;
    }

    public static void Step(WorldState w, BattleRules r)
    {
        if (w.FieldRequestsOn == 0 || w.FieldOffers.Length == 0)
        {
            return;
        }

        for (var p = 0; p < w.Players.Length; p++)
        {
            if (p >= w.XoActive.Length || w.XoActive[p] == 0 || w.Tick - w.FieldOfferTick[p] < r.FieldRequestAutoTicks)
            {
                continue;
            }

            var best = -1;
            var bestValue = -1;
            for (var o = 0; o < Offers; o++)
            {
                var tf = w.FieldOffers[(p * Offers) + o];
                if (tf < 0)
                {
                    continue;
                }

                var value = w.FieldRequestDefs[tf].Units.Sum(u => u.Count * u.Cost);
                if (value > bestValue)
                {
                    best = o;
                    bestValue = value;
                }
            }

            if (best >= 0)
            {
                Take(w, p, best);
            }
        }
    }

    static void EnsureState(WorldState w)
    {
        if (w.FieldOffers.Length != w.Players.Length * Offers)
        {
            w.FieldOffers = Enumerable.Repeat(-1, w.Players.Length * Offers).ToArray();
            w.FieldOfferTick = new int[w.Players.Length];
        }
    }
}
