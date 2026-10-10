using System.Linq;
using Godot;

/// <summary>UX-01 debrief after a battle.</summary>
public partial class DebriefScreen : Node
{
    public override void _Ready()
    {
        var you = GameSession.Winner == GameSession.MyTeam;
        var box = Ui.Screen(this, you ? "VICTORY" : GameSession.Winner == 1 - GameSession.MyTeam ? "DEFEAT" : "DRAW");
        box.AddChild(new Label
        {
            Text = $"Score {GameSession.Score[0]} to {GameSession.Score[1]}.  Kills {GameSession.Kills[0]} to {GameSession.Kills[1]}.  "
                + $"Deployed {GameSession.Deployed[0]} and {GameSession.Deployed[1]} units.  {GameSession.Ticks / 20 / 60} minutes.",
        });
        if (GameSession.Origin == "campaign")
        {
            CampaignScreen.Record(you);
            GameSession.Origin = "skirmish";
            Ui.Button(box, "Continue", "Continue the campaign", () => { GameSession.EndLan(); Ui.Go(this, new CampaignScreen()); });
        }

        Ui.Button(box, "MainMenu", "Main menu", () => { GameSession.EndLan(); Ui.Go(this, new Main()); });
        UxLog.Event("screen", ("name", "debrief"));
        UxLog.Event("debrief", ("you_team", GameSession.MyTeam), ("result", you ? "victory" : GameSession.Winner == 1 - GameSession.MyTeam ? "defeat" : "draw"), ("score0", GameSession.Score[0]), ("score1", GameSession.Score[1]));
    }

    /// <summary>Keeps a finished LAN battle's endpoint pumping so the last packets reach the other peers.</summary>
    public override void _Process(double delta)
    {
        var now = (long)Time.GetTicksMsec();
        GameSession.LanHost?.Pump(now);
        GameSession.LanClient?.Pump(now);
    }
}
