using System.Collections.Generic;
using System.Linq;
using Godot;
using SixActual.Sim;
using SixActual.Sim.Battle;
using SixActual.Sim.State.Battle;

/// <summary>
/// AUD-03 / DET-08 world sound (BRIEF D7, D068, D071).
/// <list type="bullet">
/// <item>The listener sits at the camera's ground focus point (the battlefield under the screen centre).</item>
/// <item>Distance bands: near under 50 m, mid 50-500 m, far beyond; Godot's inverse attenuation fades further.</item>
/// <item>Sound arrives delayed by distance / 343 m/s, counted in game time.</item>
/// <item>Occlusion from the sim's own line of sight (<see cref="Sight.HasLine"/>): low-pass at 900 Hz and -6 dB.</item>
/// <item>A round passing within 30 m of the listener cracks at once, ahead of its delayed report.</item>
/// <item>Concurrency limits per category (weapons 8, explosions 4, cracks 2); over the limit the farthest is culled.</item>
/// <item>Reverb zone at the listener from the terrain: interior (inside a building), town (buildings near), forest,
/// valley (lower than its surroundings) or open; the SFX bus reverb changes with it.</item>
/// </list>
/// </summary>
public partial class SfxPlayer : Node3D
{
    const float SpeedOfSound = 343f;
    static readonly Dictionary<string, int> Limits = new() { ["weapon"] = 8, ["explosion"] = 4, ["crack"] = 2 };
    readonly List<AudioStreamPlayer3D> _free = [];
    readonly List<(AudioStreamPlayer3D Player, string Category, float Distance)> _busy = [];
    readonly Dictionary<string, AudioStream?> _cache = [];
    readonly Dictionary<string, int> _stats = [];
    readonly List<(double DueTick, string Path, string Category, Vector3 At, float Distance, bool Occluded, double CalledTick, bool RealTime)> _pending = [];

    /// <summary>True while the battle runs at real-time speed (set by the view each frame); delay precision is only
    /// measured then, since under fast-forward a frame spans many ticks.</summary>
    public bool RealTime { get; set; }
    int _zoneTick = -1000;
    int _playLogged;
    int _logged;
    int _variant;
    string _zone = "";
    AudioEffectReverb _reverb = null!;

    public Vector3 Listener { get; set; }

    public override void _Ready()
    {
        if (AudioServer.GetBusIndex("Sfx") < 0)
        {
            AudioServer.AddBus();
            var bus = AudioServer.BusCount - 1;
            AudioServer.SetBusName(bus, "Sfx");
            AudioServer.AddBusEffect(bus, new AudioEffectReverb());
        }

        _reverb = (AudioEffectReverb)AudioServer.GetBusEffect(AudioServer.GetBusIndex("Sfx"), 0);
        for (var i = 0; i < 20; i++)
        {
            var p = new AudioStreamPlayer3D { AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance, UnitSize = 60, MaxDistance = 6000, Bus = "Sfx" };
            AddChild(p);
            _free.Add(p);
        }
    }

    AudioStream? Load(string path)
    {
        if (!_cache.TryGetValue(path, out var s))
        {
            s = ResourceLoader.Exists(path) ? GD.Load<AudioStream>(path) : null;
            _cache[path] = s;
        }

        return s;
    }

    /// <summary>Schedules a family ("rifle", "mg", "cannon", "explosion_small/medium/large") heard from the listener.
    /// <paramref name="nowTick"/> is the fractional sim time of the event (ticks plus the part of a tick elapsed).</summary>
    public void Play(WorldState w, BattleRules r, string family, float x, float y, in UnitState source, double nowTick)
    {
        var dist = new Vector2(x - Listener.X, y - Listener.Z).Length();
        var band = dist < 50 ? "near" : dist < 500 ? "mid" : "far";
        _variant = (_variant % 4) + 1;
        var explosion = family.StartsWith("explosion", System.StringComparison.Ordinal);
        var path = explosion ? $"res://assets/audio/sfx/explosion/{family}_v{_variant}.wav" : $"res://assets/audio/sfx/{family}/{family}_{band}_v{_variant}.wav";
        var ear = source;
        ear.X = Fp.FromInt((int)Listener.X);
        ear.Y = Fp.FromInt((int)Listener.Z);
        var occluded = !Sight.HasLine(w, r, ear, source);
        var delayTicks = dist / SpeedOfSound * 20.0;
        _pending.Add((nowTick + delayTicks, path, explosion ? "explosion" : "weapon", new Vector3(x, 2, y), dist, occluded, nowTick, RealTime));
        Count(band);
        if (occluded)
        {
            Count("occluded");
        }

    }

    /// <summary>A shot whose path from shooter to target passes within 30 m of the listener also cracks overhead.</summary>
    public void PassBy(Vector2 shooter, Vector2 target, double tick)
    {
        var l = new Vector2(Listener.X, Listener.Z);
        var seg = target - shooter;
        var t = Mathf.Clamp((l - shooter).Dot(seg) / Mathf.Max(1f, seg.LengthSquared()), 0, 1);
        var miss = (shooter + (seg * t) - l).Length();
        if (miss > 30 || (shooter - l).Length() < 60)
        {
            return;
        }

        _variant = (_variant % 4) + 1;
        _pending.Add((tick, $"res://assets/audio/sfx/crack/crack_v{_variant}.wav", "crack", Listener + new Vector3(0, 3, 0), miss, false, tick, RealTime));
        Count("crack");
        UxLog.Event("supersonic_crack", ("miss_m", (int)miss), ("tick", (int)tick));
    }

    /// <summary>Starts the sounds whose travel time has elapsed; call once per frame with the fractional sim time.</summary>
    public void Pump(WorldState w, BattleRules r, double tick)
    {
        for (var i = _busy.Count - 1; i >= 0; i--)
        {
            if (!_busy[i].Player.Playing)
            {
                _free.Add(_busy[i].Player);
                _busy.RemoveAt(i);
            }
        }

        for (var i = 0; i < _pending.Count; i++)
        {
            var s = _pending[i];
            if (s.DueTick > tick)
            {
                continue;
            }

            _pending.RemoveAt(i--);
            var stream = Load(s.Path);
            if (stream is null)
            {
                Count("missing");
                continue;
            }

            // Concurrency per category; over the limit the farthest sound of that category is culled.
            var same = _busy.Where(b => b.Category == s.Category).ToList();
            if (same.Count >= Limits[s.Category])
            {
                var far = same.OrderByDescending(b => b.Distance).First();
                if (far.Distance <= s.Distance)
                {
                    Count("culled");
                    continue;
                }

                far.Player.Stop();
                _busy.Remove(far);
                _free.Add(far.Player);
                Count("culled");
            }

            if (_free.Count == 0)
            {
                Count("culled");
                continue;
            }

            var p = _free[^1];
            _free.RemoveAt(_free.Count - 1);
            p.Stream = stream;
            p.Position = s.At;
            p.AttenuationFilterCutoffHz = s.Occluded ? 900 : 20500;
            p.VolumeDb = s.Occluded ? -6 : 0;
            p.Play();
            _busy.Add((p, s.Category, s.Distance));
            if (s.Category != "crack" && _logged++ < 30)
            {
                // Read back from the player: the filter and gain it is actually playing with (G13 checks these).
                var file = s.Path[(s.Path.LastIndexOf('/') + 1)..];
                UxLog.Event("sfx", ("family", file.Split('_')[0]), ("file", file), ("band", s.Distance < 50 ? "near" : s.Distance < 500 ? "mid" : "far"),
                    ("distance_m", (int)s.Distance), ("occluded", s.Occluded), ("cutoff_hz", (int)p.AttenuationFilterCutoffHz), ("volume_db", (int)p.VolumeDb), ("tick", w.Tick));
            }
            if (s.RealTime && RealTime && s.Category != "crack" && _playLogged++ < 30)
            {
                // D1 distance-delay test: the sound starts distance / 343 m/s after the event, within one frame.
                var actual = (tick - s.CalledTick) * 50.0;
                var expected = s.Distance / SpeedOfSound * 1000.0;
                UxLog.Event("sfx_play", ("distance_m", (int)s.Distance), ("expected_delay_ms", (int)expected), ("actual_delay_ms", (int)actual), ("delay_error_ms", (int)System.Math.Round(actual - expected)));
            }
        }

        UpdateZone(w, r);
    }

    void UpdateZone(WorldState w, BattleRules r)
    {
        if (w.Tick - _zoneTick < 40)
        {
            return;
        }

        _zoneTick = w.Tick;

        var x = Fp.FromInt((int)Listener.X);
        var y = Fp.FromInt((int)Listener.Z);
        var buildings = CountBuildings(w, (int)Listener.X, (int)Listener.Z, 150);
        var ground = Sight.GroundDm(w.Terrain, x, y);
        var around = 0;
        for (var k = 0; k < 8; k++)
        {
            var a = k * Mathf.Pi / 4;
            around += Sight.GroundDm(w.Terrain, Fp.FromInt((int)(Listener.X + (400 * Mathf.Cos(a)))), Fp.FromInt((int)(Listener.Z + (400 * Mathf.Sin(a)))));
        }

        var inside = System.Array.Exists(w.Buildings, b => Listener.X >= b.X && Listener.X <= b.X + (b.Nx * r.ChunkM) && Listener.Z >= b.Y && Listener.Z <= b.Y + (b.Ny * r.ChunkM));
        var zone = inside ? "interior" : buildings >= 3 ? "town" : Sight.ForestAt(w.Terrain, x, y) > 0 ? "forest" : (around / 8) - ground > 200 ? "valley" : "open";
        if (zone == _zone)
        {
            return;
        }

        _zone = zone;
        (_reverb.RoomSize, _reverb.Damping, _reverb.Wet) = zone switch
        {
            "interior" => (0.35f, 0.2f, 0.45f),
            "town" => (0.7f, 0.3f, 0.35f),
            "forest" => (0.5f, 0.8f, 0.25f),
            "valley" => (0.9f, 0.4f, 0.3f),
            _ => (0.2f, 0.6f, 0.08f),
        };
        UxLog.Event("reverb_zone", ("zone", zone), ("room", _reverb.RoomSize), ("wet", _reverb.Wet), ("tick", w.Tick));
    }

    static int CountBuildings(WorldState w, int x, int y, int radius)
    {
        var n = 0;
        foreach (var b in w.Buildings)
        {
            var dx = b.X - x;
            var dy = b.Y - y;
            if ((dx * dx) + (dy * dy) <= radius * radius)
            {
                n++;
            }
        }

        return n;
    }

    void Count(string k) => _stats[k] = _stats.GetValueOrDefault(k) + 1;

    public void LogStats(int tick)
    {
        UxLog.Event("sfx_stats", ("near", _stats.GetValueOrDefault("near")), ("mid", _stats.GetValueOrDefault("mid")), ("far", _stats.GetValueOrDefault("far")),
            ("occluded", _stats.GetValueOrDefault("occluded")), ("culled", _stats.GetValueOrDefault("culled")), ("crack", _stats.GetValueOrDefault("crack")),
            ("missing", _stats.GetValueOrDefault("missing")), ("busy", _busy.Count), ("pending", _pending.Count), ("tick", tick));
    }
}
