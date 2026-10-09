using System.Text.Json;
using SixActual.Sim.State.Battle;

namespace SixActual.Sim.Battle;

/// <summary>One card of the campaign operation event deck (data/operation_events.json, MEC-07).</summary>
public sealed class OperationEventDef
{
    public const int EffectWeather = 1;

    public const int EffectDpSide = 2;

    public const int EffectResupply = 3;

    public string Id { get; init; } = "";

    public int Effect { get; init; }

    public int Weather { get; init; } = -1;

    public string Side { get; init; } = "";

    public int Dp { get; init; }

    public int Weight { get; init; } = 1;

    public static OperationEventDef[] Load(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
        string[] effects = ["", "weather", "dp_side", "resupply"];
        return doc.RootElement.GetProperty("cards").EnumerateArray().Select(c =>
        {
            var p = c.GetProperty("params");
            return new OperationEventDef
            {
                Id = c.GetProperty("id").GetString()!,
                Effect = Array.IndexOf(effects, c.GetProperty("effect").GetString()!),
                Weather = p.TryGetProperty("weather", out var wx) ? BattleRules.WeatherIndex(wx.GetString()!) : -1,
                Side = p.TryGetProperty("side", out var sd) ? sd.GetString()! : "",
                Dp = p.TryGetProperty("dp", out var dp) ? dp.GetInt32() : 0,
                Weight = c.GetProperty("weight").GetInt32(),
            };
        }).ToArray();
    }
}

/// <summary>
/// MEC-07 / MEC-12 multi-phase campaign operations. The phases of an operation are fought on one persistent world:
/// wrecks, craters, rubble, fortifications, obstacles and minefields simply stay. Between phases Advance:
///   1. moves the clock one step (dawn, day, dusk, night, then dawn again);
///   2. hands every objective to the phase winner and pulls the loser's entry point back spawn_pullback_m toward
///      its map edge (MEC-12: two outcomes give two different map states);
///   3. draws one event card (weighted, battle RNG) and applies it;
///   4. restarts scoring, the time cap and the economy phases for the next phase.
/// </summary>
public static class Operation
{
    public static void Advance(WorldState w, BattleRules r, int winnerTeam)
    {
        w.OperationPhase++;
        w.TimeOfDay = (w.TimeOfDay + 1) % Math.Max(1, r.TodOpticsPct.Length);

        if (winnerTeam is 0 or 1)
        {
            for (var k = 0; k < w.Objectives.Length; k++)
            {
                w.Objectives[k].Owner = winnerTeam;
            }

            var edge = w.Terrain.SizeM;
            for (var p = 0; p < w.Players.Length; p++)
            {
                if (Frontline.TeamOf(w, p) == winnerTeam)
                {
                    continue;
                }

                ref var pl = ref w.Players[p];
                // Fall back toward the nearer map edge in y (entry points sit near the north or south edge).
                pl.SpawnY = pl.SpawnY < edge / 2 ? Math.Max(50, pl.SpawnY - r.OpSpawnPullbackM) : Math.Min(edge - 50, pl.SpawnY + r.OpSpawnPullbackM);
            }
        }

        var card = Draw(w);
        if (card >= 0)
        {
            Apply(w, r, w.EventDefs[card]);
            w.OperationEvents.Add(card);
        }

        w.Score[0] = 0;
        w.Score[1] = 0;
        w.ScoreMilli[0] = 0;
        w.ScoreMilli[1] = 0;
        w.Winner = -1;
        w.OperationStartTick = w.Tick;
        w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.OperationAdvanced, A = w.OperationPhase, B = card, C = winnerTeam, Value = w.TimeOfDay });
    }

    static int Draw(WorldState w)
    {
        var total = w.EventDefs.Sum(e => e.Weight);
        if (total <= 0)
        {
            return -1;
        }

        var roll = (int)(BattleWorld.NextRandom(w) % (ulong)total);
        for (var k = 0; k < w.EventDefs.Length; k++)
        {
            roll -= w.EventDefs[k].Weight;
            if (roll < 0)
            {
                return k;
            }
        }

        return w.EventDefs.Length - 1;
    }

    static void Apply(WorldState w, BattleRules r, OperationEventDef e)
    {
        switch (e.Effect)
        {
            case OperationEventDef.EffectWeather when e.Weather >= 0:
                w.Weather = e.Weather;
                w.AirGrounded = r.AirOps(e.Weather) ? 0 : 1;
                break;
            case OperationEventDef.EffectDpSide:
                for (var p = 0; p < w.Players.Length; p++)
                {
                    if (string.Equals(Support.PlayerSide(w, p), e.Side, StringComparison.Ordinal))
                    {
                        w.Players[p].Dp += e.Dp;
                    }
                }

                break;
            case OperationEventDef.EffectResupply:
                for (var i = 0; i < w.Units.Length; i++)
                {
                    ref var u = ref w.Units[i];
                    if (u.Alive == 0)
                    {
                        continue;
                    }

                    var d = w.Defs[u.Def];
                    for (var m = 0; m < d.Weapons.Length; m++)
                    {
                        w.MountAmmo[u.MountStart + m] = d.Weapons[m].Ammo;
                    }

                    u.Fuel = d.Fuel;
                }

                break;
        }
    }
}
