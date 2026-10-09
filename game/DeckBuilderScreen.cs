using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using SixActual.Sim.Battle;

/// <summary>
/// UX-01 deck builder: start from a shipped deck and set each card's count between 0 and its availability cap
/// (max_count, MEC-09). Custom decks are saved to user://decks.json and appear on the deck screen.
/// </summary>
public partial class DeckBuilderScreen : Node
{
    const string SavePath = "user://decks.json";
    DeckDef _base = null!;
    int[] _counts = [];
    VBoxContainer _rows = null!;
    Label _summary = null!;

    /// <summary>Custom decks saved by the builder, as DeckDefs the sim can use (cards keep the base deck's costs and
    /// phases).</summary>
    public static DeckDef[] Load()
    {
        var f = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Read);
        if (f is null)
        {
            return [];
        }

        using var doc = JsonDocument.Parse(f.GetAsText());
        f.Close();
        var decks = new List<DeckDef>();
        foreach (var d in doc.RootElement.GetProperty("decks").EnumerateArray())
        {
            var b = GameData.Data.Decks.FirstOrDefault(x => x.Id == d.GetProperty("base").GetString());
            if (b is null)
            {
                continue;
            }

            var counts = d.GetProperty("counts").EnumerateArray().Select(c => c.GetInt32()).ToArray();
            var cards = b.Cards.Select((c, i) => (c, n: i < counts.Length ? System.Math.Clamp(counts[i], 0, c.MaxCount) : c.Count))
                .Where(x => x.n > 0)
                .Select(x => new CardDef { Unit = x.c.Unit, Count = x.n, MaxCount = x.c.MaxCount, Phase = x.c.Phase, Cost = x.c.Cost })
                .ToArray();
            decks.Add(new DeckDef { Id = d.GetProperty("id").GetString()!, Income = b.Income, Era = b.Era, Side = b.Side, Nation = b.Nation, Cards = cards });
        }

        return [.. decks];
    }

    public override void _Ready()
    {
        _base = GameData.Data.Decks.First(d => d.Id == GameSession.PlayerDeck);
        _counts = _base.Cards.Select(c => c.Count).ToArray();
        var box = Ui.Screen(this, "DECK BUILDER", $"Base deck {_base.Id}. Set each card from 0 to its availability cap.");
        _summary = new Label { Name = "DeckSummary" };
        box.AddChild(_summary);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(900, 440) };
        box.AddChild(scroll);
        _rows = new VBoxContainer();
        scroll.AddChild(_rows);
        for (var i = 0; i < _base.Cards.Length; i++)
        {
            var c = i;
            var row = new HBoxContainer();
            _rows.AddChild(row);
            row.AddChild(new Label { Name = $"Count_{i}", CustomMinimumSize = new Vector2(420, 0) });
            Ui.Button(row, $"Minus_{i}", "-", () => Change(c, -1));
            Ui.Button(row, $"Plus_{i}", "+", () => Change(c, +1));
        }

        Ui.Button(box, "SaveDeck", "Save as custom deck", Save);
        Ui.Button(box, "Back", "Back", () => Ui.Go(this, new DeckScreen()));
        Refresh();
        UxLog.Event("screen", ("name", "deck_builder"));
    }

    void Change(int card, int by)
    {
        var before = _counts[card];
        _counts[card] = System.Math.Clamp(_counts[card] + by, 0, _base.Cards[card].MaxCount);
        UxLog.Event("deck_card_count", ("card", card), ("unit", GameData.Data.Units[_base.Cards[card].Unit].Id), ("count", _counts[card]), ("max", _base.Cards[card].MaxCount), ("clamped", before + by != _counts[card]));
        Refresh();
    }

    void Refresh()
    {
        for (var i = 0; i < _base.Cards.Length; i++)
        {
            var c = _base.Cards[i];
            ((Label)_rows.GetChild(i).GetChild(0)).Text = $"{GameData.Data.Units[c.Unit].Id}  {c.Cost} DP  phase {"ABC"[c.Phase]}   x{_counts[i]} (cap {c.MaxCount})";
        }

        _summary.Text = $"{_counts.Count(n => n > 0)} cards, {_counts.Sum()} units, {_base.Cards.Select((c, i) => c.Cost * _counts[i]).Sum()} DP if all are called";
    }

    void Save()
    {
        var id = $"custom_{_base.Id}";
        var existing = new List<object>();
        var f = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Read);
        if (f is not null)
        {
            using var doc = JsonDocument.Parse(f.GetAsText());
            f.Close();
            existing.AddRange(doc.RootElement.GetProperty("decks").EnumerateArray().Where(d => d.GetProperty("id").GetString() != id).Select(d => (object)JsonSerializer.Deserialize<Dictionary<string, object>>(d.GetRawText())!));
        }

        existing.Add(new Dictionary<string, object> { ["id"] = id, ["base"] = _base.Id, ["counts"] = _counts });
        using var w = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Write);
        if (w is null)
        {
            UxLog.Event("save_failed", ("file", SavePath), ("error", Godot.FileAccess.GetOpenError().ToString()));
            return;
        }

        w.StoreString(JsonSerializer.Serialize(new { decks = existing }));
        UxLog.Event("deck_saved", ("deck", id), ("cards", _counts.Count(n => n > 0)), ("units", _counts.Sum()));
        Ui.Go(this, new DeckScreen());
    }
}
