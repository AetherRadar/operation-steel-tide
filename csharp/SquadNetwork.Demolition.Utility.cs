using System;
using Godot;

namespace OperationSteelTide;

public enum DemolitionNetworkUtilityKind
{
    Fragmentation,
    Smoke,
    Incendiary,
    Flashbang
}

public readonly record struct DemolitionUtilityThrowRequest(
    long PeerId,
    int Round,
    int RequestId,
    DemolitionNetworkUtilityKind Kind,
    Vector3 Origin,
    Vector3 Direction);

public readonly record struct DemolitionUtilityThrowSpawn(
    int SpawnId,
    int Round,
    long SourcePeerId,
    int SourceActorId,
    int RequestId,
    DemolitionNetworkUtilityKind Kind,
    Vector3 Origin,
    Vector3 Direction,
    float Speed,
    float Loft);

public readonly record struct DemolitionFlashbangDetonation(
    int SpawnId,
    int Round,
    Vector3 Position);

public static class DemolitionUtilityNetworkContract
{
    public const MultiplayerPeer.TransferModeEnum TransferMode
        = MultiplayerPeer.TransferModeEnum.Reliable;

    public static bool IsRequestPayloadValid(
        int round,
        int requestId,
        int kind,
        Vector3 origin,
        Vector3 direction)
        => round >= 1
        && requestId >= 1
        && Enum.IsDefined(typeof(DemolitionNetworkUtilityKind), kind)
        && IsFinite(origin)
        && IsFinite(direction)
        && direction.LengthSquared() is >= 0.64f and <= 1.44f;

    public static bool IsSpawnPayloadValid(DemolitionUtilityThrowSpawn spawn)
        => spawn.SpawnId >= 1
        && spawn.Round >= 1
        && spawn.SourcePeerId >= 1
        && spawn.SourceActorId >= 100
        && Enum.IsDefined(typeof(DemolitionNetworkUtilityKind), spawn.Kind)
        && IsFinite(spawn.Origin)
        && IsFinite(spawn.Direction)
        && spawn.Direction.LengthSquared() is >= 0.64f and <= 1.44f
        && float.IsFinite(spawn.Speed)
        && spawn.Speed is >= 8.0f and <= 20.0f
        && float.IsFinite(spawn.Loft)
        && spawn.Loft is >= 2.0f and <= 9.0f;

    public static bool IsFlashbangDetonationPayloadValid(
        DemolitionFlashbangDetonation detonation)
        => detonation.SpawnId >= 1
        && detonation.Round >= 1
        && IsFinite(detonation.Position);

    public static bool HostMayAuthorize(
        bool host,
        bool sessionActive,
        bool roundActive,
        bool registered,
        bool alive,
        bool roundMatches,
        bool sourceNearActor,
        int inventoryCount)
        => host
        && sessionActive
        && roundActive
        && registered
        && alive
        && roundMatches
        && sourceNearActor
        && inventoryCount > 0;

    public static bool AppliesDamage(bool networkClient) => !networkClient;

    private static bool IsFinite(Vector3 value)
        => float.IsFinite(value.X)
        && float.IsFinite(value.Y)
        && float.IsFinite(value.Z);
}

public partial class SquadNetwork
{
    public event Action<DemolitionUtilityThrowRequest>? DemolitionUtilityThrowRequested;
    public event Action<DemolitionUtilityThrowSpawn>? DemolitionUtilityThrowSpawnReceived;
    public event Action<DemolitionFlashbangDetonation>? DemolitionFlashbangDetonationReceived;
    public event Action<int>? DemolitionUtilityThrowRejected;

    public bool RequestDemolitionUtilityThrow(
        int round,
        int requestId,
        DemolitionNetworkUtilityKind kind,
        Vector3 origin,
        Vector3 direction)
    {
        if (!IsOnline
            || !IsDemolitionSession
            || !DemolitionMatchStarted
            || !DemolitionUtilityNetworkContract.IsRequestPayloadValid(
                round,
                requestId,
                (int)kind,
                origin,
                direction))
        {
            return false;
        }
        direction = direction.Normalized();
        if (IsHost)
        {
            DemolitionUtilityThrowRequested?.Invoke(new DemolitionUtilityThrowRequest(
                1,
                round,
                requestId,
                kind,
                origin,
                direction));
            return true;
        }
        RpcId(
            1,
            MethodName.SubmitDemolitionUtilityThrow,
            round,
            requestId,
            (int)kind,
            origin,
            direction);
        return true;
    }

    public void BroadcastDemolitionUtilityThrow(DemolitionUtilityThrowSpawn spawn)
    {
        if (!IsOnline
            || !IsHost
            || !IsDemolitionSession
            || !DemolitionMatchStarted
            || !DemolitionUtilityNetworkContract.IsSpawnPayloadValid(spawn))
        {
            return;
        }
        foreach (var peerId in RegisteredDemolitionPeerIds())
        {
            RpcId(
                peerId,
                MethodName.ReceiveDemolitionUtilityThrow,
                spawn.SpawnId,
                spawn.Round,
                spawn.SourcePeerId,
                spawn.SourceActorId,
                spawn.RequestId,
                (int)spawn.Kind,
                spawn.Origin,
                spawn.Direction,
                spawn.Speed,
                spawn.Loft);
        }
    }

    public void RejectDemolitionUtilityThrow(long peerId, int requestId)
    {
        if (!IsOnline
            || !IsHost
            || !IsDemolitionSession
            || peerId <= 1
            || requestId < 1)
        {
            return;
        }
        RpcId(peerId, MethodName.ReceiveDemolitionUtilityThrowRejected, requestId);
    }

    public void BroadcastDemolitionFlashbangDetonation(
        DemolitionFlashbangDetonation detonation)
    {
        if (!IsOnline
            || !IsHost
            || !IsDemolitionSession
            || !DemolitionMatchStarted
            || !DemolitionUtilityNetworkContract.IsFlashbangDetonationPayloadValid(detonation))
        {
            return;
        }
        foreach (var peerId in RegisteredDemolitionPeerIds())
        {
            RpcId(
                peerId,
                MethodName.ReceiveDemolitionFlashbangDetonation,
                detonation.SpawnId,
                detonation.Round,
                detonation.Position);
        }
    }

    [Rpc(
        MultiplayerApi.RpcMode.AnyPeer,
        TransferMode = DemolitionUtilityNetworkContract.TransferMode)]
    private void SubmitDemolitionUtilityThrow(
        int round,
        int requestId,
        int kind,
        Vector3 origin,
        Vector3 direction)
    {
        var sender = Multiplayer.GetRemoteSenderId();
        if (!IsHost
            || !IsDemolitionSession
            || !DemolitionMatchStarted
            || !_demolitionAssignments.ContainsKey(sender)
            || !DemolitionUtilityNetworkContract.IsRequestPayloadValid(
                round,
                requestId,
                kind,
                origin,
                direction))
        {
            RejectDemolitionUtilityThrow(sender, requestId);
            return;
        }
        DemolitionUtilityThrowRequested?.Invoke(new DemolitionUtilityThrowRequest(
            sender,
            round,
            requestId,
            (DemolitionNetworkUtilityKind)kind,
            origin,
            direction.Normalized()));
    }

    [Rpc(
        MultiplayerApi.RpcMode.Authority,
        TransferMode = DemolitionUtilityNetworkContract.TransferMode)]
    private void ReceiveDemolitionUtilityThrow(
        int spawnId,
        int round,
        long sourcePeerId,
        int sourceActorId,
        int requestId,
        int kind,
        Vector3 origin,
        Vector3 direction,
        float speed,
        float loft)
    {
        var spawn = new DemolitionUtilityThrowSpawn(
            spawnId,
            round,
            sourcePeerId,
            sourceActorId,
            requestId,
            Enum.IsDefined(typeof(DemolitionNetworkUtilityKind), kind)
                ? (DemolitionNetworkUtilityKind)kind
                : (DemolitionNetworkUtilityKind)(-1),
            origin,
            direction,
            speed,
            loft);
        if (!IsDemolitionSession
            || !DemolitionMatchStarted
            || !DemolitionUtilityNetworkContract.IsSpawnPayloadValid(spawn))
        {
            return;
        }
        DemolitionUtilityThrowSpawnReceived?.Invoke(spawn);
    }

    [Rpc(
        MultiplayerApi.RpcMode.Authority,
        TransferMode = DemolitionUtilityNetworkContract.TransferMode)]
    private void ReceiveDemolitionUtilityThrowRejected(int requestId)
    {
        if (IsDemolitionSession && DemolitionMatchStarted && requestId >= 1)
        {
            DemolitionUtilityThrowRejected?.Invoke(requestId);
        }
    }

    [Rpc(
        MultiplayerApi.RpcMode.Authority,
        TransferMode = DemolitionUtilityNetworkContract.TransferMode)]
    private void ReceiveDemolitionFlashbangDetonation(
        int spawnId,
        int round,
        Vector3 position)
    {
        var detonation = new DemolitionFlashbangDetonation(spawnId, round, position);
        if (!IsDemolitionSession
            || !DemolitionMatchStarted
            || !DemolitionUtilityNetworkContract.IsFlashbangDetonationPayloadValid(detonation))
        {
            return;
        }
        DemolitionFlashbangDetonationReceived?.Invoke(detonation);
    }
}
