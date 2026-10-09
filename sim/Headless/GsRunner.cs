using System.Globalization;
using System.Text.Json;
using SixActual.Sim;
using SixActual.Sim.Battle;
using SixActual.Sim.GrandStrategy;

namespace SixActual.Headless;

/// <summary>
/// Grand-strategy harness: G4 scenarios of type "grand_strategy" (GS-01..04, CMD-08) and the G5 AI-vs-AI soak
/// (GS-05, MEC-13). Battles auto-resolve from their odds unless the scenario asks for "rts": then each is fought as a
/// full AI-vs-AI RTS battle on a map generated from the province terrain (GS-02).
/// </summary>
public static class GsRunner
{
    static readonly string[] DeckBySide = ["NATO:us_1989_armored", "Warsaw Pact:su_1989_motor_rifle"];

    public static bool IsGsScenario(string file)
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(file));
        return doc.RootElement.TryGetProperty("type", out var t) && t.GetString() == "grand_strategy";
    }

    static string? DeckFor(string side) => DeckBySide.FirstOrDefault(d => d.StartsWith(side + ":", StringComparison.Ordinal))?[(side.Length + 1)..];

    /// <summary>A resolver that fights each battle as a full RTS battle (attacker = player 0), falling back to the
    /// auto-resolve roll when a side has no shipped deck for the era.</summary>
    public static Func<GsState, GsBattle, int> Rts(GsTheater t, BattleData data, BattleRules rules, int cap) => (s, b) =>
    {
        var d0 = DeckFor(t.Factions[b.Attacker].Side);
        var d1 = DeckFor(t.Factions[b.Defender].Side);
        var seed = (s.Rng % 1000003UL) + (ulong)s.Turn;
        if (d0 is null || d1 is null || t.Era != "coldwar")
        {
            return (int)(seed % 1000UL) < b.OddsPermille ? 0 : 1;
        }

        var winner = SixActual.Headless.Soak.Fight(data, rules, GsBattleMap.Spec(b.Terrain, seed), b.Weather, d0, d1, seed, 24000, b.NuclearRelease);
        return winner == 0 ? 0 : 1;
    };

    public static bool RunScenario(string file, string root, string dataRoot, string rulesPath, BattleData data, BattleRules rules)
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(file));
        var e = doc.RootElement;
        var id = e.GetProperty("id").GetString()!;
        var req = e.GetProperty("requirement").GetString()!;
        var theaterPath = File.Exists(Path.Combine(dataRoot, "data", "theaters.json")) ? Path.Combine(dataRoot, "data", "theaters.json") : Path.Combine(root, "data", "theaters.json");
        var theater = GsTheater.Load(theaterPath).First(t => t.Era == e.GetProperty("era").GetString());

        var gr = GsRules.Load(rulesPath);
        var seeds = e.GetProperty("seeds").EnumerateArray().Select(x => x.GetUInt64()).ToArray();
        var turns = e.GetProperty("turns").GetInt32();
        var aiAll = e.TryGetProperty("ai", out var aiNode) && aiNode.ValueKind == JsonValueKind.String && aiNode.GetString() == "all";
        var aiList = !aiAll && aiNode.ValueKind == JsonValueKind.Array ? aiNode.EnumerateArray().Select(x => x.GetString()!).ToHashSet() : [];
        var ai = theater.Factions.Select(f => aiAll || aiList.Contains(f.Id)).ToArray();
        var rts = e.TryGetProperty("rts", out var rtsNode) && rtsNode.GetBoolean();
        var checks = e.GetProperty("assert").EnumerateArray().ToList();
        var passes = new int[checks.Count];
        var details = new string[checks.Count];
        foreach (var seed in seeds)
        {
            var s = GsSim.Start(theater, gr, seed);
            if (e.TryGetProperty("start", out var st) && st.TryGetProperty("tension", out var ten))
            {
                s.Tension = ten.GetInt32();
            }

            if (e.TryGetProperty("start", out var st2) && st2.TryGetProperty("owners", out var owners))
            {
                foreach (var o in owners.EnumerateObject())
                {
                    s.Provinces[theater.ProvinceIndex(o.Name)].Owner = theater.FactionIndex(o.Value.GetString()!);
                }
            }

            var resolver = rts ? Rts(theater, data, rules, 24000) : null;
            var done = new bool[checks.Count];
            for (var turn = 0; turn <= turns; turn++)
            {
                for (var c = 0; c < checks.Count; c++)
                {
                    if (!done[c] && checks[c].GetProperty("turn").GetInt32() == turn)
                    {
                        var (ok, detail) = Evaluate(theater, gr, s, checks[c], data, rules);
                        passes[c] += ok ? 1 : 0;
                        details[c] = detail;
                        done[c] = true;
                    }
                }

                if (turn < turns)
                {
                    GsSim.EndTurn(theater, gr, s, Orders(theater, s, e, turn), ai, resolver);
                }
            }
        }

        var allOk = true;
        for (var c = 0; c < checks.Count; c++)
        {
            var pass = passes[c] == seeds.Length;
            allOk &= pass;
            Console.WriteLine($"{(pass ? "pass" : "FAIL")} {id} [{req}] turn={checks[c].GetProperty("turn").GetInt32()} {checks[c].GetProperty("check").GetString()} :: {passes[c]}/{seeds.Length} seeds; {details[c]}");
        }

        return allOk;
    }

    static List<GsOrder> Orders(GsTheater t, GsState s, JsonElement e, int turn)
    {
        var list = new List<GsOrder>();
        if (!e.TryGetProperty("orders", out var orders))
        {
            return list;
        }

        string Str(JsonElement o, string n) => o.TryGetProperty(n, out var v) ? v.GetString()! : "";
        int Int(JsonElement o, string n, int f) => o.TryGetProperty(n, out var v) ? v.GetInt32() : f;
        foreach (var o in orders.EnumerateArray())
        {
            if (o.GetProperty("turn").GetInt32() != turn)
            {
                continue;
            }

            var faction = t.FactionIndex(Str(o, "faction"));
            var kind = Array.IndexOf(new[] { "", "move", "build", "research", "alliance", "ceasefire", "lend_lease", "war_goal", "nuke" }, o.GetProperty("kind").GetString()!);
            var armyIn = t.ProvinceIndex(Str(o, "army_in"));
            var army = armyIn >= 0 ? s.Armies.Where(a => a.Faction == faction && a.Province == armyIn).Select(a => a.Id).DefaultIfEmpty(-1).First() : -1;
            list.Add(new GsOrder
            {
                Turn = turn,
                Kind = kind,
                Faction = faction,
                Army = army,
                Province = t.ProvinceIndex(Str(o, "province")),
                Target = t.FactionIndex(Str(o, "target")),
                Value = Int(o, "value", 0),
            });
        }

        return list;
    }

    static (bool Ok, string Detail) Evaluate(GsTheater t, GsRules r, GsState s, JsonElement c, BattleData data, BattleRules rules)
    {
        string Str(string n) => c.TryGetProperty(n, out var v) ? v.GetString()! : "";
        int Int(string n, int f = 0) => c.TryGetProperty(n, out var v) ? v.GetInt32() : f;
        var expect = !c.TryGetProperty("expect", out var ex) || ex.GetBoolean();
        var p = t.ProvinceIndex(Str("province"));
        var f = t.FactionIndex(Str("faction"));
        bool ok;
        string detail;
        switch (c.GetProperty("check").GetString())
        {
            case "owner":
                ok = s.Provinces[p].Owner == f;
                detail = $"owner {t.Factions[s.Provinces[p].Owner].Id}";
                break;
            case "side_owner":
                ok = t.Factions[s.Provinces[p].Owner].Side == Str("side");
                detail = $"side {t.Factions[s.Provinces[p].Owner].Side}";
                break;
            case "army_strength_at_least":
                var str = s.Armies.Where(a => a.Faction == f && a.Province == p).Sum(a => a.Strength);
                ok = str >= Int("value");
                detail = $"strength {str}";
                break;
            case "production_at_least":
                ok = s.Factions[f].Production >= Int("value");
                detail = $"production {s.Factions[f].Production}";
                break;
            case "production_at_most":
                ok = s.Factions[f].Production <= Int("value");
                detail = $"production {s.Factions[f].Production}";
                break;
            case "faction_strength_at_most":
                var fs = s.Armies.Where(a => a.Faction == f).Sum(a => a.Strength);
                ok = fs <= Int("value");
                detail = $"faction strength {fs}";
                break;
            case "commanders_xp_total_at_least":
                var xp = s.Commanders.Sum(x => x.Xp);
                ok = xp >= Int("value");
                detail = $"total commander xp {xp}, wounded now {s.Commanders.Count(x => x.WoundedUntil > 0)}";
                break;
            case "tech_at_least":
                ok = s.Factions[f].Tech >= Int("value");
                detail = $"tech {s.Factions[f].Tech}";
                break;
            case "at_war":
                ok = s.Factions[f].AtWar;
                detail = $"at war {s.Factions[f].AtWar}";
                break;
            case "battles_at_least":
                ok = s.Battles >= Int("value");
                detail = $"battles {s.Battles}";
                break;
            case "rts_battles_at_least":
                ok = s.RtsBattles >= Int("value");
                detail = $"rts battles {s.RtsBattles}";
                break;
            case "tension_at_least":
                ok = s.Tension >= Int("value");
                detail = $"tension {s.Tension}";
                break;
            case "nuclear_level":
                ok = s.NuclearLevel == Int("value");
                detail = $"level {s.NuclearLevel}";
                break;
            case "fallout":
                ok = s.Provinces[p].FalloutUntil > s.Turn;
                detail = $"fallout until {s.Provinces[p].FalloutUntil}";
                break;
            case "industry_at_most":
                ok = s.Provinces[p].Industry <= Int("value");
                detail = $"industry {s.Provinces[p].Industry}";
                break;
            case "winner":
                var want = Str("value") switch { "stalemate" => GsState.WinnerStalemate, "exchange" => GsState.WinnerExchange, "none" => GsState.WinnerNone, var side => Array.IndexOf(t.Sides, side) };
                ok = s.Winner == want;
                detail = $"winner {s.Winner}";
                break;
            case "commander_xp_at_least":
                var cmd = s.Commanders.First(x => x.Name == Str("commander"));
                ok = cmd.Xp >= Int("value");
                detail = $"{cmd.Name} xp {cmd.Xp}, battles {cmd.Battles}, wounded until {cmd.WoundedUntil}";
                break;
            case "odds_between":
                var attackers = s.Armies.Where(a => a.Faction == f && a.Province == t.ProvinceIndex(Str("from"))).ToList();
                var odds = GsSim.Odds(t, r, s, f, attackers, p);
                ok = odds >= Int("min") && odds <= Int("max");
                detail = $"odds {odds} permille";
                break;
            case "save_load_roundtrip":
                // CMD-08: saving and loading reproduces the state exactly, and both copies play on identically.
                var json = s.Save();
                var copy = GsState.Load(json);
                var same = copy.Save() == json;
                var a1 = GsState.Load(json);
                var a2 = GsState.Load(json);
                var allAi = Enumerable.Repeat(true, t.Factions.Length).ToArray();
                for (var k = 0; k < Int("turns", 5); k++)
                {
                    GsSim.EndTurn(t, r, a1, [], allAi);
                    GsSim.EndTurn(t, r, a2, [], allAi);
                }

                ok = same && a1.Hash() == a2.Hash() && copy.Commanders.Sum(x => x.Xp) == s.Commanders.Sum(x => x.Xp);
                detail = $"identical after load {same}, identical after {Int("turns", 5)} more turns {a1.Hash() == a2.Hash()}, commander xp kept {copy.Commanders.Sum(x => x.Xp)}";
                break;
            case "battle_map":
                var spec = GsBattleMap.Spec(Array.IndexOf(GsRules.Terrains, Str("terrain")), (ulong)Int("seed", 1));
                var (forest, buildings, height) = GsBattleMap.Stats(spec);
                ok = forest >= Int("forest_pct_min") && forest <= Int("forest_pct_max", 100) && buildings >= Int("buildings_min") && buildings <= Int("buildings_max", int.MaxValue) && height >= Int("height_dm_min");
                detail = $"forest {forest}%, buildings {buildings}, max height {height} dm";
                break;
            default:
                throw new InvalidDataException($"unknown grand-strategy check {c.GetProperty("check").GetString()}");
        }

        return (ok == expect, detail);
    }

    /// <summary>G5: AI-vs-AI grand-strategy runs per era to an end state; CSV per run.</summary>
    public static int Soak(string[] args)
    {
        var n = int.Parse(args[0], CultureInfo.InvariantCulture);
        var era = args[Array.IndexOf(args, "--era") + 1];
        var start = args.Contains("--start") ? int.Parse(args[Array.IndexOf(args, "--start") + 1], CultureInfo.InvariantCulture) : 1;
        var root = ScenarioRunner.FindRootPublic();
        var theater = GsTheater.Load(Path.Combine(root, "data", "theaters.json")).First(t => t.Era == era);
        var gr = GsRules.Load(Path.Combine(root, "data", "rules.json"));
        var rows = new string[n];
        Parallel.For(0, n, k =>
        {
            var seed = (ulong)(start + k);
            try
            {
                var s = GsSim.Run(theater, gr, seed);
                // MEC-13: attacks the province survived while a reserve was within reinforce_turns of it, and whose
                // reinforce_turns window closed before the war ended (otherwise the answer cannot be observed).
                bool Counted(GsAttack a) => a.ReserveInReach && a.Held && a.Turn + gr.ReinforceTurns < s.Turn;
                var inReach = s.Attacks.Count(Counted);
                var answered = s.Attacks.Count(a => Counted(a) && a.ArrivedTurn >= 0 && a.ArrivedTurn - a.Turn <= gr.ReinforceTurns);
                rows[k] = string.Create(CultureInfo.InvariantCulture, $"{era},{seed},{s.Winner},{s.Turn},{s.Battles},{s.Captures},{inReach},{answered},{s.NuclearLevel},0");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"gs crash seed={seed}: {ex.GetType().Name}: {ex.Message}");
                rows[k] = $"{era},{seed},-1,0,0,0,0,0,0,1";
            }
        });
        Console.WriteLine("era,seed,winner,turns,battles,captures,attacks_in_reach,answered,nuclear_level,crashed");
        foreach (var row in rows)
        {
            Console.WriteLine(row);
        }

        return 0;
    }
}
