using System.Security.Cryptography;
using System.Text;
using SixActual.Sim.State;

namespace SixActual.Sim;

public static class BattleHash
{
    public static string Hash(BattleState state)
    {
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(state.Tick);
            writer.Write(state.Seed);
            writer.Write(state.Winner);
            writer.Write(state.XoCommand);
            writer.Write(state.Weather);
            writer.Write(state.Units.Length);
            for (var i = 0; i < state.Units.Length; i++)
            {
                var unit = state.Units[i];
                writer.Write(unit.Id);
                writer.Write(unit.Player);
                writer.Write(unit.Alive);
                writer.Write(unit.X.Raw);
                writer.Write(unit.Y.Raw);
                writer.Write(unit.Z.Raw);
                writer.Write(unit.Facing);
                writer.Write(unit.Hp);
                writer.Write(unit.Ammo);
                writer.Write(unit.Fuel);
                writer.Write(unit.Morale);
                writer.Write(unit.Suppression);
                writer.Write(unit.Flags);
                writer.Write(unit.HasGoal);
                writer.Write(unit.GoalX.Raw);
                writer.Write(unit.GoalY.Raw);
                writer.Write(unit.HasVia);
                writer.Write(unit.ViaX.Raw);
                writer.Write(unit.ViaY.Raw);
                writer.Write(unit.Spotted);
                writer.Write(unit.Controlled);
            }
        }

        return Convert.ToHexString(SHA256.HashData(buffer.ToArray())).ToLowerInvariant();
    }
}
