using Godot;

namespace OperationSteelTide;

public partial class SquadNetwork
{
    private void SendDemolitionPlayerStateToRegisteredPeers(
        long playerPeerId,
        DemolitionNetworkTeam team,
        int slot,
        OperatorRole role,
        Vector3 position,
        Vector3 rotation,
        float health,
        bool dead)
    {
        foreach (var peerId in RegisteredDemolitionPeerIds())
        {
            RpcId(peerId, MethodName.ReceiveDemolitionPlayerState,
                playerPeerId,
                (int)team,
                slot,
                (int)role,
                position,
                rotation,
                health,
                dead);
        }
    }

    private void BroadcastDemolitionAbilityToRegisteredPeers(
        long sourcePeerId,
        OperatorRole role,
        Vector3 origin,
        Vector3 forward)
    {
        foreach (var peerId in RegisteredDemolitionPeerIds())
        {
            RpcId(peerId, MethodName.ReceiveAbility,
                sourcePeerId, (int)role, origin, forward);
        }
    }

    private void BroadcastDemolitionShotToRegisteredPeers(
        long sourcePeerId,
        Vector3 origin,
        Vector3 end,
        int enemyId,
        float damage)
    {
        foreach (var peerId in RegisteredDemolitionPeerIds())
        {
            RpcId(peerId, MethodName.ReceiveShot,
                sourcePeerId, origin, end, enemyId, damage);
        }
    }
}
