using System.Collections.Generic;
using Godot;

/// <summary>
/// UX-03 tutorials: command basics, support, Take Control. Each is a list of steps shown one at a time over the battle;
/// a step completes when the game logs the event it teaches (the same game-state events G13 checks), so the tutorial
/// can only advance when the player really did the thing.
/// </summary>
public partial class Tutorial : Node
{
    public sealed record Step(string Text, string Event, string? Field = null, object? Value = null);

    public static readonly Dictionary<string, (string Title, Step[] Steps)> All = new()
    {
        ["command"] = ("Command basics", [
            new("Call a unit from your deck: press any lit card at the bottom.", "deployed"),
            new("Select a unit: left-click it on the map.", "selected"),
            new("Move it: right-click the ground where it should go.", "move_ordered"),
            new("Hand units to a commander: press Army1 in the Commanders panel, then drag a box over your units.", "sector_painted", "seat", "Army1"),
            new("Your commander now runs those units. Watch the radio log for their reports.", "seats"),
        ]),
        ["support"] = ("Support", [
            new("Off-map support costs support points (SP). Press a support option in the Support panel.", "support_armed"),
            new("Left-click the map where it should land.", "support_called"),
            new("The strike arrives after its delay. Wait for it.", "support_arrived"),
            new("Your own guns fire on call too: call an artillery card, select the battery, press Fire mission, then click a target.", "fire_mission"),
        ]),
        ["fly"] = ("Take Control", [
            new("Aircraft arrive in phase B. The clock runs fast until then.", "phase", "phase", "B"),
            new("Call an aircraft card.", "deployed_air"),
            new("Press Take control (T). The XO runs the battle while you fly.", "take_control"),
            new("Steer: A and D turn, W and S climb, or hold the right mouse button and point.", "stick_input"),
            new("Press Return (G) to hand the aircraft back and take the battle again.", "control_returned"),
        ]),
    };

    string _id = "";
    int _step;
    Label _text = null!;

    public void Start(string id, CanvasLayer hud)
    {
        _id = id;
        var panel = new PanelContainer { Name = "TutorialPanel", Position = new Vector2(380, 440), CustomMinimumSize = new Vector2(560, 90), MouseFilter = Control.MouseFilterEnum.Ignore };
        hud.AddChild(panel);
        _text = new Label { Name = "TutorialText", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(540, 0) };
        panel.AddChild(_text);
        UxLog.Logged += OnLogged;
        if (id == "fly")
        {
            SkirmishView.TicksPerFrame = 200; // fast-forward to phase B, when aircraft arrive
        }

        Show();
        UxLog.Event("tutorial_started", ("tutorial", id), ("steps", All[id].Steps.Length));
    }

    public override void _ExitTree() => UxLog.Logged -= OnLogged;

    void Show()
    {
        var (title, steps) = All[_id];
        _text.Text = _step < steps.Length
            ? $"{title}, step {_step + 1} of {steps.Length}:\n{steps[_step].Text}"
            : $"{title} complete. Press Surrender to leave, or keep playing.";
    }

    void OnLogged(string name, Dictionary<string, object> fields)
    {
        var steps = All[_id].Steps;
        if (_step >= steps.Length || name.StartsWith("tutorial", System.StringComparison.Ordinal))
        {
            return;
        }

        var s = steps[_step];
        if (name != s.Event || (s.Field is not null && (!fields.TryGetValue(s.Field, out var v) || !Equals(v?.ToString(), s.Value?.ToString()))))
        {
            return;
        }

        _step++;
        // Deferred: this runs inside UxLog.Event; logging from here would re-enter it.
        CallDeferred(MethodName.Advance, _step);
    }

    void Advance(int done)
    {
        if (_id == "fly" && done == 1)
        {
            SkirmishView.TicksPerFrame = 0;
        }

        UxLog.Event("tutorial_step", ("tutorial", _id), ("step", done), ("of", All[_id].Steps.Length));
        if (done == All[_id].Steps.Length)
        {
            UxLog.Event("tutorial_complete", ("tutorial", _id));
        }

        Show();
    }
}
