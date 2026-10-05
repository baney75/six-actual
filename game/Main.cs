using Godot;
using SixActual.Sim;

public partial class Main : Node
{
    public override void _Ready()
    {
        GD.Print($"Six Actual foundation {DeterminismReport.SelfTest()}");
    }
}
