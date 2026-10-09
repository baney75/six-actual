using System.Linq;
using System.Text.Json;
using Godot;

/// <summary>UX-01 credits, generated from ASSETS.md by scripts/sync_game_data.py (data/sim/credits.json).</summary>
public partial class CreditsScreen : Node
{
    public override void _Ready()
    {
        var box = Ui.Screen(this, "CREDITS");
        var scroll = new ScrollContainer { Name = "CreditsScroll", CustomMinimumSize = new Vector2(1100, 520) };
        box.AddChild(scroll);
        var list = new VBoxContainer();
        scroll.AddChild(list);
        using var doc = JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://data/sim/credits.json"));
        var lines = 0;
        var sections = 0;
        foreach (var s in doc.RootElement.GetProperty("sections").EnumerateArray())
        {
            var head = new Label { Text = s.GetProperty("title").GetString() };
            head.AddThemeFontSizeOverride("font_size", 22);
            list.AddChild(head);
            sections++;
            foreach (var l in s.GetProperty("lines").EnumerateArray())
            {
                list.AddChild(new Label { Text = l.GetString(), AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(1080, 0) });
                lines++;
            }
        }

        Ui.Button(box, "Back", "Back", () => Ui.Go(this, new Main()));
        UxLog.Event("screen", ("name", "credits"));
        UxLog.Event("credits_shown", ("sections", sections), ("lines", lines), ("source", doc.RootElement.GetProperty("source").GetString()!));
    }
}
