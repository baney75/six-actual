using System.Linq;
using Godot;

/// <summary>NET-01 / UX-01 lobby flow. A local lobby against AI slots; Steam lobbies replace the transport later.</summary>
public partial class LobbyScreen : Node
{
    int _ai;
    bool _ready;
    Label _slots = null!;

    public override void _Ready()
    {
        var box = Ui.Screen(this, "LOBBY", "Local lobby (placeholder transport: Steam lobbies arrive with NET-01). You are slot 1.");
        _slots = new Label();
        box.AddChild(_slots);
        Ui.Button(box, "AddAi", "Add AI opponent", () => { _ai++; UxLog.Event("lobby_slot_added", ("slots", 1 + _ai)); Refresh(); });
        Ui.Button(box, "Ready", "Ready", () => { _ready = true; UxLog.Event("lobby_ready"); Refresh(); });
        Ui.Button(box, "StartLobby", "Start", () =>
        {
            if (!_ready || _ai == 0)
            {
                UxLog.Event("lobby_start_refused", ("ready", _ready), ("ai", _ai));
                return;
            }

            UxLog.Event("lobby_started", ("players", 1 + _ai));
            GameSession.Origin = "lobby";
            Ui.Go(this, new DeckScreen());
        });
        Ui.Button(box, "Back", "Back", () => Ui.Go(this, new Main()));
        Refresh();
        UxLog.Event("screen", ("name", "lobby"));
        UxLog.Event("lobby_created");
    }

    void Refresh() => _slots.Text = $"Slot 1: you{(_ready ? " (ready)" : string.Empty)}\n" + string.Join("\n", Enumerable.Range(0, _ai).Select(i => $"Slot {i + 2}: AI"));
}
