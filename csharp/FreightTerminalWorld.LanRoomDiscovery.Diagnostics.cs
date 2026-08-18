using System.Text;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateLanRoomDiscovery()
    {
        await WaitFrames(3);
        var version = ProjectSettings.GetSetting("application/config/version", "dev").AsString();
        var permissionReceiver = new PacketPeerUdp();
        var permissionBindError = permissionReceiver.Bind(0, "127.0.0.1");
        var permissionProbeSent = permissionBindError == Error.Ok
            && LanRoomDiscovery.SendLocalNetworkPermissionProbeForDiagnostics(
                new[] { "127.0.0.1" },
                permissionReceiver.GetLocalPort()) == 1;
        await ToSignal(GetTree().CreateTimer(0.08, true), SceneTreeTimer.SignalName.Timeout);
        var permissionProbeReceived = permissionProbeSent
            && permissionReceiver.GetAvailablePacketCount() == 1
            && permissionReceiver.GetPacket().Length > 0;
        permissionReceiver.Close();

        var browser = new LanRoomDiscovery { Name = "LanRoomDiagnosticBrowser" };
        var advertiser = new LanRoomDiscovery { Name = "LanRoomDiagnosticAdvertiser" };
        AddChild(browser);
        AddChild(advertiser);
        browser.ConfigureTimingForDiagnostics(0.04, 180);
        advertiser.ConfigureTimingForDiagnostics(0.04, 180);

        var snapshotCount = 0;
        browser.RoomsChanged += _ => snapshotCount++;
        var browseError = browser.StartBrowsing(0, "127.0.0.1");
        var listenerReady = browseError == Error.Ok && browser.ListeningPort > 0;
        var advertisement = new LanRoomAdvertisement(
            "diagnostic-room",
            "LAN DIAGNOSTIC",
            version,
            30117,
            LanRoomKind.Extraction,
            DeploymentMapCatalog.FreightTerminalId,
            1,
            SquadNetwork.MaximumPlayers);
        if (listenerReady)
        {
            advertiser.StartAdvertisingForDiagnostics(
                advertisement,
                new[] { "127.0.0.1" },
                browser.ListeningPort);
        }

        await ToSignal(GetTree().CreateTimer(0.22, true), SceneTreeTimer.SignalName.Timeout);
        var rooms = browser.Rooms;
        var discovered = rooms.Count == 1
            && rooms[0].RoomId == advertisement.RoomId
            && rooms[0].Address == "127.0.0.1"
            && rooms[0].Endpoint == "127.0.0.1:30117"
            && rooms[0].PlayerCount == 1;

        advertiser.UpdateAdvertisingPlayers(3);
        await ToSignal(GetTree().CreateTimer(0.12, true), SceneTreeTimer.SignalName.Timeout);
        var updated = browser.Rooms.Count == 1 && browser.Rooms[0].PlayerCount == 3;

        var packet = LanRoomDiscovery.EncodeForDiagnostics(advertisement);
        var decoded = LanRoomDiscovery.TryDecodeForDiagnostics(
            packet,
            "192.168.50.24",
            version,
            out var decodedRoom)
            && decodedRoom.Address == "192.168.50.24"
            && decodedRoom.Endpoint == "192.168.50.24:30117";
        var wrongVersionRejected = !LanRoomDiscovery.TryDecodeForDiagnostics(
            packet,
            "192.168.50.24",
            version + "-other",
            out _);
        var malformedRejected = !LanRoomDiscovery.TryDecodeForDiagnostics(
            Encoding.UTF8.GetBytes("{}"),
            "192.168.50.24",
            version,
            out _)
            && !LanRoomDiscovery.TryDecodeForDiagnostics(
                Encoding.UTF8.GetBytes("{not-json"),
                "192.168.50.24",
                version,
                out _);

        _hud.ShowSquadLobby("LAN DISCOVERY VALIDATION");
        _hud.SetLanRoomBrowseAvailable(true);
        _hud.SetLanRooms(browser.Rooms);
        _hud.SelectSquadLanRoomForDiagnostics(0);
        var extractionSelection = _hud.SquadLanRoomBrowserUiReady
            && _hud.VisibleExtractionLanRoomCount == 1
            && _hud.SelectedSquadSessionMode == SquadSessionMode.Join
            && _hud.SquadNetworkAddress == "127.0.0.1:30117"
            && _hud.SelectedDeploymentMapId == DeploymentMapCatalog.FreightTerminalId;

        var packedBriefing = GD.Load<PackedScene>("res://ui/DemolitionBriefingView.tscn");
        var briefing = packedBriefing?.Instantiate<DemolitionBriefingView>();
        var demolitionSelection = false;
        if (briefing is not null)
        {
            briefing.Visible = false;
            _hud.AddChild(briefing);
            briefing.SetLanRoomBrowseAvailable(true);
            briefing.SetLanRooms(new[]
            {
                new LanRoomInfo(
                    "diagnostic-demolition-room",
                    "LAN DEMOLITION",
                    "192.168.50.25",
                    30118,
                    LanRoomKind.Demolition,
                    DemolitionMapCatalog.HarborLocksId,
                    2,
                    SquadNetwork.MaximumPlayers)
            });
            briefing.SelectLanRoomForDiagnostics(0);
            demolitionSelection = briefing.LanRoomBrowserUiReady
                && briefing.VisibleLanRoomCount == 1
                && briefing.SelectedSessionMode == SquadSessionMode.Join
                && briefing.NetworkAddress == "192.168.50.25:30118"
                && briefing.SelectedMapId == DemolitionMapCatalog.HarborLocksId;
        }

        advertiser.StopAdvertising();
        await ToSignal(GetTree().CreateTimer(0.9, true), SceneTreeTimer.SignalName.Timeout);
        var expired = browser.Rooms.Count == 0;
        var valid = permissionProbeSent && permissionProbeReceived
            && listenerReady && discovered && updated && decoded && wrongVersionRejected
            && malformedRejected && extractionSelection && demolitionSelection && expired
            && snapshotCount >= 4;
        GD.Print(
            $"LAN_DISCOVERY_CHECK valid={valid} permission_sent={permissionProbeSent} "
            + $"permission_received={permissionProbeReceived} listener={listenerReady} discovered={discovered} "
            + $"updated={updated} decoded={decoded} version_rejected={wrongVersionRejected} "
            + $"malformed_rejected={malformedRejected} extraction_ui={extractionSelection} "
            + $"demolition_ui={demolitionSelection} expired={expired} snapshots={snapshotCount}");
        GD.Print($"LAN_DISCOVERY_PASS valid={valid}");
        GetTree().Paused = false;
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }
}
