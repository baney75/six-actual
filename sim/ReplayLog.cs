using System.Text;

namespace SixActual.Sim;

public static class ReplayLog
{
    public const int FormatVersion = 1;

    public static byte[] Write(ulong seed, int entityCount, ReadOnlySpan<Order> orders)
    {
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(FormatVersion);
            writer.Write(seed);
            writer.Write(entityCount);
            writer.Write(orders.Length);
            for (var i = 0; i < orders.Length; i++)
            {
                writer.Write(orders[i].Tick);
                writer.Write(orders[i].EntityId);
                writer.Write(orders[i].Vx.Raw);
                writer.Write(orders[i].Vy.Raw);
            }
        }

        return buffer.ToArray();
    }

    public static (ulong Seed, int EntityCount, Order[] Orders) Read(ReadOnlySpan<byte> bytes)
    {
        using var buffer = new MemoryStream(bytes.ToArray());
        using var reader = new BinaryReader(buffer, Encoding.UTF8, leaveOpen: true);
        var version = reader.ReadInt32();
        if (version != FormatVersion)
        {
            throw new InvalidDataException($"Unsupported replay version {version}.");
        }

        var seed = reader.ReadUInt64();
        var entityCount = reader.ReadInt32();
        var count = reader.ReadInt32();
        if (count < 0)
        {
            throw new InvalidDataException("Replay order count is negative.");
        }

        var orders = new Order[count];
        for (var i = 0; i < count; i++)
        {
            orders[i] = new Order
            {
                Tick = reader.ReadInt32(),
                EntityId = reader.ReadInt32(),
                Vx = Fp.FromRaw(reader.ReadInt64()),
                Vy = Fp.FromRaw(reader.ReadInt64()),
            };
        }

        return (seed, entityCount, orders);
    }
}
