using SixActual.Sim.State.Battle;

namespace SixActual.Sim.Battle;

/// <summary>
/// MEC-02 dynamic frontline. Team influence fields (linear falloff to radius_m) decide territory; a cell only
/// flips when the other team's influence beats the owner's by flip_margin, so ground stays held. Team 0's rear
/// is the south edge (y = 0), team 1's the north edge. A unit whose cell cannot reach its rear edge through
/// cells its team owns is encircled. Encircled units, and units in enemy territory, cannot shed suppression;
/// encircled units cannot be resupplied.
/// </summary>
public static class Frontline
{
    public static int TeamOf(WorldState w, int player) =>
        player >= 0 && player < w.Players.Length ? w.Players[player].Team : player & 1;

    public static void Init(WorldState w, BattleRules r)
    {
        var n = Math.Max(1, w.Terrain.SizeM / r.FrontCellM);
        w.FrontCells = n;
        w.Territory = new sbyte[n * n];
        for (var y = 0; y < n; y++)
        {
            for (var x = 0; x < n; x++)
            {
                // Start split down the middle: team 0 owns the southern half.
                w.Territory[(y * n) + x] = (sbyte)(y < n / 2 ? 0 : 1);
            }
        }

        w.Encircled = new byte[w.Units.Length];
    }

    public static void Step(WorldState w, BattleRules r)
    {
        if (w.Territory.Length == 0)
        {
            Init(w, r);
        }

        if (w.Encircled.Length != w.Units.Length)
        {
            Array.Resize(ref w.Encircled, w.Units.Length);
        }

        if (w.Tick % r.FrontUpdateTicks != 0)
        {
            return;
        }

        var n = w.FrontCells;
        var inf0 = new int[n * n];
        var inf1 = new int[n * n];
        var reach = r.FrontRadiusM / r.FrontCellM;
        for (var i = 0; i < w.Units.Length; i++)
        {
            ref readonly var u = ref w.Units[i];
            var d = w.Defs[u.Def];
            var weight = d.FrontWeight >= 0 ? d.FrontWeight : r.FrontWeightByKind[Math.Clamp(d.Kind, 0, r.FrontWeightByKind.Length - 1)];
            if (u.Alive == 0 || weight <= 0 || u.MoraleState >= Morale.Routed || u.Z.Raw > Fp.FromInt(r.AirAltitudeM).Raw)
            {
                continue;
            }

            var field = TeamOf(w, u.Player) == 0 ? inf0 : inf1;
            var ux = (int)(u.X.Raw >> Fp.FractionalBits);
            var uy = (int)(u.Y.Raw >> Fp.FractionalBits);
            var cx = ux / r.FrontCellM;
            var cy = uy / r.FrontCellM;
            for (var y = Math.Max(0, cy - reach); y <= Math.Min(n - 1, cy + reach); y++)
            {
                for (var x = Math.Max(0, cx - reach); x <= Math.Min(n - 1, cx + reach); x++)
                {
                    long dx = (x * r.FrontCellM) + (r.FrontCellM / 2) - ux;
                    long dy = (y * r.FrontCellM) + (r.FrontCellM / 2) - uy;
                    var dist = Sight.IntSqrt((dx * dx) + (dy * dy));
                    if (dist >= r.FrontRadiusM)
                    {
                        continue;
                    }

                    // Weight scaled by 100 at the unit, fading to 0 at the radius.
                    field[(y * n) + x] += weight * 100 * (r.FrontRadiusM - dist) / r.FrontRadiusM;
                }
            }
        }

        var margin = r.FrontFlipMargin;
        for (var c = 0; c < n * n; c++)
        {
            var owner = w.Territory[c];
            if (owner != 0 && inf0[c] > inf1[c] + margin)
            {
                w.Territory[c] = 0;
            }
            else if (owner != 1 && inf1[c] > inf0[c] + margin)
            {
                w.Territory[c] = 1;
            }
        }

        var connected0 = Connected(w, 0);
        var connected1 = Connected(w, 1);
        for (var i = 0; i < w.Units.Length; i++)
        {
            var c = CellOf(w, r, i);
            var team = TeamOf(w, w.Units[i].Player);
            w.Encircled[i] = (byte)((team == 0 ? connected0[c] : connected1[c]) ? 0 : 1);
        }
    }

    public static int CellOf(WorldState w, BattleRules r, int unit)
    {
        var n = w.FrontCells;
        var x = Math.Clamp((int)(w.Units[unit].X.Raw >> Fp.FractionalBits) / r.FrontCellM, 0, n - 1);
        var y = Math.Clamp((int)(w.Units[unit].Y.Raw >> Fp.FractionalBits) / r.FrontCellM, 0, n - 1);
        return (y * n) + x;
    }

    public static bool InFriendlyTerritory(WorldState w, BattleRules r, int unit) =>
        w.Territory.Length == 0 || w.Territory[CellOf(w, r, unit)] == TeamOf(w, w.Units[unit].Player);

    public static bool IsEncircled(WorldState w, int unit) => unit < w.Encircled.Length && w.Encircled[unit] != 0;

    /// <summary>Cells owned by team that connect (4-neighbour) to that team's rear edge.</summary>
    static bool[] Connected(WorldState w, int team)
    {
        var n = w.FrontCells;
        var seen = new bool[n * n];
        var queue = new Queue<int>();
        var edgeY = team == 0 ? 0 : n - 1;
        for (var x = 0; x < n; x++)
        {
            var c = (edgeY * n) + x;
            if (w.Territory[c] == team)
            {
                seen[c] = true;
                queue.Enqueue(c);
            }
        }

        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            var cx = c % n;
            var cy = c / n;
            Visit(cx + 1, cy);
            Visit(cx - 1, cy);
            Visit(cx, cy + 1);
            Visit(cx, cy - 1);
        }

        return seen;

        void Visit(int x, int y)
        {
            if (x < 0 || y < 0 || x >= n || y >= n)
            {
                return;
            }

            var c = (y * n) + x;
            if (!seen[c] && w.Territory[c] == team)
            {
                seen[c] = true;
                queue.Enqueue(c);
            }
        }
    }
}
