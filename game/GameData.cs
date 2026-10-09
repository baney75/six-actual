using Godot;
using SixActual.Sim.Battle;

/// <summary>
/// The sim's data in the running game. The export ships data/sim/*.json (scripts/sync_game_data.py); the sim's loaders
/// read files with System.IO, which cannot see inside a .pck, so the files are copied to user://simdata/data first
/// (D060).
/// </summary>
public static class GameData
{
    static BattleData? _data;
    static BattleRules? _rules;

    public static BattleData Data
    {
        get
        {
            Ensure();
            return _data!;
        }
    }

    public static BattleRules Rules
    {
        get
        {
            Ensure();
            return _rules!;
        }
    }

    static void Ensure()
    {
        if (_data is not null)
        {
            return;
        }

        var root = ProjectSettings.GlobalizePath("user://simdata");
        var dataDir = System.IO.Path.Combine(root, "data");
        System.IO.Directory.CreateDirectory(dataDir);
        using var dir = DirAccess.Open("res://data/sim") ?? throw new System.InvalidOperationException("res://data/sim missing from the build");
        foreach (var name in dir.GetFiles())
        {
            if (name.EndsWith(".json", System.StringComparison.Ordinal))
            {
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dataDir, name), Godot.FileAccess.GetFileAsBytes($"res://data/sim/{name}"));
            }
        }

        _data = BattleData.Load(root);
        _rules = BattleRules.Load(System.IO.Path.Combine(dataDir, "rules.json"));
    }
}

/// <summary>What the menus chose for the next battle, and how it ended (read by the debrief).</summary>
public static class GameSession
{
    public static string Era = "coldwar";
    public static string PlayerDeck = "us_1989_armored";
    public static string EnemyDeck = "su_1989_motor_rifle";
    public static ulong Seed = 1;
    public static string Origin = "skirmish";

    /// <summary>MEC-06 skirmish toggle.</summary>
    public static bool FieldRequests;

    /// <summary>MODE-01 skirmish options.</summary>
    public static int PerSide = 1;
    public static int StartDp = 1000;
    public static string? Income;
    public static int VictoryType;
    public static int TimeOfDay = 1;
    public static int Weather;

    /// <summary>UX-03: the running tutorial ("command", "support", "fly") or null; starting SP for skirmish/tutorials.</summary>
    public static string? Tutorial;
    public static int StartSp;
    public static int Winner = -1;
    public static int[] Score = [0, 0];
    public static int[] Kills = [0, 0];
    public static int[] Deployed = [0, 0];
    public static int Ticks;
}

/// <summary>UX-02 settings, persisted in user://settings.cfg.</summary>
public static class GameSettings
{
    public static readonly float[] UiScales = [0.75f, 1f, 1.25f, 1.5f];
    public static readonly string[] Palettes = ["standard", "deuteranopia", "protanopia", "tritanopia"];
    public static int UiScaleIndex = 1;
    public static int Palette;
    public static bool Subtitles = true;
    const string Path = "user://settings.cfg";

    /// <summary>Side colors per palette (friend, enemy): Okabe-Ito pairs that stay distinct under each color-vision
    /// deficiency.</summary>
    public static readonly (Color Friend, Color Enemy)[] SideColors =
    [
        (new Color(0.25f, 0.5f, 0.95f), new Color(0.9f, 0.25f, 0.2f)),
        (new Color(0f, 0.447f, 0.698f), new Color(0.902f, 0.624f, 0f)),
        (new Color(0f, 0.447f, 0.698f), new Color(0.941f, 0.894f, 0.259f)),
        (new Color(0.835f, 0.369f, 0f), new Color(0f, 0.62f, 0.451f)),
    ];

    public static void Load()
    {
        var cfg = new ConfigFile();
        var loaded = cfg.Load(Path) == Error.Ok;
        Controls.Load(cfg);
        if (loaded)
        {
            UiScaleIndex = System.Math.Clamp((int)cfg.GetValue("ui", "scale_index", 1), 0, UiScales.Length - 1);
            Palette = System.Math.Clamp((int)cfg.GetValue("ui", "palette", 0), 0, Palettes.Length - 1);
            Subtitles = (bool)cfg.GetValue("ui", "subtitles", true);
        }
    }

    public static void Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("ui", "scale_index", UiScaleIndex);
        cfg.SetValue("ui", "palette", Palette);
        cfg.SetValue("ui", "subtitles", Subtitles);
        Controls.Save(cfg);
        cfg.Save(Path);
    }

    public static void Apply(SceneTree tree) => tree.Root.ContentScaleFactor = UiScales[UiScaleIndex];
}
