using System.Linq;
using Godot;

/// <summary>Shared menu-screen layout and navigation. Every control a G13 script presses has a stable Name.</summary>
public static class Ui
{
    public static VBoxContainer Screen(Node owner, string title, string? note = null)
    {
        var layer = new CanvasLayer();
        owner.AddChild(layer);
        layer.AddChild(new ColorRect { Color = new Color(0.08f, 0.09f, 0.08f), AnchorRight = 1, AnchorBottom = 1 });
        var box = new VBoxContainer { Position = new Vector2(80, 50), CustomMinimumSize = new Vector2(560, 0) };
        layer.AddChild(box);
        var head = new Label { Text = title };
        head.AddThemeFontSizeOverride("font_size", 36);
        box.AddChild(head);
        if (note is not null)
        {
            box.AddChild(new Label { Text = note, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(560, 0) });
        }

        return box;
    }

    public static Button Button(Container box, string name, string text, System.Action pressed)
    {
        var b = new Button { Name = name, Text = text };
        b.Pressed += pressed;
        box.AddChild(b);
        return b;
    }

    /// <summary>Replaces the current screen.</summary>
    public static void Go(Node from, Node to)
    {
        from.GetTree().Root.CallDeferred(Node.MethodName.AddChild, to);
        from.QueueFree();
    }
}
