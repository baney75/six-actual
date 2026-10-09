using System.Collections.Generic;
using Godot;
using SixActual.Sim.State.Battle;

/// <summary>
/// DET-08 ambience beds (BRIEF D7, D071): wind always; birds by day, insects at night; rain in rain or snow; fire
/// crackle when a vehicle burned within 300 m of the listener in the last 2 minutes; distant artillery rumble when
/// shells landed beyond 1.5 km in the last 30 s. Wildlife falls silent when fighting starts (battle intensity >= 4),
/// starts recovering after 2 minutes of quiet and is fully back about a minute later. All timing is in game ticks.
/// </summary>
public partial class AmbienceDirector : Node
{
    readonly Dictionary<string, AudioStreamPlayer> _beds = [];
    readonly Dictionary<string, float> _gain = [];
    readonly List<(int Tick, float X, float Y)> _fires = [];
    readonly List<int> _farImpacts = [];
    string _wildlife = "birds";
    string _state = "";
    int _lastLoud = int.MinValue / 2;
    int _eventIndex;
    int _lastTick;

    public void Setup(int timeOfDay, string weather)
    {
        _wildlife = timeOfDay == 3 ? "insects" : "birds";
        foreach (var bed in new[] { "wind", _wildlife, "rain", "fire", "rumble" })
        {
            var path = $"res://assets/audio/sfx/ambience/ambience_{bed}.wav";
            var stream = ResourceLoader.Exists(path) ? GD.Load<AudioStream>(path) : null;
            if (stream is AudioStreamWav wav)
            {
                wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
                wav.LoopEnd = (int)(wav.GetLength() * wav.MixRate);
            }

            var p = new AudioStreamPlayer { Name = $"Ambience_{bed}", Stream = stream, VolumeDb = -60 };
            AddChild(p);
            if (stream is not null)
            {
                p.Play();
            }

            _beds[bed] = p;
            _gain[bed] = -60;
        }

        _rain = weather is "rain" or "snow";
        UxLog.Event("ambience_beds", ("wind", true), ("wildlife", _wildlife), ("rain", _rain), ("time_of_day", timeOfDay));
    }

    bool _rain;

    public void Pump(WorldState w, Vector3 listener, int intensity)
    {
        for (; _eventIndex < w.Events.Count; _eventIndex++)
        {
            var e = w.Events[_eventIndex];
            if (e.Kind == BattleEvent.Killed)
            {
                var t = System.Array.FindIndex(w.Units, u => u.Id == e.B);
                if (t >= 0 && w.Defs[w.Units[t].Def].Members <= 1)
                {
                    _fires.Add((e.Tick, w.Units[t].X.Raw / 65536f, w.Units[t].Y.Raw / 65536f));
                }
            }
            else if (e.Kind == BattleEvent.Impact && new Vector2(e.A - listener.X, e.B - listener.Z).Length() > 1500)
            {
                _farImpacts.Add(e.Tick);
            }
        }

        _fires.RemoveAll(f => f.Tick < w.Tick - 2400);
        _farImpacts.RemoveAll(t => t < w.Tick - 600);
        if (intensity >= 4)
        {
            _lastLoud = w.Tick;
        }

        var quiet = w.Tick - _lastLoud;
        var state = intensity >= 4 ? "silenced" : quiet < 2400 ? "silenced" : quiet < 3600 ? "recovering" : "normal";
        if (state != _state)
        {
            _state = state;
            UxLog.Event("ambience", ("wildlife", _wildlife), ("state", state), ("intensity", intensity), ("quiet_ticks", System.Math.Min(quiet, 1_000_000)), ("tick", w.Tick));
        }

        var nearFire = _fires.Exists(f => new Vector2(f.X - listener.X, f.Y - listener.Z).Length() < 300);
        var wildlifeDb = state switch { "normal" => -14f, "recovering" => Mathf.Lerp(-60f, -14f, (quiet - 2400) / 1200f), _ => -60f };
        Target("wind", -18);
        Target(_wildlife, wildlifeDb);
        Target("rain", _rain ? -16 : -60);
        Target("fire", nearFire ? -12 : -60);
        Target("rumble", _farImpacts.Count > 0 ? -14 : -60);
        var dt = (w.Tick - _lastTick) / 20f;
        _lastTick = w.Tick;
        foreach (var (bed, p) in _beds)
        {
            _gain[bed] = Mathf.MoveToward(_gain[bed], _targets.GetValueOrDefault(bed, -60), 20f * dt);
            p.VolumeDb = _gain[bed];
        }
    }

    readonly Dictionary<string, float> _targets = [];

    void Target(string bed, float db) => _targets[bed] = db;
}
