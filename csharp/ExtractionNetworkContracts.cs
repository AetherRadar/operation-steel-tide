using Godot;

namespace OperationSteelTide;

[System.Flags]
public enum ExtractionEnemyNetworkFlags
{
    None = 0,
    Dead = 1 << 0,
    Prone = 1 << 1,
    Alerted = 1 << 2,
    HasWeapon = 1 << 3,
    WorldBoss = 1 << 4,
    Sentry = 1 << 5,
    CarriedWeaponVisible = 1 << 6
}

[System.Flags]
public enum ExtractionSquadNetworkFlags
{
    None = 0,
    Human = 1 << 0,
    Down = 1 << 1,
    BodyBag = 1 << 2,
    ReviveUsed = 1 << 3,
    HasWeapon = 1 << 4
}

public enum ExtractionLootSourceKind
{
    Static,
    EnemyCorpse,
    Dropped,
    SquadBodyBag,
    SupplyDrop
}

public readonly record struct ExtractionLobbyMember(
    long PeerId,
    int Slot,
    OperatorRole Role,
    bool Host);

public readonly record struct ExtractionLobbyState(
    string MapId,
    int PlayerCount,
    int Capacity,
    bool MatchStarted);

public readonly record struct ExtractionEnemyNetworkState(
    int NetworkId,
    int TeamId,
    Vector3 Position,
    Vector3 Rotation,
    float Health,
    int WeaponPlatform,
    int Flags);

public readonly record struct ExtractionSquadNetworkState(
    int Slot,
    long PeerId,
    OperatorRole Role,
    Vector3 Position,
    Vector3 Rotation,
    float Health,
    int Flags);

public readonly record struct ExtractionWorldNetworkState(
    int Sequence,
    ExtractionEnemyNetworkState[] Enemies,
    ExtractionSquadNetworkState[] Squad);

public readonly record struct ExtractionMissionNetworkState(
    string Phase,
    float Remaining,
    bool Online,
    int ObjectiveStage,
    string Objective,
    bool DeploymentProtected,
    bool ReinforcementPending,
    bool ReinforcementsDeployed,
    float ReinforcementCountdown,
    int EnemiesRemaining,
    bool ExtractionActive,
    float ExtractionRemaining,
    bool MissionEnded,
    bool ExtractionDeparturePlaying,
    bool MissionSucceeded,
    bool WorldBossDefeated);

public readonly record struct ExtractionLootSourceNetworkState(
    int SourceId,
    ExtractionLootSourceKind Kind,
    Vector3 Position,
    bool Opened,
    bool Granted,
    string ItemsJson);
