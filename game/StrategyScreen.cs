using Godot;
using SixActual.Sim;

public partial class StrategyScreen : Node
{
    readonly Strategy.State _state = Strategy.Start();
    Label _status = null!;
    readonly Label[] _provinces = new Label[Strategy.ProvinceCount];

    public override void _Ready()
    {
        var layer = new CanvasLayer();
        AddChild(layer);
        var backdrop = new ColorRect { Color = new Color(0.09f, 0.1f, 0.12f), AnchorRight = 1, AnchorBottom = 1 };
        layer.AddChild(backdrop);
        var box = new VBoxContainer { Position = new Vector2(80, 48) };
        layer.AddChild(box);
        var title = new Label { Text = "CENTRAL EUROPE, 1989" };
        title.AddThemeFontSizeOverride("font_size", 32);
        box.AddChild(title);
        _status = new Label();
        box.AddChild(_status);
        var row = new HBoxContainer();
        box.AddChild(row);
        for (var i = 0; i < Strategy.ProvinceCount; i++)
        {
            _provinces[i] = new Label { CustomMinimumSize = new Vector2(90, 48) };
            row.AddChild(_provinces[i]);
        }

        var end = new Button { Text = "End turn" };
        end.Pressed += () =>
        {
            Strategy.EndTurn(_state);
            Refresh();
        };
        box.AddChild(end);
        var peace = new Button { Text = "Offer ceasefire" };
        peace.Pressed += () =>
        {
            Strategy.SetPeace(_state);
            Refresh();
        };
        box.AddChild(peace);
        var back = new Button { Text = "Menu" };
        back.Pressed += () =>
        {
            GetTree().Root.AddChild(new Main());
            QueueFree();
        };
        box.AddChild(back);
        Refresh();
    }

    void Refresh()
    {
        for (var i = 0; i < Strategy.ProvinceCount; i++)
        {
            var side = _state.Owner[i] == 0 ? "NATO" : "Pact";
            _provinces[i].Text = $"{side}\nind {_state.Industry[i]}";
        }

        _status.Text = _state.Winner switch
        {
            0 => $"NATO controls the theater. Turn {_state.Turn}. Tension {_state.Tension}.",
            1 => $"Pact controls the theater. Turn {_state.Turn}. Tension {_state.Tension}.",
            2 => $"The war exhausts both sides. Turn {_state.Turn}.",
            _ => $"Turn {_state.Turn}. Tension {_state.Tension}. Nukes {_state.Nukes}. War {(_state.AtWar == 1 ? "on" : "ceasefire")}.",
        };
    }
}
