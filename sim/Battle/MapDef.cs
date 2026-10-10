using System.Text.Json;

namespace SixActual.Sim.Battle;

/// <summary>ERA-03 battle map (data/maps.json, scripts/generate_maps.py): a real 8x8 km heightfield from the Copernicus
/// DEM GLO-30. <see cref="DmB64"/> is uint16 little-endian decimetres above the map's lowest point, row 0 south.</summary>
public sealed class MapDef
{
    public string Id { get; init; } = "";

    public string Name { get; init; } = "";

    public string Era { get; init; } = "";

    public int N { get; init; }

    public int SpacingM { get; init; }

    public string DmB64 { get; init; } = "";

    public static MapDef[] Load(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var maps = new List<MapDef>();
        foreach (var m in doc.RootElement.GetProperty("maps").EnumerateArray())
        {
            var hf = m.GetProperty("heightfield");
            var def = new MapDef
            {
                Id = m.GetProperty("id").GetString()!,
                Name = m.GetProperty("name").GetString()!,
                Era = m.GetProperty("era").GetString()!,
                N = hf.GetProperty("n").GetInt32(),
                SpacingM = hf.GetProperty("spacing_m").GetInt32(),
                DmB64 = hf.GetProperty("dm_b64").GetString()!,
            };
            if (def.N < 2 || def.SpacingM <= 0 || Convert.FromBase64String(def.DmB64).Length != def.N * def.N * 2)
            {
                throw new InvalidDataException($"map {def.Id}: heightfield is not {def.N}x{def.N} uint16");
            }

            maps.Add(def);
        }

        return maps.ToArray();
    }
}
