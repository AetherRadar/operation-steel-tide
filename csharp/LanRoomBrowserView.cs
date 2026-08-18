using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

/// <summary>
/// Displays discovered LAN rooms for one game mode and emits the selected room as user intent.
/// </summary>
[GlobalClass]
public partial class LanRoomBrowserView : MenuButton
{
    public const string ScenePath = "res://ui/LanRoomBrowserView.tscn";
    private const int FirstRoomItemId = 100;

    public event Action<LanRoomInfo>? RoomSelected;

    public bool UiReady => IsInstanceValid(GetPopup());
    public bool IntentSignalsConnected
        => IsInstanceValid(GetPopup()) && GetPopup().HasConnections(PopupMenu.SignalName.IdPressed);
    public int VisibleRoomCount => _visibleRooms.Count;

    private readonly List<LanRoomInfo> _allRooms = new();
    private readonly List<LanRoomInfo> _visibleRooms = new();
    private LanRoomKind _kind;
    private string _language = "en";
    private bool _discoveryAvailable;

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.None;
        GetPopup().IdPressed += OnRoomItemPressed;
        Rebuild();
    }

    public void ApplyLanguage(string language)
    {
        _language = GameLocalization.IsChinese(language) ? "zh" : "en";
        Rebuild();
    }

    public void SetContext(LanRoomKind kind)
    {
        _kind = kind;
        Rebuild();
    }

    public void SetDiscoveryAvailable(bool available)
    {
        _discoveryAvailable = available;
        Rebuild();
    }

    public void SetRooms(IReadOnlyList<LanRoomInfo> rooms)
    {
        _allRooms.Clear();
        _allRooms.AddRange(rooms);
        Rebuild();
    }

    public void SelectRoomForDiagnostics(int index)
    {
        if (index >= 0 && index < _visibleRooms.Count && !_visibleRooms[index].IsFull)
        {
            RoomSelected?.Invoke(_visibleRooms[index]);
        }
    }

    private void Rebuild()
    {
        if (!IsInstanceValid(GetPopup()))
        {
            return;
        }
        _visibleRooms.Clear();
        foreach (var room in _allRooms)
        {
            if (room.Kind == _kind)
            {
                _visibleRooms.Add(room);
            }
        }
        var popup = GetPopup();
        popup.Clear();
        if (_visibleRooms.Count == 0)
        {
            Text = _discoveryAvailable ? "LAN  ..." : "LAN  IP";
            popup.AddItem(
                _discoveryAvailable
                    ? GameLocalization.Get("lan_no_rooms", _language, "NO LAN ROOMS FOUND  //  MANUAL IP AVAILABLE")
                    : GameLocalization.Get("lan_scan_unavailable", _language, "LAN SCAN UNAVAILABLE  //  USE MANUAL IP"),
                0);
            popup.SetItemDisabled(0, true);
        }
        else
        {
            Text = $"LAN  {_visibleRooms.Count}";
            for (var index = 0; index < _visibleRooms.Count; index++)
            {
                var room = _visibleRooms[index];
                popup.AddItem(RoomLabel(room), FirstRoomItemId + index);
                popup.SetItemDisabled(index, room.IsFull);
            }
        }
        TooltipText = GameLocalization.Get(
            "lan_rooms_tooltip",
            _language,
            "LAN ROOMS  //  SELECT TO FILL THE HOST ADDRESS");
    }

    private string RoomLabel(LanRoomInfo room)
    {
        var mapName = room.Kind == LanRoomKind.Extraction
            ? ExtractionMapName(room.MapId)
            : DemolitionMapName(room.MapId);
        var capacity = room.IsFull
            ? GameLocalization.Get("lan_room_full", _language, "FULL")
            : $"{room.PlayerCount}/{room.MaximumPlayers}";
        return $"{room.HostName}  //  {capacity}  //  {mapName}  //  {room.Endpoint}";
    }

    private string ExtractionMapName(string mapId)
    {
        var map = DeploymentMapCatalog.Resolve(mapId);
        return GameLocalization.Get(map.LocalizationKey, _language, map.EnglishName);
    }

    private string DemolitionMapName(string mapId)
    {
        var map = DemolitionMapCatalog.Resolve(mapId);
        return GameLocalization.Get(map.LocalizationKey, _language, map.EnglishName);
    }

    private void OnRoomItemPressed(long id)
    {
        var index = checked((int)id) - FirstRoomItemId;
        if (index < 0 || index >= _visibleRooms.Count || _visibleRooms[index].IsFull)
        {
            return;
        }
        RoomSelected?.Invoke(_visibleRooms[index]);
    }
}
