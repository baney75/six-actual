using Godot;

/// <summary>UX-03 tutorial menu: command basics, support, Take Control, each a guided battle.</summary>
public partial class TutorialScreen : Node
{
    public override void _Ready()
    {
        var box = Ui.Screen(this, "TUTORIALS", "Guided battles. Each step waits until you have done it.");
        foreach (var (id, (title, steps)) in Tutorial.All)
        {
            var tid = id;
            Ui.Button(box, $"Tutorial_{id}", $"{title} ({steps.Length} steps)", () =>
            {
                GameSession.Tutorial = tid;
                GameSession.Origin = "tutorial";
                GameSession.Era = "coldwar";
                GameSession.PlayerDeck = "us_1989_armored";
                GameSession.EnemyDeck = "su_1989_motor_rifle";
                GameSession.Seed = 7;
                GameSession.Map = null;
                GameSession.PerSide = 1;
                GameSession.StartDp = 2000;
                GameSession.StartSp = tid == "support" ? 200 : 0;
                GameSession.VictoryType = 0;
                GameSession.TimeOfDay = 1;
                GameSession.Weather = 0;
                GameSession.FieldRequests = false;
                Ui.Go(this, new SkirmishView());
            });
        }

        Ui.Button(box, "Back", "Back", () => Ui.Go(this, new Main()));
        UxLog.Event("screen", ("name", "tutorials"));
    }
}
