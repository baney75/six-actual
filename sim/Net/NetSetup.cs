using System.Text.Json;
using SixActual.Sim.Battle;
using SixActual.Sim.State.Battle;

namespace SixActual.Sim.Net;

/// <summary>NET-01 battle settings the host sends in Setup (D076). Every peer builds the identical world from it:
/// <see cref="Peers"/> connected players occupy slots 0..Peers-1; the remaining slots of the PerSide-vs-PerSide
/// battle are AI players. Slots listed in <see cref="Humans"/> start with their XO off (a person commands).</summary>
public sealed class NetSetup
{
    public ulong Seed { get; init; }

    public int PerSide { get; init; } = 1;

    public string Deck0 { get; init; } = "";

    public string Deck1 { get; init; } = "";

    public int StartDp { get; init; } = 1000;

    public string? Map { get; init; }

    /// <summary>SHA-256 of the host's heightfield for <see cref="Map"/>; a peer with different map data refuses.</summary>
    public string? MapSha { get; init; }

    public int VictoryType { get; init; }

    public int TimeOfDay { get; init; } = 1;

    public int Weather { get; init; }

    public int Peers { get; init; } = 2;

    public int[] Humans { get; init; } = [];

    public int InputDelayTicks { get; init; } = 4;

    public string[] Names { get; init; } = [];

    public static string MapHash(MapDef m) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(m.DmB64)));

    public byte[] ToBytes() => JsonSerializer.SerializeToUtf8Bytes(this);

    public static NetSetup FromBytes(ReadOnlySpan<byte> b) => JsonSerializer.Deserialize<NetSetup>(b) ?? throw new InvalidDataException("empty setup");

    public WorldState Build(BattleData data, BattleRules rules)
    {
        if (Peers < 2 || Peers > PerSide * 2)
        {
            throw new InvalidDataException($"{Peers} peers do not fit a {PerSide}v{PerSide} battle");
        }

        if (Map is not null)
        {
            var local = data.Maps.FirstOrDefault(m => m.Id == Map) ?? throw new InvalidDataException($"map {Map} is not installed here");
            if (MapSha is not null && MapHash(local) != MapSha)
            {
                throw new InvalidDataException($"map {Map} differs from the host's (different game data)");
            }
        }

        var w = Skirmish.Create(data, rules, Seed, Deck0, Deck1, xo0: true, xo1: true, new Skirmish.Options { PerSide = PerSide, StartDp = StartDp, Map = Map, VictoryType = VictoryType, TimeOfDay = TimeOfDay, Weather = Weather });
        foreach (var h in Humans)
        {
            w.XoActive[h] = 0;
        }

        return w;
    }
}
