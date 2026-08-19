namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private string DemolitionAttackerObjective(string score)
    {
        if (IsDemolitionNetworkClient)
        {
            return NetworkDemolitionAttackerObjective(score);
        }
        if (_demolitionDeviceLifecycle.IsGrounded)
        {
            var runner = ResolveDemolitionAttacker(_demolitionDeviceLifecycle.PickupRunnerMemberId);
            return GameLocalization.Format(
                "demolition_device_pickup_objective",
                _languageSetting,
                "{0}  //  DEVICE AT ATTACK SPAWN  //  {1} PICK IT UP",
                score,
                runner is null ? "ATTACKER" : DemolitionMemberDisplayName(runner));
        }
        var carrier = ResolveDemolitionAttacker(_demolitionDeviceLifecycle.CarrierMemberId);
        if (carrier is TacticalPlayer)
        {
            return GameLocalization.Format(
                "demolition_round_objective",
                _languageSetting,
                "{0}  //  CHOOSE SITE A OR B  //  HOLD F TO PLANT",
                score);
        }
        return GameLocalization.Format(
            "demolition_device_escort_objective",
            _languageSetting,
            "{0}  //  ESCORT {1} TO A SITE  //  PRIORITIZE THE PLANT",
            score,
            carrier is null ? "CARRIER" : DemolitionMemberDisplayName(carrier));
    }

    private string NetworkDemolitionAttackerObjective(string score)
    {
        if (_networkDevicePhase == DemolitionDevicePhase.Grounded)
        {
            return GameLocalization.Format(
                "demolition_device_pickup_objective",
                _languageSetting,
                "{0}  //  DEVICE AT ATTACK SPAWN  //  {1} PICK IT UP",
                score,
                "ATTACKER");
        }
        if (_networkDeviceCarrierActorId == LocalDemolitionActorId)
        {
            return GameLocalization.Format(
                "demolition_round_objective",
                _languageSetting,
                "{0}  //  CHOOSE SITE A OR B  //  HOLD F TO PLANT",
                score);
        }
        var carrier = DemolitionActorForId(_networkDeviceCarrierActorId);
        return GameLocalization.Format(
            "demolition_device_escort_objective",
            _languageSetting,
            "{0}  //  ESCORT {1} TO A SITE  //  PRIORITIZE THE PLANT",
            score,
            carrier is null ? "CARRIER" : DemolitionMemberDisplayName(carrier));
    }
}
