using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// UX-02 / FLY-04 input: named actions with keyboard defaults and gamepad bindings (a HOTAS stick reports as a joypad
/// axis), remappable keys saved in user://settings.cfg. Mouse-aim steering for a flown aircraft is in SkirmishView.
/// </summary>
public static class Controls
{
    public static readonly (string Action, string Label, Key Key)[] Defaults =
    [
        ("take_control", "Take control", Key.T),
        ("return_control", "Return control", Key.G),
        ("pause", "Pause", Key.Space),
        ("slow_time", "Slow time", Key.Comma),
        ("fly_left", "Fly: turn left", Key.A),
        ("fly_right", "Fly: turn right", Key.D),
        ("fly_up", "Fly: climb", Key.W),
        ("fly_down", "Fly: dive", Key.S),
    ];

    static readonly Dictionary<string, Key> Keys = Defaults.ToDictionary(d => d.Action, d => d.Key);

    /// <summary>Pad binding per action: "b:&lt;JoyButton&gt;" or "a:&lt;JoyAxis&gt;:&lt;+1|-1&gt;" (a HOTAS axis reports as a joypad axis).</summary>
    static readonly Dictionary<string, string> Pads = new()
    {
        ["take_control"] = $"b:{(int)JoyButton.Y}",
        ["return_control"] = $"b:{(int)JoyButton.B}",
        ["pause"] = $"b:{(int)JoyButton.Start}",
        ["slow_time"] = $"b:{(int)JoyButton.Back}",
        ["fly_left"] = $"a:{(int)JoyAxis.LeftX}:-1",
        ["fly_right"] = $"a:{(int)JoyAxis.LeftX}:1",
        ["fly_up"] = $"a:{(int)JoyAxis.LeftY}:1",
        ["fly_down"] = $"a:{(int)JoyAxis.LeftY}:-1",
    };

    public static string PadOf(string action) => Pads[action];

    /// <summary>The pad binding an input event would make, or null if the event is not a press or a deflected axis.</summary>
    public static string? PadBinding(InputEvent e) => e switch
    {
        InputEventJoypadButton { Pressed: true } b => $"b:{(int)b.ButtonIndex}",
        InputEventJoypadMotion m when Mathf.Abs(m.AxisValue) >= 0.5f => $"a:{(int)m.Axis}:{(m.AxisValue > 0 ? 1 : -1)}",
        _ => null,
    };

    static InputEvent PadEvent(string binding)
    {
        var p = binding.Split(':');
        return p[0] == "b"
            ? new InputEventJoypadButton { ButtonIndex = (JoyButton)int.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture) }
            : new InputEventJoypadMotion { Axis = (JoyAxis)int.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture), AxisValue = int.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture) };
    }

    public static Key KeyOf(string action) => Keys[action];

    /// <summary>Builds the InputMap: the bound key plus the gamepad button or stick axis for each action.</summary>
    public static void Apply()
    {
        foreach (var (action, _, _) in Defaults)
        {
            if (!InputMap.HasAction(action))
            {
                InputMap.AddAction(action, 0.2f);
            }

            InputMap.ActionEraseEvents(action);
            InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = Keys[action], Keycode = Keys[action] });
            InputMap.ActionAddEvent(action, PadEvent(Pads[action]));
        }
    }

    public static void Rebind(string action, Key key)
    {
        Keys[action] = key;
        Apply();
    }

    public static void RebindPad(string action, string binding)
    {
        Pads[action] = binding;
        Apply();
    }

    public static void Load(ConfigFile cfg)
    {
        foreach (var (action, _, key) in Defaults)
        {
            Keys[action] = (Key)(long)cfg.GetValue("input", action, (long)key);
            Pads[action] = (string)cfg.GetValue("input_pad", action, Pads[action]);
        }

        Apply();
    }

    public static void Save(ConfigFile cfg)
    {
        foreach (var (action, _, _) in Defaults)
        {
            cfg.SetValue("input", action, (long)Keys[action]);
            cfg.SetValue("input_pad", action, Pads[action]);
        }
    }
}
