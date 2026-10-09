using System.Linq;
using Godot;

/// <summary>UX-02 settings: UI scale, colorblind palette, subtitles. Saved to user://settings.cfg.</summary>
public partial class SettingsScreen : Node
{
    string? _rebinding;
    Button? _rebindButton;

    public override void _Input(InputEvent e)
    {
        if (_rebinding is null)
        {
            return;
        }

        var pad = Controls.PadBinding(e);
        if (pad is not null)
        {
            Controls.RebindPad(_rebinding, pad);
            _rebindButton!.Text = Describe(_rebinding);
            UxLog.Event("pad_rebound", ("action", _rebinding), ("binding", pad));
            _rebinding = null;
            GetViewport().SetInputAsHandled();
            return;
        }

        if (e is not InputEventKey { Pressed: true } k)
        {
            return;
        }

        Controls.Rebind(_rebinding, k.Keycode);
        _rebindButton!.Text = Describe(_rebinding);
        UxLog.Event("key_rebound", ("action", _rebinding), ("key", OS.GetKeycodeString(k.Keycode)));
        _rebinding = null;
        GetViewport().SetInputAsHandled();
    }

    public override void _Ready()
    {
        var box = Ui.Screen(this, "SETTINGS");
        var scale = new OptionButton { Name = "UiScale" };
        foreach (var s in GameSettings.UiScales)
        {
            scale.AddItem($"UI scale {s:0.00}");
        }

        scale.Select(GameSettings.UiScaleIndex);
        scale.ItemSelected += i => GameSettings.UiScaleIndex = (int)i;
        box.AddChild(scale);
        var palette = new OptionButton { Name = "Palette" };
        foreach (var p in GameSettings.Palettes)
        {
            palette.AddItem($"Palette: {p}");
        }

        palette.Select(GameSettings.Palette);
        palette.ItemSelected += i => GameSettings.Palette = (int)i;
        box.AddChild(palette);
        var subs = new CheckButton { Name = "Subtitles", Text = "Subtitles", ButtonPressed = GameSettings.Subtitles };
        subs.Toggled += on => GameSettings.Subtitles = on;
        box.AddChild(subs);
        box.AddChild(new Label { Text = "Controls: press a binding, then the new key, gamepad button or stick direction" });
        foreach (var (action, label, _) in Controls.Defaults)
        {
            var a = action;
            var b = Ui.Button(box, $"Rebind_{action}", Describe(action), () => { });
            b.Pressed += () =>
            {
                _rebinding = a;
                _rebindButton = b;
                b.Text = $"{Controls.Defaults.First(d => d.Action == a).Label}: press a key, button or stick";
            };
        }

        Ui.Button(box, "Save", "Save", () =>
        {
            GameSettings.Save();
            GameSettings.Apply(GetTree());
            UxLog.Event("settings_saved", ("ui_scale", GameSettings.UiScales[GameSettings.UiScaleIndex]), ("palette", GameSettings.Palettes[GameSettings.Palette]), ("subtitles", GameSettings.Subtitles), ("take_control_key", OS.GetKeycodeString(Controls.KeyOf("take_control"))));
        });
        Ui.Button(box, "Back", "Back", () => Ui.Go(this, new Main()));
        UxLog.Event("screen", ("name", "settings"));
    }

    static string Describe(string action) =>
        $"{Controls.Defaults.First(d => d.Action == action).Label}: {OS.GetKeycodeString(Controls.KeyOf(action))} / pad {Controls.PadOf(action)}";
}
