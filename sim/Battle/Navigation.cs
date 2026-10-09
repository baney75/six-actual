using SixActual.Sim.State.Battle;

namespace SixActual.Sim.Battle;

/// <summary>SIM-12: coarse nav grid per movement class plus deterministic A*.</summary>
public static class Navigation
{
    public const int Tracked = 0;

    public const int Wheeled = 1;

    public const int Infantry = 2;

    public const int None = -1;

    /// <summary>Movement class of a Phase 0 placeholder unit (the roster states it explicitly).</summary>
    public static int ClassOf(in UnitDef def) => def.Kind switch
    {
        UnitDef.KindInfantry => Infantry,
        UnitDef.KindSupply => Wheeled,
        UnitDef.KindAir => None,
        _ => Tracked,
    };

    /// <summary>Surface of a terrain cell: 0 open, 1 road, 2 forest, 3 rubble.</summary>
    public static int SurfaceAt(TerrainGrid t, Fp x, Fp y)
    {
        var cx = t.CellOf(x);
        var cy = t.CellOf(y);
        if (!t.InCell(cx, cy))
        {
            return 0;
        }

        var i = t.Index(cx, cy);
        return t.RubbleDm.Length > 0 && t.RubbleDm[i] > 0 ? 3 : t.Road[i] > 0 ? 1 : t.Forest[i] > 50 || t.Hedge[i] > 0 ? 2 : 0;
    }

    public static void EnsureGrid(WorldState w, BattleRules r)
    {
        if (w.NavVersion == w.Terrain.Version && w.NavCost.Length == 3)
        {
            return;
        }

        var t = w.Terrain;
        var navM = r.NavCellM;
        var n = t.SizeM / navM;
        var per = navM / t.CellM;
        w.NavCells = n;
        w.NavCellM = navM;
        w.NavCost = new int[3][];
        for (var mc = 0; mc < 3; mc++)
        {
            var cost = new int[n * n];
            for (var ny = 0; ny < n; ny++)
            {
                for (var nx = 0; nx < n; nx++)
                {
                    var blocked = 0;
                    var obstacle = 0;
                    var road = 0;
                    var forest = 0;
                    var rubble = 0;
                    var lo = int.MaxValue;
                    var hi = int.MinValue;
                    for (var sy = 0; sy < per; sy++)
                    {
                        for (var sx = 0; sx < per; sx++)
                        {
                            var i = t.Index((nx * per) + sx, (ny * per) + sy);
                            blocked += t.Blocked[i] > 0 ? 1 : 0;
                            obstacle += t.Obstacle.Length > i && t.Obstacle[i] > 0 ? 1 : 0;
                            road += t.Road[i] > 0 ? 1 : 0;
                            forest += t.Forest[i] > 50 || t.Hedge[i] > 0 ? 1 : 0;
                            rubble += t.RubbleDm[i] > 0 ? 1 : 0;

                            // Slope ignores rubble: rubble cost is handled as its own surface.
                            var ground = t.HeightDm[i] - t.RubbleDm[i];
                            lo = Math.Min(lo, ground);
                            hi = Math.Max(hi, ground);
                        }
                    }

                    var cells = per * per;

                    // Slope as percent: rise (dm) over the distance between the outermost sample centres (m * 10).
                    var span = Math.Max(1, (per - 1) * t.CellM);
                    var slopePct = (hi - lo) * 100 / (span * 10);
                    // SUP-03 obstacles stop vehicles (tracked 0, wheeled 1); infantry (2) cross them.
                    if (blocked * 2 > cells || slopePct > r.MaxSlopePct[mc] || (mc != 2 && obstacle * 2 > cells))
                    {
                        cost[(ny * n) + nx] = 0;
                        continue;
                    }

                    var surface = rubble > 0 ? 3 : road > 0 ? 1 : forest * 2 > cells ? 2 : 0;
                    cost[(ny * n) + nx] = r.CostPct[mc][surface];
                }
            }

            w.NavCost[mc] = cost;
        }

        BuildCoarse(w);
        w.NavVersion = t.Version;
    }

    /// <summary>
    /// Coarse grid cost: the cheapest passable fine cost in the block (optimistic, so a road through the block
    /// is never undervalued), 0 when less than a quarter of the block is passable.
    /// </summary>
    static void BuildCoarse(WorldState w)
    {
        var n = w.NavCells;
        var cn = (n + CoarseFactor - 1) / CoarseFactor;
        w.NavCoarse = new int[3][];
        for (var mc = 0; mc < 3; mc++)
        {
            var fine = w.NavCost[mc];
            var coarse = new int[cn * cn];
            for (var cy = 0; cy < cn; cy++)
            {
                for (var cx = 0; cx < cn; cx++)
                {
                    var cheapest = int.MaxValue;
                    var passable = 0;
                    var total = 0;
                    for (var fy = cy * CoarseFactor; fy < Math.Min(n, (cy + 1) * CoarseFactor); fy++)
                    {
                        for (var fx = cx * CoarseFactor; fx < Math.Min(n, (cx + 1) * CoarseFactor); fx++)
                        {
                            total++;
                            var c = fine[(fy * n) + fx];
                            if (c > 0)
                            {
                                passable++;
                                cheapest = Math.Min(cheapest, c);
                            }
                        }
                    }

                    coarse[(cy * cn) + cx] = passable * 4 >= total ? cheapest : 0;
                }
            }

            w.NavCoarse[mc] = coarse;
        }
    }

    public static int NavIndex(WorldState w, Fp x, Fp y)
    {
        var nx = (int)(x.Raw >> Fp.FractionalBits) / w.NavCellM;
        var ny = (int)(y.Raw >> Fp.FractionalBits) / w.NavCellM;
        nx = Math.Clamp(nx, 0, w.NavCells - 1);
        ny = Math.Clamp(ny, 0, w.NavCells - 1);
        return (ny * w.NavCells) + nx;
    }

    /// <summary>Coarse cell is CoarseFactor x CoarseFactor nav cells.</summary>
    public const int CoarseFactor = 4;

    /// <summary>
    /// Two-level A*: a coarse search on CoarseFactor-sized cells picks the route, then the exact search runs
    /// inside a corridor one coarse cell either side of it. Falls back to the full exact search when the
    /// corridor cannot connect (coarse costs are averages and can misjudge narrow gaps).
    /// Returns nav cell indices from the cell after start to goal, or null.
    /// </summary>
    public static int[]? FindPath(WorldState w, int moveClass, int start, int goal)
    {
        var cost = w.NavCost[moveClass];
        if (cost[goal] == 0)
        {
            goal = NearestPassable(w, cost, goal);
            if (goal < 0)
            {
                return null;
            }
        }

        var n = w.NavCells;
        var cn = (n + CoarseFactor - 1) / CoarseFactor;
        var coarse = w.NavCoarse[moveClass];
        var cs = ((start / n) / CoarseFactor * cn) + ((start % n) / CoarseFactor);
        var cg = ((goal / n) / CoarseFactor * cn) + ((goal % n) / CoarseFactor);
        var route = cs == cg ? [] : Search(coarse, cn, cs, cg, null, 0, CoarseScratch);
        if (route is not null)
        {
            var mask = MaskScratch.For(n * n);
            var mgen = ++mask.Generation;
            void Mark(int c)
            {
                var ccx = c % cn;
                var ccy = c / cn;
                for (var oy = -1; oy <= 1; oy++)
                {
                    for (var ox = -1; ox <= 1; ox++)
                    {
                        var x0 = (ccx + ox) * CoarseFactor;
                        var y0 = (ccy + oy) * CoarseFactor;
                        for (var fy = y0; fy < y0 + CoarseFactor; fy++)
                        {
                            for (var fx = x0; fx < x0 + CoarseFactor; fx++)
                            {
                                if (fx >= 0 && fy >= 0 && fx < n && fy < n)
                                {
                                    mask.Stamp[(fy * n) + fx] = mgen;
                                }
                            }
                        }
                    }
                }
            }

            Mark(cs);
            foreach (var c in route)
            {
                Mark(c);
            }

            var inCorridor = Search(cost, n, start, goal, mask.Stamp, mgen, FineScratch);
            if (inCorridor is not null)
            {
                return inCorridor;
            }
        }

        return Search(cost, n, start, goal, null, 0, FineScratch);
    }

    static readonly ThreadLocal<Scratch> CoarseScratchHolder = new(() => new Scratch());

    static readonly ThreadLocal<Scratch> FineScratchHolder = new(() => new Scratch());

    static readonly ThreadLocal<Scratch> MaskScratchHolder = new(() => new Scratch());

    static Scratch CoarseScratch => CoarseScratchHolder.Value!;

    static Scratch FineScratch => FineScratchHolder.Value!;

    static Scratch MaskScratch => MaskScratchHolder.Value!;

    /// <summary>A* on a cost grid; cells outside the mask (when given) are skipped.</summary>
    static int[]? Search(int[] cost, int n, int start, int goal, int[]? mask, int maskGen, Scratch sc0)
    {
        var minCost = int.MaxValue;
        foreach (var c in cost)
        {
            if (c > 0 && c < minCost)
            {
                minCost = c;
            }
        }

        if (minCost == int.MaxValue)
        {
            return null;
        }

        var sc = sc0.Ensure(n * n);
        var gen = ++sc.Generation;
        var open = sc.Open;
        open.Clear();
        sc.Stamp[start] = gen;
        sc.G[start] = 0;
        open.Enqueue(start, Key(Heuristic(start, goal, n, minCost), start));
        while (open.TryDequeue(out var cur, out _))
        {
            if (cur == goal)
            {
                return Rebuild(sc.From, start, goal);
            }

            if (sc.Closed[cur] == gen)
            {
                continue;
            }

            sc.Closed[cur] = gen;
            var cx = cur % n;
            var cy = cur / n;
            for (var d = 0; d < 8; d++)
            {
                var nx = cx + Dx[d];
                var ny = cy + Dy[d];
                if (nx < 0 || ny < 0 || nx >= n || ny >= n)
                {
                    continue;
                }

                var ni = (ny * n) + nx;
                if (sc.Closed[ni] == gen || cost[ni] == 0 || (mask is not null && mask[ni] != maskGen))
                {
                    continue;
                }

                // No corner cutting past impassable cells.
                if (d >= 4 && (cost[(cy * n) + nx] == 0 || cost[(ny * n) + cx] == 0))
                {
                    continue;
                }

                var step = (d < 4 ? 10 : 14) * (cost[cur] + cost[ni]) / 2;
                var cand = sc.G[cur] + step;
                if (sc.Stamp[ni] == gen && cand >= sc.G[ni])
                {
                    continue;
                }

                sc.Stamp[ni] = gen;
                sc.G[ni] = cand;
                sc.From[ni] = cur;
                open.Enqueue(ni, Key(cand + Heuristic(ni, goal, n, minCost), ni));
            }
        }

        return null;
    }

    /// <summary>
    /// Search buffers reused across calls, invalidated by a generation stamp instead of clearing.
    /// Pure scratch: nothing here is sim state, and results do not depend on what was here before.
    /// </summary>
    sealed class Scratch
    {
        public int Generation;

        public int[] Stamp = [];

        public int[] Closed = [];

        public int[] G = [];

        public int[] From = [];

        public PriorityQueue<int, long> Open = new();

        public Scratch Ensure(int cells)
        {
            if (Stamp.Length != cells || Generation >= int.MaxValue - 1)
            {
                Stamp = new int[cells];
                Closed = new int[cells];
                G = new int[cells];
                From = new int[cells];
                Generation = 0;
            }

            return this;
        }

        public Scratch For(int cells) => Ensure(cells);
    }

    static readonly int[] Dx = [1, -1, 0, 0, 1, 1, -1, -1];

    static readonly int[] Dy = [0, 0, 1, -1, 1, -1, 1, -1];

    static long Key(int f, int index) => ((long)f << 24) | (uint)index;

    /// <summary>Octile distance times the class's cheapest surface cost: admissible, so paths stay optimal.</summary>
    static int Heuristic(int a, int b, int n, int minCost)
    {
        var dx = Math.Abs((a % n) - (b % n));
        var dy = Math.Abs((a / n) - (b / n));
        var oct = (10 * Math.Max(dx, dy)) + (4 * Math.Min(dx, dy));
        return oct * minCost;
    }

    static int[] Rebuild(int[] from, int start, int goal)
    {
        var path = new List<int>();
        for (var c = goal; c != start; c = from[c])
        {
            path.Add(c);
        }

        path.Reverse();
        return path.ToArray();
    }

    static int NearestPassable(WorldState w, int[] cost, int cell)
    {
        var n = w.NavCells;
        var cx = cell % n;
        var cy = cell / n;
        for (var radius = 1; radius < 8; radius++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    var x = cx + dx;
                    var y = cy + dy;
                    if (x >= 0 && y >= 0 && x < n && y < n && cost[(y * n) + x] > 0)
                    {
                        return (y * n) + x;
                    }
                }
            }
        }

        return -1;
    }
}
