using System;
using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public readonly record struct DemolitionWeaponDropNetworkState(
    int Round,
    int DropId,
    int Revision,
    Vector3 Position,
    bool Active,
    string ItemsJson);

public readonly record struct DemolitionWeaponPickupNetworkResult(
    int Round,
    int DropId,
    int RequestedRevision,
    bool Approved,
    PlayerWeaponSlot TargetSlot,
    string AwardedItemJson,
    DemolitionWeaponDropNetworkState State);

public static class DemolitionBotLoadoutNetworkCodec
{
    private const int BitsPerSlot = 4;
    private const int SlotMask = (1 << BitsPerSlot) - 1;
    public const int SlotCount = 5;

    public static int Encode(IReadOnlyList<WeaponBuild?> loadout)
        => EncodePlatforms(
            PlatformAt(loadout, 0),
            PlatformAt(loadout, 1),
            PlatformAt(loadout, 2),
            PlatformAt(loadout, 3),
            PlatformAt(loadout, 4));

    public static int EncodePlatforms(
        WeaponPlatform? slot0,
        WeaponPlatform? slot1,
        WeaponPlatform? slot2,
        WeaponPlatform? slot3,
        WeaponPlatform? slot4)
    {
        var packed = 0;
        for (var slot = 0; slot < SlotCount; slot++)
        {
            var platform = slot switch
            {
                0 => slot0,
                1 => slot1,
                2 => slot2,
                3 => slot3,
                _ => slot4
            };
            var encoded = platform is null ? 0 : (int)platform.Value + 1;
            if (encoded is < 0 or > SlotMask)
            {
                encoded = 0;
            }
            packed |= encoded << (slot * BitsPerSlot);
        }
        return packed;
    }

    private static WeaponPlatform? PlatformAt(IReadOnlyList<WeaponBuild?> loadout, int slot)
        => slot < loadout.Count ? loadout[slot]?.Platform : null;

    public static IReadOnlyList<WeaponBuild?> Decode(int packed)
    {
        var loadout = new WeaponBuild?[SlotCount];
        for (var slot = 0; slot < SlotCount; slot++)
        {
            loadout[slot] = WeaponForSlot(packed, slot);
        }
        return loadout;
    }

    public static WeaponBuild? WeaponForSlot(int packed, int slot)
    {
        if (slot is < 0 or >= SlotCount || !IsValid(packed))
        {
            return null;
        }
        var encoded = packed >> (slot * BitsPerSlot) & SlotMask;
        return encoded == 0
            ? null
            : WeaponCatalog.Build((WeaponPlatform)(encoded - 1), 0);
    }

    public static bool IsValid(int packed)
    {
        if (packed < 0 || packed >> (SlotCount * BitsPerSlot) != 0)
        {
            return false;
        }
        for (var slot = 0; slot < SlotCount; slot++)
        {
            var encoded = packed >> (slot * BitsPerSlot) & SlotMask;
            if (encoded != 0
                && !Enum.IsDefined(typeof(WeaponPlatform), encoded - 1))
            {
                return false;
            }
        }
        return true;
    }
}

public static class DemolitionWeaponDropNetworkRules
{
    private const int MaximumItemsJsonLength = 16_384;

    public static bool IsStatePayloadValid(DemolitionWeaponDropNetworkState state)
        => state.Round >= 1
        && state.DropId >= 0
        && state.Revision >= 0
        && IsFinite(state.Position)
        && state.ItemsJson is not null
        && state.ItemsJson.Length <= MaximumItemsJsonLength
        && (!state.Active || state.ItemsJson.Length > 2);

    public static bool IsPickupRequestValid(int round, int dropId, int expectedRevision)
        => round >= 1 && dropId >= 0 && expectedRevision >= 0;

    public static bool MatchesCurrentRevision(
        DroppedWeaponPickup pickup,
        int round,
        int dropId,
        int expectedRevision)
        => GodotObject.IsInstanceValid(pickup)
        && pickup.DemolitionRound == round
        && pickup.DropId == dropId
        && pickup.Revision == expectedRevision
        && pickup.IsSearchable;

    public static bool IsNewerThanLocal(
        DemolitionWeaponDropNetworkState state,
        DroppedWeaponPickup? local)
        => local is null
        || !GodotObject.IsInstanceValid(local)
        || state.Round > local.DemolitionRound
        || state.Round == local.DemolitionRound && state.Revision > local.Revision;

    private static bool IsFinite(Vector3 value)
        => float.IsFinite(value.X)
        && float.IsFinite(value.Y)
        && float.IsFinite(value.Z);
}

public partial class SquadNetwork
{
    public event Action<long, int, int, int>? DemolitionWeaponPickupRequested;
    public event Action<DemolitionWeaponDropNetworkState>? DemolitionWeaponDropStateReceived;
    public event Action<DemolitionWeaponPickupNetworkResult>? DemolitionWeaponPickupResultReceived;

    public bool RequestDemolitionWeaponPickup(int round, int dropId, int expectedRevision)
    {
        if (!IsOnline
            || !IsDemolitionSession
            || !DemolitionMatchStarted
            || !DemolitionWeaponDropNetworkRules.IsPickupRequestValid(
                round,
                dropId,
                expectedRevision))
        {
            return false;
        }
        if (IsHost)
        {
            DemolitionWeaponPickupRequested?.Invoke(1, round, dropId, expectedRevision);
            return true;
        }
        RpcId(
            1,
            MethodName.SubmitDemolitionWeaponPickup,
            round,
            dropId,
            expectedRevision);
        return true;
    }

    public void BroadcastDemolitionWeaponDropState(DemolitionWeaponDropNetworkState state)
    {
        if (!IsOnline
            || !IsHost
            || !IsDemolitionSession
            || !DemolitionMatchStarted
            || !DemolitionWeaponDropNetworkRules.IsStatePayloadValid(state))
        {
            return;
        }
        foreach (var peerId in RegisteredDemolitionPeerIds())
        {
            RpcId(
                peerId,
                MethodName.ReceiveDemolitionWeaponDropState,
                state.Round,
                state.DropId,
                state.Revision,
                state.Position,
                state.Active,
                state.ItemsJson);
        }
    }

    public void SendDemolitionWeaponPickupResult(
        long peerId,
        DemolitionWeaponPickupNetworkResult result)
    {
        if (!IsOnline
            || !IsHost
            || !IsDemolitionSession
            || !DemolitionMatchStarted
            || peerId <= 1
            || !DemolitionWeaponDropNetworkRules.IsStatePayloadValid(result.State))
        {
            return;
        }
        RpcId(
            peerId,
            MethodName.ReceiveDemolitionWeaponPickupResult,
            result.Round,
            result.DropId,
            result.RequestedRevision,
            result.Approved,
            (int)result.TargetSlot,
            result.AwardedItemJson,
            result.State.Revision,
            result.State.Position,
            result.State.Active,
            result.State.ItemsJson);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SubmitDemolitionWeaponPickup(int round, int dropId, int expectedRevision)
    {
        var sender = Multiplayer.GetRemoteSenderId();
        if (!IsHost
            || !IsDemolitionSession
            || !DemolitionMatchStarted
            || !_demolitionAssignments.ContainsKey(sender)
            || !DemolitionWeaponDropNetworkRules.IsPickupRequestValid(
                round,
                dropId,
                expectedRevision))
        {
            return;
        }
        DemolitionWeaponPickupRequested?.Invoke(
            sender,
            round,
            dropId,
            expectedRevision);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveDemolitionWeaponDropState(
        int round,
        int dropId,
        int revision,
        Vector3 position,
        bool active,
        string itemsJson)
    {
        var state = new DemolitionWeaponDropNetworkState(
            round,
            dropId,
            revision,
            position,
            active,
            itemsJson);
        if (IsHost
            || !IsDemolitionSession
            || !DemolitionMatchStarted
            || !DemolitionWeaponDropNetworkRules.IsStatePayloadValid(state))
        {
            return;
        }
        DemolitionWeaponDropStateReceived?.Invoke(state);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveDemolitionWeaponPickupResult(
        int round,
        int dropId,
        int requestedRevision,
        bool approved,
        int targetSlot,
        string awardedItemJson,
        int stateRevision,
        Vector3 statePosition,
        bool stateActive,
        string stateItemsJson)
    {
        var state = new DemolitionWeaponDropNetworkState(
            round,
            dropId,
            stateRevision,
            statePosition,
            stateActive,
            stateItemsJson);
        if (IsHost
            || !IsDemolitionSession
            || !DemolitionMatchStarted
            || !DemolitionWeaponDropNetworkRules.IsPickupRequestValid(
                round,
                dropId,
                requestedRevision)
            || !Enum.IsDefined(typeof(PlayerWeaponSlot), targetSlot)
            || !DemolitionWeaponDropNetworkRules.IsStatePayloadValid(state)
            || awardedItemJson is null
            || awardedItemJson.Length > 16_384)
        {
            return;
        }
        DemolitionWeaponPickupResultReceived?.Invoke(
            new DemolitionWeaponPickupNetworkResult(
                round,
                dropId,
                requestedRevision,
                approved,
                (PlayerWeaponSlot)targetSlot,
                awardedItemJson,
                state));
    }
}
