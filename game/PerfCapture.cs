using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

/// <summary>
/// PERF-01 / PERF-02 capture (D074): `--perf &lt;seconds&gt; &lt;out.json&gt; [--res WxH]` runs a rendered 5v5 AI
/// battle at real-time speed, vsync off, with an off-map barrage every 30 s, records every frame time and the static
/// memory once a second, then writes percentiles, frames over 100 ms and memory growth after the first 5 minutes.
/// </summary>
public partial class PerfCapture : Node
{
    // Frame-time histogram in 0.01 ms bins up to 1 s, preallocated: storing every frame (about 1000 per second) grew
    // the managed heap by tens of MB over 20 minutes and polluted the very memory number this mode measures.
    readonly int[] _hist = new int[100_001];
    int _timedFrames;
    double _maxMs;

    // PERF-02 allows no warm-up: the worst frame and the 100 ms count cover every frame from the first.
    double _maxAllMs;
    int _over100All;
    readonly List<(double T, long Bytes)> _memory = [];
    readonly List<long> _video = [];
    readonly int[] _gpuHist = new int[10_001];
    int _gpuFrames;
    readonly List<object> _spikes = [];
    readonly List<object> _census = [];
    ulong _lastUsec;
    double _elapsed;
    double _nextSample;
    double _seconds;
    string _out = "";
    string _res = "";
    Vector2I _size;
    SubViewport? _offscreen;
    Camera3D? _offCam;

    public static bool Start(Node root)
    {
        var args = OS.GetCmdlineUserArgs();
        var at = System.Array.IndexOf(args, "--perf");
        if (at < 0 || at + 2 >= args.Length)
        {
            return false;
        }

        var cap = new PerfCapture { Name = "PerfCapture", _seconds = double.Parse(args[at + 1], System.Globalization.CultureInfo.InvariantCulture), _out = System.IO.Path.GetFullPath(args[at + 2]) };
        var r = System.Array.IndexOf(args, "--res");
        if (r >= 0 && r + 1 < args.Length)
        {
            var wh = args[r + 1].Split('x');
            cap._size = new Vector2I(int.Parse(wh[0], System.Globalization.CultureInfo.InvariantCulture), int.Parse(wh[1], System.Globalization.CultureInfo.InvariantCulture));
            cap._res = args[r + 1];
        }

        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        GameSession.PerSide = 5;
        GameSession.StartDp = 2000;
        GameSession.StartSp = 100000;
        GameSession.Seed = 11;
        GameSession.PerfAllAi = true;
        root.CallDeferred(Node.MethodName.AddChild, cap);
        root.CallDeferred(Node.MethodName.AddChild, new SkirmishView());
        return true;
    }

    public override void _Ready() => ProcessMode = ProcessModeEnum.Always;

    // A tiling window manager ignores requested window sizes, so the 3D world renders into an offscreen viewport at
    // exactly the requested resolution (main-window 3D off), shown scaled in the window. UI stays at window size.
    void MakeOffscreen()
    {
        var view = SkirmishView.Current;
        if (_size == Vector2I.Zero || view is null || _offscreen is not null)
        {
            return;
        }

        _offscreen = new SubViewport { Size = _size, World3D = GetViewport().World3D, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        _offCam = new Camera3D { Current = true, Far = view.ViewCamera.Far, Fov = view.ViewCamera.Fov };
        _offscreen.AddChild(_offCam);
        AddChild(_offscreen);
        var layer = new CanvasLayer { Layer = -1 };
        layer.AddChild(new TextureRect { Texture = _offscreen.GetTexture(), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, AnchorRight = 1, AnchorBottom = 1, MouseFilter = Control.MouseFilterEnum.Ignore });
        AddChild(layer);
        GetViewport().Disable3D = true;
    }

    static double HistPct(int[] hist, int count, double p)
    {
        var want = (long)(count * p);
        long seen = 0;
        for (var i = 0; i < hist.Length; i++)
        {
            seen += hist[i];
            if (seen > want)
            {
                return i / 100.0;
            }
        }

        return (hist.Length - 1) / 100.0;
    }

    static long RssBytes()
    {
        var statm = System.IO.File.ReadAllText("/proc/self/statm").Split(' ');
        return long.Parse(statm[1], System.Globalization.CultureInfo.InvariantCulture) * System.Environment.SystemPageSize;
    }

    public override void _Process(double delta)
    {
        MakeOffscreen();
        if (_offCam is not null && SkirmishView.Current is { } v)
        {
            _offCam.GlobalTransform = v.ViewCamera.GlobalTransform;
        }

        // Wall-clock frame time: Godot's delta smoothing snaps `delta` to display-rate multiples.
        var now = Time.GetTicksUsec();
        var ms = _lastUsec == 0 ? delta * 1000.0 : (now - _lastUsec) / 1000.0;
        _lastUsec = now;
        _elapsed += ms / 1000.0;
        _maxAllMs = System.Math.Max(_maxAllMs, ms);
        _over100All += ms >= 100 ? 1 : 0;
        if (_elapsed > 10)
        {
            _hist[System.Math.Min(_hist.Length - 1, (int)(ms * 100))]++;
            _timedFrames++;
            _maxMs = System.Math.Max(_maxMs, ms);
        }
        if (ms > 33 && _elapsed > 10)
        {
            _spikes.Add(new { t = System.Math.Round(_elapsed, 2), ms = System.Math.Round(ms, 1), tick = SkirmishView.Current?.SimTick ?? 0, gc0 = System.GC.CollectionCount(0), gc2 = System.GC.CollectionCount(2) });
        }
        var rid = (_offscreen ?? (Viewport)GetViewport()).GetViewportRid();
        RenderingServer.ViewportSetMeasureRenderTime(rid, true);
        var gpu = RenderingServer.ViewportGetMeasuredRenderTimeGpu(rid);
        if (_elapsed > 10 && gpu > 0)
        {
            _gpuHist[System.Math.Min(_gpuHist.Length - 1, (int)(gpu * 100))]++;
            _gpuFrames++;
        }
        if (_elapsed >= _nextSample)
        {
            _nextSample += 1.0;
            _memory.Add((_elapsed, RssBytes()));
            _video.Add((long)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.VideoMemUsed));
            if (_memory.Count % 30 == 1)
            {
                _census.Add(new
                {
                    t = (int)_elapsed,
                    nodes = (int)Performance.GetMonitor(Performance.Monitor.ObjectNodeCount),
                    objects = (int)Performance.GetMonitor(Performance.Monitor.ObjectCount),
                    resources = (int)Performance.GetMonitor(Performance.Monitor.ObjectResourceCount),
                    orphans = (int)Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount),
                    gc_heap_mb = System.Math.Round(System.GC.GetTotalMemory(false) / 1048576.0, 1),
                    gc_committed_mb = System.Math.Round(System.GC.GetGCMemoryInfo().TotalCommittedBytes / 1048576.0, 1),
                    tex_mb = System.Math.Round(RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TextureMemUsed) / 1048576.0, 1),
                    buf_mb = System.Math.Round(RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.BufferMemUsed) / 1048576.0, 1),
                    rss_mb = System.Math.Round(_memory[^1].Bytes / 1048576.0, 1),
                });
            }
        }

        if (_elapsed >= _seconds)
        {
            SetProcess(false);
            Write();
            SkirmishView.Current?.QueueFree();
            GetTree().CreateTimer(0.2).Timeout += () => GetTree().Quit(0);
        }
    }

    void Write()
    {
        // The first 10 s (loading, shader compilation) are left out of the frame statistics.
        double Pct(double p)
        {
            var want = (long)(_timedFrames * p);
            long seen = 0;
            for (var i = 0; i < _hist.Length; i++)
            {
                seen += _hist[i];
                if (seen > want)
                {
                    return i / 100.0;
                }
            }

            return _maxMs;
        }

        var over100 = 0;
        for (var i = 10_000; i < _hist.Length; i++)
        {
            over100 += _hist[i];
        }

        var after5 = _memory.Where(m => m.T >= 300).ToList();
        var memStart = after5.Count > 0 ? after5[0].Bytes : (_memory.Count > 0 ? _memory[0].Bytes : 0);
        var memEnd = _memory.Count > 0 ? _memory[^1].Bytes : 0;
        var report = new Dictionary<string, object>
        {
            ["resolution"] = _res.Length > 0 ? _res : $"{GetViewport().GetVisibleRect().Size.X}x{GetViewport().GetVisibleRect().Size.Y}",
            ["window_size"] = DisplayServer.WindowGetSize().ToString(),
            ["render_size_3d"] = _offscreen is null ? DisplayServer.WindowGetSize().ToString() : _offscreen.Size.ToString(),
            ["sim_ticks"] = SkirmishView.Current?.SimTick ?? 0,
            ["seconds"] = _elapsed,
            ["frames"] = _timedFrames,
            ["p50_ms"] = Pct(0.50),
            ["p95_ms"] = Pct(0.95),
            ["p99_ms"] = Pct(0.99),
            ["max_ms"] = _maxAllMs,
            ["max_ms_after_10s"] = _maxMs,
            ["gpu_render_ms_p50"] = HistPct(_gpuHist, _gpuFrames, 0.50),
            ["gpu_render_ms_p95"] = HistPct(_gpuHist, _gpuFrames, 0.95),
            ["frames_over_100ms"] = _over100All,
            ["frames_over_100ms_after_10s"] = over100,
            ["spikes_over_33ms"] = _spikes,
            ["census_every_30s"] = _census,
            ["gc_gen0"] = System.GC.CollectionCount(0),
            ["gc_gen2"] = System.GC.CollectionCount(2),
            ["memory_metric"] = "process RSS (/proc/self/statm)",
            ["rss_mb_at_5min"] = memStart / 1048576.0,
            ["rss_mb_end"] = memEnd / 1048576.0,
            ["rss_mb_max"] = _memory.Count == 0 ? 0 : _memory.Max(m => m.Bytes) / 1048576.0,
            ["video_mem_mb_end"] = _video.Count == 0 ? 0 : _video[^1] / 1048576.0,
            ["rss_mb_per_second"] = _memory.Select(m => System.Math.Round(m.Bytes / 1048576.0, 1)).ToArray(),
            ["memory_growth_pct_after_5min"] = memStart > 0 ? 100.0 * (memEnd - memStart) / memStart : 0,
            ["units_at_end"] = SkirmishView.Current?.UnitCount ?? 0,
            ["renderer"] = RenderingServer.GetVideoAdapterName(),
            ["vsync"] = "disabled",
        };
        System.IO.File.WriteAllText(_out, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        GD.Print($"perf written {_out}");
    }
}
