using Godot;
using SixActual.Sim;

public partial class CampaignScreen : Control
{
    Campaign.State _state = Campaign.Start(1);

    public override void _Ready()
    {
        var layer = new CanvasLayer();
        AddChild(layer);
        var backdrop = new ColorRect { Color = new Color(0.07f, 0.08f, 0.07f), AnchorRight = 1, AnchorBottom = 1 };
        layer.AddChild(backdrop);
        var box = new VBoxContainer { Position = new Vector2(80, 60) };
        layer.AddChild(box);
        var title = new Label { Text = "COLD WAR CAMPAIGN" };
        title.AddThemeFontSizeOverride("font_size", 32);
        box.AddChild(title);
        _status = new Label();
        box.AddChild(_status);
        var fight = new Button { Text = "Fight the next operation" };
        fight.Pressed += () => CallDeferred(MethodName.Fight);
        box.AddChild(fight);
        var back = new Button { Text = "Menu" };
        back.Pressed += () =>
        {
            GetTree().Root.AddChild(new Main());
            QueueFree();
        };
        box.AddChild(back);
        Refresh();
    }

    Label _status = null!;

    void Refresh()
    {
        _status.Text = Campaign.IsFinished(_state)
            ? $"Finished. Wins {_state.Wins}, losses {_state.Losses}, commander XP {_state.Xp}."
            : $"{Campaign.OperationName(_state)}. Wins {_state.Wins}. Losses {_state.Losses}. XP {_state.Xp}.";
    }

    void Fight()
    {
        if (Campaign.IsFinished(_state))
        {
            Refresh();
            return;
        }

        var battle = new BattleView();
        battle.OnDecided = winner =>
        {
            Campaign.ApplyResult(_state, winner == 0);
            battle.QueueFree();
            Visible = true;
            Refresh();
        };
        GetTree().Root.AddChild(battle);
        Visible = false;
    }
}
