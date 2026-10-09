using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

/// <summary>
/// G13 game-state event log: one JSON object per line ({"frame", "event", ...fields}) in the file given by
/// `--ux-log`, and on stdout. Screens log what happened (screen shown, deck chosen, unit deployed, control taken),
/// never what the input script did, so a check on the log proves the game reached that state.
/// </summary>
public static class UxLog
{
    static string? _path;
    static readonly List<string> Seen = [];

    public static void Init(string? path)
    {
        _path = path;
        if (_path is not null)
        {
            System.IO.File.WriteAllText(_path, string.Empty);
        }
    }

    public static void Event(string name, params (string Key, object Value)[] fields)
    {
        var row = new Dictionary<string, object> { ["frame"] = Engine.GetProcessFrames(), ["event"] = name };
        foreach (var (k, v) in fields)
        {
            row[k] = v;
        }

        var line = JsonSerializer.Serialize(row);
        Logged?.Invoke(name, row);
        GD.Print($"ux {line}");
        Seen.Add(name);
        if (_path is not null)
        {
            System.IO.File.AppendAllText(_path, line + "\n");
        }
    }

    public static int Count(string name) => Seen.FindAll(e => e == name).Count;

    /// <summary>Raised for every logged event (tutorials complete their steps on game-state events).</summary>
    public static event System.Action<string, Dictionary<string, object>>? Logged;
}

/// <summary>
/// G13 input replay: runs a recorded script (`--ux <file>`) against the live UI. Steps:
/// {"press": pattern} presses the first visible, enabled button whose name matches (wildcards allowed); {"select": node, "index": n} picks an OptionButton item;
/// {"key": "T"} sends a key press and release; {"wait_event": name, "count": n, "timeout_s": s} waits for the n-th
/// logged event; {"frames": n} waits; {"speed": ticks_per_frame} sets the battle's time compression.
/// A step that cannot complete in its timeout fails the script; the game then exits with code 1.
/// </summary>
public partial class UxDriver : Node
{
    JsonElement[] _steps = [];
    int _index;
    int _frames;
    ulong _stepStartMs;

    public static UxDriver? Start(Node root)
    {
        var args = OS.GetCmdlineUserArgs();
        var at = System.Array.IndexOf(args, "--ux");
        var logAt = System.Array.IndexOf(args, "--ux-log");
        UxLog.Init(logAt >= 0 && logAt + 1 < args.Length ? args[logAt + 1] : null);
        if (at < 0 || at + 1 >= args.Length)
        {
            return null;
        }

        // A headless window is 64x64. Scripts run at a fixed 1280x720 logical size (viewport stretch), so screen
        // positions mean the same thing on every machine; input is mapped through the stretch transform like real input.
        if (root is Window win)
        {
            win.ContentScaleMode = Window.ContentScaleModeEnum.Viewport;
            win.ContentScaleSize = new Vector2I(1280, 720);
        }

        using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(args[at + 1]));
        var driver = new UxDriver { Name = "UxDriver", _steps = [.. doc.RootElement.GetProperty("steps").EnumerateArray().Select(s => s.Clone())] };
        root.CallDeferred(Node.MethodName.AddChild, driver);
        UxLog.Event("ux_start", ("script", args[at + 1]), ("steps", driver._steps.Length));
        return driver;
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _stepStartMs = Time.GetTicksMsec();
    }

    public override void _Process(double delta)
    {
        if (_index >= _steps.Length)
        {
            Finish(0);
            return;
        }

        var s = _steps[_index];
        var timeout = s.TryGetProperty("timeout_s", out var t) ? t.GetInt32() : 20;
        if (Time.GetTicksMsec() - _stepStartMs > (ulong)timeout * 1000)
        {
            UxLog.Event("ux_fail", ("step", _index), ("detail", s.GetRawText()));
            Finish(1);
            return;
        }

        if (Try(s))
        {
            _index++;
            _frames = 0;
            _stepStartMs = Time.GetTicksMsec();
        }
    }

    int _exitCode = -1;

    /// <summary>Frees every screen first, then quits on a later frame, so the engine does not report resources still
    /// in use at exit (G13 fails on engine errors).</summary>
    void Finish(int code)
    {
        if (_exitCode >= 0)
        {
            return;
        }

        _exitCode = code;
        UxLog.Event(code == 0 ? "ux_done" : "ux_end", ("code", code));
        foreach (var child in GetTree().Root.GetChildren())
        {
            if (child != this)
            {
                child.QueueFree();
            }
        }

        SetProcess(false);
        GetTree().CreateTimer(0.2, processAlways: true).Timeout += () => GetTree().Quit(code);
    }

    bool Try(JsonElement s)
    {
        if (s.TryGetProperty("press", out var press))
        {
            // Wildcards allowed ("Card_*_air"): the first visible, enabled match is pressed.
            var b = GetTree().Root.FindChildren(press.GetString()!, "BaseButton", true, false).OfType<BaseButton>().FirstOrDefault(x => x.IsVisibleInTree() && !x.Disabled);
            if (b is null)
            {
                return false;
            }

            UxLog.Event("ux_press", ("node", b.Name.ToString()));
            if (b.ToggleMode)
            {
                b.ButtonPressed = !b.ButtonPressed;
            }
            else
            {
                b.EmitSignal(BaseButton.SignalName.Pressed);
            }

            return true;
        }

        if (s.TryGetProperty("select", out var select))
        {
            if (GetTree().Root.FindChild(select.GetString()!, true, false) is not OptionButton o)
            {
                return false;
            }

            var i = s.GetProperty("index").GetInt32();
            o.Select(i);
            o.EmitSignal(OptionButton.SignalName.ItemSelected, i);
            return true;
        }

        if (s.TryGetProperty("key", out var key))
        {
            var code = OS.FindKeycodeFromString(key.GetString()!);
            Input.ParseInputEvent(new InputEventKey { Keycode = code, PhysicalKeycode = code, Pressed = true });
            Input.ParseInputEvent(new InputEventKey { Keycode = code, PhysicalKeycode = code, Pressed = false });
            return true;
        }

        if (s.TryGetProperty("joy_button", out var jb))
        {
            var button = System.Enum.Parse<JoyButton>(jb.GetString()!);
            Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = true, Device = 0 });
            Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = false, Device = 0 });
            return true;
        }

        if (s.TryGetProperty("joy_axis", out var ja))
        {
            // Holds the axis at the value until another joy_axis step moves it (a HOTAS stick reports the same way).
            Input.ParseInputEvent(new InputEventJoypadMotion { Axis = System.Enum.Parse<JoyAxis>(ja.GetString()!), AxisValue = s.GetProperty("value").GetSingle(), Device = 0 });
            return true;
        }

        if (s.TryGetProperty("mouse_aim", out var ma))
        {
            // Right button held with the pointer at a fraction of the screen (-1..1 from the centre); "release" lets go.
            var size = GetViewport().GetVisibleRect().Size;
            var stretch = GetTree().Root.GetFinalTransform();
            if (ma.ValueKind == JsonValueKind.String)
            {
                Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = false });
                return true;
            }

            // Canvas position mapped to window coordinates through the stretch and UI-scale transform.
            var pos = stretch * ((size / 2) + new Vector2(ma[0].GetSingle() * size.X / 2, ma[1].GetSingle() * size.Y / 2));
            GetViewport().WarpMouse(pos);
            Input.ParseInputEvent(new InputEventMouseMotion { Position = pos, GlobalPosition = pos });
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true, Position = pos, GlobalPosition = pos });
            return true;
        }

        if (s.TryGetProperty("drag", out var dr) || s.TryGetProperty("click", out dr) || s.TryGetProperty("click_unit", out dr))
        {
            // Screen fractions (-1..1 from the centre), converted to window coordinates; click_unit clicks a friendly
            // unit's on-screen position.
            var size = GetViewport().GetVisibleRect().Size;
            var stretch = GetTree().Root.GetFinalTransform();
            Vector2 At(float fx, float fy) => stretch * ((size / 2) + new Vector2(fx * size.X / 2, fy * size.Y / 2));
            var right = s.TryGetProperty("button", out var bt) && bt.GetString() == "right";
            var button = right ? MouseButton.Right : MouseButton.Left;
            Vector2 a, b;
            if (s.TryGetProperty("click_unit", out _))
            {
                var p = SkirmishView.Current?.FriendlyScreenPos();
                if (p is null)
                {
                    return false;
                }

                a = b = stretch * p.Value;
            }
            else
            {
                a = At(dr[0].GetSingle(), dr[1].GetSingle());
                b = dr.GetArrayLength() >= 4 ? At(dr[2].GetSingle(), dr[3].GetSingle()) : a;
            }

            UxLog.Event("ux_mouse", ("from", $"{a.X:0},{a.Y:0}"), ("to", $"{b.X:0},{b.Y:0}"), ("button", button.ToString()));
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = button, Pressed = true, Position = a, GlobalPosition = a });
            Input.ParseInputEvent(new InputEventMouseMotion { Position = b, GlobalPosition = b, ButtonMask = right ? MouseButtonMask.Right : MouseButtonMask.Left });
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = button, Pressed = false, Position = b, GlobalPosition = b });
            return true;
        }

        if (s.TryGetProperty("wait_event", out var ev))
        {
            var n = s.TryGetProperty("count", out var c) ? c.GetInt32() : 1;
            return UxLog.Count(ev.GetString()!) >= n;
        }

        if (s.TryGetProperty("frames", out var f))
        {
            return ++_frames >= f.GetInt32();
        }

        if (s.TryGetProperty("speed", out var sp))
        {
            SkirmishView.TicksPerFrame = sp.GetInt32();
            return true;
        }

        throw new System.InvalidOperationException($"unknown ux step {s.GetRawText()}");
    }
}
