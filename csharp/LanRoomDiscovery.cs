using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Godot;

namespace OperationSteelTide;

public enum LanRoomKind
{
    Extraction,
    Demolition
}

public sealed record LanRoomInfo(
    string RoomId,
    string HostName,
    string Address,
    int Port,
    LanRoomKind Kind,
    string MapId,
    int PlayerCount,
    int MaximumPlayers)
{
    public string Endpoint => Address.Contains(':') ? $"[{Address}]:{Port}" : $"{Address}:{Port}";
    public bool IsFull => PlayerCount >= MaximumPlayers;
}

public sealed record LanRoomAdvertisement(
    string RoomId,
    string HostName,
    string Version,
    int Port,
    LanRoomKind Kind,
    string MapId,
    int PlayerCount,
    int MaximumPlayers);

/// <summary>
/// Advertises hosted sessions and browses nearby sessions over a small UDP broadcast protocol.
/// The service processes while the game tree is paused so deployment lobbies keep updating.
/// </summary>
public partial class LanRoomDiscovery : Node
{
    public const int DefaultDiscoveryPort = 28961;
    private const int ProtocolVersion = 1;
    private const int MaximumPacketBytes = 2048;
    private const double DefaultAdvertisementIntervalSeconds = 0.75;
    private const ulong DefaultRoomLifetimeMilliseconds = 3200;
    private const int MaximumPacketsPerFrame = 32;
    private const string GameId = "operation-steel-tide";
    private static readonly byte[] LocalNetworkPermissionProbe =
        Encoding.ASCII.GetBytes("operation-steel-tide:local-network-permission");

    public event Action<IReadOnlyList<LanRoomInfo>>? RoomsChanged;
    public event Action<bool>? BrowseAvailabilityChanged;

    public bool IsBrowsing => _browser?.IsBound() == true;
    public bool IsAdvertising => _advertiser is not null && _advertisement is not null;
    public bool BrowseAvailable { get; private set; }
    public int ListeningPort => IsBrowsing ? _browser!.GetLocalPort() : 0;
    public IReadOnlyList<LanRoomInfo> Rooms => SnapshotRooms();

    private sealed record WireRoom(
        int Protocol,
        string Game,
        string Version,
        string RoomId,
        string HostName,
        int Port,
        int Kind,
        string MapId,
        int PlayerCount,
        int MaximumPlayers);

    private sealed record TrackedRoom(LanRoomInfo Room, ulong LastSeenMilliseconds);

    private readonly Dictionary<string, TrackedRoom> _rooms = new(StringComparer.Ordinal);
    private PacketPeerUdp? _browser;
    private PacketPeerUdp? _advertiser;
    private LanRoomAdvertisement? _advertisement;
    private IReadOnlyList<string> _advertisementTargets = Array.Empty<string>();
    private int _advertisementPort = DefaultDiscoveryPort;
    private double _advertisementTimer;
    private double _expiryTimer;
    private double _advertisementIntervalSeconds = DefaultAdvertisementIntervalSeconds;
    private ulong _roomLifetimeMilliseconds = DefaultRoomLifetimeMilliseconds;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _ExitTree()
    {
        StopAdvertising();
        StopBrowsing();
    }

    public override void _Process(double delta)
    {
        PollRooms();
        UpdateAdvertisement(delta);
        ExpireRooms(delta);
    }

    public Error StartBrowsing(int port = DefaultDiscoveryPort, string bindAddress = "0.0.0.0")
    {
        StopBrowsing();
        _browser = new PacketPeerUdp();
        var error = _browser.Bind(port, bindAddress);
        if (error != Error.Ok)
        {
            _browser.Close();
            _browser = null;
            SetBrowseAvailable(false);
            return error;
        }
        SetBrowseAvailable(true);
        RequestLocalNetworkPermission();
        return Error.Ok;
    }

    public void StopBrowsing()
    {
        if (_browser is not null)
        {
            _browser.Close();
            _browser = null;
        }
        _rooms.Clear();
        _expiryTimer = 0.0;
        SetBrowseAvailable(false);
        RoomsChanged?.Invoke(Array.Empty<LanRoomInfo>());
    }

    public void StartAdvertising(LanRoomAdvertisement advertisement)
        => StartAdvertising(advertisement, BroadcastTargets(), DefaultDiscoveryPort);

    public void StopAdvertising()
    {
        if (_advertiser is not null)
        {
            _advertiser.Close();
            _advertiser = null;
        }
        _advertisement = null;
        _advertisementTargets = Array.Empty<string>();
        _advertisementTimer = 0.0;
    }

    public void UpdateAdvertisingPlayers(int playerCount)
    {
        if (_advertisement is null)
        {
            return;
        }
        _advertisement = _advertisement with
        {
            PlayerCount = Mathf.Clamp(playerCount, 1, _advertisement.MaximumPlayers)
        };
        _advertisementTimer = 0.0;
    }

    public void ConfigureTimingForDiagnostics(double intervalSeconds, ulong lifetimeMilliseconds)
    {
        _advertisementIntervalSeconds = Math.Max(0.01, intervalSeconds);
        _roomLifetimeMilliseconds = Math.Max((ulong)50, lifetimeMilliseconds);
    }

    public void StartAdvertisingForDiagnostics(
        LanRoomAdvertisement advertisement,
        IReadOnlyList<string> targets,
        int port)
        => StartAdvertising(advertisement, targets, port);

    public static byte[] EncodeForDiagnostics(LanRoomAdvertisement advertisement)
        => Encode(advertisement);

    public static bool TryDecodeForDiagnostics(
        byte[] packet,
        string sourceAddress,
        string expectedVersion,
        out LanRoomInfo room)
        => TryDecode(packet, sourceAddress, expectedVersion, out room);

    internal static int SendLocalNetworkPermissionProbeForDiagnostics(
        IReadOnlyList<string> targets,
        int port)
        => SendLocalNetworkPermissionProbe(targets, port);

    private void StartAdvertising(
        LanRoomAdvertisement advertisement,
        IReadOnlyList<string> targets,
        int port)
    {
        StopAdvertising();
        _advertiser = new PacketPeerUdp();
        _advertiser.SetBroadcastEnabled(true);
        _advertisement = advertisement;
        _advertisementTargets = targets;
        _advertisementPort = port;
        _advertisementTimer = 0.0;
    }

    private void PollRooms()
    {
        if (_browser is null)
        {
            return;
        }
        var processed = 0;
        while (_browser.GetAvailablePacketCount() > 0 && processed++ < MaximumPacketsPerFrame)
        {
            var packet = _browser.GetPacket();
            var sourceAddress = _browser.GetPacketIP();
            if (!TryDecode(packet, sourceAddress, CurrentVersion(), out var room))
            {
                continue;
            }
            var now = Time.GetTicksMsec();
            var changed = !_rooms.TryGetValue(room.RoomId, out var tracked) || tracked.Room != room;
            _rooms[room.RoomId] = new TrackedRoom(room, now);
            if (changed)
            {
                RoomsChanged?.Invoke(SnapshotRooms());
            }
        }
    }

    private void UpdateAdvertisement(double delta)
    {
        if (_advertiser is null || _advertisement is null)
        {
            return;
        }
        _advertisementTimer -= delta;
        if (_advertisementTimer > 0.0)
        {
            return;
        }
        _advertisementTimer = _advertisementIntervalSeconds;
        var packet = Encode(_advertisement);
        foreach (var target in _advertisementTargets)
        {
            if (_advertiser.SetDestAddress(target, _advertisementPort) == Error.Ok)
            {
                _advertiser.PutPacket(packet);
            }
        }
    }

    private void ExpireRooms(double delta)
    {
        if (_rooms.Count == 0)
        {
            return;
        }
        _expiryTimer -= delta;
        if (_expiryTimer > 0.0)
        {
            return;
        }
        _expiryTimer = 0.2;
        var now = Time.GetTicksMsec();
        var expired = new List<string>();
        foreach (var pair in _rooms)
        {
            if (now - pair.Value.LastSeenMilliseconds > _roomLifetimeMilliseconds)
            {
                expired.Add(pair.Key);
            }
        }
        if (expired.Count == 0)
        {
            return;
        }
        foreach (var roomId in expired)
        {
            _rooms.Remove(roomId);
        }
        RoomsChanged?.Invoke(SnapshotRooms());
    }

    private IReadOnlyList<LanRoomInfo> SnapshotRooms()
    {
        var rooms = new List<LanRoomInfo>(_rooms.Count);
        foreach (var tracked in _rooms.Values)
        {
            rooms.Add(tracked.Room);
        }
        rooms.Sort(static (left, right) =>
        {
            var host = string.Compare(left.HostName, right.HostName, StringComparison.OrdinalIgnoreCase);
            return host != 0 ? host : string.Compare(left.RoomId, right.RoomId, StringComparison.Ordinal);
        });
        return rooms;
    }

    private void SetBrowseAvailable(bool available)
    {
        if (BrowseAvailable == available)
        {
            return;
        }
        BrowseAvailable = available;
        BrowseAvailabilityChanged?.Invoke(available);
    }

    private static byte[] Encode(LanRoomAdvertisement advertisement)
    {
        var wire = new WireRoom(
            ProtocolVersion,
            GameId,
            advertisement.Version,
            advertisement.RoomId,
            advertisement.HostName,
            advertisement.Port,
            (int)advertisement.Kind,
            advertisement.MapId,
            advertisement.PlayerCount,
            advertisement.MaximumPlayers);
        return JsonSerializer.SerializeToUtf8Bytes(wire);
    }

    private static bool TryDecode(
        byte[] packet,
        string sourceAddress,
        string expectedVersion,
        out LanRoomInfo room)
    {
        room = null!;
        if (packet.Length is 0 or > MaximumPacketBytes
            || !IPAddress.TryParse(sourceAddress, out _))
        {
            return false;
        }
        WireRoom? wire;
        try
        {
            wire = JsonSerializer.Deserialize<WireRoom>(packet);
        }
        catch (JsonException)
        {
            return false;
        }
        if (wire is null
            || wire.Protocol != ProtocolVersion
            || wire.Game != GameId
            || wire.Version != expectedVersion
            || string.IsNullOrWhiteSpace(wire.RoomId)
            || string.IsNullOrWhiteSpace(wire.HostName)
            || string.IsNullOrWhiteSpace(wire.MapId)
            || wire.RoomId.Length is < 8 or > 64
            || wire.HostName.Length is < 1 or > 48
            || wire.Port is < 1 or > 65535
            || !Enum.IsDefined(typeof(LanRoomKind), wire.Kind)
            || wire.MapId.Length is < 1 or > 64
            || !HasSafeIdentifierCharacters(wire.RoomId)
            || !HasSafeIdentifierCharacters(wire.MapId)
            || wire.MaximumPlayers is < 1 or > 32
            || wire.PlayerCount is < 1
            || wire.PlayerCount > wire.MaximumPlayers)
        {
            return false;
        }
        room = new LanRoomInfo(
            wire.RoomId,
            wire.HostName.Trim(),
            sourceAddress,
            wire.Port,
            (LanRoomKind)wire.Kind,
            wire.MapId,
            wire.PlayerCount,
            wire.MaximumPlayers);
        return true;
    }

    private static bool HasSafeIdentifierCharacters(string value)
    {
        foreach (var character in value)
        {
            if (!char.IsLetterOrDigit(character) && character is not '_' and not '-')
            {
                return false;
            }
        }
        return true;
    }

    private static string CurrentVersion()
        => ProjectSettings.GetSetting("application/config/version", "dev").AsString();

    private static void RequestLocalNetworkPermission()
    {
        if (OS.HasFeature("macos"))
        {
            SendLocalNetworkPermissionProbe(BroadcastTargets(), DefaultDiscoveryPort);
        }
    }

    private static int SendLocalNetworkPermissionProbe(IReadOnlyList<string> targets, int port)
    {
        if (port is < 1 or > 65535)
        {
            return 0;
        }
        var probe = new PacketPeerUdp();
        probe.SetBroadcastEnabled(true);
        var sent = 0;
        foreach (var target in targets)
        {
            if (probe.SetDestAddress(target, port) == Error.Ok
                && probe.PutPacket(LocalNetworkPermissionProbe) == Error.Ok)
            {
                sent++;
            }
        }
        probe.Close();
        return sent;
    }

    private static IReadOnlyList<string> BroadcastTargets()
    {
        var targets = new HashSet<string>(StringComparer.Ordinal) { "255.255.255.255" };
        try
        {
            foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (network.OperationalStatus != OperationalStatus.Up
                    || network.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }
                foreach (var unicast in network.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork
                        || unicast.IPv4Mask is null)
                    {
                        continue;
                    }
                    var address = unicast.Address.GetAddressBytes();
                    var mask = unicast.IPv4Mask.GetAddressBytes();
                    var broadcast = new byte[4];
                    for (var index = 0; index < broadcast.Length; index++)
                    {
                        broadcast[index] = (byte)(address[index] | ~mask[index]);
                    }
                    targets.Add(new IPAddress(broadcast).ToString());
                }
            }
        }
        catch (NetworkInformationException)
        {
            // The limited broadcast address remains available as a cross-platform fallback.
        }
        return new List<string>(targets);
    }
}
