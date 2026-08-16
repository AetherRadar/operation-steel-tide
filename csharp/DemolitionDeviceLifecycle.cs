using System;
using System.Collections.Generic;

namespace OperationSteelTide;

internal enum DemolitionDevicePhase
{
    Inactive,
    Grounded,
    Carried,
    Planted,
    Detonated
}

internal readonly record struct DemolitionDeviceLifecycleSnapshot(
    DemolitionDevicePhase Phase,
    string? CarrierMemberId,
    string? PickupRunnerMemberId);

/// <summary>
/// Pure round-local ownership state for the demolition device. World code owns actor
/// resolution and visuals; this type keeps pickup, hand-off, plant, and detonation
/// transitions explicit and independently diagnosable.
/// </summary>
internal sealed class DemolitionDeviceLifecycle
{
    public DemolitionDevicePhase Phase { get; private set; } = DemolitionDevicePhase.Inactive;
    public string? CarrierMemberId { get; private set; }
    public string? PickupRunnerMemberId { get; private set; }

    public bool IsGrounded => Phase == DemolitionDevicePhase.Grounded;
    public bool IsCarried => Phase == DemolitionDevicePhase.Carried;
    public bool IsPlanted => Phase == DemolitionDevicePhase.Planted;

    public void BeginGrounded()
    {
        Phase = DemolitionDevicePhase.Grounded;
        CarrierMemberId = null;
        PickupRunnerMemberId = null;
    }

    public string? AssignRandomPickupRunner(
        IReadOnlyList<string> availableMemberIds,
        uint selectionToken)
    {
        ArgumentNullException.ThrowIfNull(availableMemberIds);
        if (!IsGrounded || availableMemberIds.Count == 0)
        {
            PickupRunnerMemberId = null;
            return null;
        }

        var index = (int)(selectionToken % (uint)availableMemberIds.Count);
        return AssignPickupRunner(availableMemberIds[index])
            ? PickupRunnerMemberId
            : null;
    }

    public bool AssignPickupRunner(string? memberId)
    {
        if (!IsGrounded || string.IsNullOrWhiteSpace(memberId))
        {
            return false;
        }
        PickupRunnerMemberId = memberId;
        return true;
    }

    public void ClearPickupRunner()
    {
        if (IsGrounded)
        {
            PickupRunnerMemberId = null;
        }
    }

    public bool TryPickup(string memberId)
    {
        if (!IsGrounded
            || string.IsNullOrWhiteSpace(memberId)
            || !string.Equals(PickupRunnerMemberId, memberId, StringComparison.Ordinal))
        {
            return false;
        }
        Phase = DemolitionDevicePhase.Carried;
        CarrierMemberId = memberId;
        PickupRunnerMemberId = null;
        return true;
    }

    public bool TryDrop(string memberId, string? replacementRunnerMemberId)
    {
        if (!IsCarried
            || string.IsNullOrWhiteSpace(memberId)
            || !string.Equals(CarrierMemberId, memberId, StringComparison.Ordinal))
        {
            return false;
        }
        Phase = DemolitionDevicePhase.Grounded;
        CarrierMemberId = null;
        PickupRunnerMemberId = string.IsNullOrWhiteSpace(replacementRunnerMemberId)
            ? null
            : replacementRunnerMemberId;
        return true;
    }

    public bool TryPlant(string memberId)
    {
        if (!IsCarried
            || string.IsNullOrWhiteSpace(memberId)
            || !string.Equals(CarrierMemberId, memberId, StringComparison.Ordinal))
        {
            return false;
        }
        Phase = DemolitionDevicePhase.Planted;
        CarrierMemberId = null;
        PickupRunnerMemberId = null;
        return true;
    }

    public bool TryDetonate()
    {
        if (!IsPlanted)
        {
            return false;
        }
        Phase = DemolitionDevicePhase.Detonated;
        return true;
    }

    public void Clear()
    {
        Phase = DemolitionDevicePhase.Inactive;
        CarrierMemberId = null;
        PickupRunnerMemberId = null;
    }

    public DemolitionDeviceLifecycleSnapshot Capture()
        => new(Phase, CarrierMemberId, PickupRunnerMemberId);

    public void Restore(DemolitionDeviceLifecycleSnapshot snapshot)
    {
        Phase = snapshot.Phase;
        CarrierMemberId = snapshot.CarrierMemberId;
        PickupRunnerMemberId = snapshot.PickupRunnerMemberId;
    }
}
