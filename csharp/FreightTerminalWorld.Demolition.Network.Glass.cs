using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private BreakableGlassField? _demolitionNetworkGlassField;
    private uint _demolitionBazaarGlassMask;
    private uint? _pendingDemolitionBazaarGlassMask;

    public void OnLocalPlayerGlassImpact(
        Vector3 origin,
        Vector3 end,
        float damage,
        bool melee)
    {
        if (IsDemolitionNetworkClient && _demolitionRoundActive)
        {
            _squadNetwork.RequestDemolitionGlassHit(origin, end, damage, melee);
        }
    }

    private void ConfigureDemolitionGlassNetwork()
    {
        DetachDemolitionGlassNetwork();
        var field = _demolitionArena?.BazaarGlassFields.FirstOrDefault(IsInstanceValid);
        if (!IsInstanceValid(field))
        {
            return;
        }

        _demolitionNetworkGlassField = field;
        field!.SetLocalShatterAuthority(!IsDemolitionNetworkClient);
        if (IsDemolitionNetworkClient && _pendingDemolitionBazaarGlassMask.HasValue)
        {
            _demolitionBazaarGlassMask = _pendingDemolitionBazaarGlassMask.Value
                & field.ValidPaneMask;
            field.ApplyShatteredPaneMask(_demolitionBazaarGlassMask);
            _pendingDemolitionBazaarGlassMask = null;
        }
        else
        {
            _demolitionBazaarGlassMask = field.ShatteredPaneMask;
        }
        if (_squadNetwork.IsOnline && _squadNetwork.IsHost)
        {
            field.PaneShattered += OnAuthoritativeDemolitionGlassShattered;
        }
    }

    private void DetachDemolitionGlassNetwork()
    {
        if (!IsInstanceValid(_demolitionNetworkGlassField))
        {
            _demolitionNetworkGlassField = null;
            return;
        }
        _demolitionNetworkGlassField!.PaneShattered -= OnAuthoritativeDemolitionGlassShattered;
        _demolitionNetworkGlassField.SetLocalShatterAuthority(true);
        _demolitionNetworkGlassField = null;
    }

    private void OnAuthoritativeDemolitionGlassShattered(
        BreakableGlassField field,
        int paneIndex,
        uint shatteredMask)
    {
        if (!_demolitionMode
            || !_demolitionRoundActive
            || !_squadNetwork.IsHost
            || !ReferenceEquals(field, _demolitionNetworkGlassField))
        {
            return;
        }
        _demolitionBazaarGlassMask = shatteredMask & field.ValidPaneMask;
        _squadNetwork.BroadcastDemolitionGlassState(new DemolitionGlassNetworkState(
            _demolitionBazaarGlassMask,
            paneIndex,
            field.LastShatterPosition));
    }

    private void OnDemolitionGlassHitRequested(DemolitionGlassHitNetworkRequest request)
    {
        if (!_demolitionMode
            || !_demolitionRoundActive
            || !_squadNetwork.IsHost
            || !IsInstanceValid(_demolitionNetworkGlassField))
        {
            return;
        }

        Node3D? shooter = _squadMates.FirstOrDefault(mate => IsInstanceValid(mate)
            && mate.IsHumanProxy
            && mate.NetworkPeerId == request.PeerId);
        if (!IsInstanceValid(shooter)
            && _remoteDemolitionOpponents.TryGetValue(request.PeerId, out var opponent)
            && IsInstanceValid(opponent))
        {
            shooter = opponent;
        }
        if (!IsInstanceValid(shooter)
            || shooter!.GlobalPosition.DistanceTo(request.Origin) > 4.5f)
        {
            return;
        }

        var direction = request.Origin.DirectionTo(request.End);
        if (direction.LengthSquared() < 0.5f)
        {
            return;
        }
        var extendedEnd = request.End + direction * 0.12f;
        if (!BreakableGlassField.TryFindIntactPaneAlongRay(
                GetWorld3D(),
                request.Origin,
                extendedEnd,
                out var hitField,
                out _,
                out _,
                out _)
            || !ReferenceEquals(hitField, _demolitionNetworkGlassField))
        {
            return;
        }

        BreakableGlassField.TryShatterAlongRay(
            GetWorld3D(),
            request.Origin,
            extendedEnd,
            Mathf.Clamp(request.Damage, 4.0f, 180.0f),
            direction,
            out _);
    }

    private void OnDemolitionGlassState(DemolitionGlassNetworkState state)
        => ApplyDemolitionGlassNetworkState(state, spawnEffects: true);

    private void ApplyDemolitionGlassNetworkState(
        DemolitionGlassNetworkState state,
        bool spawnEffects)
    {
        _demolitionBazaarGlassMask = state.ShatteredMask;
        if (!IsDemolitionNetworkClient)
        {
            return;
        }
        if (!IsInstanceValid(_demolitionNetworkGlassField))
        {
            _pendingDemolitionBazaarGlassMask = state.ShatteredMask;
            return;
        }
        var field = _demolitionNetworkGlassField!;
        _demolitionBazaarGlassMask &= field.ValidPaneMask;
        field.ApplyShatteredPaneMask(
            _demolitionBazaarGlassMask,
            state.EffectPaneIndex,
            state.EffectPosition,
            spawnEffects);
    }

    private uint CaptureDemolitionBazaarGlassMask()
    {
        if (IsInstanceValid(_demolitionNetworkGlassField))
        {
            _demolitionBazaarGlassMask = _demolitionNetworkGlassField!.ShatteredPaneMask;
        }
        return _demolitionBazaarGlassMask;
    }

    private void ApplyDemolitionGlassSnapshot(uint shatteredMask, bool roundChanged)
        => ApplyDemolitionGlassNetworkState(
            new DemolitionGlassNetworkState(
                MergeDemolitionGlassSnapshot(
                    _demolitionBazaarGlassMask,
                    shatteredMask,
                    roundChanged),
                -1,
                Vector3.Zero),
            spawnEffects: false);

    internal static uint MergeDemolitionGlassSnapshot(
        uint currentMask,
        uint snapshotMask,
        bool roundChanged)
        => roundChanged ? snapshotMask : currentMask | snapshotMask;

    private void SynchronizeDemolitionGlassRoundReset()
    {
        _demolitionBazaarGlassMask = 0u;
        _pendingDemolitionBazaarGlassMask = null;
        if (!_squadNetwork.IsOnline || !_squadNetwork.IsHost)
        {
            return;
        }
        _squadNetwork.BroadcastDemolitionGlassState(new DemolitionGlassNetworkState(
            0u,
            -1,
            Vector3.Zero));
    }
}
