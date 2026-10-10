using System.Linq;
using Godot;

/// <summary>MODE-01 / UX-01 deck choice: an era, then one of that era's shipped decks. The opponent is the AI with the
/// first other-side deck of the same income profile in that era.</summary>
public partial class DeckScreen : Node
{
    static readonly string[] Eras = ["coldwar", "ww2", "modern"];
    VBoxContainer _list = null!;
    OptionButton _map = null!;
    SixActual.Sim.Battle.MapDef[] _eraMaps = [];

    public override void _Ready()
    {
        var box = Ui.Screen(this, "CHOOSE A DECK", "Pick an era and a deck. The AI fields the opposing side.");
        var era = new OptionButton { Name = "Era" };
        foreach (var e in Eras)
        {
            era.AddItem(e);
        }

        era.Select(System.Array.IndexOf(Eras, GameSession.Era));
        era.ItemSelected += i => { GameSession.Era = Eras[(int)i]; Fill(); };
        box.AddChild(era);
        var fr = new CheckButton { Name = "FieldRequests", Text = "Field Requests (pick 1 of 3 task forces at each phase change)", ButtonPressed = GameSession.FieldRequests };
        fr.Toggled += on => GameSession.FieldRequests = on;
        box.AddChild(fr);
        var opts = new HBoxContainer();
        box.AddChild(opts);
        Option(opts, "Size", ["1v1", "2v2", "3v3", "4v4", "5v5"], GameSession.PerSide - 1, i => GameSession.PerSide = i + 1);
        Option(opts, "Points", ["500 DP", "1000 DP", "2000 DP"], System.Array.IndexOf(Points, GameSession.StartDp), i => GameSession.StartDp = Points[i]);
        Option(opts, "Income", ["deck income", .. Incomes], GameSession.Income is null ? 0 : System.Array.IndexOf(Incomes, GameSession.Income) + 1, i => GameSession.Income = i == 0 ? null : Incomes[i - 1]);
        Option(opts, "Victory", ["conquest", "annihilation"], GameSession.VictoryType, i => GameSession.VictoryType = i);
        Option(opts, "TimeOfDay", ["dawn", "day", "dusk", "night"], GameSession.TimeOfDay, i => GameSession.TimeOfDay = i);
        Option(opts, "Weather", SixActual.Sim.Battle.BattleRules.WeatherNames, GameSession.Weather, i => GameSession.Weather = i);
        _map = new OptionButton { Name = "Map" };
        _map.ItemSelected += i => GameSession.Map = i == 0 ? null : _eraMaps[(int)i - 1].Id;
        box.AddChild(_map);
        box.AddChild(new Label { Name = "MapPlaceholderNote", Text = "PLACEHOLDER: real-terrain maps already shape line of sight and movement, but the 3D ground is still drawn flat." });
        _list = new VBoxContainer();
        box.AddChild(_list);
        Ui.Button(box, "Build", "Build a custom deck from the last deck chosen", () => Ui.Go(this, new DeckBuilderScreen()));
        Ui.Button(box, "Back", "Back", () => Ui.Go(this, new Main()));
        Fill();
        UxLog.Event("screen", ("name", "deck"));
    }

    static readonly int[] Points = [500, 1000, 2000];
    static readonly string[] Incomes = ["balanced", "juggernaut", "vanguard", "maverick"];

    static void Option(Container box, string name, string[] items, int selected, System.Action<int> changed)
    {
        var o = new OptionButton { Name = name };
        foreach (var it in items)
        {
            o.AddItem(it);
        }

        o.Select(System.Math.Max(0, selected));
        o.ItemSelected += i => changed((int)i);
        box.AddChild(o);
    }

    void Fill()
    {
        // ERA-03: the era's real-terrain maps (Copernicus DEM), or the generated hills.
        _eraMaps = GameData.Data.Maps.Where(m => m.Era == GameSession.Era).ToArray();
        _map.Clear();
        _map.AddItem("generated ground");
        foreach (var m in _eraMaps)
        {
            _map.AddItem(m.Name);
        }

        var at = System.Array.FindIndex(_eraMaps, m => m.Id == GameSession.Map);
        GameSession.Map = at >= 0 ? GameSession.Map : null;
        _map.Select(at + 1);

        foreach (var c in _list.GetChildren())
        {
            c.QueueFree();
        }

        var decks = GameData.Data.Decks.Concat(DeckBuilderScreen.Load()).Where(d => d.Era == GameSession.Era).ToArray();
        foreach (var d in decks)
        {
            var deck = d;
            Ui.Button(_list, $"Deck_{d.Id}", $"{d.Id}  ({d.Income}, {d.Cards.Length} cards)", () =>
            {
                var enemy = decks.FirstOrDefault(o => Side(o.Id) != Side(deck.Id) && o.Income == deck.Income) ?? decks.First(o => Side(o.Id) != Side(deck.Id));
                GameSession.PlayerDeck = deck.Id;
                GameSession.EnemyDeck = enemy.Id;
                UxLog.Event("deck_selected", ("deck", deck.Id), ("enemy", enemy.Id), ("era", GameSession.Era), ("field_requests", GameSession.FieldRequests), ("map", GameSession.Map ?? "generated"));
                Ui.Go(this, new SkirmishView());
            });
        }
    }

    static string Side(string id) => GameData.Data.Decks.Concat(DeckBuilderScreen.Load()).First(x => x.Id == id).Side;
}
