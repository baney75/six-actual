using Godot;
using SixActual.Sim;

public partial class Main : Node
{
    public override void _Ready()
    {
        GD.Print($"Six Actual foundation {DeterminismReport.SelfTest()}");
        var layer = new CanvasLayer();
        AddChild(layer);
        var backdrop = new ColorRect
        {
            Color = new Color(0.08f, 0.09f, 0.08f),
            AnchorRight = 1,
            AnchorBottom = 1,
        };
        layer.AddChild(backdrop);
        var box = new VBoxContainer
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = -180,
            OffsetRight = 180,
            OffsetTop = -120,
            OffsetBottom = 120,
        };
        box.Alignment = BoxContainer.AlignmentMode.Center;
        layer.AddChild(box);
        var title = new Label { Text = "SIX ACTUAL", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 42);
        box.AddChild(title);
        var sub = new Label
        {
            Text = "Cold War skirmish. The XO has the net when you fly.",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        box.AddChild(sub);
        var play = new Button { Text = "Fulda skirmish" };
        play.Pressed += () => CallDeferred(MethodName.StartBattle);
        box.AddChild(play);
        var quit = new Button { Text = "Quit" };
        quit.Pressed += () => GetTree().Quit();
        box.AddChild(quit);
        if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--shot") >= 0)
        {
            CallDeferred(MethodName.StartBattle);
        }
    }

    void StartBattle()
    {
        var battle = new BattleView();
        GetTree().Root.AddChild(battle);
        QueueFree();
    }
}
