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

public enum DemolitionNetworkPhase
{
    Lobby,
    Buy,
    Live,
    Intermission,
    Complete
}

public readonly record struct DemolitionLobbyMember(
    long PeerId,
    DemolitionNetworkTeam Team,
    int Slot,
    OperatorRole Role,
    bool Host);

public readonly record struct DemolitionLobbyState(
    string MapId,
    int PlayerCount,
    int AlphaPlayers,
    int BravoPlayers,
    int Capacity,
    bool MatchStarted);

public readonly record struct DemolitionPurchaseNetworkResult(
    int Round,
    bool Approved,
    DemolitionPurchaseSelection Selection,
    int TotalCost,
    int RemainingFunds);

public readonly record struct DemolitionFundsNetworkState(
    int Round,
    int Funds);

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
    DemolitionNetworkPhase Phase,
    float PhaseRemaining,
    int AlphaFunds,
    int BravoFunds,
    int DevicePhase,
    int ActiveSite,
    int CarrierActorId,
    Godot.Vector3 DevicePosition);
