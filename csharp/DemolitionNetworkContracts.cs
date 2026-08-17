namespace OperationSteelTide;

public enum DemolitionNetworkTeam
{
    Alpha,
    Bravo
}

public enum DemolitionNetworkAction
{
    Plant,
    Defuse
}

public readonly record struct DemolitionPlayerNetworkState(
    long PeerId,
    DemolitionNetworkTeam Team,
    int Slot,
    OperatorRole Role,
    Godot.Vector3 Position,
    Godot.Vector3 Rotation,
    float Health,
    bool Dead);

public readonly record struct DemolitionActorNetworkState(
    int ActorId,
    OperatorRole Role,
    Godot.Vector3 Position,
    Godot.Vector3 Rotation,
    float Health,
    bool Dead,
    bool Human);

public readonly record struct DemolitionMatchNetworkState(
    int CurrentRound,
    int AlphaScore,
    int BravoScore,
    bool Overtime,
    bool Complete,
    bool RoundActive,
    bool BuyActive,
    float Remaining,
    int DevicePhase,
    int ActiveSite,
    int CarrierActorId,
    Godot.Vector3 DevicePosition);
