using System.Text.Json;
using SixActual.Sim.State.Battle;

namespace SixActual.Sim.Battle;

/// <summary>Builds a TerrainGrid from feature lists (scenario fixtures and generated maps). Heights combine by maximum.</summary>
public static class TerrainBuilder
{
    public static TerrainGrid Build(JsonElement spec)
    {
        var size = spec.GetProperty("size_m").GetInt32();
        var cell = spec.GetProperty("cell_m").GetInt32();
        var baseDm = spec.TryGetProperty("base_dm", out var b) ? b.GetInt32() : 0;
        var cells = size / cell;
        var t = new TerrainGrid
        {
            SizeM = size,
            CellM = cell,
            Cells = cells,
            HeightDm = new int[cells * cells],
            Forest = new byte[cells * cells],
            Road = new byte[cells * cells],
            Hedge = new byte[cells * cells],
            Blocked = new byte[cells * cells],
            RubbleDm = new int[cells * cells],
            BuildingAt = new int[cells * cells],
            Obstacle = new byte[cells * cells],
            Fort = new byte[cells * cells],
            MineDensity = new byte[cells * cells],
            MineTeam = new byte[cells * cells],
        };
        Array.Fill(t.HeightDm, baseDm);
        if (!spec.TryGetProperty("features", out var features))
        {
            return t;
        }

        foreach (var f in features.EnumerateArray())
        {
            switch (f.GetProperty("type").GetString())
            {
                case "hill":
                    Hill(t, f.GetProperty("x").GetInt32(), f.GetProperty("y").GetInt32(), f.GetProperty("radius_m").GetInt32(), baseDm + f.GetProperty("height_dm").GetInt32());
                    break;
                case "heightfield":
                    Heightfield(t, f, baseDm);
                    break;
                case "ridge":
                    Ridge(t, f, baseDm);
                    break;
                case "rect":
                    Rect(t, f, (i) => t.HeightDm[i] = Math.Max(t.HeightDm[i], baseDm + f.GetProperty("height_dm").GetInt32()));
                    break;
                case "forest":
                    var density = (byte)f.GetProperty("density").GetInt32();
                    Rect(t, f, (i) => t.Forest[i] = density);
                    break;
                case "road":
                    Rect(t, f, (i) => t.Road[i] = 100);
                    break;
                case "hedge":
                    Rect(t, f, (i) => t.Hedge[i] = 100);
                    break;
                case "water":
                case "wall":
                    Rect(t, f, (i) => t.Blocked[i] = 100);
                    break;
                case "obstacle":
                    Rect(t, f, (i) => t.Obstacle[i] = 100);
                    break;
                case "trench":
                    Rect(t, f, (i) => t.Fort[i] = 100);
                    break;
                case "minefield":
                    var mineDensity = (byte)f.GetProperty("density").GetInt32();
                    var mineTeam = (byte)(f.GetProperty("team").GetInt32() + 1);
                    Rect(t, f, (i) =>
                    {
                        t.MineDensity[i] = mineDensity;
                        t.MineTeam[i] = mineTeam;
                    });
                    break;
                case "crater":
                case "smoke":
                case "building":
                case "ammo_dump":
                    break;
                default:
                    throw new InvalidDataException($"Unknown terrain feature {f.GetProperty("type").GetString()}.");
            }
        }

        return t;
    }

    /// <summary>Records a crater at (x, y) in the ring of rules.crater_max (oldest overwritten).</summary>
    public static void AddCrater(WorldState w, BattleRules r, int x, int y)
    {
        if (r.CraterMax <= 0)
        {
            return;
        }

        if (w.CraterX.Length < r.CraterMax)
        {
            Array.Resize(ref w.CraterX, r.CraterMax);
            Array.Resize(ref w.CraterY, r.CraterMax);
        }

        var c = w.CraterCount % r.CraterMax;
        w.CraterX[c] = x;
        w.CraterY[c] = y;
        w.CraterCount++;
    }

    /// <summary>MEC-04 scenario craters: {"type":"crater","x","y"} features become cover craters.</summary>
    public static void PlaceCraters(WorldState w, BattleRules r, JsonElement spec)
    {
        if (!spec.TryGetProperty("features", out var features) || r.CraterMax <= 0)
        {
            return;
        }

        foreach (var f in features.EnumerateArray())
        {
            if (f.GetProperty("type").GetString() != "crater")
            {
                continue;
            }

            if (w.CraterX.Length < r.CraterMax)
            {
                Array.Resize(ref w.CraterX, r.CraterMax);
                Array.Resize(ref w.CraterY, r.CraterMax);
            }

            var c = w.CraterCount % r.CraterMax;
            w.CraterX[c] = f.GetProperty("x").GetInt32();
            w.CraterY[c] = f.GetProperty("y").GetInt32();
            w.CraterCount++;
        }
    }

    /// <summary>SUP-03 scenario ammunition dumps: {"type":"ammo_dump","x","y","team","supply","hp"}.</summary>
    public static void PlaceDumps(WorldState w, JsonElement spec)
    {
        if (!spec.TryGetProperty("features", out var features))
        {
            return;
        }

        var dumps = new List<AmmoDump>();
        foreach (var f in features.EnumerateArray())
        {
            if (f.GetProperty("type").GetString() != "ammo_dump")
            {
                continue;
            }

            dumps.Add(new AmmoDump
            {
                X = f.GetProperty("x").GetInt32(),
                Y = f.GetProperty("y").GetInt32(),
                Team = f.GetProperty("team").GetInt32(),
                SupplyLeft = f.GetProperty("supply").GetInt32(),
                Hp = f.TryGetProperty("hp", out var hp) ? hp.GetInt32() : 200,
            });
        }

        w.Dumps = dumps.ToArray();
    }

    public static void PlaceSmoke(WorldState w, JsonElement spec)
    {
        if (!spec.TryGetProperty("features", out var features))
        {
            return;
        }

        var slot = 0;
        foreach (var f in features.EnumerateArray())
        {
            if (f.GetProperty("type").GetString() != "smoke")
            {
                continue;
            }

            w.Smoke[slot++] = new SmokeCloud
            {
                Active = 1,
                X = Fp.FromInt(f.GetProperty("x").GetInt32()),
                Y = Fp.FromInt(f.GetProperty("y").GetInt32()),
                RadiusM = f.GetProperty("radius_m").GetInt32(),
                StartTick = f.TryGetProperty("from_tick", out var a) ? a.GetInt32() : 0,
                ExpiresTick = f.TryGetProperty("to_tick", out var z) ? z.GetInt32() : int.MaxValue,
            };
        }
    }

    static void Hill(TerrainGrid t, int x, int y, int radius, int peakDm)
    {
        var r2 = (long)radius * radius;
        for (var cy = 0; cy < t.Cells; cy++)
        {
            for (var cx = 0; cx < t.Cells; cx++)
            {
                long dx = (cx * t.CellM) + (t.CellM / 2) - x;
                long dy = (cy * t.CellM) + (t.CellM / 2) - y;
                var d2 = (dx * dx) + (dy * dy);
                if (d2 >= r2)
                {
                    continue;
                }

                // Smooth dome: peak * (1 - d^2/r^2).
                var h = (int)(peakDm - ((peakDm - t.HeightDm[t.Index(cx, cy)]) * d2 / r2));
                var i = t.Index(cx, cy);
                t.HeightDm[i] = Math.Max(t.HeightDm[i], h);
            }
        }
    }

    /// <summary>ERA-03 real ground: {"type":"heightfield","n","spacing_m","dm_b64"}, sampled bilinearly in integers at
    /// each cell centre (sample (i, j) sits at x = i * spacing, y = j * spacing). Combines by maximum like hills.</summary>
    static void Heightfield(TerrainGrid t, JsonElement f, int baseDm)
    {
        var n = f.GetProperty("n").GetInt32();
        var spacing = f.GetProperty("spacing_m").GetInt32();
        var raw = Convert.FromBase64String(f.GetProperty("dm_b64").GetString()!);
        if (n < 2 || spacing <= 0 || raw.Length != n * n * 2)
        {
            throw new InvalidDataException("heightfield is not n x n uint16");
        }

        int S(int i, int j) => raw[2 * ((j * n) + i)] | (raw[(2 * ((j * n) + i)) + 1] << 8);
        for (var cy = 0; cy < t.Cells; cy++)
        {
            long py = Math.Clamp(((long)cy * t.CellM) + (t.CellM / 2), 0, (long)(n - 1) * spacing);
            var j0 = (int)Math.Min(n - 2, py / spacing);
            var fy = py - ((long)j0 * spacing);
            for (var cx = 0; cx < t.Cells; cx++)
            {
                long px = Math.Clamp(((long)cx * t.CellM) + (t.CellM / 2), 0, (long)(n - 1) * spacing);
                var i0 = (int)Math.Min(n - 2, px / spacing);
                var fx = px - ((long)i0 * spacing);
                var top = (S(i0, j0 + 1) * (spacing - fx)) + (S(i0 + 1, j0 + 1) * fx);
                var bottom = (S(i0, j0) * (spacing - fx)) + (S(i0 + 1, j0) * fx);
                var h = (int)(((bottom * (spacing - fy)) + (top * fy)) / ((long)spacing * spacing));
                var idx = t.Index(cx, cy);
                t.HeightDm[idx] = Math.Max(t.HeightDm[idx], baseDm + h);
            }
        }
    }

    static void Ridge(TerrainGrid t, JsonElement f, int baseDm)
    {
        long x0 = f.GetProperty("x0").GetInt32(), y0 = f.GetProperty("y0").GetInt32();
        long x1 = f.GetProperty("x1").GetInt32(), y1 = f.GetProperty("y1").GetInt32();
        var half = f.GetProperty("width_m").GetInt32() / 2;
        var peak = baseDm + f.GetProperty("height_dm").GetInt32();
        var lx = x1 - x0;
        var ly = y1 - y0;
        var len2 = Math.Max(1, (lx * lx) + (ly * ly));
        for (var cy = 0; cy < t.Cells; cy++)
        {
            for (var cx = 0; cx < t.Cells; cx++)
            {
                long px = (cx * t.CellM) + (t.CellM / 2), py = (cy * t.CellM) + (t.CellM / 2);
                var proj = Math.Clamp((((px - x0) * lx) + ((py - y0) * ly)) * 1024 / len2, 0, 1024);
                var qx = x0 + (lx * proj / 1024);
                var qy = y0 + (ly * proj / 1024);
                var d = Sight.IntSqrt(((px - qx) * (px - qx)) + ((py - qy) * (py - qy)));
                if (d >= half)
                {
                    continue;
                }

                var h = peak - ((peak - baseDm) * d / half);
                var i = t.Index(cx, cy);
                t.HeightDm[i] = Math.Max(t.HeightDm[i], h);
            }
        }
    }

    static void Rect(TerrainGrid t, JsonElement f, Action<int> apply)
    {
        var x = f.GetProperty("x").GetInt32();
        var y = f.GetProperty("y").GetInt32();
        var w = f.GetProperty("w").GetInt32();
        var h = f.GetProperty("h").GetInt32();
        for (var cy = Math.Max(0, y / t.CellM); cy < Math.Min(t.Cells, (y + h + t.CellM - 1) / t.CellM); cy++)
        {
            for (var cx = Math.Max(0, x / t.CellM); cx < Math.Min(t.Cells, (x + w + t.CellM - 1) / t.CellM); cx++)
            {
                apply(t.Index(cx, cy));
            }
        }
    }
}
