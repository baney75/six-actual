using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SixActual.Sim.Net;

/// <summary>
/// NET-01 LAN / direct-IP session over UDP (D076), Steam deferred. Handshake: a client sends Hello (an AI flag and its
/// name) until the host answers Welcome (its player slot); when the host starts, it sends Setup (an opaque payload,
/// the game's battle settings) until each client answers SetupAck, which a client sends only once it holds a slot and
/// has built its world, or Refuse with a reason (for example a missing map). After that every datagram belongs to the
/// <see cref="LockstepPeer"/>. Datagrams start with a 4-byte session tag, so strays from other games are ignored.
/// </summary>
public static class Lan
{
    public const int DefaultPort = 47610;
    public const byte MsgHello = 1;
    public const byte MsgWelcome = 2;
    public const byte MsgSetup = 3;
    public const byte MsgFull = 7;
    public const byte MsgSetupAck = 8;
    public const byte MsgRefuse = 9;
    internal static readonly byte[] Tag = "SXA1"u8.ToArray();

    internal static byte[] Frame(ReadOnlySpan<byte> body)
    {
        var b = new byte[Tag.Length + body.Length];
        Tag.CopyTo(b, 0);
        body.CopyTo(b.AsSpan(Tag.Length));
        return b;
    }

    internal static bool Unframe(byte[] datagram, out ReadOnlySpan<byte> body)
    {
        body = default;
        if (datagram.Length <= Tag.Length || !datagram.AsSpan(0, Tag.Length).SequenceEqual(Tag))
        {
            return false;
        }

        body = datagram.AsSpan(Tag.Length);
        return true;
    }
}

/// <summary>Shared UDP plumbing with test-only loss injection.</summary>
public abstract class LanEndpoint : IDisposable
{
    protected readonly UdpClient Udp;
    ulong _rng = 0x9E3779B97F4A7C15UL;

    protected LanEndpoint(UdpClient udp)
    {
        Udp = udp;
    }

    /// <summary>Test fault injection: percentage of outgoing datagrams silently dropped.</summary>
    public int LossPct { get; set; }

    public int Sent { get; private set; }

    public int Dropped { get; private set; }

    protected void SendTo(IPEndPoint to, ReadOnlySpan<byte> body)
    {
        Sent++;
        if (LossPct > 0)
        {
            _rng ^= _rng << 13;
            _rng ^= _rng >> 7;
            _rng ^= _rng << 17;
            if ((int)(_rng % 100UL) < LossPct)
            {
                Dropped++;
                return;
            }
        }

        var b = Lan.Frame(body);
        try
        {
            Udp.Send(b, b.Length, to);
        }
        catch (SocketException)
        {
            // A refused or unreachable peer behaves like loss: lockstep resends.
        }
    }

    protected IEnumerable<(IPEndPoint From, byte[] Body)> Drain()
    {
        while (true)
        {
            byte[] d;
            IPEndPoint? from = null;
            try
            {
                if (Udp.Available == 0)
                {
                    yield break;
                }

                d = Udp.Receive(ref from);
            }
            catch (SocketException)
            {
                continue;
            }

            if (Lan.Unframe(d, out var body))
            {
                yield return (from!, body.ToArray());
            }
        }
    }

    public void Dispose()
    {
        Udp.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>The hosting player (slot 0). Accepts up to <see cref="MaxPlayers"/> players while the lobby is open.</summary>
public sealed class LanHost : LanEndpoint
{
    readonly List<IPEndPoint> _slots = [];
    readonly List<string> _names;
    readonly List<bool> _ai = [false];
    readonly List<(int Slot, string Reason)> _refusals = [];
    readonly HashSet<int> _setupAcked = [];
    byte[]? _setup;
    long _lastSetupMs = long.MinValue / 2;

    public LanHost(int port, int maxPlayers, string name)
        : base(new UdpClient(new IPEndPoint(IPAddress.Any, port)))
    {
        MaxPlayers = maxPlayers;
        _names = [name];
        _slots.Add(new IPEndPoint(IPAddress.Loopback, 0));
    }

    public int MaxPlayers { get; }

    public int Port => ((IPEndPoint)Udp.Client.LocalEndPoint!).Port;

    public IReadOnlyList<string> Players => _names;

    /// <summary>Per slot: true when that client joined as an AI player (its XO stays on).</summary>
    public IReadOnlyList<bool> IsAi => _ai;

    /// <summary>Clients that could not build the battle from Setup, with their reason.</summary>
    public IReadOnlyList<(int Slot, string Reason)> Refusals => _refusals;

    public LockstepPeer? Peer { get; set; }

    public bool Started => _setup is not null;

    public bool AllReady => Started && _setupAcked.Count == _names.Count - 1;

    /// <summary>Closes the lobby and sends <paramref name="setup"/> to every client until each acknowledges.</summary>
    public void Start(byte[] setup) => _setup = setup;

    public void Pump(long nowMs)
    {
        foreach (var (from, body) in Drain())
        {
            var slot = _slots.FindIndex(e => e.Equals(from));
            switch (body[0])
            {
                case Lan.MsgHello when slot < 0 && !Started && _names.Count < MaxPlayers && body.Length >= 2:
                    _slots.Add(from);
                    _ai.Add((body[1] & 1) != 0);
                    var name = Encoding.UTF8.GetString(body.AsSpan(2)).Trim();
                    _names.Add(name.Length > 24 ? name[..24] : name);
                    SendTo(from, [Lan.MsgWelcome, (byte)(_slots.Count - 1)]);
                    break;
                case Lan.MsgHello when slot > 0:
                    SendTo(from, [Lan.MsgWelcome, (byte)slot]);
                    break;
                case Lan.MsgHello:
                    SendTo(from, [Lan.MsgFull]);
                    break;
                case Lan.MsgSetupAck when slot > 0:
                    _setupAcked.Add(slot);
                    break;
                case Lan.MsgRefuse when slot > 0 && _refusals.All(r => r.Slot != slot):
                    var reason = Encoding.UTF8.GetString(body.AsSpan(1));
                    _refusals.Add((slot, reason.Length > 200 ? reason[..200] : reason));
                    break;
                default:
                    if (slot > 0 && Peer is not null && _setupAcked.Contains(slot))
                    {
                        Peer.Receive(slot, body);
                    }

                    break;
            }
        }

        if (_setup is not null && !AllReady && nowMs - _lastSetupMs >= 200)
        {
            _lastSetupMs = nowMs;
            for (var s = 1; s < _slots.Count; s++)
            {
                if (!_setupAcked.Contains(s))
                {
                    SendTo(_slots[s], [Lan.MsgSetup, .. _setup]);
                }
            }
        }

        if (Peer is not null)
        {
            foreach (var (to, data) in Peer.Poll(nowMs))
            {
                if (to > 0 && to < _slots.Count)
                {
                    SendTo(_slots[to], data);
                }
            }
        }
    }
}

/// <summary>A joining player. <see cref="Setup"/> becomes non-null when the host starts the battle.</summary>
public sealed class LanClient : LanEndpoint
{
    readonly IPEndPoint _host;
    readonly string _name;
    readonly bool _isAi;
    long _lastHelloMs = long.MinValue / 2;

    public LanClient(string host, int port, string name, bool isAi = false)
        : base(new UdpClient(new IPEndPoint(IPAddress.Any, 0)))
    {
        _host = new IPEndPoint(Dns.GetHostAddresses(host).First(a => a.AddressFamily == AddressFamily.InterNetwork), port);
        _name = name;
        _isAi = isAi;
    }

    /// <summary>Set by the caller when Setup cannot be built here; the host is told instead of waiting forever.</summary>
    public string? RefuseReason { get; set; }

    public int Slot { get; private set; } = -1;

    public bool Refused { get; private set; }

    public byte[]? Setup { get; private set; }

    public LockstepPeer? Peer { get; set; }

    public void Pump(long nowMs)
    {
        if (Slot < 0 && !Refused && nowMs - _lastHelloMs >= 250)
        {
            _lastHelloMs = nowMs;
            SendTo(_host, [Lan.MsgHello, (byte)(_isAi ? 1 : 0), .. Encoding.UTF8.GetBytes(_name)]);
        }

        foreach (var (from, body) in Drain())
        {
            if (!from.Equals(_host))
            {
                continue;
            }

            switch (body[0])
            {
                case Lan.MsgWelcome when body.Length == 2:
                    Slot = body[1];
                    break;
                case Lan.MsgFull:
                    Refused = Slot < 0;
                    break;
                case Lan.MsgSetup when Slot >= 0:
                    // Without a slot (a lost Welcome) the Setup is ignored; the host resends it after the next Welcome.
                    Setup ??= body[1..];
                    if (RefuseReason is not null)
                    {
                        SendTo(_host, [Lan.MsgRefuse, .. Encoding.UTF8.GetBytes(RefuseReason)]);
                    }
                    else if (Peer is not null)
                    {
                        SendTo(_host, [Lan.MsgSetupAck]);
                    }

                    break;
                case Lan.MsgSetup:
                    break;
                default:
                    Peer?.Receive(0, body);
                    break;
            }
        }

        if (Peer is not null)
        {
            foreach (var (_, data) in Peer.Poll(nowMs))
            {
                SendTo(_host, data);
            }
        }
    }
}
