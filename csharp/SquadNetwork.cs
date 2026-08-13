using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Godot;

namespace OperationSteelTide;

[GlobalClass]
public partial class SquadNetwork : Node
{
    public const int DefaultPort = 28960;
    public const int MaximumPlayers = 4;

    public event Action<long, OperatorRole, Vector3, Vector3, float, bool>? RemoteStateReceived;
    public event Action<long>? RemotePeerLeft;
    public event Action<long, OperatorRole, Vector3, Vector3>? RemoteAbilityReceived;
    public event Action<long, Vector3, Vector3, int, float>? RemoteShotReceived;
    public event Action<string>? StatusChanged;

    public bool IsOnline { get; private set; }
    public bool IsHost { get; private set; }
    public string Status { get; private set; } = "LOCAL SQUAD";
    public int ConnectedPeerCount => IsOnline ? Multiplayer.GetPeers().Length : 0;
    public TacticalPlayer? LocalPlayer { get; set; }

    private ENetMultiplayerPeer? _peer;
    private readonly Dictionary<long, ulong> _nextAbilityTimeByPeer = new();
    private float _snapshotTimer;

    public override void _Ready()
    {
        Name = "SquadNetwork";
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Multiplayer.ConnectedToServer += OnConnectedToServer;
        Multiplayer.ConnectionFailed += OnConnectionFailed;
        Multiplayer.ServerDisconnected += OnServerDisconnected;
    }

    public override void _ExitTree()
    {
        Multiplayer.PeerConnected -= OnPeerConnected;
        Multiplayer.PeerDisconnected -= OnPeerDisconnected;
        Multiplayer.ConnectedToServer -= OnConnectedToServer;
        Multiplayer.ConnectionFailed -= OnConnectionFailed;
        Multiplayer.ServerDisconnected -= OnServerDisconnected;
        Close();
    }

    public Error Host(int port = DefaultPort)
    {
        Close();
        _peer = new ENetMultiplayerPeer();
        var error = _peer.CreateServer(port, MaximumPlayers - 1);
        if (error != Error.Ok)
        {
            SetStatus($"HOST FAILED  //  {error}");
            _peer = null;
            return error;
        }
        Multiplayer.MultiplayerPeer = _peer;
        IsOnline = true;
        IsHost = true;
        SetStatus($"HOSTING UDP {port}  //  1/{MaximumPlayers}");
        return Error.Ok;
    }

    public Error Join(string address, int port = DefaultPort)
    {
        Close();
        if (!TryParseEndpoint(address, port, out var host, out var endpointPort))
        {
            SetStatus("JOIN FAILED  //  INVALID HOST OR PORT");
            return Error.InvalidParameter;
        }
        _peer = new ENetMultiplayerPeer();
        var error = _peer.CreateClient(host, endpointPort);
        if (error != Error.Ok)
        {
            SetStatus($"JOIN FAILED  //  {error}");
            _peer = null;
            return error;
        }
        Multiplayer.MultiplayerPeer = _peer;
        IsOnline = false;
        IsHost = false;
        SetStatus($"CONNECTING  //  {FormatEndpoint(host, endpointPort)}");
        return Error.Ok;
    }

    public static bool TryParseEndpoint(string? value, int defaultPort, out string host, out int port)
    {
        host = string.IsNullOrWhiteSpace(value) ? "127.0.0.1" : value.Trim();
        port = defaultPort;
        if (defaultPort is < 1 or > 65535)
        {
            return false;
        }

        if (host.StartsWith('['))
        {
            var closingBracket = host.IndexOf(']');
            if (closingBracket < 2)
            {
                return false;
            }

            var suffix = host[(closingBracket + 1)..];
            var bracketedHost = host[1..closingBracket];
            if (!IPAddress.TryParse(bracketedHost, out var bracketedAddress)
                || bracketedAddress.AddressFamily != AddressFamily.InterNetworkV6)
            {
                return false;
            }
            if (suffix.Length == 0)
            {
                host = bracketedHost;
                return true;
            }
            if (!suffix.StartsWith(':') || !TryParsePort(suffix[1..], out port))
            {
                return false;
            }

            host = bracketedHost;
            return true;
        }

        var colonCount = 0;
        foreach (var character in host)
        {
            if (character == ':')
            {
                colonCount++;
            }
        }
        if (colonCount == 0)
        {
            return host.Length > 0;
        }
        if (colonCount > 1)
        {
            return IPAddress.TryParse(host, out var address)
                && address.AddressFamily == AddressFamily.InterNetworkV6;
        }

        var separator = host.LastIndexOf(':');
        var suppliedHost = host[..separator].Trim();
        if (suppliedHost.Length == 0 || !TryParsePort(host[(separator + 1)..], out port))
        {
            return false;
        }

        host = suppliedHost;
        return true;
    }

    private static bool TryParsePort(string value, out int port)
        => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out port)
        && port is >= 1 and <= 65535;

    private static string FormatEndpoint(string host, int port)
        => host.Contains(':') ? $"[{host}]:{port}" : $"{host}:{port}";

    public void Close()
    {
        if (_peer is not null)
        {
            _peer.Close();
            _peer = null;
        }
        if (Multiplayer.MultiplayerPeer is not null)
        {
            Multiplayer.MultiplayerPeer = null;
        }
        IsOnline = false;
        IsHost = false;
        _snapshotTimer = 0.0f;
        _nextAbilityTimeByPeer.Clear();
    }

    public override void _Process(double delta)
    {
        if (!IsOnline || LocalPlayer is null || LocalPlayer.IsDead)
        {
            return;
        }
        _snapshotTimer -= (float)delta;
        if (_snapshotTimer > 0.0f)
        {
            return;
        }
        _snapshotTimer = 0.075f;
        var peerId = Multiplayer.GetUniqueId();
        if (IsHost)
        {
            Rpc(MethodName.ReceiveState, peerId, (int)LocalPlayer.Role, LocalPlayer.GlobalPosition,
                LocalPlayer.Rotation, LocalPlayer.Health, LocalPlayer.IsDead);
        }
        else
        {
            RpcId(1, MethodName.SubmitClientState, (int)LocalPlayer.Role, LocalPlayer.GlobalPosition,
                LocalPlayer.Rotation, LocalPlayer.Health, LocalPlayer.IsDead);
        }
    }

    public void BroadcastAbility(OperatorRole role, Vector3 origin, Vector3 forward)
    {
        if (!IsOnline)
        {
            return;
        }
        if (IsHost)
        {
            Rpc(MethodName.ReceiveAbility, Multiplayer.GetUniqueId(), (int)role, origin, forward);
        }
        else
        {
            RpcId(1, MethodName.SubmitClientAbility, (int)role, origin, forward);
        }
    }

    public void BroadcastShot(Vector3 origin, Vector3 end, int enemyId, float damage)
    {
        if (!IsOnline)
        {
            return;
        }
        if (IsHost)
        {
            Rpc(MethodName.ReceiveShot, Multiplayer.GetUniqueId(), origin, end, enemyId, damage);
        }
        else
        {
            RpcId(1, MethodName.SubmitClientShot, origin, end, enemyId, damage);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void SubmitClientState(int role, Vector3 position, Vector3 rotation, float health, bool dead)
    {
        if (!IsHost)
        {
            return;
        }
        var sender = Multiplayer.GetRemoteSenderId();
        RemoteStateReceived?.Invoke(sender, (OperatorRole)role, position, rotation, health, dead);
        Rpc(MethodName.ReceiveState, sender, role, position, rotation, health, dead);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void ReceiveState(long peerId, int role, Vector3 position, Vector3 rotation, float health, bool dead)
    {
        if (peerId == Multiplayer.GetUniqueId())
        {
            return;
        }
        RemoteStateReceived?.Invoke(peerId, (OperatorRole)role, position, rotation, health, dead);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SubmitClientAbility(int role, Vector3 origin, Vector3 forward)
    {
        if (!IsHost)
        {
            return;
        }
        var sender = Multiplayer.GetRemoteSenderId();
        if (!Enum.IsDefined(typeof(OperatorRole), role))
        {
            return;
        }
        var now = Time.GetTicksMsec();
        if (_nextAbilityTimeByPeer.TryGetValue(sender, out var readyAt) && now < readyAt)
        {
            return;
        }
        _nextAbilityTimeByPeer[sender] = now + (ulong)(OperatorRoles.Spec((OperatorRole)role).SkillCooldown * 1000.0f);
        RemoteAbilityReceived?.Invoke(sender, (OperatorRole)role, origin, forward);
        Rpc(MethodName.ReceiveAbility, sender, role, origin, forward);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveAbility(long peerId, int role, Vector3 origin, Vector3 forward)
    {
        if (peerId == Multiplayer.GetUniqueId())
        {
            return;
        }
        RemoteAbilityReceived?.Invoke(peerId, (OperatorRole)role, origin, forward);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SubmitClientShot(Vector3 origin, Vector3 end, int enemyId, float damage)
    {
        if (!IsHost)
        {
            return;
        }
        var sender = Multiplayer.GetRemoteSenderId();
        RemoteShotReceived?.Invoke(sender, origin, end, enemyId, damage);
        Rpc(MethodName.ReceiveShot, sender, origin, end, enemyId, damage);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveShot(long peerId, Vector3 origin, Vector3 end, int enemyId, float damage)
    {
        if (peerId == Multiplayer.GetUniqueId())
        {
            return;
        }
        RemoteShotReceived?.Invoke(peerId, origin, end, enemyId, damage);
    }

    private void OnPeerConnected(long peerId)
    {
        var connected = Multiplayer.GetPeers().Length + 1;
        SetStatus(IsHost
            ? $"HOSTING UDP {DefaultPort}  //  {connected}/{MaximumPlayers}"
            : $"CONNECTED  //  SQUAD {connected}/{MaximumPlayers}");
    }

    private void OnPeerDisconnected(long peerId)
    {
        _nextAbilityTimeByPeer.Remove(peerId);
        RemotePeerLeft?.Invoke(peerId);
        var connected = Mathf.Max(1, Multiplayer.GetPeers().Length + 1);
        SetStatus(IsHost
            ? $"HOSTING UDP {DefaultPort}  //  {connected}/{MaximumPlayers}"
            : $"CONNECTED  //  SQUAD {connected}/{MaximumPlayers}");
    }

    private void OnConnectedToServer()
    {
        IsOnline = true;
        SetStatus($"CONNECTED  //  PEER {Multiplayer.GetUniqueId()}");
    }

    private void OnConnectionFailed()
    {
        Close();
        SetStatus("CONNECTION FAILED  //  AI SQUAD ACTIVE");
    }

    private void OnServerDisconnected()
    {
        Close();
        SetStatus("HOST LOST  //  AI SQUAD ACTIVE");
    }

    private void SetStatus(string value)
    {
        Status = value;
        StatusChanged?.Invoke(value);
    }
}
