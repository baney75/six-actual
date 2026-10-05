using Godot;
using SixActual.Sim;
using SixActual.Sim.State;

public partial class BattleView : Node3D
{
    readonly System.Collections.Generic.Dictionary<int, Node3D> _views = new();
    BattleState _state = null!;
    Camera3D _camera = null!;
    public System.Action<int>? OnDecided;
    Label _status = null!;
    Label _result = null!;
    double _accum;
    bool _reported;
    int _selected = -1;
    bool _shot;

    public override void _Ready()
    {
        var data = ProjectSettings.GlobalizePath("res://data/units.json");
        var defs = UnitCatalog.Load(data);
        _state = BattleSim.CreateFulda(defs);
        _camera = new Camera3D { Current = true, Position = new Vector3(230, 14, 680) };
        AddChild(_camera);
        _camera.LookAt(new Vector3(185, 2, 760), Vector3.Up);
        var sun = new DirectionalLight3D { RotationDegrees = new Vector3(-48, 30, 0) };
        AddChild(sun);
        var world = new WorldEnvironment();
        var env = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(0.55f, 0.62f, 0.7f) };
        world.Environment = env;
        AddChild(world);
        var ground = new MeshInstance3D();
        var plane = new PlaneMesh { Size = new Vector2(2000, 2000) };
        ground.Mesh = plane;
        ground.Position = new Vector3(1000, 0, 1000);
        var grass = new StandardMaterial3D { AlbedoColor = new Color(0.28f, 0.34f, 0.2f) };
        ground.MaterialOverride = grass;
        AddChild(ground);
        foreach (var block in _state.Blocks)
        {
            var mesh = new MeshInstance3D();
            mesh.Mesh = new BoxMesh { Size = new Vector3(block.W, System.Math.Max(2, block.Height), block.H) };
            mesh.Position = new Vector3(block.X + (block.W / 2f), block.Height / 2f, block.Y + (block.H / 2f));
            var shade = block.Height > 20 ? new Color(0.35f, 0.38f, 0.32f) : new Color(0.12f, 0.28f, 0.14f);
            mesh.MaterialOverride = new StandardMaterial3D { AlbedoColor = shade };
            AddChild(mesh);
        }

        for (var i = 0; i < _state.Units.Length; i++)
        {
            AddChild(MakeView(i));
        }

        var layer = new CanvasLayer();
        AddChild(layer);
        _status = new Label { Position = new Vector2(16, 12), Size = new Vector2(900, 28) };
        _status.AddThemeColorOverride("font_color", Colors.White);
        layer.AddChild(_status);
        _result = new Label { Position = new Vector2(16, 44), Visible = false };
        _result.AddThemeFontSizeOverride("font_size", 28);
        layer.AddChild(_result);
        var help = new Label
        {
            Text = "WASD move  Q/E turn  wheel zoom  left select  right move  T fly  G return  Esc menu",
            Position = new Vector2(16, 980),
        };
        layer.AddChild(help);
    }

    public override void _Process(double delta)
    {
        if (_state.Winner < 0)
        {
            _accum += delta;
            var orders = ReadOrders();
            while (_accum >= 0.05)
            {
                BattleSim.Step(_state, orders);
                orders = System.ReadOnlySpan<BattleOrder>.Empty;
                _accum -= 0.05;
            }
        }

        SyncViews();
        var alive0 = Count(0);
        var alive1 = Count(1);
        _status.Text = _state.XoCommand == 1
            ? $"XO has the battle.  NATO {alive0}   Pact {alive1}   tick {_state.Tick}"
            : $"Six Actual.  NATO {alive0}   Pact {alive1}   tick {_state.Tick}";
        if (_state.Winner >= 0 && !_reported)
        {
            _reported = true;
            _result.Visible = true;
            OnDecided?.Invoke(_state.Winner);
            _result.Text = _state.Winner switch
            {
                0 => "NATO holds the field",
                1 => "Pact holds the field",
                _ => "The battle times out",
            };
        }

        FlyCamera();
        if (!_shot && _state.Tick > 30)
        {
            _shot = true;
            var args = OS.GetCmdlineUserArgs();
            if (System.Array.IndexOf(args, "--shot") >= 0)
            {
                var image = GetViewport().GetTexture().GetImage();
                image.SavePng(ProjectSettings.GlobalizePath("res://../build/verify/battle.png"));
                GetTree().Quit();
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouse && mouse.Pressed)
        {
            if (mouse.ButtonIndex == MouseButton.WheelUp)
            {
                _camera.Position += new Vector3(0, -8, 0);
            }
            else if (mouse.ButtonIndex == MouseButton.WheelDown)
            {
                _camera.Position += new Vector3(0, 8, 0);
            }
            else if (mouse.ButtonIndex == MouseButton.Left)
            {
                _selected = Pick(mouse.Position);
            }
            else if (mouse.ButtonIndex == MouseButton.Right && _selected >= 0)
            {
                var point = GroundPoint(mouse.Position);
                if (point is not null)
                {
                    _state.Units[_selected].HasGoal = 1;
                    _state.Units[_selected].GoalX = Fp.FromInt((int)point.Value.X);
                    _state.Units[_selected].GoalY = Fp.FromInt((int)point.Value.Z);
                }
            }
        }
        else if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.Keycode == Key.T && _selected >= 0 && _state.Defs[_state.Units[_selected].Def].IsAir)
            {
                _state.Units[_selected].Controlled = 1;
            }
            else if (key.Keycode == Key.G && _selected >= 0)
            {
                _state.Units[_selected].Controlled = 0;
            }
            else if (key.Keycode == Key.Escape)
            {
                GetTree().ChangeSceneToFile("res://Main.tscn");
            }
        }
    }

    System.ReadOnlySpan<BattleOrder> ReadOrders()
    {
        if (_selected < 0 || _state.Units[_selected].Controlled == 0)
        {
            return System.ReadOnlySpan<BattleOrder>.Empty;
        }

        var turn = 0;
        if (Input.IsPhysicalKeyPressed(Key.Q))
        {
            turn = -4;
        }
        else if (Input.IsPhysicalKeyPressed(Key.E))
        {
            turn = 4;
        }

        if (turn != 0)
        {
            _state.Units[_selected].Facing = (_state.Units[_selected].Facing + turn) & 255;
        }

        return System.ReadOnlySpan<BattleOrder>.Empty;
    }

    void FlyCamera()
    {
        if (_selected >= 0 && _state.Units[_selected].Controlled == 1)
        {
            var unit = _state.Units[_selected];
            var pos = Place(unit);
            _camera.Position = pos + new Vector3(0, 6, 18);
            _camera.LookAt(pos, Vector3.Up);
            return;
        }

        var move = Vector3.Zero;
        if (Input.IsPhysicalKeyPressed(Key.W))
        {
            move.Z -= 1;
        }

        if (Input.IsPhysicalKeyPressed(Key.S))
        {
            move.Z += 1;
        }

        if (Input.IsPhysicalKeyPressed(Key.A))
        {
            move.X -= 1;
        }

        if (Input.IsPhysicalKeyPressed(Key.D))
        {
            move.X += 1;
        }

        if (move != Vector3.Zero)
        {
            _camera.Position += move.Normalized() * 2.2f;
        }
    }

    Node3D MakeView(int index)
    {
        var unit = _state.Units[index];
        var id = _state.Defs[unit.Def].Id;
        Node3D node;
        var packed = GD.Load<PackedScene>($"res://assets/units/{id}.glb");
        if (packed is not null)
        {
            node = packed.Instantiate<Node3D>();
        }
        else
        {
            node = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(3, 2, 6) },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = unit.Player == 0 ? Colors.DarkGreen : Colors.SaddleBrown },
            };
        }

        node.Name = $"Unit{unit.Id}";
        node.Position = Place(unit);
        var marker = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.7f, Height = 1.4f },
            Position = new Vector3(0, 4, 0),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = unit.Player == 0 ? Colors.DodgerBlue : Colors.OrangeRed,
            },
        };
        node.AddChild(marker);
        _views[unit.Id] = node;
        return node;
    }

    void SyncViews()
    {
        if (_state.LastShotActive == 1)
        {
            var tracer = GetNodeOrNull<MeshInstance3D>("Tracer");
            if (tracer is null)
            {
                tracer = new MeshInstance3D { Name = "Tracer", Mesh = new BoxMesh { Size = new Vector3(0.4f, 0.4f, 1) } };
                tracer.MaterialOverride = new StandardMaterial3D { AlbedoColor = Colors.OrangeRed, EmissionEnabled = true, Emission = Colors.Orange };
                AddChild(tracer);
            }

            var from = new Vector3(Meters(_state.LastShotX0), 2, Meters(_state.LastShotY0));
            var to = new Vector3(Meters(_state.LastShotX1), 2, Meters(_state.LastShotY1));
            tracer.Position = (from + to) / 2f;
            tracer.LookAt(to, Vector3.Up);
            tracer.Scale = new Vector3(1, 1, from.DistanceTo(to));
        }

        for (var i = 0; i < _state.Units.Length; i++)
        {
            var unit = _state.Units[i];
            if (!_views.TryGetValue(unit.Id, out var node))
            {
                continue;
            }

            node.Visible = unit.Alive == 1;
            node.Position = Place(unit);
            node.Rotation = new Vector3(0, -unit.Facing * Mathf.Tau / 256f, 0);
            if (_selected == i)
            {
                node.Scale = new Vector3(1.05f, 1.05f, 1.05f);
            }
            else
            {
                node.Scale = Vector3.One;
            }
        }
    }

    int Pick(Vector2 screen)
    {
        var origin = _camera.ProjectRayOrigin(screen);
        var dir = _camera.ProjectRayNormal(screen);
        if (Mathf.Abs(dir.Y) < 0.001f)
        {
            return _selected;
        }

        var t = -origin.Y / dir.Y;
        var hit = origin + (dir * t);
        var best = -1;
        var bestDist = 30f;
        for (var i = 0; i < _state.Units.Length; i++)
        {
            if (_state.Units[i].Alive == 0)
            {
                continue;
            }

            var pos = Place(_state.Units[i]);
            var dist = new Vector2(pos.X, pos.Z).DistanceTo(new Vector2(hit.X, hit.Z));
            if (dist < bestDist)
            {
                best = i;
                bestDist = dist;
            }
        }

        return best;
    }

    Vector3? GroundPoint(Vector2 screen)
    {
        var origin = _camera.ProjectRayOrigin(screen);
        var dir = _camera.ProjectRayNormal(screen);
        if (Mathf.Abs(dir.Y) < 0.001f)
        {
            return null;
        }

        var t = -origin.Y / dir.Y;
        return origin + (dir * t);
    }

    int Count(int player)
    {
        var count = 0;
        for (var i = 0; i < _state.Units.Length; i++)
        {
            if (_state.Units[i].Alive == 1 && _state.Units[i].Player == player)
            {
                count++;
            }
        }

        return count;
    }

    static Vector3 Place(BattleUnit unit) => new(Meters(unit.X), Meters(unit.Z), Meters(unit.Y));

    static float Meters(Fp value) => value.Raw / 65536f;
}
