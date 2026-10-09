using System.Linq;
using Godot;

/// <summary>UX-01 main menu.</summary>
public partial class Main : Node
{
    static bool _booted;

    public override void _Ready()
    {
        if (!_booted)
        {
            _booted = true;
            GameSettings.Load();
            GameSettings.Apply(GetTree());
            UxDriver.Start(GetTree().Root);
            UxLog.Event("settings_loaded", ("ui_scale", GameSettings.UiScales[GameSettings.UiScaleIndex]), ("palette", GameSettings.Palettes[GameSettings.Palette]), ("subtitles", GameSettings.Subtitles), ("take_control_key", OS.GetKeycodeString(Controls.KeyOf("take_control"))), ("fly_left_pad", Controls.PadOf("fly_left")));
        }

        var box = Ui.Screen(this, "SIX ACTUAL", "Three eras of combined arms. The XO has the net when you fly.");
        Ui.Button(box, "Skirmish", "Skirmish", () => { GameSession.Origin = "skirmish"; Ui.Go(this, new DeckScreen()); });
        Ui.Button(box, "Campaign", "Cold War campaign", () => Ui.Go(this, new CampaignScreen()));
        Ui.Button(box, "Strategy", "Grand strategy", () => Ui.Go(this, new StrategyScreen()));
        Ui.Button(box, "Lobby", "Multiplayer lobby", () => Ui.Go(this, new LobbyScreen()));
        Ui.Button(box, "Tutorials", "Tutorials", () => Ui.Go(this, new TutorialScreen()));
        Ui.Button(box, "Settings", "Settings", () => Ui.Go(this, new SettingsScreen()));
        Ui.Button(box, "Credits", "Credits", () => Ui.Go(this, new CreditsScreen()));
        Ui.Button(box, "Quit", "Quit", () => GetTree().Quit());
        UxLog.Event("screen", ("name", "main_menu"));
    }
}
