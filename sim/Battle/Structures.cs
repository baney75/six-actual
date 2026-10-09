using System.Text.Json;
using SixActual.Sim.State.Battle;

namespace SixActual.Sim.Battle;

/// <summary>
/// SIM-10 structural destruction. Buildings are chunk grids (chunk_m wide, floor_dm tall). A chunk above
/// ground stays up while the chunk under it exists, or a same-floor neighbour that is itself standing on
/// something (one-chunk cantilever). Unsupported chunks fall after collapse_delay_ticks, crush units under
/// them and leave rubble that raises the ground (LOS) and changes movement.
/// </summary>
public static class Structures
{
    public static void Place(WorldState w, BattleRules r, JsonElement terrainSpec)
    {
        if (!terrainSpec.TryGetProperty("features", out var features))
        {
            return;
        }

        var list = new List<Building>();
        var hp = new List<int>();
        foreach (var f in features.EnumerateArray())
        {
            if (f.GetProperty("type").GetString() != "building")
            {
                continue;
            }

            var b = new Building
            {
                X = f.GetProperty("x").GetInt32(),
                Y = f.GetProperty("y").GetInt32(),
                Nx = f.GetProperty("nx").GetInt32(),
                Ny = f.GetProperty("ny").GetInt32(),
                Floors = f.GetProperty("floors").GetInt32(),
                Material = BattleRules.MaterialIndex(f.GetProperty("material").GetString()!),
                ChunkStart = hp.Count,
            };
            b.BaseDm = Sight.GroundDm(w.Terrain, Fp.FromInt(b.X), Fp.FromInt(b.Y));
            for (var i = 0; i < b.Nx * b.Ny * b.Floors; i++)
            {
                hp.Add(r.MaterialHp[b.Material]);
            }

            list.Add(b);
        }

        w.Buildings = list.ToArray();
        w.ChunkHp = hp.ToArray();
        w.ChunkFallTick = new int[hp.Count];
        var t = w.Terrain;
        for (var bi = 0; bi < w.Buildings.Length; bi++)
        {
            var b = w.Buildings[bi];
            ForCells(t, r, b, (cell) =>
            {
                if (t.BuildingAt[cell] != 0 && t.BuildingAt[cell] != bi + 1)
                {
                    throw new InvalidDataException($"Buildings {t.BuildingAt[cell] - 1} and {bi} share terrain cell {cell}.");
                }

                t.BuildingAt[cell] = bi + 1;
            });
            RefreshFootprint(w, r, bi);
        }

        t.Version++;
    }

    public static int ChunkIndex(in Building b, int ix, int iy, int floor) => b.ChunkStart + (((floor * b.Ny) + iy) * b.Nx) + ix;

    public static bool Exists(WorldState w, in Building b, int ix, int iy, int floor) =>
        ix >= 0 && iy >= 0 && floor >= 0 && ix < b.Nx && iy < b.Ny && floor < b.Floors && w.ChunkHp[ChunkIndex(b, ix, iy, floor)] > 0;

    /// <summary>Explosion at (x, y, zDm above local ground). Damage falls off linearly to zero at radius; material armor subtracts.</summary>
    public static void Blast(WorldState w, BattleRules r, int x, int y, int zDm, int radiusM, int damage)
    {
        var rDm = radiusM * 10L;
        for (var bi = 0; bi < w.Buildings.Length; bi++)
        {
            var b = w.Buildings[bi];
            var chunkDm = r.ChunkM * 10;
            for (var f = 0; f < b.Floors; f++)
            {
                for (var iy = 0; iy < b.Ny; iy++)
                {
                    for (var ix = 0; ix < b.Nx; ix++)
                    {
                        var c = ChunkIndex(b, ix, iy, f);
                        if (w.ChunkHp[c] <= 0)
                        {
                            continue;
                        }

                        long dx = ((b.X * 10L) + (ix * chunkDm) + (chunkDm / 2)) - (x * 10L);
                        long dy = ((b.Y * 10L) + (iy * chunkDm) + (chunkDm / 2)) - (y * 10L);
                        long dz = (f * (long)r.FloorDm) + (r.FloorDm / 2) - zDm;
                        long d = Sight.IntSqrt((dx * dx) + (dy * dy) + (dz * dz));
                        if (d >= rDm)
                        {
                            continue;
                        }

                        var dealt = (int)(damage * (rDm - d) / rDm) - r.MaterialArmor[b.Material];
                        if (dealt <= 0)
                        {
                            continue;
                        }

                        w.ChunkHp[c] -= dealt;
                        if (w.ChunkHp[c] <= 0)
                        {
                            Destroy(w, r, bi, ix, iy, f, BattleEvent.ChunkDestroyed);
                        }
                    }
                }
            }
        }
    }

    /// <summary>Per tick: drop chunks whose fall time has come, then re-check support if anything changed.</summary>
    public static void Step(WorldState w, BattleRules r)
    {
        for (var bi = 0; bi < w.Buildings.Length; bi++)
        {
            var b = w.Buildings[bi];
            for (var f = 0; f < b.Floors; f++)
            {
                for (var iy = 0; iy < b.Ny; iy++)
                {
                    for (var ix = 0; ix < b.Nx; ix++)
                    {
                        var c = ChunkIndex(b, ix, iy, f);
                        if (w.ChunkHp[c] > 0 && w.ChunkFallTick[c] != 0 && w.ChunkFallTick[c] <= w.Tick)
                        {
                            Crush(w, r, b, ix, iy, f);
                            Destroy(w, r, bi, ix, iy, f, BattleEvent.ChunkFell);
                        }
                    }
                }
            }
        }

        while (w.StructureDirty != 0)
        {
            w.StructureDirty = 0;
            MarkUnsupported(w, r);
        }
    }

    static void MarkUnsupported(WorldState w, BattleRules r)
    {
        for (var bi = 0; bi < w.Buildings.Length; bi++)
        {
            var b = w.Buildings[bi];
            for (var f = 1; f < b.Floors; f++)
            {
                for (var iy = 0; iy < b.Ny; iy++)
                {
                    for (var ix = 0; ix < b.Nx; ix++)
                    {
                        var c = ChunkIndex(b, ix, iy, f);
                        if (w.ChunkHp[c] <= 0 || w.ChunkFallTick[c] != 0 || Supported(w, b, ix, iy, f))
                        {
                            continue;
                        }

                        w.ChunkFallTick[c] = w.Tick + r.CollapseDelayTicks;
                    }
                }
            }
        }
    }

    static bool Supported(WorldState w, in Building b, int ix, int iy, int f)
    {
        if (Standing(w, b, ix, iy, f - 1))
        {
            return true;
        }

        return Standing(w, b, ix + 1, iy, f - 1) && Standing(w, b, ix + 1, iy, f)
            || Standing(w, b, ix - 1, iy, f - 1) && Standing(w, b, ix - 1, iy, f)
            || Standing(w, b, ix, iy + 1, f - 1) && Standing(w, b, ix, iy + 1, f)
            || Standing(w, b, ix, iy - 1, f - 1) && Standing(w, b, ix, iy - 1, f);
    }

    /// <summary>A chunk that exists and is not already on its way down.</summary>
    static bool Standing(WorldState w, in Building b, int ix, int iy, int f) =>
        Exists(w, b, ix, iy, f) && w.ChunkFallTick[ChunkIndex(b, ix, iy, f)] == 0;

    static void Destroy(WorldState w, BattleRules r, int bi, int ix, int iy, int f, int eventKind)
    {
        var b = w.Buildings[bi];
        var c = ChunkIndex(b, ix, iy, f);
        w.ChunkHp[c] = 0;
        w.ChunkFallTick[c] = 0;
        w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = eventKind, A = bi, B = c - b.ChunkStart });

        // Debris lands in the cell under the chunk.
        var t = w.Terrain;
        var cx = ((b.X + (ix * r.ChunkM)) + (r.ChunkM / 2)) / t.CellM;
        var cy = ((b.Y + (iy * r.ChunkM)) + (r.ChunkM / 2)) / t.CellM;
        if (t.InCell(cx, cy))
        {
            var cell = t.Index(cx, cy);
            t.RubbleDm[cell] += r.RubbleDmPerChunk;
            t.HeightDm[cell] += r.RubbleDmPerChunk;
        }

        RefreshFootprint(w, r, bi);
        t.Version++;
        w.StructureDirty = 1;
    }

    /// <summary>Units inside the falling chunk's column, at or below its floor, take fall damage.</summary>
    static void Crush(WorldState w, BattleRules r, in Building b, int ix, int iy, int f)
    {
        var x0 = b.X + (ix * r.ChunkM);
        var y0 = b.Y + (iy * r.ChunkM);
        var topDm = (f + 1) * r.FloorDm;
        for (var u = 0; u < w.Units.Length; u++)
        {
            if (w.Units[u].Alive == 0)
            {
                continue;
            }

            var ux = (int)(w.Units[u].X.Raw >> Fp.FractionalBits);
            var uy = (int)(w.Units[u].Y.Raw >> Fp.FractionalBits);
            var uz = (int)((w.Units[u].Z.Raw * 10) >> Fp.FractionalBits);
            if (ux < x0 || uy < y0 || ux >= x0 + r.ChunkM || uy >= y0 + r.ChunkM || uz >= topDm)
            {
                continue;
            }

            w.Units[u].Hp -= r.FallDamage;
            w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.CrushDamage, A = w.Units[u].Id, Value = r.FallDamage });
            if (w.Units[u].Hp <= 0)
            {
                w.Units[u].Alive = 0;
                w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Killed, A = -1, B = w.Units[u].Id });
            }
        }
    }

    /// <summary>A terrain cell is blocked to ground movement while any ground-floor chunk of the building stands in it.</summary>
    static void RefreshFootprint(WorldState w, BattleRules r, int bi)
    {
        var t = w.Terrain;
        var b = w.Buildings[bi];
        ForCells(t, r, b, (cell) => t.Blocked[cell] = 0);
        for (var iy = 0; iy < b.Ny; iy++)
        {
            for (var ix = 0; ix < b.Nx; ix++)
            {
                if (!Exists(w, b, ix, iy, 0))
                {
                    continue;
                }

                var cx = (b.X + (ix * r.ChunkM) + (r.ChunkM / 2)) / t.CellM;
                var cy = (b.Y + (iy * r.ChunkM) + (r.ChunkM / 2)) / t.CellM;
                if (t.InCell(cx, cy))
                {
                    t.Blocked[t.Index(cx, cy)] = 100;
                }
            }
        }
    }

    static void ForCells(TerrainGrid t, BattleRules r, in Building b, Action<int> visit)
    {
        var x1 = b.X + (b.Nx * r.ChunkM) - 1;
        var y1 = b.Y + (b.Ny * r.ChunkM) - 1;
        for (var cy = b.Y / t.CellM; cy <= y1 / t.CellM; cy++)
        {
            for (var cx = b.X / t.CellM; cx <= x1 / t.CellM; cx++)
            {
                if (t.InCell(cx, cy))
                {
                    visit(t.Index(cx, cy));
                }
            }
        }
    }

    /// <summary>
    /// True when a standing chunk blocks the segment (x0,y0,z0)-(x1,y1,z1) (metres raw Fp, heights in dm).
    /// Walks every terrain cell the segment crosses (grid traversal), then marches 1 m steps through each building it meets.
    /// The first and last chunk_m metres are ignored so a unit inside a building can see out of its own window.
    /// </summary>
    /// <summary>Conservative prefilter for <see cref="Blocks"/> (G6, D072): true unless the segment clearly misses every
    /// building's footprint padded by two terrain cells (bounding-box overlap, then the line's distance to the
    /// footprint centre against a radius covering the padded footprint). A false here means the cell walk could not
    /// meet a building cell either, so skipping it changes no result.</summary>
    static bool NearAnyBuilding(WorldState w, BattleRules r, Fp x0, Fp y0, Fp x1, Fp y1)
    {
        long px = x0.Raw >> Fp.FractionalBits, py = y0.Raw >> Fp.FractionalBits;
        long qx = x1.Raw >> Fp.FractionalBits, qy = y1.Raw >> Fp.FractionalBits;
        long ddx = qx - px, ddy = qy - py;
        var len2 = (ddx * ddx) + (ddy * ddy);
        long pad = 2L * w.Terrain.CellM;
        long minX = Math.Min(px, qx), maxX = Math.Max(px, qx), minY = Math.Min(py, qy), maxY = Math.Max(py, qy);
        foreach (var b in w.Buildings)
        {
            long bw = (long)b.Nx * r.ChunkM, bh = (long)b.Ny * r.ChunkM;
            if (maxX < b.X - pad || minX > b.X + bw + pad || maxY < b.Y - pad || minY > b.Y + bh + pad)
            {
                continue;
            }

            long cxm = b.X + (bw / 2), cym = b.Y + (bh / 2);
            var radius = ((bw + bh) / 2) + pad + 1; // >= half the padded diagonal
            var cross = (ddx * (cym - py)) - (ddy * (cxm - px));
            if (len2 == 0 || cross * cross <= radius * radius * len2)
            {
                return true;
            }
        }

        return false;
    }

    public static bool Blocks(WorldState w, BattleRules r, Fp x0, Fp y0, long z0, Fp x1, Fp y1, long z1)
    {
        if (w.Buildings.Length == 0)
        {
            return false;
        }

        var t = w.Terrain;
        long dx = x1.Raw - x0.Raw;
        long dy = y1.Raw - y0.Raw;
        long len = Sight.IntSqrt((dx * dx) + (dy * dy)) >> Fp.FractionalBits;
        if (len <= 0)
        {
            return false;
        }

        if (!NearAnyBuilding(w, r, x0, y0, x1, y1))
        {
            return false;
        }

        // Amanatides-Woo traversal: visit every terrain cell the segment crosses, each exactly once.
        var cell = (long)t.CellM << Fp.FractionalBits;
        var cx = (int)(x0.Raw / cell);
        var cy = (int)(y0.Raw / cell);
        var ex = (int)(x1.Raw / cell);
        var ey = (int)(y1.Raw / cell);
        var stepX = dx > 0 ? 1 : dx < 0 ? -1 : 0;
        var stepY = dy > 0 ? 1 : dy < 0 ? -1 : 0;

        // Parametric distances in units of 1/2^20 of the segment, integer-only.
        const long One = 1L << 20;
        long adx = Math.Abs(dx), ady = Math.Abs(dy);
        long tDeltaX = adx == 0 ? long.MaxValue : cell * One / adx;
        long tDeltaY = ady == 0 ? long.MaxValue : cell * One / ady;
        long nextBx = stepX > 0 ? ((cx + 1) * cell) - x0.Raw : x0.Raw - (cx * cell);
        long nextBy = stepY > 0 ? ((cy + 1) * cell) - y0.Raw : y0.Raw - (cy * cell);
        long tMaxX = adx == 0 ? long.MaxValue : nextBx * One / adx;
        long tMaxY = ady == 0 ? long.MaxValue : nextBy * One / ady;
        var tested = 0UL;
        var guard = Math.Abs(ex - cx) + Math.Abs(ey - cy) + 2;
        for (var visit = 0; visit <= guard; visit++)
        {
            if (t.InCell(cx, cy))
            {
                var bi = t.BuildingAt[t.Index(cx, cy)] - 1;
                if (bi >= 0)
                {
                    var bit = 1UL << (bi & 63);
                    if ((tested & bit) == 0)
                    {
                        tested |= bit;
                        if (MarchBuilding(w, r, bi, x0, y0, z0, dx, dy, z1 - z0, len))
                        {
                            return true;
                        }
                    }
                }
            }

            if (cx == ex && cy == ey)
            {
                break;
            }

            if (tMaxX < tMaxY)
            {
                cx += stepX;
                tMaxX += tDeltaX;
            }
            else
            {
                cy += stepY;
                tMaxY += tDeltaY;
            }
        }

        return false;
    }

    static bool MarchBuilding(WorldState w, BattleRules r, int bi, Fp x0, Fp y0, long z0, long dx, long dy, long dz, long len)
    {
        var b = w.Buildings[bi];
        for (var m = r.ChunkM; m <= len - r.ChunkM; m++)
        {
            var x = (int)((x0.Raw + (dx * m / len)) >> Fp.FractionalBits);
            var y = (int)((y0.Raw + (dy * m / len)) >> Fp.FractionalBits);
            var ix = (x - b.X) < 0 ? -1 : (x - b.X) / r.ChunkM;
            var iy = (y - b.Y) < 0 ? -1 : (y - b.Y) / r.ChunkM;
            if (ix < 0 || iy < 0 || ix >= b.Nx || iy >= b.Ny)
            {
                continue;
            }

            var z = z0 + (dz * m / len) - b.BaseDm;
            if (z < 0)
            {
                return true;
            }

            var floor = (int)(z / r.FloorDm);
            if (floor < b.Floors && Exists(w, b, ix, iy, floor))
            {
                return true;
            }
        }

        return false;
    }
}
