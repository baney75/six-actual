using Godot;
using SixActual.Sim.Battle;

/// <summary>MODE-02 operation briefing: the voiced line (generated locally, D065) with the briefing text, then deploy
/// into the operation's battle (decks, victory type, time of day and weather from data/campaign.json).</summary>
public partial class BriefingScreen : Node
{
    public override void _Ready()
    {
        var op = CampaignScreen.Next!;
        var box = Ui.Screen(this, op.Name.ToUpperInvariant(), $"Operation {CampaignScreen.Current.Path.Count + 1} of 6.  {op.TimeOfDay}, {op.Weather}.  Victory: {op.Victory}.");
        box.AddChild(new Label { Name = "BriefingText", Text = $"{op.Speaker}: {op.Briefing}", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(900, 0) });
        var stream = ResourceLoader.Exists(op.Audio) ? GD.Load<AudioStream>(op.Audio) : null;
        var player = new AudioStreamPlayer { Name = "BriefingVoice", Stream = stream, Bus = "Master" };
        AddChild(player);
        if (stream is not null)
        {
            player.Play();
        }

        Ui.Button(box, "Deploy", "Deploy", () =>
        {
            GameSession.Origin = "campaign";
            GameSession.Era = CampaignScreen.Current.Era;
            GameSession.PlayerDeck = op.PlayerDeck;
            GameSession.EnemyDeck = op.EnemyDeck;
            GameSession.Seed = op.Seed;
            GameSession.Map = null;
            GameSession.FieldRequests = true;
            GameSession.PerSide = 1;
            GameSession.StartDp = 1000;
            GameSession.Income = null;
            GameSession.VictoryType = op.Victory == "annihilation" ? 1 : 0;
            GameSession.TimeOfDay = System.Array.IndexOf(new[] { "dawn", "day", "dusk", "night" }, op.TimeOfDay);
            GameSession.Weather = System.Array.IndexOf(BattleRules.WeatherNames, op.Weather);
            UxLog.Event("campaign_operation", ("operation", op.Id), ("seed", (long)op.Seed));
            Ui.Go(this, new SkirmishView());
        });
        Ui.Button(box, "Back", "Back", () => Ui.Go(this, new CampaignScreen()));
        UxLog.Event("screen", ("name", "briefing"));
        UxLog.Event("briefing", ("operation", op.Id), ("level", op.Level), ("speaker", op.Speaker), ("voiced", stream is not null), ("duration_ms", stream is null ? 0 : (int)(stream.GetLength() * 1000)),
            ("subtitle", GameSettings.Subtitles));
    }
}
