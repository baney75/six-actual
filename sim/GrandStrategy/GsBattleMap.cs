using System.Text.Json;
using SixActual.Sim.Battle;

namespace SixActual.Sim.GrandStrategy;

/// <summary>
/// GS-02: the 8 x 8 km RTS battle map for a province battle, generated from the province's terrain type with the
/// battle seed. Open country: low rolling hills, scattered woods, a few villages. Forest: dense woodland blocks.
/// Hills: many high hills and some woods. Urban: towns (building blocks) across the map. The turn's weather goes to
/// the battle unchanged.
/// </summary>
public static class GsBattleMap
{
    public static JsonElement Spec(int terrain, ulong seed)
    {
        var rng = new XorShift64(seed);
        int Next(int n) => (int)(rng.NextULong() % (ulong)n);
        var (hills, hillMaxDm, forests, villages) = terrain switch
        {
            1 => (20, 300, 170, 3),     // forest
            2 => (90, 900, 40, 4),      // hills
            3 => (20, 300, 25, 24),     // urban
            _ => (25, 250, 20, 4),      // open
        };
        var features = new List<object>();
        for (var i = 0; i < hills; i++)
        {
            features.Add(new { type = "hill", x = Next(8000), y = Next(8000), radius_m = 300 + Next(900), height_dm = 100 + Next(hillMaxDm) });
        }

        for (var i = 0; i < forests; i++)
        {
            features.Add(new { type = "forest", x = Next(7600), y = Next(7600), w = 150 + Next(400), h = 150 + Next(400), density = 70 + Next(30) });
        }

        for (var k = 1; k < 4; k++)
        {
            features.Add(new { type = "road", x = 0, y = (k * 2000) - 8, w = 8000, h = 16 });
            features.Add(new { type = "road", x = (k * 2000) - 8, y = 0, w = 16, h = 8000 });
        }

        var placed = new List<(int X, int Y)>();
        while (placed.Count < villages)
        {
            var vx = 800 + Next(6400);
            var vy = 800 + Next(6400);
            if (placed.Any(v => Math.Max(Math.Abs(v.X - vx), Math.Abs(v.Y - vy)) < 160))
            {
                continue;
            }

            placed.Add((vx, vy));
            for (var b = 0; b < 8; b++)
            {
                features.Add(new { type = "building", x = vx + ((b % 4) * 24), y = vy + ((b / 4) * 24), nx = 4, ny = 4, floors = 2, material = "brick" });
            }
        }

        return JsonSerializer.SerializeToElement(new { size_m = 8000, cell_m = 8, features });
    }

    /// <summary>Forest share (percent of cells with forest density above 50), building count and highest ground (dm).</summary>
    public static (int ForestPct, int Buildings, int MaxHeightDm) Stats(JsonElement spec)
    {
        var t = TerrainBuilder.Build(spec);
        var forest = t.Forest.Count(f => f > 50);
        var buildings = spec.GetProperty("features").EnumerateArray().Count(f => f.GetProperty("type").GetString() == "building");
        return ((int)(forest * 100L / Math.Max(1, t.Forest.Length)), buildings, t.HeightDm.Max());
    }
}
