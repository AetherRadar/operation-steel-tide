using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private readonly record struct DemolitionDeviceRuntimeDiagnosticState(
        DemolitionDeviceLifecycleSnapshot Lifecycle,
        Vector3 GroundPosition,
        Vector3 LastCarrierPosition,
        string Name,
        Transform3D Transform,
        Vector3 Scale,
        bool Visible,
        bool BeaconVisible,
        float BeaconEnergy,
        float BeaconRange,
        int DetonationCount);

    private DemolitionDeviceRuntimeDiagnosticState CaptureDemolitionDeviceRuntimeForDiagnostics()
        => new(
            _demolitionDeviceLifecycle.Capture(),
            _demolitionDeviceGroundPosition,
            _demolitionDeviceLastCarrierPosition,
            IsInstanceValid(_demolitionDevice) ? _demolitionDevice!.Name.ToString() : string.Empty,
            IsInstanceValid(_demolitionDevice) ? _demolitionDevice!.GlobalTransform : Transform3D.Identity,
            IsInstanceValid(_demolitionDevice) ? _demolitionDevice!.Scale : Vector3.One,
            IsInstanceValid(_demolitionDevice) && _demolitionDevice!.Visible,
            IsInstanceValid(_demolitionDeviceBeacon) && _demolitionDeviceBeacon!.Visible,
            IsInstanceValid(_demolitionDeviceBeacon) ? _demolitionDeviceBeacon!.LightEnergy : 0.0f,
            IsInstanceValid(_demolitionDeviceBeacon) ? _demolitionDeviceBeacon!.OmniRange : 0.0f,
            _demolitionDetonationCount);

    private void RestoreDemolitionDeviceRuntimeForDiagnostics(
        DemolitionDeviceRuntimeDiagnosticState state)
    {
        _demolitionDeviceLifecycle.Restore(state.Lifecycle);
        _demolitionDeviceGroundPosition = state.GroundPosition;
        _demolitionDeviceLastCarrierPosition = state.LastCarrierPosition;
        _demolitionDetonationCount = state.DetonationCount;
        if (IsInstanceValid(_demolitionDevice))
        {
            if (!string.IsNullOrWhiteSpace(state.Name))
            {
                _demolitionDevice!.Name = state.Name;
            }
            _demolitionDevice!.GlobalTransform = state.Transform;
            _demolitionDevice.Scale = state.Scale;
            _demolitionDevice.Visible = state.Visible;
        }
        if (IsInstanceValid(_demolitionDeviceBeacon))
        {
            _demolitionDeviceBeacon!.Visible = state.BeaconVisible;
            _demolitionDeviceBeacon.LightEnergy = state.BeaconEnergy;
            _demolitionDeviceBeacon.OmniRange = state.BeaconRange;
        }
    }

    private void ForceDemolitionDevicePickupRunnerForDiagnostics(Node3D runner)
    {
        var previousEnemyCarrier = _demolitionCarrier;
        _demolitionCarrier = null;
        ResetDemolitionOpponentRoute(previousEnemyCarrier);
        _demolitionSquadObjectiveMate = null;
        _demolitionPlantProgress = 0.0f;
        _demolitionEnemyPlantProgress = 0.0f;
        _demolitionSquadPlantProgress = 0.0f;
        _demolitionDeviceLifecycle.BeginGrounded();
        _demolitionDeviceLifecycle.AssignPickupRunner(DemolitionMemberId(runner));
        ApplyDemolitionDevicePickupAssignment(runner);
        SyncDemolitionDeviceVisual();
    }

    private bool ForceDemolitionDeviceCarrierForDiagnostics(Node3D carrier)
    {
        ForceDemolitionDevicePickupRunnerForDiagnostics(carrier);
        return TryPickupDemolitionDevice(carrier);
    }
}
