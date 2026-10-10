using SixActual.Sim.Battle;
using SixActual.Sim.State.Battle;

namespace SixActual.Sim.Net;

/// <summary>
/// NET-01 order screen (D076 review). In a network battle any peer can put any order on the wire, so before a tick
/// executes, every peer drops orders a player may not give: another player's unit or seat, an index outside its
/// table, a point off the map, an out-of-range parameter, or a test-only kind. It runs on the world as it stands at
/// that tick, which is identical on every peer, so all peers drop the same orders and stay in sync. Offline play and
/// the G4 scenarios do not pass through it.
/// </summary>
public static class NetOrders
{
    public static bool Valid(WorldState w, UnitOrder o)
    {
        if (o.Player < 0 || o.Player >= w.Players.Length || o.Tick != w.Tick)
        {
            return false;
        }

        bool OnMap(int x, int y) => x >= 0 && y >= 0 && x <= w.Terrain.SizeM && y <= w.Terrain.SizeM;
        bool OwnSeat(int seat) => seat >= 0 && seat < w.Seats.Length && seat / Command.SeatsPerPlayer == o.Player;
        bool OwnUnit()
        {
            var u = BattleWorld.IndexOf(w, o.UnitId);
            return u >= 0 && w.Units[u].Player == o.Player;
        }

        switch (o.Kind)
        {
            case UnitOrder.Deploy:
                return o.Card >= 0 && o.Card < w.Decks[w.Players[o.Player].Deck].Cards.Length && OnMap(o.X, o.Y);
            case UnitOrder.Support:
                return o.Card >= 0 && o.Card < w.SupportDefs.Length && OnMap(o.X, o.Y);
            case UnitOrder.FieldRequest:
                return o.Card is >= 0 and < FieldRequests.Offers;
            case UnitOrder.QueueTask:
                return OwnSeat(o.Card) && o.Damage is >= 0 and < 16 && OnMap(o.X, o.Y);
            case UnitOrder.SetIntent:
                return OwnSeat(o.Card) && o.Damage is >= 0 and <= 4 && o.X is >= 0 and <= 100;
            case UnitOrder.Decide:
                var req = w.Requests.FindIndex(q => q.Id == o.Card);
                return req >= 0 && OwnSeat(w.Requests[req].FromSeat);
            case UnitOrder.AssignSeat:
                return OwnUnit() && (o.Card == -1 || OwnSeat(o.Card));
            case UnitOrder.FireMission:
                return OwnUnit() && OnMap(o.X, o.Y) && o.Damage is >= 1 and <= 24 && o.RadiusM is >= 0 and <= 7;
            case UnitOrder.Move:
                return OwnUnit() && OnMap(o.X, o.Y);
            case UnitOrder.Stop:
            case UnitOrder.Retreat:
            case UnitOrder.TakeControl:
            case UnitOrder.Release:
            case UnitOrder.Stick:
                return OwnUnit();
            default:
                // Blast is a test fixture; unknown kinds are refused.
                return false;
        }
    }
}
