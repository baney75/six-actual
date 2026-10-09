using SixActual.Sim.State.Battle;

namespace SixActual.Sim.Battle;

/// <summary>
/// CMD-01..06: the XO and eight sub-commanders per player, their task queues, intents and support requests,
/// and an HTN-style planner that decomposes each seat's active task into unit orders once per plan tick.
///
/// Methods (one per task kind):
///   Attack   assemble at a staging point (>= assemble_pct of the fit force, or timeout) -> request preparatory
///            fires and wait for them to land (or timeout) -> assault in line; damaged units withdraw; a failing
///            attack escalates and asks the armor commander for help.
///   Defend   spread units along a line, each in the best cover near its slot; hold.
///   Hold     stop and hold where they are.
///   Recon    spread recon units over an area with return-fire ROE.
///   FirePlan artillery seats service approved fire requests and counter-battery fixes.
///   Resupply logistics seats send trucks to units that asked for ammunition or fuel.
///   Cap      air seats fly a racetrack orbit around a point; fighters engage enemy air in range.
///   Cas      air seats hold attack aircraft at a standby point; approved CAS requests (from army seats'
///            attacks) get the nearest free aircraft, which works the target area for cas_loiter_ticks and then
///            returns to base (one sortie per request); no CAS into a spotted enemy air-defence umbrella (SEAD first).
///   Interdict air seats strike spotted enemy movement inside a box behind the line, supply vehicles first.
/// The XO assigns tasks to idle seats from the standing intent when it has command (AI player, or the
/// player is flying an aircraft).
/// </summary>
public static class Command
{
    public const int Xo = 0;

    public const int Army1 = 1;

    public const int Army2 = 2;

    public const int Arty1 = 3;

    public const int Arty2 = 4;

    public const int Air1 = 5;

    public const int Air2 = 6;

    public const int Armor = 7;

    public const int Logistics = 8;

    public const int SeatsPerPlayer = 9;

    public static readonly string[] SeatNames = ["XO", "Army1", "Army2", "Arty1", "Arty2", "Air1", "Air2", "Armor", "Logistics"];

    public const int TaskAttack = 1;

    public const int TaskDefend = 2;

    public const int TaskHold = 3;

    public const int TaskRecon = 4;

    public const int TaskFirePlan = 5;

    public const int TaskResupply = 6;

    public const int TaskScreen = 7;

    public const int TaskCap = 8;

    public const int TaskCas = 9;

    public const int TaskInterdict = 10;

    public const int TaskSpearhead = 11;

    public const int TaskGarrison = 12;

    public static readonly string[] TaskNames = ["", "attack", "defend", "hold", "recon", "fire_plan", "resupply", "screen", "cap", "cas", "interdict", "spearhead", "garrison"];

    /// <summary>MEC-05 battle-planner orders build preparation: front (defend), offensive arrow (attack), spearhead, garrison.</summary>
    public static bool IsPlannerTask(int kind) => kind is TaskAttack or TaskDefend or TaskSpearhead or TaskGarrison;

    /// <summary>Full preparation in hundredths of a point (CommandTask.Prep runs 0..10000).</summary>
    public const int PrepFull = 10_000;

    public const int ReqFire = 1;

    public const int ReqCas = 2;

    public const int ReqResupply = 3;

    public const int ReqArmor = 4;

    public static int SeatIndex(int player, int role) => (player * SeatsPerPlayer) + role;

    public static int SeatRole(string name)
    {
        var i = Array.IndexOf(SeatNames, name);
        return i >= 0 ? i : throw new InvalidDataException($"Unknown commander seat {name}.");
    }

    public static int TaskKind(string name)
    {
        // Battle-planner names (MEC-05): a front is a defend line, an offensive arrow an attack.
        name = name switch { "front" => "defend", "offensive_arrow" => "attack", _ => name };
        var i = Array.IndexOf(TaskNames, name);
        return i > 0 ? i : throw new InvalidDataException($"Unknown task {name}.");
    }

    /// <summary>Creates nine seats per player with balanced default intent.</summary>
    public static void Init(WorldState w)
    {
        var players = Math.Max(w.Players.Length, 2);
        w.Seats = new SeatState[players * SeatsPerPlayer];
        for (var p = 0; p < players; p++)
        {
            for (var role = 0; role < SeatsPerPlayer; role++)
            {
                w.Seats[SeatIndex(p, role)] = new SeatState { Player = p, Role = role, Aggression = 50, CasualtyTolerance = 50, Roe = Combat.RoeFree };
            }
        }

        if (w.XoActive.Length < players)
        {
            Array.Resize(ref w.XoActive, players);
        }
    }

    public static void Assign(WorldState w, int unit, int seat)
    {
        if (unit < 0 || unit >= w.Units.Length)
        {
            return;
        }

        w.Units[unit].Seat = seat;
        if (seat >= 0 && seat < w.Seats.Length)
        {
            w.Units[unit].Roe = w.Seats[seat].Roe;
        }
    }

    public static void AddTask(WorldState w, int seat, int kind, int x, int y, int x2, int y2, int deadline) =>
        w.Tasks.Add(new CommandTask { Seat = seat, Kind = kind, X = x, Y = y, X2 = x2, Y2 = y2, Deadline = deadline, Status = 0 });

    public static void Step(WorldState w, BattleRules r)
    {
        if (w.Seats.Length == 0)
        {
            Init(w);
        }

        Victory(w, r);
        if (w.Tick % r.Command.PlanTicks != 0)
        {
            return;
        }

        for (var seat = 0; seat < w.Seats.Length; seat++)
        {
            var player = w.Seats[seat].Player;
            if (w.Seats[seat].Role == Xo)
            {
                if (player < w.XoActive.Length && w.XoActive[player] != 0)
                {
                    XoPlan(w, r, player);
                }

                continue;
            }

            Maintain(w, r, seat);
            switch (w.Seats[seat].Role)
            {
                case Arty1:
                case Arty2:
                    FirePlan(w, r, seat);
                    break;
                case Logistics:
                    ResupplyPlan(w, r, seat);
                    break;
                case Air1:
                case Air2:
                    CasPlan(w, r, seat);
                    break;
            }

            var t = ActiveTask(w, seat);
            if (t < 0)
            {
                continue;
            }

            var task = w.Tasks[t];
            if (IsPlannerTask(task.Kind) && task.Prep < PrepFull && r.Command.PlannerPrepTicks > 0)
            {
                // Preparation (hundredths) grows while the plan is left to its commander (MEC-05).
                task.Prep = Math.Min(PrepFull, task.Prep + Math.Max(1, PrepFull * r.Command.PlanTicks / r.Command.PlannerPrepTicks));
                w.Tasks[t] = task;
            }

            if (task.Deadline > 0 && w.Tick > task.Deadline && task.Kind == TaskAttack)
            {
                Finish(w, t, failed: true);
                continue;
            }

            switch (task.Kind)
            {
                case TaskAttack:
                    PlanAttack(w, r, seat, t);
                    break;
                case TaskDefend:
                    PlanDefend(w, r, seat, t);
                    break;
                case TaskHold:
                    PlanHold(w, seat, t);
                    break;
                case TaskRecon:
                    PlanRecon(w, r, seat, t);
                    break;
                case TaskScreen:
                    PlanScreen(w, r, seat, t);
                    break;
                case TaskCap:
                    PlanCap(w, r, seat, t);
                    break;
                case TaskCas:
                    PlanAirStandby(w, seat, t);
                    break;
                case TaskInterdict:
                    PlanInterdict(w, seat, t);
                    break;
                case TaskSpearhead:
                    PlanSpearhead(w, r, seat, t);
                    break;
                case TaskGarrison:
                    PlanGarrison(w, r, seat, t);
                    break;
            }
        }
    }

    /// <summary>The seat's first unfinished task, activated if it was queued; -1 if none.</summary>
    static int ActiveTask(WorldState w, int seat)
    {
        for (var i = 0; i < w.Tasks.Count; i++)
        {
            var t = w.Tasks[i];
            if (t.Seat != seat || t.Status >= 2)
            {
                continue;
            }

            if (t.Status == 0)
            {
                t.Status = 1;
                t.Step = 0;
                t.StepTick = w.Tick;
                w.Tasks[i] = t;
            }

            return i;
        }

        return -1;
    }

    static void Finish(WorldState w, int t, bool failed)
    {
        var task = w.Tasks[t];
        task.Status = failed ? 3 : 2;
        w.Tasks[t] = task;
        w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = failed ? BattleEvent.TaskFailed : BattleEvent.TaskDone, A = task.Seat, B = t, Value = task.Kind });
    }

    /// <summary>Per seat, every plan: damaged units withdraw, units low on ammunition ask logistics.</summary>
    static void Maintain(WorldState w, BattleRules r, int seat)
    {
        var tolerance = w.Seats[seat].CasualtyTolerance;

        // Casualty tolerance moves the withdrawal threshold: 0 -> 2x the base, 100 -> 0 (never withdraw).
        var threshold = r.Command.DamagedHpPct * (100 - tolerance) / 50;
        for (var i = 0; i < w.Units.Length; i++)
        {
            ref var u = ref w.Units[i];
            if (u.Seat != seat || u.Alive == 0 || u.MoraleState >= Morale.Routed)
            {
                continue;
            }

            var d = w.Defs[u.Def];
            if (u.WasDamaged == 0 && !d.IsAir && d.Supply == 0 && (u.Hp * 100) < (d.Hp * r.Command.DamagedHpPct))
            {
                u.WasDamaged = 1;
                w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Damaged, A = u.Id, B = seat });
            }

            if (u.Withdrawing == 0 && !d.IsAir && d.Supply == 0 && (u.Hp * 100) < (d.Hp * threshold))
            {
                u.Withdrawing = 1;
                u.HasGoal = 1;
                u.GoalX = Fp.FromInt(u.HomeX);
                u.GoalY = Fp.FromInt(u.HomeY);
                u.PathStep = -1;
                w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Withdraw, A = u.Id, B = seat });
                Request(w, seat, SeatIndex(u.Player, Logistics), ReqResupply, u.HomeX, u.HomeY, 1, i);
            }

            if (u.AskedResupply == 0 && d.Weapons.Length > 0 && !d.IsAir && d.Supply == 0)
            {
                var full = d.Weapons[0].Ammo;
                if (full > 0 && w.MountAmmo[u.MountStart] * 100 < full * r.Command.LowAmmoPct)
                {
                    u.AskedResupply = 1;
                    Request(w, seat, SeatIndex(u.Player, Logistics), ReqResupply, (int)(u.X.Raw >> Fp.FractionalBits), (int)(u.Y.Raw >> Fp.FractionalBits), 2, i);
                }
            }
        }
    }

    /// <summary>Files a support request; auto-approved unless the receiving seat asks the player first.</summary>
    public static int Request(WorldState w, int from, int to, int kind, int x, int y, int priority, int unit)
    {
        var id = w.NextRequestId++;
        var ask = to >= 0 && to < w.Seats.Length && w.Seats[to].ApprovalMode != 0;
        w.Requests.Add(new SupportRequest { Id = id, FromSeat = from, ToSeat = to, Kind = kind, X = x, Y = y, Priority = priority, Status = ask ? 0 : 1, Tick = w.Tick, Unit = unit, Assignee = -1 });
        w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.RequestMade, A = id, B = from, C = to, Value = kind });
        if (!ask)
        {
            w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.RequestApproved, A = id, B = from, C = to, Value = kind });
        }

        return id;
    }

    /// <summary>Player decision on a pending request (CMD-05). priority >= 0 also reprioritises it.</summary>
    public static void Decide(WorldState w, int requestId, bool approve, int priority)
    {
        for (var i = 0; i < w.Requests.Count; i++)
        {
            if (w.Requests[i].Id != requestId)
            {
                continue;
            }

            var q = w.Requests[i];
            if (priority >= 0)
            {
                q.Priority = priority;
            }

            if (q.Status == 0)
            {
                q.Status = approve ? 1 : 2;
                w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = approve ? BattleEvent.RequestApproved : BattleEvent.RequestDenied, A = q.Id, B = q.FromSeat, C = q.ToSeat, Value = q.Kind });
            }

            w.Requests[i] = q;
            return;
        }
    }

    // ------------------------------------------------------------------ attack

    static void PlanAttack(WorldState w, BattleRules r, int seat, int t)
    {
        var task = w.Tasks[t];
        var cr = r.Command;
        var fit = new List<int>();
        var all = 0;
        for (var i = 0; i < w.Units.Length; i++)
        {
            ref readonly var u = ref w.Units[i];
            if (u.Seat != seat)
            {
                continue;
            }

            all++;
            if (u.Alive != 0 && u.Withdrawing == 0 && u.MoraleState < Morale.Routed && !w.Defs[u.Def].IsAir)
            {
                fit.Add(i);
            }
        }

        if (fit.Count == 0)
        {
            if (all > 0)
            {
                Finish(w, t, failed: true);
            }

            return;
        }

        if (ObjectiveTaken(w, r, seat, task.X, task.Y))
        {
            Finish(w, t, failed: false);
            return;
        }

        var (cx, cy) = Centroid(w, fit);
        var aggression = w.Seats[seat].Aggression;
        switch (task.Step)
        {
            case 0:
                {
                    if (task.StepTick == w.Tick || (task.StagingX == 0 && task.StagingY == 0))
                    {
                        // Staging point short of the objective, nearer when aggressive.
                        var dist = cr.StagingDistanceM * (150 - aggression) / 100;
                        long dx = cx - task.X;
                        long dy = cy - task.Y;
                        var len = Math.Max(1, Sight.IntSqrt((dx * dx) + (dy * dy)));
                        if (len <= dist)
                        {
                            task.StagingX = cx;
                            task.StagingY = cy;
                        }
                        else
                        {
                            task.StagingX = task.X + (int)(dx * dist / len);
                            task.StagingY = task.Y + (int)(dy * dist / len);
                        }

                        Formation(w, r, fit, task.StagingX, task.StagingY, task.X, task.Y);
                    }

                    var assembled = 0;
                    foreach (var i in fit)
                    {
                        if (Near(w.Units[i], task.StagingX, task.StagingY, cr.AssembleRadiusM))
                        {
                            assembled++;
                        }
                    }

                    // Aggressive commanders go with less of the force assembled.
                    var needPct = Math.Clamp(cr.AssemblePct + ((50 - aggression) / 2), 40, 100);
                    if (assembled * 100 >= fit.Count * needPct || w.Tick - task.StepTick >= cr.AssembleTimeoutTicks)
                    {
                        // No gun in range but guns alive: bring them forward and hold the assault for them, up to a
                        // second assembly timeout (CMD-06: attacks go in behind fire support).
                        if (FireSupportSeat(w, seat, task.X, task.Y) < 0 && w.Tick - task.StepTick < 2 * cr.AssembleTimeoutTicks
                            && BringGuns(w, w.Seats[seat].Player, task.X, task.Y))
                        {
                            break;
                        }

                        task.Step = 1;
                        task.StepTick = w.Tick;
                        var arty = FireSupportSeat(w, seat, task.X, task.Y);
                        if (arty >= 0)
                        {
                            Request(w, seat, arty, ReqFire, task.X, task.Y, 1, -1);
                            task.FireRequestTick = w.Tick;
                        }
                        else
                        {
                            task.FireRequestTick = -1;
                        }

                        // Close air support on the objective, if an air seat has a free attack aircraft.
                        var air = AirSupportSeat(w, w.Seats[seat].Player);
                        if (air >= 0)
                        {
                            Request(w, seat, air, ReqCas, task.X, task.Y, 1, -1);
                        }
                    }

                    break;
                }

            case 1:
                {
                    var landed = task.FireRequestTick >= 0 && ImpactsNear(w, task.X, task.Y, cr.ObjectiveRadiusM * 2, task.FireRequestTick);
                    if (task.FireRequestTick < 0 || landed || w.Tick - task.StepTick >= cr.PrepWaitTicks)
                    {
                        task.Step = 2;
                        task.StepTick = w.Tick;
                        w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.AssaultStarted, A = seat, B = fit.Count, C = CountFit(w, seat), Value = landed ? 1 : 0 });
                        Formation(w, r, fit, task.X, task.Y, task.X + (task.X - cx), task.Y + (task.Y - cy));
                    }

                    break;
                }

            default:
                {
                    // Keep pushing units that stopped short; escalate if the attack is failing.
                    foreach (var i in fit)
                    {
                        if (w.Units[i].HasGoal == 0 && !Near(w.Units[i], task.X, task.Y, cr.ObjectiveRadiusM / 2))
                        {
                            Order(w, i, task.X, task.Y);
                        }
                    }

                    if (task.Escalated == 0 && fit.Count * 100 < all * cr.FailStrengthPct)
                    {
                        task.Escalated = 1;
                        w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Escalation, A = seat, Value = 1 });
                        var armor = SeatIndex(w.Seats[seat].Player, Armor);
                        if (armor != seat)
                        {
                            Request(w, seat, armor, ReqArmor, task.X, task.Y, 1, -1);
                            if (ActiveTask(w, armor) < 0)
                            {
                                AddTask(w, armor, TaskAttack, task.X, task.Y, 0, 0, 0);
                            }
                        }
                    }

                    break;
                }
        }

        w.Tasks[t] = task;
    }

    /// <summary>Line abreast across the approach axis (from -> toward), centred on (x, y).</summary>
    static void Formation(WorldState w, BattleRules r, List<int> units, int x, int y, int towardX, int towardY)
    {
        long ax = towardX - x;
        long ay = towardY - y;
        var len = Math.Max(1, Sight.IntSqrt((ax * ax) + (ay * ay)));

        // Perpendicular unit vector (scaled by 1000).
        var px = -ay * 1000 / len;
        var py = ax * 1000 / len;
        for (var k = 0; k < units.Count; k++)
        {
            var slot = (k - ((units.Count - 1) / 2)) * r.Command.FormationSpacingM;
            Order(w, units[k], x + (int)(px * slot / 1000), y + (int)(py * slot / 1000));
        }
    }

    static int FireSupportSeat(WorldState w, int seat, int x, int y)
    {
        var player = w.Seats[seat].Player;
        foreach (var role in new[] { Arty1, Arty2 })
        {
            var s = SeatIndex(player, role);
            for (var i = 0; i < w.Units.Length; i++)
            {
                if (w.Units[i].Seat == s && w.Units[i].Alive != 0 && InRange(w, i, x, y))
                {
                    return s;
                }
            }
        }

        return -1;
    }

    /// <summary>Orders the player's idle batteries that have ammunition but cannot reach (x, y) to a firing position
    /// 60% of their maximum range short of it; true if any battery is on its way or already moving.</summary>
    static bool BringGuns(WorldState w, int player, int x, int y)
    {
        var any = false;
        foreach (var role in new[] { Arty1, Arty2 })
        {
            var seat = SeatIndex(player, role);
            for (var b = 0; b < w.Units.Length; b++)
            {
                ref readonly var u = ref w.Units[b];
                var m = u.Seat == seat && u.Alive != 0 ? Fires.IndirectMount(w, b) : -1;
                if (m < 0 || w.MountAmmo[u.MountStart + m] <= 0 || (b < w.Missions.Length && w.Missions[b].Active != 0))
                {
                    continue;
                }

                var weapon = w.Weapons[w.Defs[u.Def].Weapons[m].Weapon];
                if (weapon.RangeMinM >= w.Terrain.SizeM)
                {
                    continue;
                }

                any = true;
                if (u.HasGoal != 0)
                {
                    continue;
                }

                long dx = (player < w.Players.Length ? w.Players[player].SpawnX : (int)(u.X.Raw >> Fp.FractionalBits)) - x;
                long dy = (player < w.Players.Length ? w.Players[player].SpawnY : (int)(u.Y.Raw >> Fp.FractionalBits)) - y;
                var len = Math.Max(1L, Sight.IntSqrt((dx * dx) + (dy * dy)));
                var stand = Math.Max(weapon.RangeMinM + 200L, weapon.RangeMaxM * 60L / 100);
                Order(w, b, (int)Math.Clamp(x + (dx * stand / len), 50, w.Terrain.SizeM - 50), (int)Math.Clamp(y + (dy * stand / len), 50, w.Terrain.SizeM - 50));
            }
        }

        return any;
    }

    /// <summary>Shoot and scoot: an idle battery that fired within the enemy's counter-battery locate time moves
    /// 300 m sideways, so the fix the enemy radar is building points at an empty position (CMD-06).</summary>
    static void Scoot(WorldState w, BattleRules r, List<int> batteries)
    {
        foreach (var b in batteries)
        {
            ref readonly var u = ref w.Units[b];
            var firedAt = u.RevealUntilTick - r.RevealOnFireTicks;
            if (u.HasGoal != 0 || u.RevealUntilTick <= 0 || w.Tick - firedAt > r.CbLocateTicks)
            {
                continue;
            }

            var side = ((b + (w.Tick / Math.Max(1, r.CbLocateTicks))) & 1) == 0 ? 300 : -300;
            var x = (int)Math.Clamp((u.X.Raw >> Fp.FractionalBits) + side, 50, w.Terrain.SizeM - 50);
            var y = (int)(u.Y.Raw >> Fp.FractionalBits);
            Order(w, b, x, y);
        }
    }

    static int BestPen(WorldState w, int def)
    {
        var best = 0;
        foreach (var m in w.Defs[def].Weapons)
        {
            var p = w.Weapons[m.Weapon].Pen;
            best = Math.Max(best, p.Length > 0 ? p[0][1] : 0);
        }

        return best;
    }

    /// <summary>False for an indirect-fire unit whose minimum range is at least the map's size (MLRS on an 8 km map):
    /// it could never fire, so the AI does not buy it.</summary>
    static bool UsableOnMap(WorldState w, int def)
    {
        var d = w.Defs[def];
        if (d.Kind != UnitDef.KindArtillery)
        {
            return true;
        }

        foreach (var m in d.Weapons)
        {
            var weapon = w.Weapons[m.Weapon];
            if (weapon.Indirect && weapon.RangeMinM < w.Terrain.SizeM)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Idle batteries that cannot reach the nearest objective their team does not hold move to a firing
    /// position 60% of their maximum range short of it, on the line back toward their own entry point, so attacks
    /// can get fire support (CMD-06).</summary>
    static void Displace(WorldState w, int seat, List<int> batteries)
    {
        if (w.Objectives.Length == 0)
        {
            return;
        }

        var player = w.Seats[seat].Player;
        var team = Frontline.TeamOf(w, player);
        foreach (var b in batteries)
        {
            ref readonly var u = ref w.Units[b];
            if (u.HasGoal != 0)
            {
                continue;
            }

            var bx = (int)(u.X.Raw >> Fp.FractionalBits);
            var by = (int)(u.Y.Raw >> Fp.FractionalBits);
            var o = NearestObjective(w, bx, by, team, held: false);
            if (o < 0 || InRange(w, b, w.Objectives[o].X, w.Objectives[o].Y))
            {
                continue;
            }

            var weapon = w.Weapons[w.Defs[u.Def].Weapons[Fires.IndirectMount(w, b)].Weapon];
            if (w.MountAmmo[u.MountStart + Fires.IndirectMount(w, b)] <= 0)
            {
                continue;
            }

            long ox = w.Objectives[o].X, oy = w.Objectives[o].Y;
            long dx = (player < w.Players.Length ? w.Players[player].SpawnX : bx) - ox;
            long dy = (player < w.Players.Length ? w.Players[player].SpawnY : by) - oy;
            var len = Math.Max(1L, Sight.IntSqrt((dx * dx) + (dy * dy)));
            var stand = Math.Max(weapon.RangeMinM + 200L, weapon.RangeMaxM * 60L / 100);
            var tx = (int)Math.Clamp(ox + (dx * stand / len), 50, w.Terrain.SizeM - 50);
            var ty = (int)Math.Clamp(oy + (dy * stand / len), 50, w.Terrain.SizeM - 50);
            Order(w, b, tx, ty);
        }
    }

    static bool InRange(WorldState w, int battery, int x, int y)
    {
        var m = Fires.IndirectMount(w, battery);
        if (m < 0)
        {
            return false;
        }

        var weapon = w.Weapons[w.Defs[w.Units[battery].Def].Weapons[m].Weapon];
        long dx = (w.Units[battery].X.Raw >> Fp.FractionalBits) - x;
        long dy = (w.Units[battery].Y.Raw >> Fp.FractionalBits) - y;
        var d = Sight.IntSqrt((dx * dx) + (dy * dy));
        return d >= weapon.RangeMinM && d <= weapon.RangeMaxM && w.MountAmmo[w.Units[battery].MountStart + m] > 0;
    }

    static bool ImpactsNear(WorldState w, int x, int y, int radius, int sinceTick)
    {
        for (var e = w.Events.Count - 1; e >= 0; e--)
        {
            var ev = w.Events[e];
            if (ev.Tick < sinceTick)
            {
                break;
            }

            if (ev.Kind == BattleEvent.Impact)
            {
                long dx = ev.A - x;
                long dy = ev.B - y;
                if ((dx * dx) + (dy * dy) <= (long)radius * radius)
                {
                    return true;
                }
            }
        }

        return false;
    }

    static bool ObjectiveTaken(WorldState w, BattleRules r, int seat, int x, int y)
    {
        var team = Frontline.TeamOf(w, w.Seats[seat].Player);
        var own = false;
        for (var i = 0; i < w.Units.Length; i++)
        {
            ref readonly var u = ref w.Units[i];
            if (u.Alive == 0 || w.Defs[u.Def].IsAir || !Near(u, x, y, r.Command.ObjectiveRadiusM))
            {
                continue;
            }

            if (Frontline.TeamOf(w, u.Player) == team)
            {
                own = true;
            }
            else if (u.MoraleState < Morale.Surrendered)
            {
                return false;
            }
        }

        return own;
    }

    /// <summary>Available assigned force: every living ground combat unit of the seat, including withdrawn or routed ones.</summary>
    static int CountFit(WorldState w, int seat)
    {
        var n = 0;
        foreach (var u in w.Units)
        {
            n += u.Seat == seat && u.Alive != 0 && !w.Defs[u.Def].IsAir && w.Defs[u.Def].Supply == 0 ? 1 : 0;
        }

        return n;
    }

    // ------------------------------------------------------------------ defend / hold / recon

    static void PlanDefend(WorldState w, BattleRules r, int seat, int t)
    {
        var task = w.Tasks[t];
        if (task.Step != 0)
        {
            return;
        }

        var units = SeatGroundUnits(w, seat);
        if (units.Count == 0)
        {
            return;
        }

        long lx = task.X2 - task.X;
        long ly = task.Y2 - task.Y;
        for (var k = 0; k < units.Count; k++)
        {
            // Slots evenly along the line, then the best cover within cover_search_m of the slot.
            var sx = task.X + (int)(lx * ((2 * k) + 1) / (2 * units.Count));
            var sy = task.Y + (int)(ly * ((2 * k) + 1) / (2 * units.Count));
            var (bx, by) = BestCover(w, r, sx, sy);
            Order(w, units[k], bx, by);
        }

        task.Step = 1;
        w.Tasks[t] = task;
    }

    static (int X, int Y) BestCover(WorldState w, BattleRules r, int x, int y)
    {
        var t = w.Terrain;
        var reach = r.Command.CoverSearchM / t.CellM;
        var cx = x / t.CellM;
        var cy = y / t.CellM;
        var best = (X: x, Y: y);
        var bestScore = 0;
        for (var dy = -reach; dy <= reach; dy++)
        {
            for (var dx = -reach; dx <= reach; dx++)
            {
                var gx = cx + dx;
                var gy = cy + dy;
                if (!t.InCell(gx, gy))
                {
                    continue;
                }

                var i = t.Index(gx, gy);
                if (t.Blocked[i] != 0)
                {
                    continue;
                }

                // Forest density, a building next door, or a rubble pile; prefer closer cells on ties.
                var score = t.Forest[i] + (AdjacentBuilding(t, gx, gy) ? 120 : 0) + (t.RubbleDm[i] > 0 ? 80 : 0);
                score = (score * 100) - (Math.Abs(dx) + Math.Abs(dy));
                if (score > bestScore)
                {
                    bestScore = score;
                    best = ((gx * t.CellM) + (t.CellM / 2), (gy * t.CellM) + (t.CellM / 2));
                }
            }
        }

        return best;
    }

    static bool AdjacentBuilding(TerrainGrid t, int x, int y)
    {
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (t.InCell(x + dx, y + dy) && t.BuildingAt[t.Index(x + dx, y + dy)] != 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    static void PlanHold(WorldState w, int seat, int t)
    {
        var task = w.Tasks[t];
        if (task.Step != 0)
        {
            return;
        }

        foreach (var i in SeatGroundUnits(w, seat))
        {
            w.Units[i].HasGoal = 0;
        }

        task.Step = 1;
        w.Tasks[t] = task;
    }

    static void PlanRecon(WorldState w, BattleRules r, int seat, int t)
    {
        var task = w.Tasks[t];
        if (task.Step != 0)
        {
            return;
        }

        var units = SeatGroundUnits(w, seat);
        foreach (var i in units)
        {
            w.Units[i].Roe = Combat.RoeReturn;
        }

        Formation(w, r, units, task.X, task.Y, task.X, task.Y + 1);
        task.Step = 1;
        w.Tasks[t] = task;
    }

    /// <summary>Screen: observation posts spread along the line (X,Y)-(X2,Y2) with return-fire ROE; a post that
    /// is engaged (hit or fired on) and is not already moving falls back screen_withdraw_m straight away from the
    /// nearest enemy its side can see.</summary>
    static void PlanScreen(WorldState w, BattleRules r, int seat, int t)
    {
        var task = w.Tasks[t];
        var units = SeatGroundUnits(w, seat);
        if (task.Step == 0)
        {
            long lx = task.X2 - task.X;
            long ly = task.Y2 - task.Y;
            for (var k = 0; k < units.Count; k++)
            {
                w.Units[units[k]].Roe = Combat.RoeReturn;
                Order(w, units[k], task.X + (int)(lx * ((2 * k) + 1) / (2 * Math.Max(1, units.Count))), task.Y + (int)(ly * ((2 * k) + 1) / (2 * Math.Max(1, units.Count))));
            }

            task.Step = 1;
            w.Tasks[t] = task;
            return;
        }

        var player = w.Seats[seat].Player;
        foreach (var i in units)
        {
            ref readonly var u = ref w.Units[i];
            if (u.HasGoal != 0 || u.LastHitByTick == 0 || w.Tick - u.LastHitByTick > r.Command.ScreenContactTicks)
            {
                continue;
            }

            // Engaged (hit or fired on): fall back away from the nearest enemy this side can see.
            var a = -1;
            var bestD = long.MaxValue;
            for (var j = 0; j < w.Units.Length; j++)
            {
                if (w.Units[j].Alive == 0 || w.Units[j].Player == player || !Sight.Visible(w, player, j))
                {
                    continue;
                }

                long ex = (w.Units[j].X.Raw - u.X.Raw) >> Fp.FractionalBits;
                long ey = (w.Units[j].Y.Raw - u.Y.Raw) >> Fp.FractionalBits;
                if ((ex * ex) + (ey * ey) < bestD)
                {
                    bestD = (ex * ex) + (ey * ey);
                    a = j;
                }
            }

            if (a < 0)
            {
                continue;
            }

            long dx = (u.X.Raw - w.Units[a].X.Raw) >> Fp.FractionalBits;
            long dy = (u.Y.Raw - w.Units[a].Y.Raw) >> Fp.FractionalBits;
            var len = Math.Max(1L, (long)Sight.IntSqrt((dx * dx) + (dy * dy)));
            var ux = (int)(u.X.Raw >> Fp.FractionalBits) + (int)(dx * r.Command.ScreenWithdrawM / len);
            var uy = (int)(u.Y.Raw >> Fp.FractionalBits) + (int)(dy * r.Command.ScreenWithdrawM / len);
            Order(w, i, ux, uy);
        }
    }

    // ------------------------------------------------------------------ artillery and logistics

    /// <summary>Artillery seat: approved fire requests (highest priority first), then counter-battery fixes.</summary>
    static void FirePlan(WorldState w, BattleRules r, int seat)
    {
        var batteries = new List<int>();
        for (var i = 0; i < w.Units.Length; i++)
        {
            if (w.Units[i].Seat == seat && w.Units[i].Alive != 0 && Fires.IndirectMount(w, i) >= 0
                && (i >= w.Missions.Length || w.Missions[i].Active == 0))
            {
                batteries.Add(i);
            }
        }

        if (batteries.Count == 0)
        {
            return;
        }

        var order = Enumerable.Range(0, w.Requests.Count)
            .Where(k => w.Requests[k].ToSeat == seat && w.Requests[k].Kind == ReqFire && w.Requests[k].Status == 1)
            .OrderBy(k => w.Requests[k].Priority).ThenBy(k => w.Requests[k].Id).ToList();
        foreach (var k in order)
        {
            var q = w.Requests[k];
            var fired = false;
            foreach (var b in batteries.ToList())
            {
                if (InRange(w, b, q.X, q.Y) && Fires.Order(w, r, b, q.X, q.Y, r.Command.PrepRounds, Fires.KindHe))
                {
                    batteries.Remove(b);
                    fired = true;
                }
            }

            if (fired)
            {
                q.Status = 3;
                w.Requests[k] = q;
            }

            if (batteries.Count == 0)
            {
                return;
            }
        }

        Scoot(w, r, batteries);
        Displace(w, seat, batteries);

        // Counter-battery: engage the newest fix this team holds, if the seat auto-approves.
        if (w.Seats[seat].ApprovalMode != 0)
        {
            return;
        }

        var team = Frontline.TeamOf(w, w.Seats[seat].Player);
        for (var f = w.Fixes.Count - 1; f >= 0; f--)
        {
            var fix = w.Fixes[f];
            if (fix.Active == 0 || fix.Team != team || w.Units[fix.Battery].Alive == 0)
            {
                continue;
            }

            foreach (var b in batteries.ToList())
            {
                // Counter-battery only from batteries holding three preparation missions' worth of rounds: the rest
                // is kept for fires in support of an assault (CMD-06).
                var cm = Fires.IndirectMount(w, b);
                if (w.MountAmmo[w.Units[b].MountStart + cm] < 3 * r.Command.PrepRounds)
                {
                    continue;
                }

                if (InRange(w, b, fix.X, fix.Y) && Fires.Order(w, r, b, fix.X, fix.Y, r.Command.PrepRounds, Fires.KindHe))
                {
                    batteries.Remove(b);
                }
            }

            fix.Active = 0;
            w.Fixes[f] = fix;
            return;
        }
    }

    // ------------------------------------------------------------------ battle planner (MEC-05)

    /// <summary>Hit-chance bonus (%) for a seat's units from its active planner order's preparation.</summary>
    public static int PrepBonusPct(WorldState w, BattleRules r, int seat)
    {
        if (seat < 0)
        {
            return 0;
        }

        var t = ActiveTaskIndex(w, seat);
        return t >= 0 && IsPlannerTask(w.Tasks[t].Kind) ? w.Tasks[t].Prep * r.Command.PlannerPrepBonusPct / PrepFull : 0;
    }

    /// <summary>A direct order to a unit under a planner order is micromanagement: the plan loses preparation.</summary>
    public static void OnDirectOrder(WorldState w, BattleRules r, int unit)
    {
        var seat = w.Units[unit].Seat;
        if (seat < 0 || seat >= w.Seats.Length)
        {
            return;
        }

        var t = ActiveTaskIndex(w, seat);
        if (t < 0 || !IsPlannerTask(w.Tasks[t].Kind))
        {
            return;
        }

        var task = w.Tasks[t];
        task.Prep = Math.Max(0, task.Prep - (r.Command.PlannerMicroDecay * PrepFull / 100));
        w.Tasks[t] = task;
        w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.PrepLost, A = seat, Value = task.Prep });
    }

    /// <summary>Spearhead: a narrow column with armour leading, assembled short of the objective, then driven through
    /// it without waiting for preparatory fires.</summary>
    static void PlanSpearhead(WorldState w, BattleRules r, int seat, int t)
    {
        var task = w.Tasks[t];
        var units = SeatGroundUnits(w, seat);
        if (units.Count == 0)
        {
            return;
        }

        // Tanks lead, then infantry fighting vehicles, then the rest.
        units.Sort((a, b) =>
        {
            int Rank(int i) => w.Defs[w.Units[i].Def].Kind switch { UnitDef.KindTank => 0, UnitDef.KindIfv => 1, _ => 2 };
            var c = Rank(a).CompareTo(Rank(b));
            return c != 0 ? c : a.CompareTo(b);
        });
        var (cx, cy) = Centroid(w, units);
        long dx = task.X - cx;
        long dy = task.Y - cy;
        var len = Math.Max(1L, (long)Sight.IntSqrt((dx * dx) + (dy * dy)));
        if (task.Step == 0)
        {
            var dist = Math.Min(len, (long)r.Command.StagingDistanceM);
            task.StagingX = task.X - (int)(dx * dist / len);
            task.StagingY = task.Y - (int)(dy * dist / len);
            Column(w, r, units, task.StagingX, task.StagingY, dx, dy, len);
            task.Step = 1;
            task.StepTick = w.Tick;
        }
        else if (task.Step == 1)
        {
            var near = 0;
            foreach (var i in units)
            {
                near += Near(w.Units[i], task.StagingX, task.StagingY, r.Command.AssembleRadiusM + (units.Count * r.Command.FormationSpacingM)) ? 1 : 0;
            }

            if (near * 100 >= units.Count * 60 || w.Tick - task.StepTick >= r.Command.AssembleTimeoutTicks)
            {
                // Drive through: the column's head aims past the objective.
                var tx = task.X + (int)(dx * r.Command.ObjectiveRadiusM / len);
                var ty = task.Y + (int)(dy * r.Command.ObjectiveRadiusM / len);
                Column(w, r, units, tx, ty, dx, dy, len);
                task.Step = 2;
                task.StepTick = w.Tick;
                w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.AssaultStarted, A = seat, B = units.Count, C = units.Count, Value = 0 });
            }
        }

        w.Tasks[t] = task;
    }

    /// <summary>Column slots: unit k stands k * formation_spacing_m behind (x, y) along the axis (dx, dy).</summary>
    static void Column(WorldState w, BattleRules r, List<int> units, int x, int y, long dx, long dy, long len)
    {
        for (var k = 0; k < units.Count; k++)
        {
            long back = (long)k * r.Command.FormationSpacingM;
            Order(w, units[k], x - (int)(dx * back / len), y - (int)(dy * back / len));
        }
    }

    /// <summary>Garrison: the seat's infantry occupy distinct ground-floor building chunks within planner_garrison_radius_m
    /// of the point, nearest first; vehicles and any infantry left over hold at the point.</summary>
    static void PlanGarrison(WorldState w, BattleRules r, int seat, int t)
    {
        var task = w.Tasks[t];
        if (task.Step != 0)
        {
            return;
        }

        var units = SeatGroundUnits(w, seat);
        units.Sort((a, b) =>
        {
            var c = (w.Defs[w.Units[a].Def].IsInfantry ? 0 : 1).CompareTo(w.Defs[w.Units[b].Def].IsInfantry ? 0 : 1);
            return c != 0 ? c : a.CompareTo(b);
        });
        var cells = new List<(long D, int X, int Y)>();
        var radius = (long)r.Command.PlannerGarrisonRadiusM;
        foreach (var b in w.Buildings)
        {
            for (var ix = 0; ix < b.Nx; ix++)
            {
                for (var iy = 0; iy < b.Ny; iy++)
                {
                    if (!Structures.Exists(w, b, ix, iy, 0))
                    {
                        continue;
                    }

                    var x = b.X + (ix * r.ChunkM) + (r.ChunkM / 2);
                    var y = b.Y + (iy * r.ChunkM) + (r.ChunkM / 2);
                    long ddx = x - task.X;
                    long ddy = y - task.Y;
                    var d = (ddx * ddx) + (ddy * ddy);
                    if (d <= radius * radius)
                    {
                        cells.Add((d, x, y));
                    }
                }
            }
        }

        cells.Sort((a, b) => a.D != b.D ? a.D.CompareTo(b.D) : a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
        var cell = 0;
        foreach (var u in units)
        {
            // Infantry take the building chunks; vehicles (and infantry left over) hold at the point.
            if (w.Defs[w.Units[u].Def].IsInfantry && cell < cells.Count)
            {
                Order(w, u, cells[cell].X, cells[cell].Y);
                cell++;
            }
            else
            {
                Order(w, u, task.X, task.Y);
            }
        }

        task.Step = 1;
        w.Tasks[t] = task;
    }

    // ------------------------------------------------------------------ air

    /// <summary>An on-map aircraft of this seat that is not working a CAS request.</summary>
    static bool FreeAircraft(WorldState w, int seat, int i)
    {
        ref readonly var u = ref w.Units[i];
        if (u.Seat != seat || u.Alive == 0 || u.OffMapUntil > 0 || u.SortieDone != 0 || u.Flown != 0 || !w.Defs[u.Def].IsAir)
        {
            return false;
        }

        foreach (var q in w.Requests)
        {
            if (q.Kind == ReqCas && q.Status == 1 && q.Assignee == i)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>True if the unit has rounds left in a weapon that can hit ground targets.</summary>
    static bool HasGroundWeapon(WorldState w, int i)
    {
        var def = w.Defs[w.Units[i].Def];
        for (var m = 0; m < def.Weapons.Length; m++)
        {
            if (w.Weapons[def.Weapons[m].Weapon].HitsGround && w.MountAmmo[w.Units[i].MountStart + m] > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The player's air seat on CAS standby ("cas" task) with a free attack aircraft; -1 if none.</summary>
    static int AirSupportSeat(WorldState w, int player)
    {
        var fallback = -1;
        foreach (var role in new[] { Air1, Air2 })
        {
            var seat = SeatIndex(player, role);
            if (seat >= w.Seats.Length)
            {
                continue;
            }

            var has = false;
            for (var i = 0; i < w.Units.Length && !has; i++)
            {
                has = FreeAircraft(w, seat, i) && HasGroundWeapon(w, i);
            }

            if (!has)
            {
                continue;
            }

            var t = ActiveTaskIndex(w, seat);
            if (t >= 0 && w.Tasks[t].Kind == TaskCas)
            {
                return seat;
            }
        }

        // CAS on request needs an air seat tasked with "cas"; other air tasks (CAP, interdiction) do not answer.
        return fallback;
    }

    /// <summary>Like ActiveTask but without activating a queued task.</summary>
    static int ActiveTaskIndex(WorldState w, int seat)
    {
        for (var i = 0; i < w.Tasks.Count; i++)
        {
            if (w.Tasks[i].Seat == seat && w.Tasks[i].Status < 2)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Air seat: dispatch the nearest free attack aircraft to each approved CAS request and hold it on
    /// station for cas_loiter_ticks; a lost or off-map aircraft is replaced.</summary>
    static void CasPlan(WorldState w, BattleRules r, int seat)
    {
        if (w.AirGrounded != 0)
        {
            // SIM-11: fog has closed the airfields; requests wait.
            return;
        }

        for (var k = 0; k < w.Requests.Count; k++)
        {
            var q = w.Requests[k];
            if (q.ToSeat != seat || q.Kind != ReqCas || q.Status != 1)
            {
                continue;
            }

            if (q.Assignee >= 0)
            {
                ref readonly var a = ref w.Units[q.Assignee];
                if (a.Alive == 0 || a.OffMapUntil > 0)
                {
                    q.Assignee = -1;
                    q.OnStationTick = 0;
                }
                else if (q.OnStationTick == 0 && Near(a, q.X, q.Y, r.Command.CasOnStationM))
                {
                    q.OnStationTick = w.Tick;
                    w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.CasOnStation, A = q.Id, B = a.Id });
                }
                else if (q.OnStationTick > 0 && w.Tick - q.OnStationTick >= r.Command.CasLoiterTicks)
                {
                    // Sortie flown: the aircraft returns to base and turns around off-map (one sortie per request).
                    q.Status = 3;
                    w.Units[q.Assignee].SortieDone = 1;
                    w.Units[q.Assignee].HasGoal = 0;
                }
                else if (q.OnStationTick == 0)
                {
                    Order(w, q.Assignee, q.X, q.Y);
                }

                w.Requests[k] = q;
                continue;
            }

            // Air defence first: no CAS into a target area covered by spotted enemy air defence. The request waits,
            // and the air seat asks artillery to suppress the threat (SEAD by fire).
            var threat = AirThreatNear(w, w.Seats[seat].Player, q.X, q.Y, r.Command.CasThreatMarginM);
            if (threat >= 0)
            {
                RequestSead(w, seat, threat);
                continue;
            }

            var best = -1;
            var bestD = long.MaxValue;
            for (var i = 0; i < w.Units.Length; i++)
            {
                if (!FreeAircraft(w, seat, i) || !HasGroundWeapon(w, i))
                {
                    continue;
                }

                long dx = (w.Units[i].X.Raw >> Fp.FractionalBits) - q.X;
                long dy = (w.Units[i].Y.Raw >> Fp.FractionalBits) - q.Y;
                var d = (dx * dx) + (dy * dy);
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }

            if (best >= 0)
            {
                q.Assignee = best;
                Order(w, best, q.X, q.Y);
                w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.CasDispatched, A = q.Id, B = w.Units[best].Id });
                w.Requests[k] = q;
            }
        }
    }

    /// <summary>A living, spotted enemy ground unit with a loaded anti-air weapon whose reach (+ margin) covers (x, y); -1 if none.</summary>
    static int AirThreatNear(WorldState w, int player, int x, int y, int marginM)
    {
        for (var j = 0; j < w.Units.Length; j++)
        {
            ref readonly var e = ref w.Units[j];
            var def = w.Defs[e.Def];
            if (e.Alive == 0 || e.Player == player || def.IsAir || !Sight.Visible(w, player, j))
            {
                continue;
            }

            var reach = 0;
            for (var m = 0; m < def.Weapons.Length; m++)
            {
                var wd = w.Weapons[def.Weapons[m].Weapon];
                if (wd.HitsAir && w.MountAmmo[e.MountStart + m] > 0)
                {
                    reach = Math.Max(reach, wd.RangeMaxM);
                }
            }

            if (reach > 0 && Near(e, x, y, reach + marginM))
            {
                return j;
            }
        }

        return -1;
    }

    /// <summary>Asks an artillery seat to fire on an air-defence unit, unless a fire request on that spot is already open.</summary>
    static void RequestSead(WorldState w, int seat, int threat)
    {
        var tx = (int)(w.Units[threat].X.Raw >> Fp.FractionalBits);
        var ty = (int)(w.Units[threat].Y.Raw >> Fp.FractionalBits);
        foreach (var q in w.Requests)
        {
            if (q.Kind == ReqFire && q.Status < 2 && Math.Abs(q.X - tx) < 300 && Math.Abs(q.Y - ty) < 300)
            {
                return;
            }
        }

        var arty = FireSupportSeat(w, seat, tx, ty);
        if (arty >= 0)
        {
            Request(w, seat, arty, ReqFire, tx, ty, 0, -1);
        }
    }

    /// <summary>CAP: free aircraft fly a four-point racetrack around the task point, staggered per aircraft.</summary>
    static void PlanCap(WorldState w, BattleRules r, int seat, int t)
    {
        var task = w.Tasks[t];
        var n = 0;
        for (var i = 0; i < w.Units.Length; i++)
        {
            if (!FreeAircraft(w, seat, i))
            {
                continue;
            }

            var leg = ((w.Tick / Math.Max(1, r.Command.CapLegTicks)) + n++) % 4;
            var rad = r.Command.CapOrbitM;
            var (ox, oy) = leg switch { 0 => (rad, 0), 1 => (0, rad), 2 => (-rad, 0), _ => (0, -rad) };
            Order(w, i, task.X + ox, task.Y + oy);
        }
    }

    /// <summary>CAS standby: free aircraft hold at the task point, waiting for requests.</summary>
    static void PlanAirStandby(WorldState w, int seat, int t)
    {
        var task = w.Tasks[t];
        for (var i = 0; i < w.Units.Length; i++)
        {
            if (FreeAircraft(w, seat, i) && !Near(w.Units[i], task.X, task.Y, 200))
            {
                Order(w, i, task.X, task.Y);
            }
        }
    }

    /// <summary>Interdiction: free attack aircraft go after spotted enemy units inside the box (X,Y)-(X2,Y2),
    /// supply vehicles first, then the nearest; with nothing spotted they sweep to the box centre.</summary>
    static void PlanInterdict(WorldState w, int seat, int t)
    {
        var task = w.Tasks[t];
        var player = w.Seats[seat].Player;
        int x0 = Math.Min(task.X, task.X2), x1 = Math.Max(task.X, task.X2), y0 = Math.Min(task.Y, task.Y2), y1 = Math.Max(task.Y, task.Y2);
        for (var i = 0; i < w.Units.Length; i++)
        {
            if (!FreeAircraft(w, seat, i) || !HasGroundWeapon(w, i))
            {
                continue;
            }

            var best = -1;
            var bestScore = long.MaxValue;
            for (var j = 0; j < w.Units.Length; j++)
            {
                ref readonly var e = ref w.Units[j];
                if (e.Alive == 0 || e.Player == player || w.Defs[e.Def].IsAir || !Sight.Visible(w, player, j))
                {
                    continue;
                }

                var ex = (int)(e.X.Raw >> Fp.FractionalBits);
                var ey = (int)(e.Y.Raw >> Fp.FractionalBits);
                if (ex < x0 || ex > x1 || ey < y0 || ey > y1)
                {
                    continue;
                }

                long dx = (w.Units[i].X.Raw >> Fp.FractionalBits) - ex;
                long dy = (w.Units[i].Y.Raw >> Fp.FractionalBits) - ey;
                var score = (dx * dx) + (dy * dy) - (w.Defs[e.Def].Supply > 0 ? 1L << 40 : 0);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = j;
                }
            }

            if (best >= 0)
            {
                Order(w, i, (int)(w.Units[best].X.Raw >> Fp.FractionalBits), (int)(w.Units[best].Y.Raw >> Fp.FractionalBits));
            }
            else if (!Near(w.Units[i], (x0 + x1) / 2, (y0 + y1) / 2, 300))
            {
                Order(w, i, (x0 + x1) / 2, (y0 + y1) / 2);
            }
        }
    }

    /// <summary>Logistics seat: send the nearest free truck to each approved resupply request; bring it back when done.</summary>
    static void ResupplyPlan(WorldState w, BattleRules r, int seat)
    {
        for (var k = 0; k < w.Requests.Count; k++)
        {
            var q = w.Requests[k];
            if (q.ToSeat != seat || q.Kind != ReqResupply || q.Status != 1)
            {
                continue;
            }

            if (q.Assignee >= 0)
            {
                ref var truck = ref w.Units[q.Assignee];
                var target = q.Unit >= 0 ? w.Units[q.Unit] : default;
                var satisfied = q.Unit < 0 || target.Alive == 0 || Full(w, q.Unit);
                if (truck.Alive == 0 || truck.SupplyLeft == 0 || satisfied)
                {
                    q.Status = 3;
                    if (truck.Alive != 0)
                    {
                        truck.HasGoal = 1;
                        truck.GoalX = Fp.FromInt(truck.HomeX);
                        truck.GoalY = Fp.FromInt(truck.HomeY);
                        truck.PathStep = -1;
                    }

                    if (q.Unit >= 0)
                    {
                        // Ammunition is back; a withdrawn (damaged) unit stays out until repaired (SUP-03).
                        w.Units[q.Unit].AskedResupply = 0;
                    }
                }
                else if (q.Unit >= 0 && !Near(truck, (int)(target.X.Raw >> Fp.FractionalBits), (int)(target.Y.Raw >> Fp.FractionalBits), r.SupplyRadiusM / 2))
                {
                    Order(w, q.Assignee, (int)(target.X.Raw >> Fp.FractionalBits) + 10, (int)(target.Y.Raw >> Fp.FractionalBits));
                }

                w.Requests[k] = q;
                continue;
            }

            var best = -1;
            long bestD = long.MaxValue;
            for (var i = 0; i < w.Units.Length; i++)
            {
                ref readonly var u = ref w.Units[i];
                if (u.Seat != seat || u.Alive == 0 || u.SupplyLeft <= 0 || Busy(w, i))
                {
                    continue;
                }

                long dx = (u.X.Raw >> Fp.FractionalBits) - q.X;
                long dy = (u.Y.Raw >> Fp.FractionalBits) - q.Y;
                if ((dx * dx) + (dy * dy) < bestD)
                {
                    bestD = (dx * dx) + (dy * dy);
                    best = i;
                }
            }

            if (best >= 0)
            {
                q.Assignee = best;
                Order(w, best, q.X + 10, q.Y);
                w.Requests[k] = q;
            }
        }
    }

    static bool Busy(WorldState w, int truck)
    {
        foreach (var q in w.Requests)
        {
            if (q.Assignee == truck && q.Status == 1)
            {
                return true;
            }
        }

        return false;
    }

    static bool Full(WorldState w, int unit)
    {
        var d = w.Defs[w.Units[unit].Def];
        for (var m = 0; m < d.Weapons.Length; m++)
        {
            if (w.MountAmmo[w.Units[unit].MountStart + m] < d.Weapons[m].Ammo)
            {
                return false;
            }
        }

        return w.Units[unit].Fuel >= d.Fuel;
    }

    // ------------------------------------------------------------------ XO

    /// <summary>
    /// The XO runs the battle on standing intent: units without a seat are given one by type, and every idle
    /// manoeuvre seat gets a task: attack the nearest objective it does not hold when the XO's aggression is
    /// at least 50, otherwise defend the line through the nearest objective it does hold.
    /// </summary>
    static void XoPlan(WorldState w, BattleRules r, int player)
    {
        AiDeploy(w, player);
        var xo = w.Seats[SeatIndex(player, Xo)];
        var armyToggle = 0;
        for (var i = 0; i < w.Units.Length; i++)
        {
            ref var u = ref w.Units[i];
            if (u.Player != player || u.Alive == 0 || u.Seat >= 0)
            {
                continue;
            }

            var d = w.Defs[u.Def];
            var role = d.Kind switch
            {
                UnitDef.KindTank => Armor,
                UnitDef.KindArtillery => Fires.IndirectMount(w, i) >= 0 ? (armyToggle++ % 2 == 0 ? Arty1 : Arty2) : Army1,
                UnitDef.KindAir => armyToggle++ % 2 == 0 ? Air1 : Air2,
                UnitDef.KindSupply => Logistics,
                _ => armyToggle++ % 2 == 0 ? Army1 : Army2,
            };
            Assign(w, i, SeatIndex(player, role));
        }

        // Air seats with aircraft and no task stand by for close air support a quarter of the way from the entry
        // point to the map centre, so assaults can call them (CMD-04, W6).
        foreach (var role in new[] { Air1, Air2 })
        {
            var seat = SeatIndex(player, role);
            if (ActiveTask(w, seat) >= 0 || player >= w.Players.Length)
            {
                continue;
            }

            var hasAir = false;
            for (var i = 0; i < w.Units.Length && !hasAir; i++)
            {
                hasAir = w.Units[i].Seat == seat && w.Units[i].Alive != 0 && w.Defs[w.Units[i].Def].IsAir;
            }

            if (hasAir)
            {
                var c = w.Terrain.SizeM / 2;
                var sx = w.Players[player].SpawnX;
                var sy = w.Players[player].SpawnY;
                AddTask(w, seat, TaskCas, sx + ((c - sx) / 4), sy + ((c - sy) / 4), 0, 0, 0);
            }
        }

        var team = Frontline.TeamOf(w, player);
        foreach (var role in new[] { Army1, Army2, Armor })
        {
            var seat = SeatIndex(player, role);
            if (ActiveTask(w, seat) >= 0 || SeatGroundUnits(w, seat).Count == 0)
            {
                continue;
            }

            var units = SeatGroundUnits(w, seat);
            var (cx, cy) = Centroid(w, units);
            var target = NearestObjective(w, cx, cy, team, held: xo.Aggression < 50);
            if (target < 0)
            {
                target = NearestObjective(w, cx, cy, team, held: xo.Aggression >= 50);
            }

            if (target < 0)
            {
                continue;
            }

            var o = w.Objectives[target];
            if (xo.Aggression >= 50 && o.Owner != team)
            {
                AddTask(w, seat, TaskAttack, o.X, o.Y, 0, 0, 0);
            }
            else
            {
                // Defend a 600 m line across the approach to the objective.
                AddTask(w, seat, TaskDefend, o.X - 300, o.Y, o.X + 300, o.Y, 0);
            }
        }
    }

    /// <summary>Target share of the force by sim kind (percent of deployment points), for the AI's card choice.</summary>
    static readonly int[] MixPct = [0, 22, 24, 22, 12, 6, 6, 6, 2];

    /// <summary>Below this many manoeuvre units (infantry, tanks, IFVs) alive, the AI buys only manoeuvre units (and
    /// its first two guns, air defence while enemy aircraft are in sight, and aircraft for a maverick deck): objectives
    /// are held by units that can take them (MEC-09).</summary>
    const int MinManoeuvre = 12;

    /// <summary>
    /// AI spending (MEC-10): pick the legal, in-stock card whose kind is furthest below its target share of points
    /// spent so far (ties go to the cheaper card, then the lower card index) and call it when affordable. When that
    /// card is not yet affordable the AI saves for it instead of buying cheaper cards, so expensive phase-B and
    /// phase-C cards (tanks, aircraft) are bought at all (MEC-09). With fewer than two guns alive, an artillery card comes first; AA cards wait until an
    /// enemy aircraft is visible; below MinManoeuvre manoeuvre units only manoeuvre cards are bought.
    /// </summary>
    static void AiDeploy(WorldState w, int player)
    {
        if (player >= w.Players.Length || w.Players[player].Deck < 0)
        {
            return;
        }

        var deck = w.Decks[w.Players[player].Deck];
        var spentByKind = new long[MixPct.Length];
        long spent = 0;
        var guns = 0;
        var manoeuvre = 0;
        var aaAlive = 0;
        foreach (var u in w.Units)
        {
            if (u.Player == player && u.Cost > 0)
            {
                spentByKind[Math.Clamp(w.Defs[u.Def].Kind, 0, MixPct.Length - 1)] += u.Cost;
                spent += u.Cost;
            }

            if (u.Player == player && u.Alive != 0 && w.Defs[u.Def].Kind == UnitDef.KindArtillery)
            {
                guns++;
            }

            if (u.Player == player && u.Alive != 0 && w.Defs[u.Def].Kind is UnitDef.KindInfantry or UnitDef.KindTank or UnitDef.KindIfv)
            {
                manoeuvre++;
            }

            if (u.Player == player && u.Alive != 0 && w.Defs[u.Def].Kind == UnitDef.KindAa)
            {
                aaAlive++;
            }
        }

        // Air defence is bought once enemy aircraft have been seen, not against an empty sky; when the enemy in
        // sight is mostly armour, the anti-armour card of each kind is preferred (MEC-09).
        var airThreat = false;
        int seenArmor = 0, seenSoft = 0;
        for (var j = 0; j < w.Units.Length; j++)
        {
            ref readonly var e = ref w.Units[j];
            if (e.Alive == 0 || e.Player == player || !Sight.Visible(w, player, j))
            {
                continue;
            }

            var ek = w.Defs[e.Def].Kind;
            airThreat |= ek == UnitDef.KindAir;
            seenArmor += ek is UnitDef.KindTank or UnitDef.KindIfv ? 1 : 0;
            seenSoft += ek == UnitDef.KindInfantry ? 1 : 0;
        }

        var antiArmor = seenArmor > 0 && seenArmor >= 2 * seenSoft;

        // A maverick (air-cavalry / air-assault) deck fights with its aircraft: they are not held back by the floor.
        var airDoctrine = deck.Income == "maverick";

        for (var guard = 0; guard < 8; guard++)
        {
            var best = -1;
            long bestNeed = long.MinValue;
            for (var c = 0; c < deck.Cards.Length; c++)
            {
                var card = deck.Cards[c];
                if (card.Phase > w.Phase || w.CardStock[w.Players[player].StockStart + c] <= 0 || !UsableOnMap(w, card.Unit) || (!airThreat && w.Defs[card.Unit].Kind == UnitDef.KindAa) || (w.AirGrounded != 0 && w.Defs[card.Unit].IsAir)
                    || (manoeuvre < MinManoeuvre && !(w.Defs[card.Unit].Kind is UnitDef.KindInfantry or UnitDef.KindTank or UnitDef.KindIfv)
                        && !(guns < 2 && w.Defs[card.Unit].Kind == UnitDef.KindArtillery)
                        && !(airThreat && w.Defs[card.Unit].Kind == UnitDef.KindAa)
                        && !(airDoctrine && w.Defs[card.Unit].IsAir)))
                {
                    continue;
                }

                var kind = Math.Clamp(w.Defs[card.Unit].Kind, 0, MixPct.Length - 1);

                // Need = target points for this kind minus points already spent on it. With fewer than two guns alive, artillery
                // comes first: every attack is meant to go in behind fire support (CMD-06). Enemy aircraft in sight with no air
                // defence alive: an AA card comes first (MEC-09).
                var need = (guns < 2 && kind == UnitDef.KindArtillery) || (airThreat && aaAlive == 0 && kind == UnitDef.KindAa)
                    ? long.MaxValue / 2
                    : ((spent + card.Cost) * MixPct[kind] / 100) - spentByKind[kind];
                var better = antiArmor ? BestPen(w, card.Unit) > BestPen(w, deck.Cards[Math.Max(0, best)].Unit)
                    || (BestPen(w, card.Unit) == BestPen(w, deck.Cards[Math.Max(0, best)].Unit) && card.Cost < deck.Cards[Math.Max(0, best)].Cost)
                    : card.Cost < deck.Cards[Math.Max(0, best)].Cost;
                if (need > bestNeed || (need == bestNeed && best >= 0 && better))
                {
                    best = c;
                    bestNeed = need;
                }
            }

            if (best < 0 || deck.Cards[best].Cost > w.Players[player].Dp)
            {
                return;
            }

            var unit = Economy.Deploy(w, player, best, 0, 0);
            if (unit < 0)
            {
                return;
            }

            var bk = Math.Clamp(w.Defs[deck.Cards[best].Unit].Kind, 0, MixPct.Length - 1);
            spentByKind[bk] += deck.Cards[best].Cost;
            spent += deck.Cards[best].Cost;
        }
    }

    static int NearestObjective(WorldState w, int x, int y, int team, bool held)
    {
        var best = -1;
        long bestD = long.MaxValue;
        for (var k = 0; k < w.Objectives.Length; k++)
        {
            var mine = w.Objectives[k].Owner == team;
            if (mine != held)
            {
                continue;
            }

            long dx = w.Objectives[k].X - x;
            long dy = w.Objectives[k].Y - y;
            if ((dx * dx) + (dy * dy) < bestD)
            {
                bestD = (dx * dx) + (dy * dy);
                best = k;
            }
        }

        return best;
    }

    // ------------------------------------------------------------------ victory

    /// <summary>Conquest: an objective belongs to the team with ground units in it and no enemy; held objectives score.</summary>
    static void Victory(WorldState w, BattleRules r)
    {
        if (w.Winner >= 0)
        {
            return;
        }

        if (w.VictoryType == 1 && w.Tick - w.OperationStartTick >= r.AnnihilationGraceTicks && w.Tick % r.Command.PlanTicks == 0)
        {
            // Annihilation (MODE-01): a team with no ground combat units left (alive, not routed, not supply) loses.
            var alive = new int[2];
            foreach (var u in w.Units)
            {
                if (u.Alive != 0 && u.MoraleState < Morale.Routed && !w.Defs[u.Def].IsAir && w.Defs[u.Def].Kind != UnitDef.KindSupply)
                {
                    alive[Frontline.TeamOf(w, u.Player) & 1]++;
                }
            }

            if ((alive[0] == 0) != (alive[1] == 0))
            {
                w.Winner = alive[0] > 0 ? 0 : 1;
                w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.GameOver, A = w.Winner, B = w.Score[0], C = w.Score[1], Value = 1 });
                return;
            }
        }

        if (w.Objectives.Length == 0)
        {
            return;
        }

        if (w.Tick % r.Command.PlanTicks == 0)
        {
            for (var k = 0; k < w.Objectives.Length; k++)
            {
                var present = new int[2];
                foreach (var u in w.Units)
                {
                    if (u.Alive != 0 && u.MoraleState < Morale.Routed && !w.Defs[u.Def].IsAir && w.Defs[u.Def].Kind != UnitDef.KindSupply
                        && Near(u, w.Objectives[k].X, w.Objectives[k].Y, r.Command.ObjectiveRadiusM))
                    {
                        present[Frontline.TeamOf(w, u.Player) & 1]++;
                    }
                }

                var owner = present[0] > 0 && present[1] == 0 ? 0 : present[1] > 0 && present[0] == 0 ? 1 : w.Objectives[k].Owner;
                if (owner != w.Objectives[k].Owner)
                {
                    w.Objectives[k].Owner = owner;
                    w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.ObjectiveTaken, A = k, B = owner });
                }
            }
        }

        var perMinuteTicks = 60 * r.TickHz;
        for (var team = 0; team < 2; team++)
        {
            var held = 0;
            foreach (var o in w.Objectives)
            {
                held += o.Owner == team ? 1 : 0;
            }

            w.ScoreMilli[team] += held * r.VictoryPointsPerMinute;
            w.Score[team] += w.ScoreMilli[team] / perMinuteTicks;
            w.ScoreMilli[team] %= perMinuteTicks;
        }

        if (w.Score[0] >= r.WinPoints || w.Score[1] >= r.WinPoints || w.Tick - w.OperationStartTick >= r.TimeCapTicks)
        {
            w.Winner = w.Score[0] > w.Score[1] ? 0 : w.Score[1] > w.Score[0] ? 1 : 2;
            w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.GameOver, A = w.Winner, B = w.Score[0], C = w.Score[1] });
        }
    }

    // ------------------------------------------------------------------ helpers

    static List<int> SeatGroundUnits(WorldState w, int seat)
    {
        var list = new List<int>();
        for (var i = 0; i < w.Units.Length; i++)
        {
            if (w.Units[i].Seat == seat && w.Units[i].Alive != 0 && w.Units[i].Withdrawing == 0 && !w.Defs[w.Units[i].Def].IsAir
                && w.Defs[w.Units[i].Def].Supply == 0 && w.Units[i].MoraleState < Morale.Routed)
            {
                list.Add(i);
            }
        }

        return list;
    }

    static (int X, int Y) Centroid(WorldState w, List<int> units)
    {
        long sx = 0;
        long sy = 0;
        foreach (var i in units)
        {
            sx += w.Units[i].X.Raw >> Fp.FractionalBits;
            sy += w.Units[i].Y.Raw >> Fp.FractionalBits;
        }

        return units.Count == 0 ? (0, 0) : ((int)(sx / units.Count), (int)(sy / units.Count));
    }

    static bool Near(in UnitState u, int x, int y, int metres)
    {
        long dx = (u.X.Raw >> Fp.FractionalBits) - x;
        long dy = (u.Y.Raw >> Fp.FractionalBits) - y;
        return (dx * dx) + (dy * dy) <= (long)metres * metres;
    }

    static void Order(WorldState w, int unit, int x, int y)
    {
        ref var u = ref w.Units[unit];
        if ((u.Flags & Combat.FlagTracked) != 0)
        {
            return;
        }

        u.HasGoal = 1;
        u.GoalX = Fp.FromInt(Math.Clamp(x, 1, w.Terrain.SizeM - 2));
        u.GoalY = Fp.FromInt(Math.Clamp(y, 1, w.Terrain.SizeM - 2));
        u.PathStep = -1;
    }
}
