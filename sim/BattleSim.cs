using SixActual.Sim.State;

namespace SixActual.Sim;

public readonly struct BattleOrder
{
    public int Tick { get; init; }

    public int UnitId { get; init; }

    public int Kind { get; init; }

    public Fp X { get; init; }

    public Fp Y { get; init; }

    public const int Move = 1;

    public const int Stop = 2;

    public const int TakeControl = 3;

    public const int Release = 4;
}

public static class BattleSim
{
    public const int BingoFuel = 80;

    public static BattleState CreateDuel(int armorFront, int pen, bool hill, bool fromSide = false)
    {
        var defs = new[]
        {
            Gun(pen),
            Target(armorFront),
        };
        var state = NewState(defs, mapSize: 400, seed: 3);
        state.Units =
        [
            Make(defs, 0, 0, 0, fromSide ? 180 : 40, fromSide ? 40 : 200),
            Make(defs, 1, 1, 1, 180, 200),
        ];
        state.Units[0].Facing = 0;
        state.Units[1].Facing = 128;
        if (hill)
        {
            state.Blocks = [new MapBlock { X = 90, Y = 160, W = 40, H = 80, Height = 40, BlocksSight = 1 }];
        }

        return state;
    }

    public static BattleState CreateWall()
    {
        var defs = new[] { new UnitDef { Id = "runner", Kind = UnitDef.KindTank, SpeedMmS = 20000, Hp = 10, OpticsM = 10 } };
        var state = NewState(defs, mapSize: 256, seed: 9);
        state.Units = [Make(defs, 0, 0, 0, 20, 128)];
        state.Units[0].HasGoal = 1;
        state.Units[0].GoalX = Fp.FromInt(230);
        state.Units[0].GoalY = Fp.FromInt(40);
        state.Blocks =
        [
            new MapBlock { X = 100, Y = 64, W = 24, H = 192, Height = 8, BlocksMove = 1, BlocksSight = 1 },
        ];
        return state;
    }

    public static BattleState CreateArtillery()
    {
        var defs = new[]
        {
            new UnitDef { Id = "gun", Kind = UnitDef.KindArtillery, SpeedMmS = 0, Hp = 40, Ammo = 6, RangeM = 2000, ReloadTicks = 5, OpticsM = 1000, PenMm = 80 },
            new UnitDef { Id = "target", Kind = UnitDef.KindTank, SpeedMmS = 0, Hp = 80, ArmorFront = 20, ArmorSide = 10, OpticsM = 100 },
        };
        var state = NewState(defs, 400, 12);
        state.Units =
        [
            Make(defs, 0, 0, 0, 40, 200),
            Make(defs, 1, 1, 1, 200, 200),
        ];
        return state;
    }

    public static BattleState CreateSupply()
    {
        var defs = new[]
        {
            new UnitDef { Id = "tank", Kind = UnitDef.KindTank, SpeedMmS = 0, Hp = 50, Ammo = 40, Fuel = 100 },
            new UnitDef { Id = "truck", Kind = UnitDef.KindSupply, SpeedMmS = 0, Hp = 20, Ammo = 0, Fuel = 400 },
        };
        var state = NewState(defs, 200, 4);
        state.Units =
        [
            Make(defs, 0, 0, 0, 40, 40),
            Make(defs, 1, 1, 0, 50, 40),
        ];
        state.Units[0].Ammo = 0;
        state.Units[0].Fuel = 5;
        return state;
    }

    public static BattleState CreateFlight(int fuel, int ammo, bool destroy)
    {
        var defs = new[]
        {
            new UnitDef { Id = "jet", Kind = UnitDef.KindAir, SpeedMmS = 100000, Hp = 40, Ammo = 20, Fuel = 200, RangeM = 800, PenMm = 30, ReloadTicks = 2 },
        };
        var state = NewState(defs, 2000, 6);
        state.Units = [Make(defs, 0, 0, 0, 400, 400)];
        state.Units[0].Z = Fp.FromInt(200);
        state.Units[0].Fuel = fuel;
        state.Units[0].Ammo = ammo;
        state.Units[0].HasGoal = 1;
        state.Units[0].GoalX = Fp.FromInt(800);
        state.Units[0].GoalY = Fp.FromInt(400);
        if (destroy)
        {
            state.Units[0].Hp = 0;
            state.Units[0].Alive = 0;
        }

        return state;
    }

    public static BattleState CreateFulda(UnitDef[] defs)
    {
        var state = NewState(defs, 2000, 11);
        state.TickCap = 20 * 60 * 15;
        state.Aggression0 = 70;
        state.Aggression1 = 70;
        state.Blocks =
        [
            new MapBlock { X = 860, Y = 200, W = 280, H = 1600, Height = 36, BlocksSight = 1 },
            new MapBlock { X = 200, Y = 1400, W = 500, H = 400, Height = 14, BlocksSight = 1 },
            new MapBlock { X = 1300, Y = 200, W = 400, H = 360, Height = 14, BlocksSight = 1 },
        ];
        var spawns = new (string Id, int Player, int X, int Y, int Count)[]
        {
            ("m1_abrams", 0, 180, 700, 3),
            ("m2_bradley", 0, 180, 980, 2),
            ("us_rifle_cw", 0, 140, 1200, 3),
            ("m109", 0, 80, 500, 1),
            ("supply_us_cw", 0, 60, 400, 1),
            ("f16", 0, 40, 300, 1),
            ("t80", 1, 1760, 700, 3),
            ("bmp2", 1, 1760, 980, 2),
            ("su_rifle_cw", 1, 1800, 1200, 3),
            ("s2s3", 1, 1880, 500, 1),
            ("supply_su_cw", 1, 1900, 400, 1),
            ("mig29", 1, 1920, 300, 1),
        };
        var units = new List<BattleUnit>();
        var id = 0;
        foreach (var spawn in spawns)
        {
            var def = UnitCatalog.IndexOf(defs, spawn.Id);
            for (var n = 0; n < spawn.Count; n++)
            {
                var unit = Make(defs, id++, def, spawn.Player, spawn.X, spawn.Y + (n * 36));
                if (defs[def].IsAir)
                {
                    unit.Z = Fp.FromInt(180);
                    unit.HasGoal = 1;
                    unit.GoalX = Fp.FromInt(spawn.Player == 0 ? 900 : 1100);
                    unit.GoalY = Fp.FromInt(1000);
                }

                units.Add(unit);
            }
        }

        state.Units = units.ToArray();
        return state;
    }

    public static void Step(BattleState state, ReadOnlySpan<BattleOrder> orders)
    {
        ArgumentNullException.ThrowIfNull(state);
        ApplyOrders(state, orders);
        Command(state);
        Move(state);
        Spot(state);
        Shoot(state);
        ResolveImpacts(state);
        Recover(state);
        Supply(state);
        state.Tick++;
        UpdateCommand(state);
        Judge(state);
    }

    public static void Run(BattleState state, int ticks, ReadOnlySpan<BattleOrder> orders)
    {
        for (var i = 0; i < ticks; i++)
        {
            Step(state, orders);
        }
    }

    public static int WeatherScale(int weather) => weather switch
    {
        1 => 75,
        2 => 45,
        3 => 60,
        4 => 55,
        _ => 100,
    };

    public static bool SeenBy(BattleState state, int player, int unitIndex)
    {
        var unit = state.Units[unitIndex];
        if (unit.Player == player)
        {
            return true;
        }

        return (unit.Spotted & (1 << player)) != 0;
    }

    public static readonly string[] CommanderSeats = ["XO", "Army1", "Army2", "Arty1", "Arty2", "Air1", "Air2", "Armor", "Logistics"];

    public static int Penetration(UnitDef def, int rangeM)
    {
        var drop = def.PenDropPerKm * rangeM / 1000;
        var pen = def.PenMm - drop;
        return pen < 0 ? 0 : pen;
    }

    public static int ArmorOnFace(UnitDef def, BattleUnit target, Fp fromX, Fp fromY)
    {
        Trig.FacingVector(target.Facing, out var east, out var north);
        var dx = fromX - target.X;
        var dy = fromY - target.Y;
        var dot = (east.Raw * dx.Raw) + (north.Raw * dy.Raw);
        var reach = Abs(dx.Raw) + Abs(dy.Raw);
        if (reach == 0)
        {
            return def.ArmorFront;
        }

        if (dot > reach / 2)
        {
            return def.ArmorFront;
        }

        if (dot < -reach / 2)
        {
            return def.ArmorRear;
        }

        return def.ArmorSide;
    }

    static void ApplyOrders(BattleState state, ReadOnlySpan<BattleOrder> orders)
    {
        for (var i = 0; i < orders.Length; i++)
        {
            if (orders[i].Tick != state.Tick)
            {
                continue;
            }

            var index = Find(state, orders[i].UnitId);
            if (index < 0 || state.Units[index].Alive == 0)
            {
                continue;
            }

            switch (orders[i].Kind)
            {
                case BattleOrder.Move:
                    state.Units[index].HasGoal = 1;
                    state.Units[index].GoalX = orders[i].X;
                    state.Units[index].GoalY = orders[i].Y;
                    break;
                case BattleOrder.Stop:
                    state.Units[index].HasGoal = 0;
                    break;
                case BattleOrder.TakeControl:
                    if (state.Defs[state.Units[index].Def].IsAir)
                    {
                        state.Units[index].Controlled = 1;
                    }

                    break;
                case BattleOrder.Release:
                    state.Units[index].Controlled = 0;
                    break;
            }
        }
    }

    static void Command(BattleState state)
    {
        if ((state.Tick % 20) != 0)
        {
            return;
        }

        for (var i = 0; i < state.Units.Length; i++)
        {
            if (state.Units[i].Alive == 0 || state.Units[i].Controlled == 1)
            {
                continue;
            }

            var aggression = state.Units[i].Player == 0 ? state.Aggression0 : state.Aggression1;
            if (state.Defs[state.Units[i].Def].IsAir)
            {
                continue;
            }

            if (aggression < 40)
            {
                state.Units[i].HasGoal = 1;
                state.Units[i].GoalX = Fp.FromInt(state.Units[i].HomeX);
                state.Units[i].GoalY = Fp.FromInt(state.Units[i].HomeY);
                continue;
            }

            var enemy = NearestEnemy(state, i, requireSpot: true);
            if (enemy < 0)
            {
                enemy = NearestEnemy(state, i, requireSpot: false);
            }

            if (enemy >= 0 && aggression >= 60)
            {
                state.Units[i].HasGoal = 1;
                state.Units[i].GoalX = state.Units[enemy].X;
                state.Units[i].GoalY = state.Units[enemy].Y;
            }
        }
    }

    static void Move(BattleState state)
    {
        for (var i = 0; i < state.Units.Length; i++)
        {
            if (state.Units[i].Alive == 0 || state.Units[i].HasGoal == 0)
            {
                continue;
            }

            if ((state.Units[i].Flags & BattleState.FlagTracked) != 0 || state.Defs[state.Units[i].Def].SpeedMmS <= 0)
            {
                continue;
            }

            var def = state.Defs[state.Units[i].Def];
            var speed = def.SpeedMmS;
            if ((state.Units[i].Flags & BattleState.FlagEngine) != 0)
            {
                speed /= 2;
            }

            if (def.IsAir)
            {
                MoveAir(state, i, speed);
                continue;
            }

            var step = MetersPerTick(speed);
            if (state.Units[i].HasVia == 1)
            {
                var viaDx = state.Units[i].ViaX - state.Units[i].X;
                var viaDy = state.Units[i].ViaY - state.Units[i].Y;
                if (Abs(viaDx.Raw) + Abs(viaDy.Raw) < Fp.FromInt(8).Raw)
                {
                    state.Units[i].HasVia = 0;
                }
            }

            var aimX = state.Units[i].HasVia == 1 ? state.Units[i].ViaX : state.Units[i].GoalX;
            var aimY = state.Units[i].HasVia == 1 ? state.Units[i].ViaY : state.Units[i].GoalY;
            var dx = aimX - state.Units[i].X;
            var dy = aimY - state.Units[i].Y;
            if (state.Units[i].HasVia == 0 && Abs(dx.Raw) + Abs(dy.Raw) < Fp.FromInt(6).Raw)
            {
                state.Units[i].HasGoal = 0;
                continue;
            }

            var angle = Trig.Atan2(dy, dx);
            state.Units[i].Facing = FacingFromAngle(angle);
            Trig.FacingVector(state.Units[i].Facing, out var east, out var north);
            if (!Propose(state, i, east * step, north * step))
            {
                SetBypass(state, i);
            }
        }
    }

    static void MoveAir(BattleState state, int index, int speed)
    {
        var unit = state.Units[index];
        if (unit.Fuel <= BingoFuel || unit.Ammo <= 0)
        {
            unit.HasGoal = 1;
            unit.GoalX = Fp.FromInt(unit.HomeX);
            unit.GoalY = Fp.FromInt(unit.HomeY);
            unit.Controlled = 0;
        }

        var dx = unit.GoalX - unit.X;
        var dy = unit.GoalY - unit.Y;
        if (Abs(dx.Raw) + Abs(dy.Raw) < Fp.FromInt(30).Raw && unit.Fuel <= BingoFuel)
        {
            unit.Z = Fp.Zero;
            unit.Fuel = state.Defs[unit.Def].Fuel;
            unit.Ammo = state.Defs[unit.Def].Ammo;
            unit.HasGoal = 0;
            state.Units[index] = unit;
            return;
        }

        unit.Facing = FacingFromAngle(Trig.Atan2(dy, dx));
        Trig.FacingVector(unit.Facing, out var east, out var north);
        var step = MetersPerTick(speed);
        unit.X += east * step;
        unit.Y += north * step;
        if (unit.Z.Raw < Fp.FromInt(180).Raw)
        {
            unit.Z += Fp.FromInt(4);
            if (unit.Fuel > 0)
            {
                unit.Fuel--;
            }
        }

        if (unit.Fuel > 0)
        {
            unit.Fuel--;
        }

        state.Units[index] = unit;
    }

    static void SetBypass(BattleState state, int index)
    {
        var aheadX = state.Units[index].X;
        var aheadY = state.Units[index].Y;
        Trig.FacingVector(state.Units[index].Facing, out var east, out var north);
        aheadX += east * Fp.FromInt(4);
        aheadY += north * Fp.FromInt(4);
        var metersX = (int)(aheadX.Raw >> Fp.FractionalBits);
        var metersY = (int)(aheadY.Raw >> Fp.FractionalBits);
        for (var i = 0; i < state.Blocks.Length; i++)
        {
            var block = state.Blocks[i];
            if (block.BlocksMove == 0)
            {
                continue;
            }

            if (metersX < block.X || metersY < block.Y || metersX >= block.X + block.W || metersY >= block.Y + block.H)
            {
                continue;
            }

            var unitX = (int)(state.Units[index].X.Raw >> Fp.FractionalBits);
            var unitY = (int)(state.Units[index].Y.Raw >> Fp.FractionalBits);
            var corners = new[]
            {
                (block.X - 8, block.Y - 8),
                (block.X + block.W + 8, block.Y - 8),
                (block.X - 8, block.Y + block.H + 8),
                (block.X + block.W + 8, block.Y + block.H + 8),
            };
            var best = -1;
            var bestDist = int.MaxValue;
            for (var c = 0; c < corners.Length; c++)
            {
                if (corners[c].Item1 < 0 || corners[c].Item2 < 0 || corners[c].Item1 >= state.MapSize || corners[c].Item2 >= state.MapSize)
                {
                    continue;
                }

                var dist = Math.Abs(corners[c].Item1 - unitX) + Math.Abs(corners[c].Item2 - unitY);
                if (dist < 12)
                {
                    continue;
                }

                if (dist < bestDist)
                {
                    best = c;
                    bestDist = dist;
                }
            }

            if (best < 0)
            {
                return;
            }

            state.Units[index].HasVia = 1;
            state.Units[index].ViaX = Fp.FromInt(corners[best].Item1);
            state.Units[index].ViaY = Fp.FromInt(corners[best].Item2);
            return;
        }
    }

    static bool Propose(BattleState state, int index, Fp dx, Fp dy)
    {
        var nextX = state.Units[index].X + dx;
        var nextY = state.Units[index].Y + dy;
        if (Blocked(state, nextX, nextY))
        {
            return false;
        }

        state.Units[index].X = nextX;
        state.Units[index].Y = nextY;
        return true;
    }

    static void Spot(BattleState state)
    {
        for (var i = 0; i < state.Units.Length; i++)
        {
            state.Units[i].Spotted = 0;
        }

        for (var i = 0; i < state.Units.Length; i++)
        {
            if (state.Units[i].Alive == 0)
            {
                continue;
            }

            var optics = state.Defs[state.Units[i].Def].OpticsM * WeatherScale(state.Weather) / 100;
            if ((state.Units[i].Flags & BattleState.FlagOptics) != 0)
            {
                optics /= 2;
            }

            for (var j = 0; j < state.Units.Length; j++)
            {
                if (i == j || state.Units[j].Alive == 0 || state.Units[i].Player == state.Units[j].Player)
                {
                    continue;
                }

                var range = DistanceMeters(state.Units[i], state.Units[j]);
                var stealth = state.Defs[state.Units[j].Def].Stealth;
                if (range > optics - stealth)
                {
                    continue;
                }

                if (!Sees(state, i, j))
                {
                    continue;
                }

                state.Units[j].Spotted |= 1 << state.Units[i].Player;
            }
        }
    }

    static void Shoot(BattleState state)
    {
        state.LastShotActive = 0;
        for (var i = 0; i < state.Units.Length; i++)
        {
            if (state.Units[i].Reload > 0)
            {
                state.Units[i].Reload--;
            }

            if (state.Units[i].Alive == 0 || state.Units[i].Ammo <= 0 || state.Units[i].Reload > 0)
            {
                continue;
            }

            if ((state.Units[i].Flags & (BattleState.FlagBail | BattleState.FlagTurret)) != 0)
            {
                continue;
            }

            var def = state.Defs[state.Units[i].Def];
            if (def.Kind == UnitDef.KindSupply || def.RangeM <= 0)
            {
                continue;
            }

            var target = NearestEnemy(state, i, requireSpot: false);
            if (target < 0)
            {
                continue;
            }

            var range = DistanceMeters(state.Units[i], state.Units[target]);
            if (range > def.RangeM || !Sees(state, i, target))
            {
                continue;
            }

            if (def.Kind == UnitDef.KindArtillery)
            {
                QueueImpact(state, i, target);
                continue;
            }

            if (def.Kind == UnitDef.KindAa && state.Units[target].Z.Raw < Fp.FromInt(20).Raw && state.Defs[state.Units[target].Def].IsAir)
            {
                continue;
            }

            if (def.Kind != UnitDef.KindAa && state.Defs[state.Units[target].Def].IsAir && state.Units[target].Z.Raw > Fp.FromInt(80).Raw)
            {
                continue;
            }

            ResolveHit(state, i, target, range);
        }
    }

    static void ResolveHit(BattleState state, int shooter, int target, int range)
    {
        var weapon = state.Defs[state.Units[shooter].Def];
        var armorDef = state.Defs[state.Units[target].Def];
        var pen = Penetration(weapon, range);
        var face = state.Units[target].Z.Raw > Fp.FromInt(20).Raw
            ? armorDef.ArmorTop
            : ArmorOnFace(armorDef, state.Units[target], state.Units[shooter].X, state.Units[shooter].Y);
        state.Units[shooter].Ammo--;
        state.Units[shooter].Reload = weapon.ReloadTicks;
        state.LastShotActive = 1;
        state.LastShotX0 = state.Units[shooter].X;
        state.LastShotY0 = state.Units[shooter].Y;
        state.LastShotX1 = state.Units[target].X;
        state.LastShotY1 = state.Units[target].Y;
        if (pen <= face)
        {
            state.Units[target].Suppression += 8;
            return;
        }

        var damage = 8 + ((pen - face) / 10);
        state.Units[target].Hp -= damage;
        state.Units[target].Morale -= 8;
        state.Units[target].Suppression += 20;
        if (pen > face + 30)
        {
            ApplyCritical(state, target);
        }

        if (state.Units[target].Hp <= 0 || state.Units[target].Morale <= 0)
        {
            state.Units[target].Alive = 0;
            state.Units[target].Flags |= BattleState.FlagBail;
        }
    }

    static void ApplyCritical(BattleState state, int target)
    {
        var roll = (int)(Next(state) % 5UL);
        var flag = roll switch
        {
            0 => BattleState.FlagTracked,
            1 => BattleState.FlagOptics,
            2 => BattleState.FlagEngine,
            3 => BattleState.FlagTurret,
            _ => BattleState.FlagFire,
        };
        state.Units[target].Flags |= flag;
    }

    static void QueueImpact(BattleState state, int shooter, int target)
    {
        for (var i = 0; i < state.Impacts.Length; i++)
        {
            if (state.Impacts[i].Active != 0)
            {
                continue;
            }

            state.Impacts[i] = new PendingImpact
            {
                Active = 1,
                TickDue = state.Tick + 40,
                X = state.Units[target].X,
                Y = state.Units[target].Y,
                Radius = 40,
                Damage = 25,
                Shooter = shooter,
            };
            state.Units[shooter].Ammo--;
            state.Units[shooter].Reload = state.Defs[state.Units[shooter].Def].ReloadTicks;
            return;
        }
    }

    static void ResolveImpacts(BattleState state)
    {
        for (var i = 0; i < state.Impacts.Length; i++)
        {
            if (state.Impacts[i].Active == 0 || state.Impacts[i].TickDue > state.Tick)
            {
                continue;
            }

            for (var u = 0; u < state.Units.Length; u++)
            {
                if (state.Units[u].Alive == 0)
                {
                    continue;
                }

                var dx = state.Units[u].X - state.Impacts[i].X;
                var dy = state.Units[u].Y - state.Impacts[i].Y;
                if (Abs(dx.Raw) + Abs(dy.Raw) > Fp.FromInt(state.Impacts[i].Radius).Raw)
                {
                    continue;
                }

                state.Units[u].Hp -= state.Impacts[i].Damage;
                state.Units[u].Suppression += 30;
                if (state.Units[u].Hp <= 0)
                {
                    state.Units[u].Alive = 0;
                }
            }

            state.Impacts[i].Active = 0;
        }
    }

    static void Recover(BattleState state)
    {
        for (var i = 0; i < state.Units.Length; i++)
        {
            if (state.Units[i].Alive == 0)
            {
                continue;
            }

            if (state.Units[i].Suppression > 0)
            {
                state.Units[i].Suppression--;
            }

            if ((state.Units[i].Flags & BattleState.FlagFire) != 0)
            {
                state.Units[i].Hp -= 1;
                if (state.Units[i].Hp <= 0)
                {
                    state.Units[i].Alive = 0;
                }
            }

            if (state.Units[i].Suppression > 70)
            {
                state.Units[i].Morale -= 1;
                state.Units[i].HasGoal = 1;
                state.Units[i].GoalX = Fp.FromInt(state.Units[i].HomeX);
                state.Units[i].GoalY = Fp.FromInt(state.Units[i].HomeY);
            }
            else if (state.Units[i].Morale < 100)
            {
                state.Units[i].Morale++;
            }

            if (state.Units[i].Morale <= 0)
            {
                state.Units[i].Flags |= BattleState.FlagBail;
                state.Units[i].Alive = 0;
            }
        }
    }

    static void Supply(BattleState state)
    {
        for (var i = 0; i < state.Units.Length; i++)
        {
            if (state.Units[i].Alive == 0 || state.Defs[state.Units[i].Def].Kind != UnitDef.KindSupply)
            {
                continue;
            }

            for (var j = 0; j < state.Units.Length; j++)
            {
                if (i == j || state.Units[j].Alive == 0 || state.Units[i].Player != state.Units[j].Player)
                {
                    continue;
                }

                if (state.Defs[state.Units[j].Def].Kind == UnitDef.KindSupply)
                {
                    continue;
                }

                if (DistanceMeters(state.Units[i], state.Units[j]) > 30)
                {
                    continue;
                }

                var def = state.Defs[state.Units[j].Def];
                if (state.Units[j].Ammo < def.Ammo)
                {
                    state.Units[j].Ammo++;
                }

                if (state.Units[j].Fuel < def.Fuel)
                {
                    state.Units[j].Fuel++;
                }
            }
        }
    }

    static void UpdateCommand(BattleState state)
    {
        var flying = 0;
        for (var i = 0; i < state.Units.Length; i++)
        {
            if (state.Units[i].Controlled == 1)
            {
                flying = 1;
            }
        }

        state.XoCommand = flying;
    }

    static void Judge(BattleState state)
    {
        var alive0 = 0;
        var alive1 = 0;
        for (var i = 0; i < state.Units.Length; i++)
        {
            if (state.Units[i].Alive == 0 || state.Defs[state.Units[i].Def].Kind == UnitDef.KindSupply)
            {
                continue;
            }

            if (state.Units[i].Player == 0)
            {
                alive0++;
            }
            else
            {
                alive1++;
            }
        }

        if (alive0 == 0 && alive1 == 0)
        {
            state.Winner = 2;
        }
        else if (alive0 == 0)
        {
            state.Winner = 1;
        }
        else if (alive1 == 0)
        {
            state.Winner = 0;
        }
        else if (state.Tick >= state.TickCap && state.TickCap > 0)
        {
            state.Winner = 2;
        }
    }

    static bool Sees(BattleState state, int from, int to)
    {
        var samples = 12;
        var fromZ = state.Units[from].Z + Fp.FromInt(2);
        var toZ = state.Units[to].Z + Fp.FromInt(2);
        for (var i = 1; i < samples; i++)
        {
            var t = Fp.FromInt(i) / Fp.FromInt(samples);
            var x = state.Units[from].X + ((state.Units[to].X - state.Units[from].X) * t);
            var y = state.Units[from].Y + ((state.Units[to].Y - state.Units[from].Y) * t);
            var line = fromZ + ((toZ - fromZ) * t);
            if (Terrain(state, x, y).Raw > line.Raw)
            {
                return false;
            }
        }

        return true;
    }

    static Fp Terrain(BattleState state, Fp x, Fp y)
    {
        var metersX = (int)(x.Raw >> Fp.FractionalBits);
        var metersY = (int)(y.Raw >> Fp.FractionalBits);
        var height = 0;
        for (var i = 0; i < state.Blocks.Length; i++)
        {
            var block = state.Blocks[i];
            if (block.BlocksSight == 0 && block.Height == 0)
            {
                continue;
            }

            if (metersX < block.X || metersY < block.Y || metersX >= block.X + block.W || metersY >= block.Y + block.H)
            {
                continue;
            }

            if (block.Height > height)
            {
                height = block.Height;
            }
        }

        return Fp.FromInt(height);
    }

    static bool Blocked(BattleState state, Fp x, Fp y)
    {
        var metersX = (int)(x.Raw >> Fp.FractionalBits);
        var metersY = (int)(y.Raw >> Fp.FractionalBits);
        if (metersX < 0 || metersY < 0 || metersX >= state.MapSize || metersY >= state.MapSize)
        {
            return true;
        }

        for (var i = 0; i < state.Blocks.Length; i++)
        {
            var block = state.Blocks[i];
            if (block.BlocksMove == 0)
            {
                continue;
            }

            if (metersX >= block.X && metersY >= block.Y && metersX < block.X + block.W && metersY < block.Y + block.H)
            {
                return true;
            }
        }

        return false;
    }

    static int NearestEnemy(BattleState state, int index, bool requireSpot)
    {
        var best = -1;
        var bestRange = int.MaxValue;
        for (var i = 0; i < state.Units.Length; i++)
        {
            if (i == index || state.Units[i].Alive == 0 || state.Units[i].Player == state.Units[index].Player)
            {
                continue;
            }

            if (requireSpot && state.Units[i].Spotted == 0)
            {
                continue;
            }

            var range = DistanceMeters(state.Units[index], state.Units[i]);
            if (range < bestRange)
            {
                best = i;
                bestRange = range;
            }
        }

        return best;
    }

    static int DistanceMeters(BattleUnit left, BattleUnit right)
    {
        var dx = Abs((int)((left.X.Raw - right.X.Raw) >> Fp.FractionalBits));
        var dy = Abs((int)((left.Y.Raw - right.Y.Raw) >> Fp.FractionalBits));
        return (int)(dx + dy);
    }

    static int Find(BattleState state, int id)
    {
        for (var i = 0; i < state.Units.Length; i++)
        {
            if (state.Units[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    static int FacingFromAngle(Fp angle)
    {
        var raw = angle.Raw;
        if (raw < 0)
        {
            raw += Trig.TwoPi.Raw;
        }

        var facing = (int)(raw * 256 / Trig.TwoPi.Raw);
        return facing & 255;
    }

    static Fp MetersPerTick(int speedMmS)
    {
        var mmPerTick = speedMmS / Simulation.TickHz;
        return Fp.FromRaw((long)mmPerTick * Fp.OneRaw / 1000L);
    }

    static ulong Next(BattleState state)
    {
        var rng = new XorShift64(state.Rng);
        var value = rng.NextULong();
        state.Rng = value == 0 ? 1UL : value;
        return value;
    }

    static long Abs(long value) => value < 0 ? -value : value;

    static BattleState NewState(UnitDef[] defs, int mapSize, ulong seed)
    {
        return new BattleState
        {
            Seed = seed,
            Rng = seed == 0 ? 1UL : seed,
            MapSize = mapSize,
            TickCap = 400,
            Aggression0 = 80,
            Aggression1 = 80,
            Winner = -1,
            Defs = defs,
        };
    }

    static BattleUnit Make(UnitDef[] defs, int id, int def, int player, int x, int y)
    {
        return new BattleUnit
        {
            Id = id,
            Def = def,
            Player = player,
            Alive = 1,
            X = Fp.FromInt(x),
            Y = Fp.FromInt(y),
            Hp = defs[def].Hp,
            Ammo = defs[def].Ammo,
            Fuel = defs[def].Fuel,
            Morale = 100,
            HomeX = x,
            HomeY = y,
        };
    }

    static UnitDef Gun(int pen) => new()
    {
        Id = "gun",
        Kind = UnitDef.KindTank,
        SpeedMmS = 0,
        Hp = 50,
        PenMm = pen,
        RangeM = 500,
        ReloadTicks = 1,
        OpticsM = 1000,
        Ammo = 20,
    };

    static UnitDef Target(int front) => new()
    {
        Id = "target",
        Kind = UnitDef.KindTank,
        SpeedMmS = 0,
        Hp = 80,
        ArmorFront = front,
        ArmorSide = 30,
        ArmorRear = 20,
        ArmorTop = 10,
        OpticsM = 100,
    };
}
