using System;
using Godot;

namespace OperationSteelTide;

public partial class SquadNetwork
{
    private const float MinimumNetworkGlassDamage = 4.0f;
    private const float MaximumNetworkGlassDamage = 180.0f;
    private const float MaximumNetworkGlassShotDistance = 260.0f;
    private const float MaximumNetworkGlassMeleeDistance = 4.0f;

    public event Action<DemolitionGlassHitNetworkRequest>? DemolitionGlassHitRequested;
    public event Action<DemolitionGlassNetworkState>? DemolitionGlassStateReceived;

    public void RequestDemolitionGlassHit(
        Vector3 origin,
        Vector3 end,
        float damage,
        bool melee)
    {
        if (IsHost
            || !IsOnline
            || !IsDemolitionSession
            || !DemolitionMatchStarted
            || !GlassHitPayloadValid(origin, end, damage, melee))
        {
            return;
        }
        RpcId(1, MethodName.SubmitDemolitionGlassHit, origin, end, damage, melee);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SubmitDemolitionGlassHit(
        Vector3 origin,
        Vector3 end,
        float damage,
        bool melee)
    {
        var sender = Multiplayer.GetRemoteSenderId();
        if (!IsHost
            || !IsDemolitionSession
            || !DemolitionMatchStarted
            || !_demolitionAssignments.ContainsKey(sender)
            || !GlassHitPayloadValid(origin, end, damage, melee))
        {
            return;
        }
        DemolitionGlassHitRequested?.Invoke(new DemolitionGlassHitNetworkRequest(
            sender,
            origin,
            end,
            Mathf.Clamp(damage, MinimumNetworkGlassDamage, MaximumNetworkGlassDamage),
            melee));
    }

    public void BroadcastDemolitionGlassState(DemolitionGlassNetworkState state)
    {
        if (!IsOnline
            || !IsHost
            || !IsDemolitionSession
            || !DemolitionMatchStarted
            || state.EffectPaneIndex is < -1 or >= sizeof(uint) * 8
            || !IsFinite(state.EffectPosition)
            || state.EffectPaneIndex >= 0
                && (state.ShatteredMask & (1u << state.EffectPaneIndex)) == 0u)
        {
            return;
        }
        foreach (var peerId in RegisteredDemolitionPeerIds())
        {
            RpcId(
                peerId,
                MethodName.ReceiveDemolitionGlassState,
                state.ShatteredMask,
                state.EffectPaneIndex,
                state.EffectPosition);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveDemolitionGlassState(
        uint shatteredMask,
        int effectPaneIndex,
        Vector3 effectPosition)
    {
        if (!IsDemolitionSession
            || !DemolitionMatchStarted
            || effectPaneIndex is < -1 or >= sizeof(uint) * 8
            || !IsFinite(effectPosition)
            || effectPaneIndex >= 0
                && (shatteredMask & (1u << effectPaneIndex)) == 0u)
        {
            return;
        }
        DemolitionGlassStateReceived?.Invoke(new DemolitionGlassNetworkState(
            shatteredMask,
            effectPaneIndex,
            effectPosition));
    }

    private static bool GlassHitPayloadValid(
        Vector3 origin,
        Vector3 end,
        float damage,
        bool melee)
    {
        if (!IsFinite(origin)
            || !IsFinite(end)
            || !float.IsFinite(damage)
            || damage is < MinimumNetworkGlassDamage or > MaximumNetworkGlassDamage)
        {
            return false;
        }
        var distance = origin.DistanceTo(end);
        return distance > 0.015f
            && distance <= (melee
                ? MaximumNetworkGlassMeleeDistance
                : MaximumNetworkGlassShotDistance);
    }
}
