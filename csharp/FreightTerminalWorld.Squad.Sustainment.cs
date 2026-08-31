using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld : ISquadSustainmentRuntime
{
    private SquadSustainmentService? _squadSustainmentService;

    private SquadSustainmentService SquadSustainment
        => _squadSustainmentService ??= new SquadSustainmentService(this);

    internal bool IsSquadSustainmentEnabled => SquadSustainment.Enabled;
    internal bool IsSquadEvacuationInProgress => SquadSustainment.EvacuationInProgress;

    internal bool ShouldSuppressSquadLooting(SquadMate mate)
        => SquadSustainment.ShouldSuppressLooting(mate);

    internal bool TryReserveBestSquadSustainmentSource(
        SquadMate mate,
        float range,
        out ILootSource? source)
        => SquadSustainment.TryReserveBestSource(mate, range, out source);

    internal void ReleaseSquadSustainmentSource(SquadMate mate, ILootSource source)
        => SquadSustainment.Release(mate, source);

    internal void ReleaseSquadSustainmentSource(SquadMate mate, ulong sourceId)
        => SquadSustainment.Release(mate, sourceId);

    internal bool IsSquadSustainmentReservationOwner(SquadMate mate, ulong sourceId)
        => SquadSustainment.IsReservationOwner(mate, sourceId);

    internal bool TryMateTakeSustainmentLoot(SquadMate mate, ILootSource source)
        => SquadSustainment.TryTakeLoot(mate, source);

    internal int LivingSquadRecoveredSustainmentValue()
        => SquadSustainment.RecoveredValue(_squadMates);

    bool ISquadSustainmentRuntime.IsDemolitionMode => _demolitionMode;
    bool ISquadSustainmentRuntime.MissionEnded => _missionEnded;
    bool ISquadSustainmentRuntime.ExtractionCountdownActive
        => _extractionCountdownActive;
    bool ISquadSustainmentRuntime.LocalPlayerDowned => _localPlayerDowned;
    bool ISquadSustainmentRuntime.LocalPlayerEliminated => _localPlayerEliminated;
    bool ISquadSustainmentRuntime.ExtractionNetworkMatch => IsExtractionNetworkMatch;
    bool ISquadSustainmentRuntime.PlayerCanBeRevived
        => IsInstanceValid(_player) && _player.CanBeRevived;
    IReadOnlyList<SquadMate> ISquadSustainmentRuntime.SquadMates => _squadMates;
    IReadOnlyList<ILootSource> ISquadSustainmentRuntime.LootSources => _lootSources;
    ILootSource? ISquadSustainmentRuntime.OpenLootSource => _openLootSource;

    bool ISquadSustainmentRuntime.IsExtractionLootLeasedByOther(ILootSource source)
    {
        if (!IsExtractionNetworkMatch
            || !_extractionLootIds.TryGetValue(source.LootNode.GetInstanceId(), out var sourceId))
        {
            return false;
        }
        return _extractionLootLeaseOwners.ContainsKey(sourceId);
    }

    void ISquadSustainmentRuntime.CommitLootMutation(ILootSource source)
    {
        RefreshGradedLootPickupPresentation(source);
        PublishExtractionLootMutation(source);
        RetireEmptyGradedLootPickup(source);
    }
}
