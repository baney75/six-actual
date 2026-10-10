using System.Text.Json;
using SixActual.Sim.Battle;

namespace SixActual.Sim.Tests;

/// <summary>ERA-03 maps: four real heightfields per era, decoded exactly and deterministically into the terrain grid.</summary>
public class MapTests
{
    static readonly string Root = FindRepoRoot();

    static readonly BattleData Defs = BattleData.Load(Root);

    [Fact]
    public void Every_era_ships_four_real_maps()
    {
        foreach (var era in new[] { "ww2", "coldwar", "modern" })
        {
            Assert.True(Defs.Maps.Count(m => m.Era == era) >= 4, $"{era} has fewer than 4 maps");
        }

        Assert.Equal(Defs.Maps.Length, Defs.Maps.Select(m => m.Id).Distinct().Count());
    }

    [Fact]
    public void Heightfield_samples_land_exactly_on_their_cells_and_interpolate_between()
    {
        // 3x3 samples at 16 m on a 32 m map of 8 m cells: cell centres at 4, 12, 20, 28 m.
        var dm = new ushort[] { 0, 160, 320, 0, 160, 320, 800, 800, 800 };
        var bytes = dm.SelectMany(v => new[] { (byte)(v & 0xff), (byte)(v >> 8) }).ToArray();
        var spec = JsonSerializer.SerializeToElement(new { size_m = 32, cell_m = 8, base_dm = 50, features = new object[] { new { type = "heightfield", n = 3, spacing_m = 16, dm_b64 = Convert.ToBase64String(bytes) } } });
        var t = TerrainBuilder.Build(spec);

        // Row 0 (y = 4 m) lies between sample rows 0 and 1, which are equal: x-only ramp 0 -> 160 -> 320.
        Assert.Equal(50 + 40, t.HeightDm[t.Index(0, 0)]);
        Assert.Equal(50 + 200, t.HeightDm[t.Index(2, 0)]);

        // Row 3 (y = 28 m) is 3/4 of the way from sample row 1 to row 2 (800).
        Assert.Equal(50 + ((40 * 1 / 4) + (800 * 3 / 4)), t.HeightDm[t.Index(0, 3)]);
    }

    [Fact]
    public void Map_terrain_is_real_relief_and_deterministic()
    {
        foreach (var m in Defs.Maps)
        {
            var a = TerrainBuilder.Build(Skirmish.Terrain(7, m));
            var b = TerrainBuilder.Build(Skirmish.Terrain(7, m));
            Assert.Equal(a.HeightDm, b.HeightDm);
            var relief = a.HeightDm.Max() - a.HeightDm.Min();
            Assert.True(relief >= 50, $"{m.Id}: relief {relief} dm is not real ground");

            // The same seed keeps the same vegetation and villages with or without a map.
            var plain = TerrainBuilder.Build(Skirmish.Terrain(7));
            Assert.Equal(plain.Forest, a.Forest);
            Assert.Equal(plain.Road, a.Road);
        }
    }

    [Fact]
    public void A_skirmish_on_a_map_runs_and_differs_from_the_generated_ground()
    {
        var rules = BattleRules.Load(Path.Combine(Root, "data", "rules.json"));
        var deck0 = Defs.Decks.First(d => d.Era == "coldwar").Id;
        var deck1 = Defs.Decks.First(d => d.Era == "coldwar" && d.Side != Defs.Decks.First(x => x.Id == deck0).Side).Id;
        var onMap = Skirmish.Create(Defs, rules, 3, deck0, deck1, true, true, new Skirmish.Options { Map = "fulda" });
        var plain = Skirmish.Create(Defs, rules, 3, deck0, deck1, true, true, new Skirmish.Options());
        Assert.NotEqual(plain.Terrain.HeightDm, onMap.Terrain.HeightDm);
        for (var i = 0; i < 200; i++)
        {
            BattleWorld.Step(onMap, rules, ReadOnlySpan<UnitOrder>.Empty);
        }

        Assert.Equal(200, onMap.Tick);
        Assert.Throws<InvalidDataException>(() => Skirmish.Create(Defs, rules, 3, deck0, deck1, true, true, new Skirmish.Options { Map = "atlantis" }));
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SixActual.slnx")))
        {
            dir = dir.Parent;
        }

        return dir!.FullName;
    }
}
