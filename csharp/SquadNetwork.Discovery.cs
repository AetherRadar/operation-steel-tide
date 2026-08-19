using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class SquadNetwork
{
    public event Action<IReadOnlyList<LanRoomInfo>>? LanRoomsChanged;
    public event Action<bool>? LanRoomBrowseAvailabilityChanged;

    public bool IsLanRoomBrowsing => _lanRoomDiscovery?.IsBrowsing == true;
    public bool IsLanRoomAdvertising => _lanRoomDiscovery?.IsAdvertising == true;
    public bool IsLanRoomBrowsingRequested => _lanRoomBrowsingRequested;

    private LanRoomDiscovery _lanRoomDiscovery = null!;
    private readonly string _lanRoomId = Guid.NewGuid().ToString("N");
    private bool _lanRoomBrowsingRequested;
    private LanRoomKind _lanRoomKind = LanRoomKind.Extraction;
    private string _lanRoomMapId = DeploymentMapCatalog.FreightTerminalId;
    private int _hostPort = DefaultPort;

    public void StartLanRoomBrowsing()
    {
        _lanRoomBrowsingRequested = true;
        if (!IsOnline && !IsHost)
        {
            _lanRoomDiscovery.StartBrowsing();
        }
    }

    public void StopLanRoomBrowsing()
    {
        _lanRoomBrowsingRequested = false;
        _lanRoomDiscovery.StopBrowsing();
    }

    private void InitializeLanRoomDiscovery()
    {
        _lanRoomDiscovery = new LanRoomDiscovery { Name = "LanRoomDiscovery" };
        AddChild(_lanRoomDiscovery);
        _lanRoomDiscovery.RoomsChanged += rooms => LanRoomsChanged?.Invoke(rooms);
        _lanRoomDiscovery.BrowseAvailabilityChanged += available =>
            LanRoomBrowseAvailabilityChanged?.Invoke(available);
    }

    private void ConfigureLanRoom(LanRoomKind kind, string mapId)
    {
        _lanRoomKind = kind;
        _lanRoomMapId = mapId;
    }

    private void StartLanRoomAdvertisement(int port)
    {
        _hostPort = port;
        _lanRoomDiscovery.StartAdvertising(new LanRoomAdvertisement(
            _lanRoomId,
            FriendlyHostName(),
            ProjectSettings.GetSetting("application/config/version", "dev").AsString(),
            port,
            _lanRoomKind,
            _lanRoomMapId,
            Multiplayer.GetPeers().Length + 1,
            ActivePlayerCapacity));
    }

    private void UpdateLanRoomAdvertisement()
        => _lanRoomDiscovery.UpdateAdvertisingPlayers(Multiplayer.GetPeers().Length + 1);

    private void StopLanRoomAdvertisement()
        => _lanRoomDiscovery.StopAdvertising();

    private void PauseLanRoomBrowsing()
        => _lanRoomDiscovery.StopBrowsing();

    private void ResumeLanRoomBrowsingIfRequested()
    {
        if (_lanRoomBrowsingRequested)
        {
            _lanRoomDiscovery.StartBrowsing();
        }
    }

    private string HostStatus(int connected)
        => $"HOSTING UDP {_hostPort}  //  {connected}/{ActivePlayerCapacity}";

    private static string FriendlyHostName()
    {
        var value = System.Environment.MachineName.Trim();
        if (value.Length == 0)
        {
            return "STEEL-TIDE-HOST";
        }
        return value.Length <= 48 ? value : value[..48];
    }
}
