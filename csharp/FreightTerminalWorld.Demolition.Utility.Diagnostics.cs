using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateDemolitionUtility()
    {
        await WaitFrames(3);
        var origin = new Vector3(0.0f, 0.0f, 0.0f);
        var objective = new Vector3(15.0f, 0.0f, 0.0f);
        var contact = new Vector3(13.0f, 0.9f, 2.0f);

        var executeSmoke = DemolitionUtilityPlanner.Plan(Context(
            DemolitionTeam.Attackers,
            DemolitionStrategyPhase.Opening,
            DemolitionDuty.Entry,
            origin,
            objective,
            contact,
            hasContact: false,
            hasFrag: false,
            hasSmoke: true,
            hasIncendiary: false));
        var retakeSmoke = DemolitionUtilityPlanner.Plan(Context(
            DemolitionTeam.Defenders,
            DemolitionStrategyPhase.PostPlant,
            DemolitionDuty.Retake,
            origin,
            objective,
            contact,
            hasContact: true,
            hasFrag: false,
            hasSmoke: true,
            hasIncendiary: false));
        var pushIncendiary = DemolitionUtilityPlanner.Plan(Context(
            DemolitionTeam.Defenders,
            DemolitionStrategyPhase.Opening,
            DemolitionDuty.AnchorA,
            origin,
            objective,
            contact,
            hasContact: true,
            hasFrag: false,
            hasSmoke: false,
            hasIncendiary: true));
        var coverFrag = DemolitionUtilityPlanner.Plan(Context(
            DemolitionTeam.Attackers,
            DemolitionStrategyPhase.Opening,
            DemolitionDuty.Flank,
            origin,
            new Vector3(3.0f, 0.0f, 0.0f),
            contact,
            hasContact: true,
            hasFrag: true,
            hasSmoke: false,
            hasIncendiary: false));
        var channelHold = DemolitionUtilityPlanner.Plan(Context(
            DemolitionTeam.Defenders,
            DemolitionStrategyPhase.PostPlant,
            DemolitionDuty.Defuse,
            origin,
            objective,
            contact,
            hasContact: true,
            hasFrag: true,
            hasSmoke: true,
            hasIncendiary: true,
            channeling: true));
        var unsafeFire = DemolitionUtilityPlanner.Plan(new DemolitionUtilityContext(
            DemolitionTeam.Defenders,
            DemolitionStrategyPhase.Opening,
            DemolitionDuty.AnchorB,
            origin,
            objective,
            contact,
            true,
            false,
            false,
            false,
            true,
            true,
            false,
            40.0f));

        var requestContract = DemolitionUtilityNetworkContract.TransferMode
                == MultiplayerPeer.TransferModeEnum.Reliable
            && DemolitionUtilityNetworkContract.IsRequestPayloadValid(
                3,
                7,
                (int)DemolitionNetworkUtilityKind.Smoke,
                origin,
                Vector3.Forward)
            && !DemolitionUtilityNetworkContract.IsRequestPayloadValid(
                3,
                7,
                99,
                origin,
                Vector3.Forward)
            && !DemolitionUtilityNetworkContract.IsRequestPayloadValid(
                3,
                7,
                (int)DemolitionNetworkUtilityKind.Smoke,
                origin,
                Vector3.Zero);
        var spawnContract = DemolitionUtilityNetworkContract.IsSpawnPayloadValid(
            new DemolitionUtilityThrowSpawn(
                4,
                3,
                2,
                101,
                7,
                DemolitionNetworkUtilityKind.Incendiary,
                origin,
                Vector3.Forward,
                14.0f,
                5.0f));
        var authorityContract = DemolitionUtilityNetworkContract.HostMayAuthorize(
                true,
                true,
                true,
                true,
                true,
                true,
                true,
                1)
            && !DemolitionUtilityNetworkContract.HostMayAuthorize(
                false,
                true,
                true,
                true,
                true,
                true,
                true,
                1)
            && DemolitionUtilityNetworkContract.AppliesDamage(networkClient: false)
            && !DemolitionUtilityNetworkContract.AppliesDamage(networkClient: true);

        var openingBudgetValid = true;
        for (var slot = 0; slot < 5; slot++)
        {
            var weapon = DemolitionBotLoadoutPlanner.BuildForSlot(800, slot);
            var inventory = DemolitionBotUtilityBudgetPlanner.Plan(1, slot, 800, weapon);
            openingBudgetValid &= inventory.FragmentationGrenades == 0
                && inventory.SmokeGrenades == 1
                && inventory.IncendiaryGrenades == 0
                && DemolitionBotUtilityBudgetPlanner.WeaponPrice(weapon) + inventory.TotalCost <= 800;
        }
        var sniperWeapon = DemolitionBotLoadoutPlanner.BuildForSlot(4300, 2);
        var sniperUtility = DemolitionBotUtilityBudgetPlanner.Plan(1, 2, 4300, sniperWeapon);
        var rifleWeapon = DemolitionBotLoadoutPlanner.BuildForSlot(4300, 1);
        var rifleUtility = DemolitionBotUtilityBudgetPlanner.Plan(1, 1, 4300, rifleWeapon);
        var budgetContract = openingBudgetValid
            && sniperWeapon?.Platform == WeaponPlatform.M24
            && sniperUtility == DemolitionBotUtilityInventory.Empty
            && rifleUtility.TotalCost > 0
            && DemolitionBotUtilityBudgetPlanner.WeaponPrice(rifleWeapon)
                + rifleUtility.TotalCost <= 4300;

        const long delayedRequestPeer = 9_900_017;
        _demolitionRemoteUtilityRequestIds.Remove(delayedRequestPeer);
        var currentUtilityRound = _demolitionMatch.CurrentRound;
        var delayedRoundRequestRejected = !TryAdvanceDemolitionUtilityRequestHighWater(
            delayedRequestPeer,
            currentUtilityRound + 1,
            900);
        if (currentUtilityRound > 1)
        {
            delayedRoundRequestRejected &= !TryAdvanceDemolitionUtilityRequestHighWater(
                delayedRequestPeer,
                currentUtilityRound - 1,
                901);
        }
        var currentRoundRequestAccepted = TryAdvanceDemolitionUtilityRequestHighWater(
            delayedRequestPeer,
            currentUtilityRound,
            1);
        var requestHighWaterRoundScoped = delayedRoundRequestRejected
            && currentRoundRequestAccepted
            && _demolitionRemoteUtilityRequestIds.TryGetValue(
                delayedRequestPeer,
                out var currentRoundHighWater)
            && currentRoundHighWater == 1;
        _demolitionRemoteUtilityRequestIds.Remove(delayedRequestPeer);

        var incendiaries = new List<IncendiaryGrenade>();
        for (var index = 0; index < 5; index++)
        {
            var grenade = new IncendiaryGrenade
            {
                Name = $"IncendiaryCapProbe_{index}",
                Position = new Vector3(index * 0.2f, 40.0f, 0.0f),
                OwnerBody = _player
            };
            AddChild(grenade);
            incendiaries.Add(grenade);
        }
        var incendiaryCap = _activeIncendiaryGrenades.Count == MaximumActiveIncendiaryGrenades
            && incendiaries[0].IsQueuedForDeletion();

        var smokes = new List<SmokeGrenade>();
        for (var index = 0; index < 5; index++)
        {
            var grenade = new SmokeGrenade
            {
                Name = $"SmokeCapProbe_{index}",
                Position = new Vector3(index * 0.2f, 42.0f, 0.0f),
                OwnerBody = _player
            };
            AddChild(grenade);
            smokes.Add(grenade);
        }
        var smokeCap = _activeSmokeGrenades.Count == MaximumActiveSmokeGrenades
            && smokes[0].IsQueuedForDeletion();

        var tickProbe = new Node { Name = "IncendiaryDamageCadenceProbe" };
        AddChild(tickProbe);
        var overlapGuard = TryAcquireIncendiaryDamageTickForDiagnostics(tickProbe)
            && !TryAcquireIncendiaryDamageTickForDiagnostics(tickProbe);
        var lowFrequency = DemolitionUtilityDecisionInterval >= 0.5f
            && DemolitionUtilityTeamCooldown >= 5.0f;
        var fireTuning = Mathf.IsEqualApprox(IncendiaryGrenade.FireDuration, 7.2f)
            && Mathf.IsEqualApprox(IncendiaryGrenade.FireRadius, 4.0f);
        var frag = new FragGrenade
        {
            Name = "FragRoundCleanupProbe",
            Position = new Vector3(0.0f, 44.0f, 0.0f),
            OwnerBody = _player,
            Main = this
        };
        AddChild(frag);
        frag.Arm(Vector3.Forward);
        var fragGrouped = frag.IsInGroup(FragGrenade.ActiveGroupName);
        ClearDemolitionUtilityProjectiles();
        var roundCleanup = fragGrouped
            && frag.IsQueuedForDeletion()
            && smokes[4].IsQueuedForDeletion()
            && incendiaries[4].IsQueuedForDeletion();
        var roundActiveBeforePostRoundProbe = _demolitionRoundActive;
        var playerHealthBeforePostRoundProbe = _player.Health;
        var damageTicksBeforePostRoundProbe = _incendiaryLastDamageTicksMsec.Count;
        var dropsBeforePostRoundProbe = DemolitionWeaponDropCountForDiagnostics;
        _demolitionRoundActive = false;
        ApplyIncendiaryDamageTick(
            _player.GlobalPosition,
            100.0f,
            500.0f,
            tickProbe,
            tickProbe);
        var postRoundDamageBlocked = Mathf.IsEqualApprox(
                _player.Health,
                playerHealthBeforePostRoundProbe)
            && _incendiaryLastDamageTicksMsec.Count == damageTicksBeforePostRoundProbe
            && DemolitionWeaponDropCountForDiagnostics == dropsBeforePostRoundProbe;
        var postRoundProjectilesCleared = _activeSmokeGrenades.Count == 0
            && _activeIncendiaryGrenades.Count == 0;
        _demolitionRoundActive = roundActiveBeforePostRoundProbe;
        var valid = executeSmoke.Kind == DemolitionAiUtilityKind.Smoke
            && retakeSmoke.Kind == DemolitionAiUtilityKind.Smoke
            && pushIncendiary.Kind == DemolitionAiUtilityKind.Incendiary
            && coverFrag.Kind == DemolitionAiUtilityKind.Fragmentation
            && channelHold.Kind == DemolitionAiUtilityKind.None
            && unsafeFire.Kind == DemolitionAiUtilityKind.None
            && incendiaryCap
            && smokeCap
            && overlapGuard
            && lowFrequency
            && fireTuning
            && requestContract
            && spawnContract
            && authorityContract
            && requestHighWaterRoundScoped
            && budgetContract
            && roundCleanup
            && postRoundDamageBlocked
            && postRoundProjectilesCleared;
        GD.Print(
            $"DEMOLITION_UTILITY_CHECK valid={valid} execute={executeSmoke.Kind} retake={retakeSmoke.Kind} "
            + $"push={pushIncendiary.Kind} cover={coverFrag.Kind} channel={channelHold.Kind} "
            + $"unsafe={unsafeFire.Kind} caps={incendiaryCap}/{smokeCap} overlap={overlapGuard} "
            + $"decision={DemolitionUtilityDecisionInterval:0.00}s cooldown={DemolitionUtilityTeamCooldown:0.0}s "
            + $"fire={IncendiaryGrenade.FireRadius:0.0}m/{IncendiaryGrenade.FireDuration:0.0}s "
            + $"network={requestContract}/{spawnContract}/{authorityContract} "
            + $"round_scoped_request={requestHighWaterRoundScoped} "
            + $"budget={budgetContract} cleanup={roundCleanup} "
            + $"post_round={postRoundProjectilesCleared}/{postRoundDamageBlocked}");
        GD.Print($"DEMOLITION_UTILITY_PASS valid={valid}");
        tickProbe.QueueFree();
        GetTree().Paused = false;
        await WaitFrames(3);
        GetTree().Quit(valid ? 0 : 2);
    }

    private static DemolitionUtilityContext Context(
        DemolitionTeam team,
        DemolitionStrategyPhase phase,
        DemolitionDuty duty,
        Vector3 actor,
        Vector3 objective,
        Vector3 contact,
        bool hasContact,
        bool hasFrag,
        bool hasSmoke,
        bool hasIncendiary,
        bool channeling = false)
        => new(
            team,
            phase,
            duty,
            actor,
            objective,
            contact,
            hasContact,
            channeling,
            hasFrag,
            hasSmoke,
            hasIncendiary,
            true,
            true,
            40.0f);
}
