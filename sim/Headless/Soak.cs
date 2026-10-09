using System.Globalization;
using System.Text;
using SixActual.Sim;
using SixActual.Sim.Battle;
using SixActual.Sim.State.Battle;

namespace SixActual.Headless;

/// <summary>
/// G5 AI-vs-AI soak: each match generates an 8 km map from its seed, gives both players a deck and an AI XO,
/// and runs to a winner or the tick cap. One CSV row per match. Matches run in parallel; each is deterministic
/// in its seed. Usage: --soak N [--start S] [--cap TICKS] [--out FILE] [--mirror nato|pact] [--single KIND] [--decks ID0,ID1]
/// </summary>
public static class Soak
{
    static readonly (int X, int Y)[] ObjectiveSpots = [(2000, 4000), (4000, 4000), (6000, 4000), (4000, 2600), (4000, 5400)];

    public static int Run(string[] args)
    {
        int Arg(string name, int fallback)
        {
            var i = Array.IndexOf(args, name);
            return i >= 0 ? int.Parse(args[i + 1], CultureInfo.InvariantCulture) : fallback;
        }

        string? Str(string name)
        {
            var i = Array.IndexOf(args, name);
            return i >= 0 ? args[i + 1] : null;
        }

        var n = int.Parse(args[0], CultureInfo.InvariantCulture);
        var start = Arg("--start", 1);
        var cap = Arg("--cap", 24000);
        var mirror = Str("--mirror");
        var single = Str("--single");
        var teams = Arg("--teams", 0);
        if (teams > 0)
        {
            return RunTeams(n, start, cap, teams, Str("--out"));
        }

        var pair = Str("--decks")?.Split(',');
        if (pair is not null && (pair.Length != 2 || pair.Any(id => Array.FindIndex(BattleData.Load(ScenarioRunner.FindRootPublic()).Decks, d => d.Id == id) < 0)))
        {
            Console.Error.WriteLine("--decks needs two shipped deck ids: <player 0>,<player 1>");
            return 2;
        }
        var root = ScenarioRunner.FindRootPublic();
        var data = BattleData.Load(root);
        var rules = BattleRules.Load(Path.Combine(root, "data", "rules.json"));
        var decks = single is null ? data.Decks : WithSingleClassDeck(data, mirror == "pact" ? "su_1989_motor_rifle" : "us_1989_armored", single);
        var rows = new string[n];
        Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 2) }, k =>
        {
            rows[k] = Match(data, decks, rules, (ulong)(start + k), cap, mirror, single is not null, pair, args.Contains("--stuck-report"));
        });

        var header = "seed,winner,ticks,score0,score1,kills0,kills1,deployed0,deployed1,assaults_fires,assaults_nofires,committed,available,damaged,withdrawn,stuck,moving_units,bank_pct_a,bank_pct_b,bank_pct_c,crashed,hash,cas0,cas1,air0,air1,air_lost0,air_lost1,air_kills0,air_kills1,aa";
        var text = header + "\n" + string.Join("\n", rows) + "\n";
        var outPath = Str("--out");
        if (outPath is null)
        {
            Console.Write(text);
        }
        else
        {
            File.WriteAllText(outPath, text);
            Console.WriteLine($"soak matches={n} out={outPath}");
        }

        return 0;
    }

    /// <summary>MODE-01: N-vs-N AI skirmishes (Cold War balanced decks, every player an AI XO) on the shared skirmish
    /// setup. Columns: seed, winner, ticks, players, min_deployed (fewest units any one player called), crashed,
    /// teammate_fire (shots at a unit of the shooter's own team; must be 0).</summary>
    static int RunTeams(int n, int start, int cap, int perSide, string? outPath)
    {
        var root = ScenarioRunner.FindRootPublic();
        var data = BattleData.Load(root);
        var rules = BattleRules.Load(Path.Combine(root, "data", "rules.json"));
        var rows = new string[n];
        Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 2) }, k =>
        {
            var seed = (ulong)(start + k);
            try
            {
                var w = Skirmish.Create(data, rules, seed, "us_1989_armored", "su_1989_motor_rifle", true, true, new Skirmish.Options { PerSide = perSide });
                while (w.Winner < 0 && w.Tick <= cap)
                {
                    BattleWorld.Step(w, rules, ReadOnlySpan<UnitOrder>.Empty);
                }

                var minDeployed = Enumerable.Range(0, w.Players.Length).Min(p => w.Units.Count(u => u.Player == p));
                int TeamOfUnit(int id) => Frontline.TeamOf(w, w.Units[Array.FindIndex(w.Units, u => u.Id == id)].Player);
                var teammateFire = w.Events.Count(e => e.Kind == BattleEvent.Fired && TeamOfUnit(e.A) == TeamOfUnit(e.B));
                rows[k] = string.Create(CultureInfo.InvariantCulture, $"{seed},{w.Winner},{w.Tick},{w.Players.Length},{minDeployed},0,{teammateFire}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"crash seed={seed}: {ex.GetType().Name}: {ex.Message}");
                rows[k] = $"{seed},-1,0,0,0,1,0";
            }
        });
        var text = "seed,winner,ticks,players,min_deployed,crashed,teammate_fire\n" + string.Join("\n", rows) + "\n";
        if (outPath is null)
        {
            Console.Write(text);
        }
        else
        {
            File.WriteAllText(outPath, text);
        }

        return 0;
    }

    /// <summary>GS-02: one AI-vs-AI RTS battle on a given 8 km terrain spec and weather, decks by id (attacker is
    /// player 0, entering from the south). Returns the winner (0 attacker, 1 defender, 2 draw).</summary>
    public static int Fight(BattleData data, BattleRules rules, System.Text.Json.JsonElement spec, int weather, string deck0, string deck1, ulong seed, int cap, int nuclearRelease = 0)
    {
        var w = new WorldState { Seed = seed, Rng = BattleWorld.MixSeed(seed), Defs = data.Units, Weapons = data.Weapons, SupportDefs = data.Support, Terrain = TerrainBuilder.Build(spec), Weather = weather, NuclearRelease = nuclearRelease };
        w.AirGrounded = rules.AirOps(weather) ? 0 : 1;
        Structures.Place(w, rules, spec);
        w.Decks = data.Decks;
        var decks = new[] { Array.FindIndex(data.Decks, d => d.Id == deck0), Array.FindIndex(data.Decks, d => d.Id == deck1) };
        var stock = new List<int>();
        w.Players = new PlayerState[2];
        for (var p = 0; p < 2; p++)
        {
            w.Players[p] = new PlayerState
            {
                Dp = 1000,
                Profile = BattleRules.ProfileIndex(data.Decks[decks[p]].Income),
                SpawnX = 4000,
                SpawnY = p == 0 ? 600 : 7400,
                Deck = decks[p],
                StockStart = stock.Count,
                Team = p,
            };
            foreach (var c in data.Decks[decks[p]].Cards)
            {
                stock.Add(c.Count);
            }
        }

        w.CardStock = stock.ToArray();
        w.Objectives = ObjectiveSpots.Select(o => new Objective { X = o.X, Y = o.Y, Owner = -1 }).ToArray();
        Command.Init(w);
        w.XoActive[0] = 1;
        w.XoActive[1] = 1;
        while (w.Winner < 0 && w.Tick <= cap)
        {
            BattleWorld.Step(w, rules, ReadOnlySpan<UnitOrder>.Empty);
        }

        return w.Winner < 0 ? 2 : w.Winner;
    }

    /// <summary>MEC-09: a copy of the shipped decks plus, last, a single-class deck built from `baseId`'s cards of one
    /// sim kind (tank, ifv, infantry, ...), every card at its availability cap (max_count).</summary>
    static DeckDef[] WithSingleClassDeck(BattleData data, string baseId, string kind)
    {
        var k = UnitDef.ParseKind(kind);
        var b = data.Decks.First(d => d.Id == baseId);
        var picked = b.Cards.Where(c => data.Units[c.Unit].Kind == k).ToArray();
        if (picked.Length == 0)
        {
            throw new InvalidDataException($"deck {baseId} has no {kind} cards");
        }

        var cards = picked.Select(c => new CardDef { Unit = c.Unit, Phase = c.Phase, Cost = c.Cost, Count = c.MaxCount, MaxCount = c.MaxCount }).ToArray();
        return [.. data.Decks, new DeckDef { Id = $"{baseId}_only_{kind}", Income = b.Income, Cards = cards }];
    }

    static string Match(BattleData data, DeckDef[] decks, BattleRules rules, ulong seed, int cap, string? mirror, bool single, string[]? pair, bool report)
    {
        try
        {
            var spec = Bench.Terrain(seed);
            var w = new WorldState { Seed = seed, Rng = BattleWorld.MixSeed(seed), Defs = data.Units, Weapons = data.Weapons, Terrain = TerrainBuilder.Build(spec) };
            Structures.Place(w, rules, spec);
            w.Decks = decks;
            var natoDeck = Array.FindIndex(decks, d => d.Id == "us_1989_armored");
            var pactDeck = Array.FindIndex(decks, d => d.Id == "su_1989_motor_rifle");
            var deck0 = single ? decks.Length - 1 : mirror == "pact" ? pactDeck : natoDeck;
            var deck1 = mirror == "nato" ? natoDeck : pactDeck;
            if (pair is not null)
            {
                deck0 = Array.FindIndex(decks, d => d.Id == pair[0]);
                deck1 = Array.FindIndex(decks, d => d.Id == pair[1]);
            }
            var stock = new List<int>();
            w.Players = new PlayerState[2];
            for (var p = 0; p < 2; p++)
            {
                var deck = p == 0 ? deck0 : deck1;
                w.Players[p] = new PlayerState
                {
                    Dp = 1000,
                    Profile = BattleRules.ProfileIndex(decks[deck].Income),
                    SpawnX = 4000,
                    SpawnY = p == 0 ? 600 : 7400,
                    Deck = deck,
                    StockStart = stock.Count,
                    Team = p,
                };
                foreach (var c in decks[deck].Cards)
                {
                    stock.Add(c.Count);
                }
            }

            w.CardStock = stock.ToArray();
            w.Objectives = ObjectiveSpots.Select(o => new Objective { X = o.X, Y = o.Y, Owner = -1 }).ToArray();
            Command.Init(w);
            w.XoActive[0] = 1;
            w.XoActive[1] = 1;
            var rng = new XorShift64(BattleWorld.MixSeed(seed ^ 0xA5A5));
            w.Seats[Command.SeatIndex(0, Command.Xo)].Aggression = 50 + (int)(rng.NextULong() % 30);
            w.Seats[Command.SeatIndex(1, Command.Xo)].Aggression = 50 + (int)(rng.NextULong() % 30);

            // Stuck tracking: a unit that wants to move but has not gained 2 m in stuck_seconds.
            var lastPos = new Dictionary<int, (long X, long Y, int Tick)>();
            var stuck = new HashSet<int>();
            var moving = new HashSet<int>();
            var bank = new long[3];
            var income = new long[3];
            var lastPhase = 0;
            var phaseStartDp = new long[2];
            var phaseStartEarned = new long[2];
            for (var p = 0; p < 2; p++)
            {
                phaseStartDp[p] = w.Players[p].Dp;
                phaseStartEarned[p] = w.Players[p].DpEarned;
            }

            // Run one tick past the cap so the rules' time-cap judgement (tick >= cap) gets to fire.
            while (w.Winner < 0 && w.Tick <= cap)
            {
                BattleWorld.Step(w, rules, ReadOnlySpan<UnitOrder>.Empty);
                if (w.Phase != lastPhase || w.Tick == cap)
                {
                    // Unspent bank at the end of a phase, against that phase's income (MEC-10).
                    for (var p = 0; p < 2; p++)
                    {
                        bank[lastPhase] += w.Players[p].Dp;
                        income[lastPhase] += Math.Max(1, w.Players[p].DpEarned - phaseStartEarned[p]);
                        phaseStartEarned[p] = w.Players[p].DpEarned;
                    }

                    lastPhase = w.Phase;
                }

                if (w.Tick % 20 == 0)
                {
                    for (var i = 0; i < w.Units.Length; i++)
                    {
                        ref readonly var u = ref w.Units[i];
                        if (u.Alive == 0 || u.HasGoal == 0)
                        {
                            lastPos.Remove(i);
                            continue;
                        }

                        moving.Add(i);
                        var x = u.X.Raw >> Fp.FractionalBits;
                        var y = u.Y.Raw >> Fp.FractionalBits;
                        if (!lastPos.TryGetValue(i, out var lp) || Math.Abs(lp.X - x) + Math.Abs(lp.Y - y) >= 2)
                        {
                            lastPos[i] = (x, y, w.Tick);
                        }
                        else if (w.Tick - lp.Tick > 120 * rules.TickHz)
                        {
                            if (stuck.Add(i) && report)
                            {
                                var d = data.Units[u.Def];
                                Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
                                    $"stuck seed={seed} t={w.Tick} unit={d.Id} p={u.Player} at=({x},{y}) goal=({u.GoalX.Raw >> 16},{u.GoalY.Raw >> 16}) step={u.PathStep}/{w.Paths[i]?.Length ?? -1} nopath={u.NoPath} flags={u.Flags} morale={u.MoraleState} fuel={u.Fuel} seat={u.Seat} withdrawing={u.Withdrawing} cell_blocked={w.Terrain.Blocked[w.Terrain.Index((int)x / 8, (int)y / 8)]}"));
                            }
                        }
                    }
                }
            }

            if (Environment.GetEnvironmentVariable("SOAK_BUYS") == "1")
            {
                // Balance diagnostic: what each AI fielded (by phase of arrival) and when objectives changed hands.
                for (var p = 0; p < 2; p++)
                {
                    var bought = w.Units.Where(u => u.Player == p).GroupBy(u => w.Defs[u.Def].Id).Select(g => $"{g.Key}x{g.Count()}");
                    Console.Error.WriteLine($"buys seed={seed} p{p} {string.Join(' ', bought)}");
                }

                var taken = w.Events.Where(e => e.Kind == BattleEvent.ObjectiveTaken).Select(e => $"{e.Tick}:{e.A}->{e.B}");
                Console.Error.WriteLine($"objectives seed={seed} winner={w.Winner} {string.Join(' ', taken)}");
            }

            int Count(int kind, Func<BattleEvent, bool>? f = null) => w.Events.Count(e => e.Kind == kind && (f is null || f(e)));
            var kills = new int[2];
            foreach (var e in w.Events.Where(e => e.Kind == BattleEvent.Killed))
            {
                var victim = w.Units.FirstOrDefault(u => u.Id == e.B);
                kills[1 - (victim.Player & 1)]++;
            }

            long committed = 0;
            long available = 0;
            foreach (var e in w.Events.Where(e => e.Kind == BattleEvent.AssaultStarted))
            {
                committed += e.B;
                available += e.C;
            }

            string Pct(int ph) => income[ph] == 0 ? "" : (bank[ph] * 100 / income[ph]).ToString(CultureInfo.InvariantCulture);
            var sb = new StringBuilder();
            sb.Append(CultureInfo.InvariantCulture, $"{seed},{w.Winner},{w.Tick},{w.Score[0]},{w.Score[1]},{kills[0]},{kills[1]},");
            sb.Append(CultureInfo.InvariantCulture, $"{w.Units.Count(u => u.Player == 0)},{w.Units.Count(u => u.Player == 1)},");
            sb.Append(CultureInfo.InvariantCulture, $"{Count(BattleEvent.AssaultStarted, e => e.Value != 0)},{Count(BattleEvent.AssaultStarted, e => e.Value == 0)},{committed},{available},");
            sb.Append(CultureInfo.InvariantCulture, $"{Count(BattleEvent.Damaged)},{Count(BattleEvent.Withdraw)},{stuck.Count},{moving.Count},{Pct(0)},{Pct(1)},{Pct(2)},0,{WorldHash.Of(w)[..16]}");

            // Air war: CAS dispatches per requesting side, aircraft deployed and lost per side.
            var cas = new int[2];
            foreach (var e in w.Events.Where(e => e.Kind == BattleEvent.CasDispatched))
            {
                var q = w.Requests.FirstOrDefault(r => r.Id == e.A);
                cas[w.Seats[q.FromSeat].Player & 1]++;
            }

            var airLost = new int[2];
            foreach (var e in w.Events.Where(e => e.Kind == BattleEvent.Killed))
            {
                var victim = w.Units.FirstOrDefault(u => u.Id == e.B);
                if (data.Units[victim.Def].IsAir)
                {
                    airLost[victim.Player & 1]++;
                }
            }

            var airKills = new int[2];
            foreach (var e in w.Events.Where(e => e.Kind == BattleEvent.Killed))
            {
                var killer = w.Units.FirstOrDefault(u => u.Id == e.A);
                if (e.A >= 0 && data.Units[killer.Def].IsAir)
                {
                    airKills[killer.Player & 1]++;
                }
            }

            sb.Append(CultureInfo.InvariantCulture, $",{cas[0]},{cas[1]},{w.Units.Count(u => u.Player == 0 && data.Units[u.Def].IsAir)},{w.Units.Count(u => u.Player == 1 && data.Units[u.Def].IsAir)},{airLost[0]},{airLost[1]},{airKills[0]},{airKills[1]}");

            // MEC-09 AA efficiency: per AA card, card cost deployed and the card cost of the aircraft it destroyed
            // ("unit:deployed:killed" joined by '|'; costs from each owner's deck).
            int CardCost(UnitState u)
            {
                var def = u.Def;
                return decks[w.Players[u.Player].Deck].Cards.FirstOrDefault(c => c.Unit == def)?.Cost ?? 0;
            }

            var aaDeployed = new SortedDictionary<string, long>(StringComparer.Ordinal);
            var aaKilled = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (var u in w.Units.Where(u => data.Units[u.Def].Kind == UnitDef.KindAa))
            {
                var id = data.Units[u.Def].Id;
                aaDeployed[id] = aaDeployed.GetValueOrDefault(id) + CardCost(u);
                aaKilled.TryAdd(id, 0);
            }

            foreach (var e in w.Events.Where(e => e.Kind == BattleEvent.Killed && e.A >= 0))
            {
                var killer = w.Units.FirstOrDefault(u => u.Id == e.A);
                var victim = w.Units.FirstOrDefault(u => u.Id == e.B);
                if (data.Units[killer.Def].Kind == UnitDef.KindAa && data.Units[victim.Def].IsAir)
                {
                    var id = data.Units[killer.Def].Id;
                    aaKilled[id] = aaKilled.GetValueOrDefault(id) + CardCost(victim);
                }
            }

            sb.Append(',').Append(string.Join('|', aaDeployed.Select(kv => string.Create(CultureInfo.InvariantCulture, $"{kv.Key}:{kv.Value}:{aaKilled[kv.Key]}"))));
            return sb.ToString();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"crash seed={seed}: {ex.GetType().Name}: {ex.Message}");
            return $"{seed},-1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,,,,1,{ex.GetType().Name},0,0,0,0,0,0,0,0,";
        }
    }
}
