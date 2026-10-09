namespace SixActual.Sim.GrandStrategy;

/// <summary>A player or scenario order for one turn.</summary>
public sealed class GsOrder
{
    public const int Move = 1;          // Army -> Province (adjacent steps along the shortest path)

    public const int Build = 2;         // Faction spends Value production on Army (or a new army in its capital: Army = -1)

    public const int Research = 3;      // Faction puts Value production into research

    public const int Alliance = 4;      // Faction (neutral) joins its side and enters the war

    public const int Ceasefire = 5;     // both sides stop fighting for ceasefire_turns

    public const int LendLease = 6;     // Faction gives lend_lease_pct of its production to Target faction each turn

    public const int WarGoal = 7;       // Faction declares Province as its war goal

    public const int Nuke = 8;          // Faction releases nuclear level Value (2 theater strike on Province, 3 strategic)

    public int Turn { get; init; }

    public int Kind { get; init; }

    public int Faction { get; init; } = -1;

    public int Army { get; init; } = -1;

    public int Province { get; init; } = -1;

    public int Target { get; init; } = -1;

    public int Value { get; init; }
}

/// <summary>A battle the turn produced, for the resolver (auto-resolve or full RTS, GS-02).</summary>
public sealed class GsBattle
{
    public int Province { get; init; }

    public int Attacker { get; init; }

    public int Defender { get; init; }

    /// <summary>Attacker's chance of winning, permille (shown to the player as the transparent odds).</summary>
    public int OddsPermille { get; init; }

    public int Terrain { get; init; }

    public int Weather { get; init; }

    /// <summary>GS-04: 1 when the escalation has released tactical nuclear weapons for the battle.</summary>
    public int NuclearRelease { get; init; }
}

/// <summary>
/// GS-01..05, MEC-13, CMD-08: the grand-strategy turn. Each turn: orders, AI decisions, movement and battles,
/// supply and fallout attrition, production and manpower, commander recovery, escalation, and the end-state test.
/// A battle is auto-resolved from its transparent odds unless a resolver is supplied (Headless plugs in a full RTS
/// battle on a map generated from the province's terrain and the turn's weather); either way the result feeds back
/// into strength, ownership, commander XP and tension.
/// </summary>
public static class GsSim
{
    public static GsState Start(GsTheater t, GsRules r, ulong seed)
    {
        var s = new GsState
        {
            Era = t.Era,
            Rng = seed == 0 ? 0x9E3779B97F4A7C15UL : seed,
            Provinces = t.Provinces.Select(p => new GsProvince { Owner = p.Owner, Industry = p.Industry, Manpower = p.Manpower }).ToArray(),
            Factions = t.Factions.Select((f, i) => new GsFaction { Side = t.SideOf(i), AtWar = f.AtWar }).ToArray(),
        };
        for (var f = 0; f < t.Factions.Length; f++)
        {
            for (var c = 0; c < 2; c++)
            {
                s.Commanders.Add(new GsCommander { Id = s.Commanders.Count, Name = $"{t.Factions[f].Id}-cmdr-{c + 1}", Faction = f });
            }
        }

        for (var p = 0; p < t.Provinces.Length; p++)
        {
            var owner = t.Provinces[p].Owner;
            var cmd = s.Commanders.FirstOrDefault(c => c.Faction == owner && s.Armies.All(a => a.Commander != c.Id));
            s.Armies.Add(new GsArmy
            {
                Id = s.NextArmyId++,
                Faction = owner,
                Province = p,
                Strength = Math.Min(r.ArmyMax, t.Provinces[p].Industry * r.StartArmyPerIndustry),
                Commander = cmd?.Id ?? -1,
            });
        }

        return s;
    }

    static ulong Next(GsState s)
    {
        var x = s.Rng;
        x ^= x >> 12;
        x ^= x << 25;
        x ^= x >> 27;
        s.Rng = x;
        return x * 0x2545F4914F6CDD1DUL;
    }

    static int Roll(GsState s, int n) => (int)(Next(s) % (ulong)Math.Max(1, n));

    public static bool Hostile(GsState s, int a, int b) =>
        s.Factions[a].Side != s.Factions[b].Side && s.Factions[a].AtWar && s.Factions[b].AtWar && s.Turn >= s.CeasefireUntil;

    public static void EndTurn(GsTheater t, GsRules r, GsState s, IReadOnlyList<GsOrder> orders, bool[] ai, Func<GsState, GsBattle, int>? resolver = null)
    {
        if (s.Winner != GsState.WinnerNone)
        {
            return;
        }

        s.Weather = Roll(s, 100) < 25 ? 3 : Roll(s, 100) < 30 ? 2 : 0; // fog 25%, else rain ~22%, else clear
        foreach (var o in orders)
        {
            if (o.Turn == s.Turn)
            {
                Apply(t, r, s, o);
            }
        }

        for (var f = 0; f < s.Factions.Length; f++)
        {
            if (f < ai.Length && ai[f])
            {
                Ai(t, r, s, f);
            }
        }

        MoveAndFight(t, r, s, resolver);
        Attrition(t, r, s);
        Produce(t, r, s);
        foreach (var c in s.Commanders)
        {
            if (c.WoundedUntil > 0 && s.Turn >= c.WoundedUntil)
            {
                c.WoundedUntil = 0;
            }
        }

        Escalate(t, r, s, ai);
        TrackReinforcements(r, s);
        s.Turn++;
        Judge(t, r, s);
    }

    static void Apply(GsTheater t, GsRules r, GsState s, GsOrder o)
    {
        switch (o.Kind)
        {
            case GsOrder.Move:
                var army = s.Armies.FirstOrDefault(a => a.Id == o.Army);
                if (army != null && o.Province >= 0)
                {
                    army.Target = o.Province;
                }

                break;
            case GsOrder.Build:
                Build(t, r, s, o.Faction, o.Army, o.Value);
                break;
            case GsOrder.Research:
                Invest(r, s, o.Faction, o.Value);
                break;
            case GsOrder.Alliance:
                s.Factions[o.Faction].AtWar = true;
                break;
            case GsOrder.Ceasefire:
                s.CeasefireUntil = s.Turn + r.CeasefireTurns;
                break;
            case GsOrder.LendLease:
                s.Factions[o.Faction].LendLeaseTo = o.Target;
                break;
            case GsOrder.WarGoal:
                s.Factions[o.Faction].WarGoal = o.Province;
                break;
            case GsOrder.Nuke:
                Nuke(t, r, s, o.Faction, o.Value, o.Province);
                break;
        }
    }

    static void Build(GsTheater t, GsRules r, GsState s, int faction, int armyId, int points)
    {
        var f = s.Factions[faction];
        points = Math.Min(points, f.Production);
        var army = armyId >= 0 ? s.Armies.FirstOrDefault(a => a.Id == armyId && a.Faction == faction) : null;
        if (army == null)
        {
            var cap = t.Factions[faction].Capital;
            if (s.Provinces[cap].Owner != faction)
            {
                return;
            }

            army = new GsArmy { Id = s.NextArmyId++, Faction = faction, Province = cap, Strength = 0 };
            s.Armies.Add(army);
        }

        var room = (r.ArmyMax - army.Strength + r.StrengthPerPoint - 1) / r.StrengthPerPoint;
        var byManpower = f.ManpowerPool;
        points = Math.Max(0, Math.Min(points, Math.Min(room, byManpower)));
        army.Strength = Math.Min(r.ArmyMax, army.Strength + (points * r.StrengthPerPoint));
        f.Production -= points;
        f.ManpowerPool -= points;
    }

    static void Invest(GsRules r, GsState s, int faction, int points)
    {
        var f = s.Factions[faction];
        points = Math.Min(points, f.Production);
        if (f.Tech >= r.TechMax || points <= 0)
        {
            return;
        }

        f.Production -= points;
        f.Research += points;
        var cost = r.ResearchBase + (f.Tech * r.ResearchStep);
        while (f.Research >= cost && f.Tech < r.TechMax)
        {
            f.Research -= cost;
            f.Tech++;
            cost = r.ResearchBase + (f.Tech * r.ResearchStep);
        }
    }

    /// <summary>Combat power of a faction's armies in a province (defender gets terrain), percent-scaled integers.</summary>
    public static long Power(GsTheater t, GsRules r, GsState s, int faction, IEnumerable<GsArmy> armies, int terrainPct)
    {
        long total = 0;
        foreach (var a in armies)
        {
            long p = a.Strength * (100L + (s.Factions[faction].Tech * r.TechBonusPct));
            var cmd = a.Commander >= 0 ? s.Commanders[a.Commander] : null;
            if (cmd != null && cmd.WoundedUntil == 0)
            {
                p = p * (100 + r.CommanderBonusPct + Math.Min(10, cmd.Xp / 10)) / 100;
            }

            total += p * terrainPct / 100;
        }

        return total;
    }

    /// <summary>Transparent odds (permille) of an attack by `attackers` on province p.</summary>
    public static int Odds(GsTheater t, GsRules r, GsState s, int attacker, List<GsArmy> attackers, int p)
    {
        var defender = s.Provinces[p].Owner;
        var defenders = s.Armies.Where(a => a.Province == p && a.Faction == defender && a.Strength > 0);
        var atk = Power(t, r, s, attacker, attackers, 100);
        var def = Power(t, r, s, defender, defenders, r.TerrainDefencePct[t.Provinces[p].Terrain]);
        return atk + def == 0 ? 500 : (int)(atk * 1000 / (atk + def));
    }

    static void MoveAndFight(GsTheater t, GsRules r, GsState s, Func<GsState, GsBattle, int>? resolver)
    {
        // Each army with a target takes one step along the shortest path; steps into hostile provinces are attacks.
        var attacks = new SortedDictionary<long, List<GsArmy>>();
        foreach (var a in s.Armies.Where(a => a.Target >= 0 && a.Strength > 0).OrderBy(a => a.Id).ToList())
        {
            if (a.Target == a.Province)
            {
                a.Target = -1;
                continue;
            }

            var step = NextStep(t, s, a.Faction, a.Province, a.Target);
            if (step < 0)
            {
                a.Target = -1;
                continue;
            }

            var owner = s.Provinces[step].Owner;
            if (owner == a.Faction || s.Factions[owner].Side == s.Factions[a.Faction].Side)
            {
                a.Province = step;
                if (step == a.Target)
                {
                    a.Target = -1;
                }

                continue;
            }

            if (!Hostile(s, a.Faction, owner))
            {
                a.Target = -1;
                continue;
            }

            var key = ((long)step << 8) | (uint)a.Faction;
            if (!attacks.TryGetValue(key, out var list))
            {
                attacks[key] = list = [];
            }

            list.Add(a);
        }

        foreach (var (key, group) in attacks)
        {
            var p = (int)(key >> 8);
            var attacker = (int)(key & 0xFF);
            var defender = s.Provinces[p].Owner;
            if (defender == attacker)
            {
                foreach (var a in group)
                {
                    a.Province = p;
                }

                continue;
            }

            var defenders = s.Armies.Where(a => a.Province == p && a.Faction == defender && a.Strength > 0).ToList();
            if (defenders.Count == 0)
            {
                Capture(r, s, p, attacker, group);
                continue;
            }

            var battle = new GsBattle
            {
                Province = p,
                Attacker = attacker,
                Defender = defender,
                OddsPermille = Odds(t, r, s, attacker, group, p),
                Terrain = t.Provinces[p].Terrain,
                Weather = s.Weather,
                NuclearRelease = s.Tension >= r.TensionTactical ? 1 : 0,
            };
            var record = new GsAttack { Turn = s.Turn, Province = p, Faction = defender, ReserveInReach = ReserveInReach(t, r, s, defender, p) };
            s.Attacks.Add(record);
            var attackerWon = resolver != null ? resolver(s, battle) == 0 : Roll(s, 1000) < battle.OddsPermille;
            if (resolver != null)
            {
                s.RtsBattles++;
            }

            s.Battles++;
            s.Tension += r.TensionBattle;
            Bleed(group, attackerWon ? r.LossWinnerPct : r.LossLoserPct);
            Bleed(defenders, attackerWon ? r.LossLoserPct : r.LossWinnerPct);
            Experience(r, s, group, attackerWon);
            Experience(r, s, defenders, !attackerWon);
            record.Held = !attackerWon;
            if (attackerWon)
            {
                // Defenders fall back to an adjacent friendly province, or are destroyed if cut off.
                foreach (var d in defenders)
                {
                    var back = t.Provinces[p].Neighbours.FirstOrDefault(n => s.Provinces[n].Owner == defender, -1);
                    if (back < 0)
                    {
                        d.Strength = 0;
                    }
                    else
                    {
                        d.Province = back;
                        d.Target = -1;
                    }
                }

                Capture(r, s, p, attacker, group);
            }
            else
            {
                foreach (var a in group)
                {
                    a.Target = -1;
                }
            }
        }

        s.Armies.RemoveAll(a => a.Strength <= 0);
    }

    static void Capture(GsRules r, GsState s, int p, int attacker, List<GsArmy> group)
    {
        s.Provinces[p].Owner = attacker;
        s.Captures++;
        s.Tension += r.TensionCapture;
        foreach (var a in group)
        {
            a.Province = p;
            if (a.Target == p)
            {
                a.Target = -1;
            }
        }
    }

    static void Bleed(List<GsArmy> armies, int pct)
    {
        foreach (var a in armies)
        {
            a.Strength -= (a.Strength * pct + 99) / 100;
        }
    }

    static void Experience(GsRules r, GsState s, List<GsArmy> armies, bool won)
    {
        foreach (var a in armies)
        {
            if (a.Commander < 0)
            {
                continue;
            }

            var c = s.Commanders[a.Commander];
            if (c.WoundedUntil > 0)
            {
                continue;
            }

            c.Battles++;
            c.Xp += won ? r.CommanderXpWin : r.CommanderXpLoss;
            if (Roll(s, 100) < r.CommanderWoundPct)
            {
                c.WoundedUntil = s.Turn + r.CommanderWoundTurns;
            }
        }
    }

    /// <summary>Next province on the shortest path from `from` to `to` through provinces of the faction's side (and
    /// the destination itself), breadth-first with neighbour order as the tie-break.</summary>
    public static int NextStep(GsTheater t, GsState s, int faction, int from, int to)
    {
        var dist = Distances(t, s, faction, to, allowHostileGoal: true);
        if (dist[from] < 0)
        {
            return -1;
        }

        var best = -1;
        foreach (var n in t.Provinces[from].Neighbours)
        {
            if (dist[n] >= 0 && dist[n] == dist[from] - 1 && (best < 0 || n < best))
            {
                best = n;
            }
        }

        return best;
    }

    /// <summary>Steps from every province to `to`, moving only through the faction's side's provinces.</summary>
    static int[] Distances(GsTheater t, GsState s, int faction, int to, bool allowHostileGoal)
    {
        var side = s.Factions[faction].Side;
        var dist = Enumerable.Repeat(-1, t.Provinces.Length).ToArray();
        var queue = new Queue<int>();
        dist[to] = 0;
        queue.Enqueue(to);
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            foreach (var n in t.Provinces[p].Neighbours)
            {
                if (dist[n] >= 0)
                {
                    continue;
                }

                var friendly = s.Factions[s.Provinces[n].Owner].Side == side;
                if (!friendly)
                {
                    continue;
                }

                dist[n] = dist[p] + 1;
                queue.Enqueue(n);
            }
        }

        if (!allowHostileGoal && s.Factions[s.Provinces[to].Owner].Side != side)
        {
            dist[to] = -1;
        }

        return dist;
    }

    static bool Frontline(GsTheater t, GsState s, int faction, int p) =>
        t.Provinces[p].Neighbours.Any(n => Hostile(s, faction, s.Provinces[n].Owner));

    /// <summary>A reserve: an army of the faction with strength left, not in a front-line province and not already
    /// marching; "in reach" when within reinforce_turns steps of p.</summary>
    static bool ReserveInReach(GsTheater t, GsRules r, GsState s, int faction, int p)
    {
        var dist = Distances(t, s, faction, p, allowHostileGoal: false);
        return s.Armies.Any(a => a.Faction == faction && a.Province != p && a.Strength >= 20 && a.Target < 0
            && !Frontline(t, s, faction, a.Province) && dist[a.Province] > 0 && dist[a.Province] <= r.ReinforceTurns);
    }

    static void Attrition(GsTheater t, GsRules r, GsState s)
    {
        foreach (var a in s.Armies)
        {
            var cap = t.Factions[a.Faction].Capital;
            var supplied = s.Factions[s.Provinces[cap].Owner].Side == s.Factions[a.Faction].Side
                && Distances(t, s, a.Faction, cap, allowHostileGoal: false)[a.Province] >= 0;
            if (!supplied)
            {
                a.Strength -= (a.Strength * r.AttritionUnsuppliedPct + 99) / 100;
            }

            if (s.Provinces[a.Province].FalloutUntil > s.Turn)
            {
                a.Strength -= (a.Strength * r.FalloutAttritionPct + 99) / 100;
            }
        }

        s.Armies.RemoveAll(a => a.Strength <= 0);
    }

    static void Produce(GsTheater t, GsRules r, GsState s)
    {
        var income = new int[s.Factions.Length];
        for (var p = 0; p < s.Provinces.Length; p++)
        {
            var pr = s.Provinces[p];
            if (pr.FalloutUntil > s.Turn)
            {
                continue;
            }

            income[pr.Owner] += pr.Industry;
            s.Factions[pr.Owner].ManpowerPool += pr.Manpower;
        }

        for (var f = 0; f < s.Factions.Length; f++)
        {
            var to = s.Factions[f].LendLeaseTo;
            if (to >= 0 && to != f && s.Factions[to].Side == s.Factions[f].Side)
            {
                var give = income[f] * r.LendLeasePct / 100;
                income[f] -= give;
                income[to] += give;
            }
        }

        for (var f = 0; f < s.Factions.Length; f++)
        {
            s.Factions[f].Production += income[f];
        }
    }

    /// <summary>GS-04: release a nuclear level. Tactical (1) is an RTS support card unlocked by tension; theater (2)
    /// strikes a province (industry destroyed, fallout zone); strategic (3) is a general exchange that ends the war.</summary>
    public static bool Nuke(GsTheater t, GsRules r, GsState s, int faction, int level, int province)
    {
        var need = level switch { 1 => r.TensionTactical, 2 => r.TensionTheater, 3 => r.TensionStrategic, _ => int.MaxValue };
        if (s.Tension < need || !s.Factions[faction].AtWar)
        {
            return false;
        }

        s.NuclearLevel = Math.Max(s.NuclearLevel, level);
        if (level == 2 && province >= 0 && s.Factions[s.Provinces[province].Owner].Side != s.Factions[faction].Side)
        {
            var pr = s.Provinces[province];
            pr.Industry = Math.Max(0, pr.Industry - r.TheaterStrikeIndustry);
            pr.FalloutUntil = s.Turn + r.FalloutTurns;
            s.Tension += 10;
            for (var f = 0; f < s.Factions.Length; f++)
            {
                if (s.Factions[f].Side == s.Factions[pr.Owner].Side)
                {
                    s.Factions[f].OwesRetaliation = 1;
                }
            }
        }
        else if (level == 3)
        {
            s.Winner = GsState.WinnerExchange;
        }

        return true;
    }

    /// <summary>AI escalation: a side that has lost most of its start provinces and stands at theater tension strikes
    /// the enemy's most industrial front-line province; a struck side retaliates in kind; at strategic tension the
    /// exchange happens.</summary>
    static void Escalate(GsTheater t, GsRules r, GsState s, bool[] ai)
    {
        for (var f = 0; f < s.Factions.Length; f++)
        {
            if (f >= ai.Length || !ai[f] || !s.Factions[f].AtWar)
            {
                continue;
            }

            var owned = s.Provinces.Count(p => s.Factions[p.Owner].Side == s.Factions[f].Side);
            var start = t.Provinces.Count(p => t.SideOf(p.Owner) == s.Factions[f].Side);
            var losing = owned * 100 < start * 50;
            if (s.Tension >= r.TensionStrategic && losing)
            {
                Nuke(t, r, s, f, 3, -1);
                return;
            }

            if (s.Tension >= r.TensionTheater && (losing || s.Factions[f].OwesRetaliation != 0))
            {
                var target = -1;
                for (var p = 0; p < s.Provinces.Length; p++)
                {
                    if (s.Factions[s.Provinces[p].Owner].Side != s.Factions[f].Side && s.Provinces[p].FalloutUntil <= s.Turn
                        && (target < 0 || s.Provinces[p].Industry > s.Provinces[target].Industry))
                    {
                        target = p;
                    }
                }

                if (target >= 0 && Nuke(t, r, s, f, 2, target))
                {
                    s.Factions[f].OwesRetaliation = 0;
                }
            }
        }
    }

    /// <summary>MEC-13: a recorded attack is answered when the reserve the AI sent reaches the province within
    /// reinforce_turns.</summary>
    static void TrackReinforcements(GsRules r, GsState s)
    {
        foreach (var at in s.Attacks)
        {
            if (at.ArrivedTurn >= 0 || at.Reinforcer < 0 || s.Turn - at.Turn > r.ReinforceTurns)
            {
                continue;
            }

            var army = s.Armies.FirstOrDefault(a => a.Id == at.Reinforcer);
            if (army != null && army.Province == at.Province)
            {
                at.ArrivedTurn = s.Turn;
            }
        }
    }

    static void Ai(GsTheater t, GsRules r, GsState s, int f)
    {
        var me = s.Factions[f];
        if (!me.AtWar)
        {
            return;
        }

        // MEC-13: answer attacks of the last reinforce_turns turns with the nearest reserve.
        foreach (var at in s.Attacks.Where(x => x.Faction == f && x.ArrivedTurn < 0 && x.Reinforcer < 0 && s.Turn - x.Turn < r.ReinforceTurns).ToList())
        {
            if (s.Provinces[at.Province].Owner != f)
            {
                continue;
            }

            // Nearest rear reserve first; if none, strip a quiet front-line sector (one not attacked itself lately).
            var dist = Distances(t, s, f, at.Province, allowHostileGoal: false);
            var underAttack = s.Attacks.Where(x => x.Faction == f && s.Turn - x.Turn <= 1).Select(x => x.Province).ToHashSet();
            var reserve = s.Armies.Where(a => a.Faction == f && a.Province != at.Province && a.Strength >= 20 && a.Target < 0
                    && dist[a.Province] > 0 && !underAttack.Contains(a.Province))
                .OrderBy(a => Frontline(t, s, f, a.Province) ? 1 : 0).ThenBy(a => dist[a.Province]).ThenBy(a => a.Id).FirstOrDefault();
            if (reserve != null)
            {
                reserve.Target = at.Province;
                at.Reinforcer = reserve.Id;
            }
        }

        // Production: top up the weakest front-line army, then research with what is left over a reserve.
        var weakest = s.Armies.Where(a => a.Faction == f && Frontline(t, s, f, a.Province)).OrderBy(a => a.Strength).ThenBy(a => a.Id).FirstOrDefault();
        if (weakest != null && weakest.Strength < r.ArmyMax)
        {
            Build(t, r, s, f, weakest.Id, me.Production);
        }
        else if (!s.Armies.Any(a => a.Faction == f))
        {
            Build(t, r, s, f, -1, me.Production);
        }

        if (me.Production > 20)
        {
            Invest(r, s, f, me.Production - 10);
        }

        // Attack: each idle front-line army attacks the adjacent hostile province with the best odds above the bar,
        // preferring the war goal.
        foreach (var a in s.Armies.Where(a => a.Faction == f && a.Target < 0 && a.Strength >= 30).OrderBy(a => a.Id).ToList())
        {
            var best = -1;
            var bestOdds = r.AttackOddsPermille - 1;
            foreach (var n in t.Provinces[a.Province].Neighbours)
            {
                if (!Hostile(s, f, s.Provinces[n].Owner))
                {
                    continue;
                }

                var odds = Odds(t, r, s, f, [a], n) + (n == me.WarGoal ? 100 : 0);
                if (odds > bestOdds)
                {
                    best = n;
                    bestOdds = odds;
                }
            }

            if (best >= 0)
            {
                a.Target = best;
            }
        }
    }

    static void Judge(GsTheater t, GsRules r, GsState s)
    {
        if (s.Winner != GsState.WinnerNone)
        {
            return;
        }

        // A side wins when it holds every capital of the factions at war against it.
        for (var side = 0; side < t.Sides.Length; side++)
        {
            var enemyCapitals = Enumerable.Range(0, s.Factions.Length)
                .Where(f => s.Factions[f].Side != side && s.Factions[f].AtWar)
                .Select(f => t.Factions[f].Capital).ToList();
            if (enemyCapitals.Count > 0 && enemyCapitals.All(c => s.Factions[s.Provinces[c].Owner].Side == side))
            {
                s.Winner = side;
                return;
            }
        }

        // War goals: a side whose at-war factions all hold their declared war goal wins.
        for (var side = 0; side < t.Sides.Length; side++)
        {
            var goals = Enumerable.Range(0, s.Factions.Length).Where(f => s.Factions[f].Side == side && s.Factions[f].AtWar && s.Factions[f].WarGoal >= 0).ToList();
            if (goals.Count > 0 && goals.All(f => s.Factions[s.Provinces[s.Factions[f].WarGoal].Owner].Side == side))
            {
                s.Winner = side;
                return;
            }
        }

        if (s.Turn >= r.TurnLimit)
        {
            s.Winner = GsState.WinnerStalemate;
        }
    }

    /// <summary>Runs AI-vs-AI to an end state (GS-05 soak).</summary>
    public static GsState Run(GsTheater t, GsRules r, ulong seed, Func<GsState, GsBattle, int>? resolver = null)
    {
        var s = Start(t, r, seed);
        var ai = Enumerable.Repeat(true, t.Factions.Length).ToArray();
        while (s.Winner == GsState.WinnerNone)
        {
            EndTurn(t, r, s, [], ai, resolver);
        }

        return s;
    }
}
