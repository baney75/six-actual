using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

/// <summary>
/// CMD-07 / AUD-03 radio net: commanders report by voice (lines from dialogue/lines.csv, generated locally, D066),
/// text log and map pings. Lines play on a "Radio" bus with a band-pass filter (the radio chain), one at a time from a
/// priority queue, at most one line per speaker every 30 s and the same report every 2 min of game time (escalations
/// and the XO's handover excepted).
/// Commander names come from audio/voices.json; traits from the seat's intent settings in the sim.
/// </summary>
public partial class Radio : Node
{
    public sealed record Line(string Id, string Speaker, string Trigger, int Priority, string Text, string Path);

    readonly Dictionary<(string Speaker, string Trigger), Line> _lines = [];
    readonly Dictionary<string, JsonElement> _voices = [];
    readonly Dictionary<string, int> _lastTick = [];
    readonly Dictionary<(string, string), int> _lastLine = [];
    readonly List<(Line Line, int Tick)> _queue = [];
    readonly List<string> _log = [];
    AudioStreamPlayer _player = null!;
    Label _logLabel = null!;
    string _era = "coldwar";
    string _bus = "Radio";
    AudioStreamPlayer _squelch = null!;
    int _squelchAt = int.MaxValue;

    /// <summary>True while a transmission holds the net (for music ducking).</summary>
    public bool Busy(int tick) => tick < _busyUntilTick;
    int _busyUntilTick = int.MinValue;

    public static readonly string[] Speakers = ["xo", "army1", "army2", "arty1", "arty2", "air1", "air2", "armor", "logistics"];

    public void Setup(string era, CanvasLayer hud)
    {
        _era = era;
        using (var doc = JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://data/sim/voices.json")))
        {
            foreach (var s in doc.RootElement.GetProperty("speakers").EnumerateObject())
            {
                _voices[s.Name] = s.Value.Clone();
            }
        }

        using (var doc = JsonDocument.Parse(Godot.FileAccess.GetFileAsString("res://data/sim/dialogue_lines.json")))
        {
            foreach (var row in doc.RootElement.GetProperty("lines").EnumerateArray())
            {
                string F(string k) => row.GetProperty(k).GetString()!;
                if (F("era") == era)
                {
                    _lines[(F("speaker"), F("trigger"))] = new Line(F("id"), F("speaker"), F("trigger"), int.Parse(F("priority"), System.Globalization.CultureInfo.InvariantCulture), F("text"), $"res://assets/audio/dialogue/{era}/{F("id")}.wav");
                }
            }
        }

        // BRIEF D7: the radio chain varies by era. WW2: narrow band, heavy saturation; Cold War: medium; modern: wide,
        // nearly clean. Each ends a transmission with its era's squelch tail.
        var busName = $"Radio_{era}";
        if (AudioServer.GetBusIndex(busName) < 0)
        {
            AudioServer.AddBus();
            var bus = AudioServer.BusCount - 1;
            AudioServer.SetBusName(bus, busName);
            var (hz, res, drive) = era switch { "ww2" => (1300f, 1.0f, 0.45f), "modern" => (2400f, 0.5f, 0.05f), _ => (1800f, 0.7f, 0.15f) };
            AudioServer.AddBusEffect(bus, new AudioEffectBandPassFilter { CutoffHz = hz, Resonance = res });
            AudioServer.AddBusEffect(bus, new AudioEffectDistortion { Mode = AudioEffectDistortion.ModeEnum.Clip, Drive = drive, PostGain = -2 });
        }

        _bus = busName;
        _player = new AudioStreamPlayer { Name = "RadioVoice", Bus = busName };
        AddChild(_player);
        _squelch = new AudioStreamPlayer { Name = "RadioSquelch", Bus = busName, Stream = ResourceLoader.Exists($"res://assets/audio/sfx/squelch/squelch_{era}.wav") ? GD.Load<AudioStream>($"res://assets/audio/sfx/squelch/squelch_{era}.wav") : null };
        AddChild(_squelch);
        _logLabel = new Label { Name = "RadioLog", Position = new Vector2(12, 96), CustomMinimumSize = new Vector2(620, 0), AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _logLabel.AddThemeFontSizeOverride("font_size", 13);
        hud.AddChild(_logLabel);
    }

    public string CommanderName(string speaker) => _voices.TryGetValue(speaker, out var v) ? v.GetProperty("names").GetProperty(_era).GetString()! : speaker;

    public static string Trait(int aggression, int casualtyTolerance) =>
        aggression >= 65 ? "aggressive" : aggression <= 35 ? "cautious" : casualtyTolerance <= 35 ? "careful with lives" : "steady";

    /// <summary>Queues a report; <paramref name="tick"/> is the sim tick (rate limits count game time). Returns false
    /// when the report is dropped (no line, or the speaker spoke within the last 30 s).</summary>
    public bool Report(string speaker, string trigger, int tick)
    {
        if (!_lines.TryGetValue((speaker, trigger), out var line))
        {
            return false;
        }

        // Rate limits in game time: a speaker at most every 30 s, the same report at most every 2 min.
        var urgent = line.Priority == 1;
        if (!urgent && ((_lastTick.TryGetValue(speaker, out var last) && tick - last < 600) || (_lastLine.TryGetValue((speaker, trigger), out var same) && tick - same < 2400)))
        {
            return false;
        }

        _lastTick[speaker] = tick;
        _lastLine[(speaker, trigger)] = tick;
        _queue.Add((line, tick));
        _queue.Sort((a, b) => a.Line.Priority != b.Line.Priority ? a.Line.Priority.CompareTo(b.Line.Priority) : a.Tick.CompareTo(b.Tick));
        if (_queue.Count > 4)
        {
            _queue.RemoveAt(_queue.Count - 1);
        }

        return true;
    }

    /// <summary>Called by the battle view each frame with the sim tick. A line holds the net for its length in game
    /// time (20 ticks per second): the same as real time at normal speed, compressed consistently when the battle runs
    /// faster (and a headless build's audio player never reports the end of a stream).</summary>
    public void Pump(int nowTick)
    {
        if (nowTick >= _squelchAt)
        {
            _squelchAt = int.MaxValue;
            if (_squelch.Stream is not null)
            {
                _squelch.Play();
            }
        }

        if (nowTick < _busyUntilTick || _queue.Count == 0)
        {
            return;
        }

        var (line, tick) = _queue[0];
        _queue.RemoveAt(0);
        var stream = ResourceLoader.Exists(line.Path) ? GD.Load<AudioStream>(line.Path) : null;
        if (stream is not null)
        {
            _player.Stream = stream;
            _player.Play();
            _busyUntilTick = nowTick + (int)(stream.GetLength() * 20) + 6;
            _squelchAt = nowTick + (int)(stream.GetLength() * 20);
        }

        var who = $"{CommanderName(line.Speaker)} ({line.Speaker})";
        _log.Add($"{who}: {line.Text}");
        if (_log.Count > 6)
        {
            _log.RemoveAt(0);
        }

        _logLabel.Text = string.Join("\n", _log);
        UxLog.Event("radio", ("speaker", line.Speaker), ("name", CommanderName(line.Speaker)), ("trigger", line.Trigger), ("line", line.Id), ("voiced", stream is not null),
            ("duration_ms", stream is null ? 0 : (int)(stream.GetLength() * 1000)), ("bus", _player.Bus.ToString()), ("squelch", _squelch.Stream is not null), ("subtitle", GameSettings.Subtitles), ("tick", tick));
    }
}
