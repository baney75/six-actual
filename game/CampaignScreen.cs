using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using SixActual.Sim;

/// <summary>
/// MODE-02 campaigns: one per era from data/campaign.json (scripts/generate_campaign.py). Each operation has a voiced
/// briefing and branches: a win leads to the next advance operation, a loss to the next fallback one; every path is six
/// operations. Progress (the sim's CMD-08 campaign state plus the current operation and the path taken) is saved in
/// user://campaign.json.
/// </summary>
public partial class CampaignScreen : Node
{
    const string SavePath = "user://campaign.json";
    static readonly string[] Eras = ["ww2", "coldwar", "modern"];
    Label _status = null!;

    public sealed class Save
    {
        public string Era { get; set; } = "coldwar";
        public string Node { get; set; } = "";
        public List<string> Path { get; set; } = [];
        public Campaign.State State { get; set; } = Campaign.Start(1);
    }

    public record Operation(string Id, string Name, int Level, string PlayerDeck, string EnemyDeck, string Victory, string TimeOfDay, string Weather, ulong Seed, string Speaker, string Briefing, string Audio, string? NextWin, string? NextLoss);

    static readonly JsonSerializerOptions Json = new() { IncludeFields = true };
    static Save? _save;
    static Dictionary<string, (string Start, Dictionary<string, Operation> Ops)>? _graph;

    public static Save Current
    {
        get
        {
            if (_save is null)
            {
                var text = Godot.FileAccess.GetFileAsString(SavePath);
                _save = string.IsNullOrEmpty(text) ? null : JsonSerializer.Deserialize<Save>(text, Json);
                _save ??= New("coldwar");
            }

            return _save;
        }
    }

    public static Dictionary<string, (string Start, Dictionary<string, Operation> Ops)> Graph
    {
        get
        {
            if (_graph is null)
            {
                _graph = [];
                using var doc = JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://data/sim/campaign.json"));
                foreach (var era in doc.RootElement.GetProperty("eras").EnumerateObject())
                {
                    var ops = new Dictionary<string, Operation>();
                    foreach (var o in era.Value.GetProperty("operations").EnumerateArray())
                    {
                        string S(string k) => o.GetProperty(k).GetString()!;
                        string? N(string k) => o.GetProperty(k).ValueKind == JsonValueKind.Null ? null : o.GetProperty(k).GetString();
                        ops[S("id")] = new Operation(S("id"), S("name"), o.GetProperty("level").GetInt32(), S("player_deck"), S("enemy_deck"), S("victory"), S("time_of_day"), S("weather"),
                            o.GetProperty("seed").GetUInt64(), S("speaker"), S("briefing"), S("audio"), N("next_win"), N("next_loss"));
                    }

                    _graph[era.Name] = (era.Value.GetProperty("start").GetString()!, ops);
                }
            }

            return _graph;
        }
    }

    public static Operation? Next => Finished ? null : Graph[Current.Era].Ops[Current.Node];

    public static bool Finished => string.IsNullOrEmpty(Current.Node) || Campaign.IsFinished(Current.State);

    static Save New(string era) => new() { Era = era, Node = Graph[era].Start, State = Campaign.Start(System.Array.IndexOf(Campaign.Eras, era == "coldwar" ? "cold_war" : era)) };

    public static void Store()
    {
        using var f = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Write);
        if (f is null)
        {
            UxLog.Event("save_failed", ("file", SavePath), ("error", Godot.FileAccess.GetOpenError().ToString()));
            return;
        }

        f.StoreString(JsonSerializer.Serialize(Current, Json));
    }

    public override void _Ready()
    {
        var box = Ui.Screen(this, "CAMPAIGN", "One campaign per era, six operations on every path. A win and a loss lead to different operations. Three losses end the campaign.");
        _status = new Label { Name = "CampaignStatus", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(900, 0) };
        box.AddChild(_status);
        Ui.Button(box, "Fight", "Briefing for the next operation", () =>
        {
            if (!Finished)
            {
                Ui.Go(this, new BriefingScreen());
            }
        });
        var row = new HBoxContainer();
        box.AddChild(row);
        foreach (var era in Eras)
        {
            var e = era;
            Ui.Button(row, $"NewCampaign_{era}", $"New {era} campaign", () =>
            {
                _save = New(e);
                Store();
                UxLog.Event("campaign_new", ("era", e), ("operation", Current.Node));
                Refresh();
            });
        }

        Ui.Button(box, "Back", "Menu", () => Ui.Go(this, new Main()));
        Refresh();
        UxLog.Event("screen", ("name", "campaign"));
        UxLog.Event("campaign_started", ("era", Current.Era), ("operation", Finished ? "finished" : Current.Node), ("fought", Current.Path.Count), ("wins", Current.State.Wins), ("losses", Current.State.Losses), ("xp", Current.State.Xp));
    }

    void Refresh()
    {
        var path = string.Join(" > ", Current.Path.Select(p => Graph[Current.Era].Ops[p.Split(':')[0]].Name + (p.EndsWith(":won", System.StringComparison.Ordinal) ? " (won)" : " (lost)")));
        _status.Text = (Finished
            ? $"{Current.Era} campaign finished. Wins {Current.State.Wins}, losses {Current.State.Losses}, commander XP {Current.State.Xp}."
            : $"{Current.Era} campaign. Next: {Next!.Name} (operation {Current.Path.Count + 1} of 6).  Wins {Current.State.Wins}.  Losses {Current.State.Losses}.  XP {Current.State.Xp}.")
            + (path.Length > 0 ? $"\nPath: {path}" : string.Empty);
    }

    /// <summary>Called by the debrief: records the operation's result, follows the branch and saves.</summary>
    public static void Record(bool won)
    {
        var op = Next;
        if (op is null)
        {
            return;
        }

        Campaign.ApplyResult(Current.State, won);
        Current.Path.Add($"{op.Id}:{(won ? "won" : "lost")}");
        Current.Node = (won ? op.NextWin : op.NextLoss) ?? string.Empty;
        Store();
        UxLog.Event("campaign_result", ("operation", op.Id), ("won", won), ("next", Finished ? "finished" : Current.Node), ("wins", Current.State.Wins), ("losses", Current.State.Losses), ("xp", Current.State.Xp));
    }
}
