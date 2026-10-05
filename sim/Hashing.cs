using System.Security.Cryptography;
using System.Text;
using SixActual.Sim.State;

namespace SixActual.Sim;

public static class Hashing
{
    public static string Hash(MatchState state)
    {
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(state.Tick);
            writer.Write(state.Seed);
            writer.Write(state.Entities.Length);
            for (var i = 0; i < state.Entities.Length; i++)
            {
                var entity = state.Entities[i];
                writer.Write(entity.Id);
                writer.Write(entity.X.Raw);
                writer.Write(entity.Y.Raw);
                writer.Write(entity.Vx.Raw);
                writer.Write(entity.Vy.Raw);
            }
        }

        var digest = SHA256.HashData(buffer.ToArray());
        return Convert.ToHexString(digest).ToLowerInvariant();
    }
}
