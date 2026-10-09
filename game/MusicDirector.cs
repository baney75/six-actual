using Godot;
using SixActual.Sim.State.Battle;

/// <summary>
/// AUD-03 dynamic music: three looping stems per era (calm, tension, combat; scripts/generate_music.py, D067) play
/// together and cross-fade by battle intensity, measured from the sim's events over the last 30 s of game time
/// (shots fired and units killed anywhere on the map, kills counting 5). Below 4 the calm stem leads, from 4 tension, from 20
/// combat, stepping down only below half those levels. Fades run in game time so they track the battle under time compression too.
/// </summary>
public partial class MusicDirector : Node
{
    static readonly string[] Layers = ["calm", "tension", "combat"];
    readonly AudioStreamPlayer[] _players = new AudioStreamPlayer[3];
    readonly float[] _gainDb = [-60f, -60f, -60f];
    readonly System.Collections.Generic.Queue<(int Tick, int Weight)> _recent = new();
    int _intensity;
    int _layer = -1;
    int _eventIndex;
    int _lastTick;
    bool _ducked;

    /// <summary>Battle intensity: shots fired plus 5 per kill over the last 30 s of game time.</summary>
    public int Intensity => _intensity;

    /// <summary>BRIEF D7 mix: the music ducks 6 dB under radio dialogue.</summary>
    public void Duck(bool on, int tick)
    {
        if (on != _ducked)
        {
            _ducked = on;
            UxLog.Event("music_duck", ("ducked", on), ("tick", tick));
        }
    }

    public void Setup(string era)
    {
        for (var i = 0; i < 3; i++)
        {
            var path = $"res://assets/audio/music/{era}_{Layers[i]}.ogg";
            var stream = ResourceLoader.Exists(path) ? GD.Load<AudioStream>(path) : null;
            if (stream is AudioStreamOggVorbis ogg)
            {
                ogg.Loop = true;
            }

            _players[i] = new AudioStreamPlayer { Name = $"Music_{Layers[i]}", Stream = stream, VolumeDb = -60, Bus = "Master" };
            AddChild(_players[i]);
            if (stream is not null)
            {
                _players[i].Play();
            }
        }

        UxLog.Event("music_ready", ("era", era), ("stems", System.Array.FindAll(_players, p => p.Stream is not null).Length));
    }

    /// <summary>Feeds new sim events and advances the fades; call once per frame with the world.</summary>
    public void Pump(WorldState w)
    {
        for (; _eventIndex < w.Events.Count; _eventIndex++)
        {
            var e = w.Events[_eventIndex];
            if (e.Kind is BattleEvent.Fired or BattleEvent.Killed)
            {
                _recent.Enqueue((e.Tick, e.Kind == BattleEvent.Killed ? 5 : 1));
                _intensity += e.Kind == BattleEvent.Killed ? 5 : 1;
            }
        }

        while (_recent.Count > 0 && _recent.Peek().Tick < w.Tick - 600)
        {
            _intensity -= _recent.Dequeue().Weight;
        }

        // Up at 4 (tension) and 20 (combat); down only below half of that, so the music does not flap at a threshold.
        var target = _intensity >= 20 ? 2 : _intensity >= 4 ? 1 : 0;
        if (target < _layer)
        {
            var hold = _layer == 2 && _intensity >= 10 ? 2 : _intensity >= 2 ? 1 : 0;
            target = System.Math.Max(target, hold);
        }
        if (target != _layer)
        {
            _layer = target;
            UxLog.Event("music_layer", ("layer", Layers[target]), ("intensity", _intensity), ("tick", w.Tick));
        }

        // 2 s fades in game time: 30 dB per second of battle.
        var dt = (w.Tick - _lastTick) / 20f;
        _lastTick = w.Tick;
        for (var i = 0; i < 3; i++)
        {
            var goal = i == _layer ? (_ducked ? -14f : -8f) : -60f;
            _gainDb[i] = Mathf.MoveToward(_gainDb[i], goal, 30f * dt);
            _players[i].VolumeDb = _gainDb[i];
        }
    }
}
