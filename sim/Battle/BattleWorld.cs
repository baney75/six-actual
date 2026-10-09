using SixActual.Sim.State.Battle;

namespace SixActual.Sim.Battle;

public readonly struct UnitOrder
{
    public int Tick { get; init; }

    public int UnitId { get; init; }

    public int Kind { get; init; }

    public int X { get; init; }

    public int Y { get; init; }

    /// <summary>Blast height above ground in decimetres.</summary>
    public int ZDm { get; init; }

    public int RadiusM { get; init; }

    public int Damage { get; init; }

    public int Player { get; init; }

    public int Card { get; init; }

    public const int Move = 1;

    public const int Stop = 2;

    /// <summary>Scripted explosion (scenario stand-in for an impact; artillery uses the same Structures.Blast).</summary>
    public const int Blast = 3;

    /// <summary>Call a deck card (MEC-01): Player, Card, optional X/Y entry point.</summary>
    public const int Deploy = 4;

    /// <summary>Fire mission (SUP-01): UnitId = battery, X/Y target, Damage = rounds, RadiusM = kind (0 HE, 1 smoke).</summary>
    public const int FireMission = 5;

    /// <summary>Assign a unit to a commander seat (CMD-02): UnitId, Card = seat index (-1 = player).</summary>
    public const int AssignSeat = 6;

    /// <summary>Queue a task (CMD-03): Card = seat, Damage = task kind, X/Y, ZDm/RadiusM = X2/Y2, Player = deadline tick.</summary>
    public const int QueueTask = 7;

    /// <summary>Set intent (CMD-04): Card = seat, Damage = field (0 aggression, 1 casualty tolerance, 2 ROE, 3 approval mode), X = value.</summary>
    public const int SetIntent = 8;

    /// <summary>Approve (X = 1) or deny (X = 0) request Card; Y >= 0 reprioritises (CMD-05).</summary>
    public const int Decide = 9;

    /// <summary>FLY-01: Player takes control of aircraft UnitId.</summary>
    public const int TakeControl = 10;

    /// <summary>FLY-02: manual return of a flown aircraft to its AI pilot.</summary>
    public const int Release = 11;

    /// <summary>FLY-03: flown aircraft controls: X = throttle 0..100, Y = turn stick, ZDm = climb stick (-100..100).</summary>
    public const int Stick = 12;

    /// <summary>MEC-04: unit falls back home without firing, is reinforced there and keeps its veterancy.</summary>
    public const int Retreat = 13;

    /// <summary>MEC-03: Player calls off-map support option Card at (X, Y).</summary>
    public const int Support = 14;

    /// <summary>MEC-06: Player takes Field Requests offer Card (0-2).</summary>
    public const int FieldRequest = 15;
}

/// <summary>The data-driven battle simulation (Phase 1 replacement for BattleSim). One call = one 20 Hz tick.</summary>
public static class BattleWorld
{
    public static void Step(WorldState w, BattleRules r, ReadOnlySpan<UnitOrder> orders)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentNullException.ThrowIfNull(r);
        BeginTick(w);
        Economy.Step(w, r);
        FieldRequests.Step(w, r);
        ApplyOrders(w, r, orders);
        Flight.Step(w, r);
        Structures.Step(w, r);
        Command.Step(w, r);
        Movement.Step(w, r);
        Sight.Spot(w, r);
        Frontline.Step(w, r);
        Combat.Step(w, r);
        SixActual.Sim.Battle.Support.Step(w, r);
        Fires.Step(w, r);
        Morale.Step(w, r);
        Supply.Step(w, r);
        w.Tick++;
    }

    public static void BeginTick(WorldState w)
    {
        for (var i = 0; i < w.Units.Length; i++)
        {
            w.Units[i].MovedThisTick = 0;
        }
    }

    public static void ApplyOrders(WorldState w, BattleRules r, ReadOnlySpan<UnitOrder> orders)
    {
        foreach (var o in orders)
        {
            if (o.Tick != w.Tick)
            {
                continue;
            }

            if (o.Kind == UnitOrder.Blast)
            {
                Structures.Blast(w, r, o.X, o.Y, o.ZDm, o.RadiusM, o.Damage);
                continue;
            }

            if (o.Kind == UnitOrder.Deploy)
            {
                Economy.Deploy(w, o.Player, o.Card, o.X, o.Y);
                continue;
            }

            if (o.Kind == UnitOrder.FieldRequest)
            {
                FieldRequests.Take(w, o.Player, o.Card);
                continue;
            }

            if (o.Kind == UnitOrder.Support)
            {
                SixActual.Sim.Battle.Support.Order(w, r, o.Player, o.Card, o.X, o.Y);
                continue;
            }

            if (o.Kind == UnitOrder.QueueTask)
            {
                if (w.Seats.Length == 0)
                {
                    Command.Init(w);
                }

                Command.AddTask(w, o.Card, o.Damage, o.X, o.Y, o.ZDm, o.RadiusM, o.Player);
                continue;
            }

            if (o.Kind == UnitOrder.SetIntent)
            {
                if (w.Seats.Length == 0)
                {
                    Command.Init(w);
                }

                ref var seat = ref w.Seats[o.Card];
                switch (o.Damage)
                {
                    case 0: seat.Aggression = o.X; break;
                    case 1: seat.CasualtyTolerance = o.X; break;
                    case 2:
                        seat.Roe = o.X;
                        for (var k = 0; k < w.Units.Length; k++)
                        {
                            if (w.Units[k].Seat == o.Card)
                            {
                                w.Units[k].Roe = o.X;
                            }
                        }

                        break;
                    case 3: seat.ApprovalMode = o.X; break;
                    case 4: seat.PriorityKind = o.X; break;
                }

                continue;
            }

            if (o.Kind == UnitOrder.Decide)
            {
                Command.Decide(w, o.Card, o.X != 0, o.Y);
                continue;
            }

            var u = IndexOf(w, o.UnitId);
            if (u < 0 || w.Units[u].Alive == 0)
            {
                continue;
            }

            if (o.Kind == UnitOrder.FireMission)
            {
                Fires.Order(w, r, u, o.X, o.Y, o.Damage, o.RadiusM);
                continue;
            }

            if (o.Kind == UnitOrder.AssignSeat)
            {
                if (w.Seats.Length == 0)
                {
                    Command.Init(w);
                }

                Command.Assign(w, u, o.Card);
                continue;
            }

            switch (o.Kind)
            {
                case UnitOrder.Retreat:
                    w.Units[u].Retreating = 1;
                    w.Units[u].HasGoal = 1;
                    w.Units[u].GoalX = Fp.FromInt(w.Units[u].HomeX);
                    w.Units[u].GoalY = Fp.FromInt(w.Units[u].HomeY);
                    w.Units[u].PathStep = -1;
                    continue;
                case UnitOrder.TakeControl:
                    Flight.TakeControl(w, u, o.Player);
                    continue;
                case UnitOrder.Release:
                    Flight.ReturnControl(w, u, Flight.ReasonManual);
                    continue;
                case UnitOrder.Stick:
                    w.Units[u].Throttle = Math.Clamp(o.X, 0, 100);
                    w.Units[u].StickTurn = Math.Clamp(o.Y, -100, 100);
                    w.Units[u].StickClimb = Math.Clamp(o.ZDm, -100, 100);
                    continue;
                case UnitOrder.Move:
                    Command.OnDirectOrder(w, r, u);
                    w.Units[u].HasGoal = 1;
                    w.Units[u].GoalX = Fp.FromInt(o.X);
                    w.Units[u].GoalY = Fp.FromInt(o.Y);
                    w.Units[u].PathStep = -1;
                    break;
                case UnitOrder.Stop:
                    Command.OnDirectOrder(w, r, u);
                    w.Units[u].HasGoal = 0;
                    break;
            }
        }
    }

    public static int IndexOf(WorldState w, int id)
    {
        for (var i = 0; i < w.Units.Length; i++)
        {
            if (w.Units[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// SplitMix64 finaliser: turns small consecutive seeds (1, 2, 3...) into well-spread generator states.
    /// xorshift64* started directly from tiny seeds gives correlated first outputs.
    /// </summary>
    public static ulong MixSeed(ulong seed)
    {
        var z = seed + 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        z ^= z >> 31;
        return z == 0 ? 1UL : z;
    }

    public static ulong NextRandom(WorldState w)
    {
        var rng = new XorShift64(w.Rng);
        var v = rng.NextULong();
        w.Rng = v == 0 ? 1UL : v;
        return v;
    }
}
