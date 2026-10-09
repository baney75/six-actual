using System.Text.Json;
using SixActual.Sim.State.Battle;

namespace SixActual.Sim.Battle;

/// <summary>
/// A 1v1 skirmish world on a generated 8x8 km map: the setup the G5 soak, the network runner and the game's skirmish
/// screen share. Player 0 deploys from the south, player 1 from the north; five objectives across the centre.
/// </summary>
public static class Skirmish
{
    public static readonly (int X, int Y)[] ObjectiveSpots = [(2000, 4000), (4000, 4000), (6000, 4000), (4000, 2600), (4000, 5400)];

    /// <summary>Deterministic terrain from a seed: rolling hills, forest blocks, a road grid and six villages.</summary>
    public static JsonElement Terrain(ulong seed)
    {
        var rng = new XorShift64(seed);
        int Next(int n) => (int)(rng.NextULong() % (ulong)n);
        var features = new List<object>();
        for (var i = 0; i < 40; i++)
        {
            features.Add(new { type = "hill", x = Next(8000), y = Next(8000), radius_m = 300 + Next(900), height_dm = 100 + Next(500) });
        }

        for (var i = 0; i < 60; i++)
        {
            features.Add(new { type = "forest", x = Next(7600), y = Next(7600), w = 100 + Next(400), h = 100 + Next(400), density = 50 + Next(50) });
        }

        for (var k = 1; k < 4; k++)
        {
            features.Add(new { type = "road", x = 0, y = (k * 2000) - 8, w = 8000, h = 16 });
            features.Add(new { type = "road", x = (k * 2000) - 8, y = 0, w = 16, h = 8000 });
        }

        // Villages (4x2 blocks, ~84x36 m) at least 160 m apart, so no two buildings share a terrain cell.
        var villages = new List<(int X, int Y)>();
        while (villages.Count < 6)
        {
            var vx = 1000 + Next(6000);
            var vy = 1000 + Next(6000);
            if (villages.Any(v => Math.Max(Math.Abs(v.X - vx), Math.Abs(v.Y - vy)) < 160))
            {
                continue;
            }

            villages.Add((vx, vy));
            for (var b = 0; b < 8; b++)
            {
                features.Add(new { type = "building", x = vx + ((b % 4) * 24), y = vy + ((b / 4) * 24), nx = 4, ny = 4, floors = 2, material = "brick" });
            }
        }

        return JsonSerializer.SerializeToElement(new { size_m = 8000, cell_m = 8, features });
    }

    /// <summary>Skirmish options (MODE-01). <see cref="PerSide"/> players per team (1-5): even players are team 0 from
    /// the south with deck 0, odd players team 1 from the north with deck 1. Player 0's XO starts as <c>xo0</c>; every
    /// other player is an AI XO. <see cref="Income"/> overrides the decks' income profile when set.</summary>
    public sealed class Options
    {
        public int PerSide { get; init; } = 1;
        public int StartDp { get; init; } = 1000;
        public string? Income { get; init; }
        public int VictoryType { get; init; }
        public int TimeOfDay { get; init; } = 1;
        public int Weather { get; init; }
        public bool FieldRequests { get; init; }

        /// <summary>Starting support points for every player (off-map support, MEC-03).</summary>
        public int StartSp { get; init; }
    }

    /// <summary>Builds the skirmish world for two shipped decks. <paramref name="xo0"/> and <paramref name="xo1"/> say
    /// whether players 0 and 1 start with their XO in command (the human player's XO starts off and takes over on
    /// Take Control).</summary>
    public static WorldState Create(BattleData data, BattleRules rules, ulong seed, string deck0, string deck1, bool xo0, bool xo1, int startDp = 1000, bool fieldRequests = false, DeckDef[]? customDecks = null)
        => Create(data, rules, seed, deck0, deck1, xo0, xo1, new Options { StartDp = startDp, FieldRequests = fieldRequests }, customDecks);

    public static WorldState Create(BattleData data, BattleRules rules, ulong seed, string deck0, string deck1, bool xo0, bool xo1, Options opt, DeckDef[]? customDecks = null)
    {
        var spec = Terrain(seed);
        var w = new WorldState
        {
            Seed = seed,
            Rng = BattleWorld.MixSeed(seed),
            Defs = data.Units,
            Weapons = data.Weapons,
            SupportDefs = data.Support,
            Terrain = TerrainBuilder.Build(spec),
            VictoryType = opt.VictoryType,
            TimeOfDay = opt.TimeOfDay,
            Weather = opt.Weather,
            AirGrounded = rules.AirOps(opt.Weather) ? 0 : 1,
        };
        Structures.Place(w, rules, spec);
        // Custom decks (the game's deck builder) follow the shipped ones, so shipped deck indices are unchanged.
        var allDecks = customDecks is { Length: > 0 } ? [.. data.Decks, .. customDecks] : data.Decks;
        w.Decks = allDecks;
        w.FieldRequestDefs = data.FieldRequests;
        w.FieldRequestsOn = opt.FieldRequests ? 1 : 0;
        var decks = new[] { Array.FindIndex(allDecks, d => d.Id == deck0), Array.FindIndex(allDecks, d => d.Id == deck1) };
        if (decks[0] < 0 || decks[1] < 0)
        {
            throw new ArgumentException($"unknown deck: {(decks[0] < 0 ? deck0 : deck1)}");
        }

        var perSide = Math.Clamp(opt.PerSide, 1, 5);
        // The battle's era (support options, field requests) is player 0's deck era.
        w.Era = allDecks[decks[0]].Era is { Length: > 0 } era ? era : "coldwar";
        var stock = new List<int>();
        w.Players = new PlayerState[perSide * 2];
        for (var p = 0; p < w.Players.Length; p++)
        {
            var deck = decks[p % 2];
            w.Players[p] = new PlayerState
            {
                Dp = opt.StartDp,
                Sp = opt.StartSp,
                Profile = BattleRules.ProfileIndex(opt.Income ?? allDecks[deck].Income),
                SpawnX = perSide == 1 ? 4000 : 1000 + ((p / 2) * 6000 / (perSide - 1)),
                SpawnY = p % 2 == 0 ? 600 : 7400,
                Deck = deck,
                StockStart = stock.Count,
                Team = p % 2,
            };
            foreach (var c in allDecks[deck].Cards)
            {
                stock.Add(c.Count);
            }
        }

        w.CardStock = stock.ToArray();
        w.Objectives = ObjectiveSpots.Select(o => new Objective { X = o.X, Y = o.Y, Owner = -1 }).ToArray();
        Command.Init(w);
        for (var p = 0; p < w.Players.Length; p++)
        {
            w.XoActive[p] = p == 0 ? (xo0 ? 1 : 0) : p == 1 ? (xo1 ? 1 : 0) : 1;
        }
        return w;
    }
}
