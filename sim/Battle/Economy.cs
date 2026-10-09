using SixActual.Sim.State.Battle;

namespace SixActual.Sim.Battle;

/// <summary>A deck card: a unit type, how many may be called, the earliest battle phase and the cost.</summary>
public sealed class CardDef
{
    /// <summary>Availability cap: the most copies any deck may hold (MEC-09).</summary>
    public int MaxCount { get; init; }

    public int Unit { get; init; }

    public int Count { get; init; }

    /// <summary>0 = phase A, 1 = B, 2 = C (MEC-01).</summary>
    public int Phase { get; init; }

    public int Cost { get; init; }
}

public sealed class DeckDef
{
    public string Id { get; init; } = "";

    /// <summary>Income profile name (rules.economy.income_per_minute); a player using the deck gets it unless the
    /// scenario names another.</summary>
    public string Income { get; init; } = "balanced";

    public CardDef[] Cards { get; init; } = [];

    /// <summary>Deck metadata for menus (ERA-01): era, side and nation as written in decks.json. Not sim state.</summary>
    public string Era { get; init; } = "";

    public string Side { get; init; } = "";

    public string Nation { get; init; } = "";
}

/// <summary>
/// MEC-01 battle phases and income profiles; MEC-03 support points. Deployment points accrue per tick from
/// the player's income profile for the current phase. A deploy order spends DP to spawn a card's unit at the
/// player's entry point if the phase allows it and stock remains. Support points are earned from kills,
/// spotting for kills, and supply delivered.
/// </summary>
public static class Economy
{
    /// <summary>Aircraft enter the battle at this altitude (well above rules.air_altitude_m).</summary>
    public const int DeployAltitudeM = 300;

    public static int PhaseAt(BattleRules r, int tick) =>
        tick < r.PhaseTicks[0] ? 0 : tick < r.PhaseTicks[0] + r.PhaseTicks[1] ? 1 : 2;

    public static void Step(WorldState w, BattleRules r)
    {
        var phase = PhaseAt(r, w.Tick - w.OperationStartTick);
        if (phase != w.Phase)
        {
            w.Phase = phase;
            w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.PhaseChange, Value = phase });
            FieldRequests.OnPhaseChange(w);
        }

        for (var p = 0; p < w.Players.Length; p++)
        {
            ref var pl = ref w.Players[p];
            // Exact accrual: carry the remainder in units of 1 / (ticks per minute) of a point.
            var ticksPerMinute = 60 * r.TickHz;
            pl.DpMilli += r.IncomePerMinute[pl.Profile][phase];
            pl.Dp += pl.DpMilli / ticksPerMinute;
            pl.DpEarned += pl.DpMilli / ticksPerMinute;
            pl.DpMilli %= ticksPerMinute;
        }
    }

    /// <summary>Spends DP and spawns the card's unit. Returns the new unit's index, or -1 if refused.</summary>
    public static int Deploy(WorldState w, int player, int card, int x, int y)
    {
        if (player < 0 || player >= w.Players.Length)
        {
            return -1;
        }

        ref var pl = ref w.Players[player];
        var deck = w.Decks[pl.Deck];
        if (card < 0 || card >= deck.Cards.Length)
        {
            return -1;
        }

        var c = deck.Cards[card];
        var stock = pl.StockStart + card;
        if (c.Phase > w.Phase || w.CardStock[stock] <= 0 || pl.Dp < c.Cost || (w.AirGrounded != 0 && w.Defs[c.Unit].IsAir))
        {
            w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.DeployRefused, A = player, B = card });
            return -1;
        }

        pl.Dp -= c.Cost;
        pl.DpSpent += c.Cost;
        w.CardStock[stock]--;
        var u = Spawn(w, c.Unit, player, x == 0 && y == 0 ? pl.SpawnX : x, x == 0 && y == 0 ? pl.SpawnY : y, c.Cost);
        w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.Deployed, A = player, B = w.Units[u].Id, Value = card });
        return u;
    }

    /// <summary>Appends a unit (with its mounts and path slot) to the world.</summary>
    public static int Spawn(WorldState w, int def, int player, int x, int y, int cost)
    {
        var d = w.Defs[def];
        var mountStart = w.MountAmmo.Length;
        Array.Resize(ref w.MountAmmo, mountStart + d.Weapons.Length);
        Array.Resize(ref w.MountReload, mountStart + d.Weapons.Length);
        for (var m = 0; m < d.Weapons.Length; m++)
        {
            w.MountAmmo[mountStart + m] = d.Weapons[m].Ammo;
        }

        var i = w.Units.Length;
        Array.Resize(ref w.Units, i + 1);
        Array.Resize(ref w.Paths, i + 1);
        w.Units[i] = new UnitState
        {
            Id = w.NextUnitId++,
            Def = def,
            Player = player,
            Alive = 1,
            X = Fp.FromInt(x),
            Y = Fp.FromInt(y),
            Z = d.IsAir ? Fp.FromInt(DeployAltitudeM) : Fp.Zero,
            Hp = d.Hp,
            Fuel = d.Fuel,
            Morale = 100,
            PathStep = -1,
            LastAttacker = -1,
            Seat = -1,
            MountStart = mountStart,
            Members = d.Members,
            HomeX = x,
            HomeY = y,
            IsHq = d.Kind == UnitDef.KindCommand ? 1 : 0,
            SupplyLeft = d.Supply,
            Cost = cost,
        };
        return i;
    }

    /// <summary>Called when a unit is destroyed: the killer's player and any other player spotting the victim earn SP.</summary>
    public static void OnKill(WorldState w, BattleRules r, int shooter, int victim)
    {
        var cost = w.Units[victim].Cost;
        if (cost <= 0 || w.Players.Length == 0)
        {
            return;
        }

        var killerPlayer = shooter >= 0 ? w.Units[shooter].Player : -1;
        if (killerPlayer >= 0 && killerPlayer < w.Players.Length)
        {
            w.Players[killerPlayer].Sp += cost * r.SpKillPct / 100;
        }

        for (var p = 0; p < w.Players.Length; p++)
        {
            if (p != killerPlayer && p != w.Units[victim].Player && (w.Units[victim].SpottedBy & (1 << p)) != 0)
            {
                w.Players[p].Sp += cost * r.SpSpotAssistPct / 100;
            }
        }

        // Recon pays within a side too (MEC-03): if the shooter could not see the victim itself (indirect fire on a
        // spotted target), the kill came from another unit's spotting, and the killer's player earns the assist.
        if (killerPlayer >= 0 && killerPlayer < w.Players.Length && (w.Units[victim].SpottedBy & (1 << killerPlayer)) != 0
            && !Sight.HasLine(w, r, w.Units[shooter], w.Units[victim]))
        {
            w.Players[killerPlayer].Sp += cost * r.SpSpotAssistPct / 100;
            w.Events.Add(new BattleEvent { Tick = w.Tick, Kind = BattleEvent.SpotAssist, A = killerPlayer, B = w.Units[victim].Id });
        }
    }

    public static void OnSupplyDelivered(WorldState w, BattleRules r, int player, int points)
    {
        if (player < 0 || player >= w.Players.Length)
        {
            return;
        }

        ref var pl = ref w.Players[player];
        var before = pl.SupplyDelivered / r.SpPerSupplyPoints;
        pl.SupplyDelivered += points;
        pl.Sp += (pl.SupplyDelivered / r.SpPerSupplyPoints) - before;
    }
}
