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
        var safeFlash = DemolitionUtilityPlanner.Plan(Context(
            DemolitionTeam.Attackers,
            DemolitionStrategyPhase.Opening,
            DemolitionDuty.Entry,
            origin,
            objective,
            contact,
            hasContact: true,
            hasFrag: false,
            hasSmoke: false,
            hasIncendiary: false,
            hasFlashbang: true));
        var unsafeFlash = DemolitionUtilityPlanner.Plan(Context(
            DemolitionTeam.Attackers,
            DemolitionStrategyPhase.Opening,
            DemolitionDuty.Entry,
            origin,
            objective,
            contact,
            hasContact: true,
            hasFrag: false,
            hasSmoke: false,
            hasIncendiary: false,
            hasFlashbang: true,
            flashbangFriendlySafe: false));
        var flashSource = new Vector3(0.0f, 0.0f, -4.0f);
        var flashFacing = FlashbangExposureResolver.Resolve(
            flashSource,
            Vector3.Zero,
            Vector3.Forward,
            hasLineOfSight: true);
        var flashAway = FlashbangExposureResolver.Resolve(
            flashSource,
            Vector3.Zero,
            Vector3.Back,
            hasLineOfSight: true);
        var flashBlocked = FlashbangExposureResolver.Resolve(
            flashSource,
            Vector3.Zero,
            Vector3.Forward,
            hasLineOfSight: false);
        var flashOutOfRange = FlashbangExposureResolver.Resolve(
            Vector3.Forward * (FlashbangExposureResolver.MaximumRadius + 1.0f),
            Vector3.Zero,
            Vector3.Forward,
            hasLineOfSight: true);
        var flashResolverReady = flashFacing.Intensity >= 0.99f
            && flashFacing.DurationSeconds >= 5.4f
            && flashFacing.DurationSeconds > flashAway.DurationSeconds
            && flashFacing.Intensity > flashAway.Intensity
            && flashAway.Intensity >= 0.70f
            && flashAway.DurationSeconds >= 4.0f
            && flashFacing.FacingDot >= 0.99f
            && flashAway.FacingDot <= -0.99f
            && FlashbangExposureResolver.FullEffectRadius >= 4.0f
            && FlashbangExposureResolver.MaximumDuration >= 5.4f
            && FlashbangOverlayView.ResolveScreenAlpha(0.82f) >= 0.99f
            && Mathf.IsZeroApprox(flashBlocked.Intensity)
            && Mathf.IsZeroApprox(flashBlocked.DurationSeconds)
            && Mathf.IsZeroApprox(flashOutOfRange.Intensity);
        var flashThrowerSafetySource = Vector3.Forward * 9.0f;
        var flashThrowerSafetyReady = !IsPredictedFlashbangExposureSafe(
                Vector3.Zero,
                Vector3.Forward,
                flashThrowerSafetySource)
            && IsPredictedFlashbangExposureSafe(
                Vector3.Zero,
                Vector3.Back,
                flashThrowerSafetySource);

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
        var flashNetworkContract = DemolitionUtilityNetworkContract.IsRequestPayloadValid(
                3,
                8,
                (int)DemolitionNetworkUtilityKind.Flashbang,
                origin,
                Vector3.Forward)
            && DemolitionUtilityNetworkContract.IsSpawnPayloadValid(
                new DemolitionUtilityThrowSpawn(
                    4,
                    3,
                    2,
                    102,
                    8,
                    DemolitionNetworkUtilityKind.Flashbang,
                    origin,
                    Vector3.Forward,
                    14.0f,
                    5.0f));
        var flashDetonationContract = DemolitionUtilityNetworkContract
                .IsFlashbangDetonationPayloadValid(
                    new DemolitionFlashbangDetonation(102, 3, new Vector3(1.0f, 2.0f, 3.0f)))
            && !DemolitionUtilityNetworkContract.IsFlashbangDetonationPayloadValid(
                new DemolitionFlashbangDetonation(0, 3, Vector3.Zero))
            && !DemolitionUtilityNetworkContract.IsFlashbangDetonationPayloadValid(
                new DemolitionFlashbangDetonation(102, 0, Vector3.Zero))
            && !DemolitionUtilityNetworkContract.IsFlashbangDetonationPayloadValid(
                new DemolitionFlashbangDetonation(
                    102,
                    3,
                    new Vector3(float.NaN, 0.0f, 0.0f)));
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

        var flashbangs = new List<FlashbangGrenade>();
        for (var index = 0; index < 7; index++)
        {
            var grenade = new FlashbangGrenade
            {
                Name = $"FlashbangCapProbe_{index}",
                Position = new Vector3(index * 0.2f, 46.0f, 0.0f),
                OwnerBody = _player
            };
            AddChild(grenade);
            flashbangs.Add(grenade);
        }
        var flashbangCap = ActiveFlashbangCountForDiagnostics == MaximumActiveFlashbangGrenades
            && flashbangs[0].IsQueuedForDeletion();

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
            && incendiaries[4].IsQueuedForDeletion()
            && flashbangs[6].IsQueuedForDeletion();
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
            && _activeIncendiaryGrenades.Count == 0
            && ActiveFlashbangCountForDiagnostics == 0;
        _demolitionRoundActive = roundActiveBeforePostRoundProbe;
        _hud.ClearFlashbangExposure();
        if (!_player.IsInGroup(FlashbangGrenade.TargetGroupName))
        {
            _player.AddToGroup(FlashbangGrenade.TargetGroupName);
        }
        // Keep the end-to-end LOS probe well above every authored arena volume.
        // Several maps have tall center structures whose collision can otherwise
        // make this intentionally unobstructed ray depend on the selected arena.
        _player.GlobalPosition = new Vector3(0.0f, 400.0f, 0.0f);
        _player.Velocity = Vector3.Zero;
        var replicatedFlashPosition = _player.FlashbangViewOrigin
            + _player.FlashbangViewForward.Normalized() * 4.0f;
        var replicatedFlash = new FlashbangGrenade
        {
            Name = "AuthoritativeFlashbangDetonationProbe",
            Position = replicatedFlashPosition,
            OwnerBody = this
        };
        replicatedFlash.ConfigureNetworkReplication(
            9102,
            Mathf.Max(1, _demolitionMatch.CurrentRound),
            waitForAuthoritativeDetonation: true);
        AddChild(replicatedFlash);
        replicatedFlash.Arm(Vector3.Forward, speed: 0.0f, loft: 0.0f);
        replicatedFlash._PhysicsProcess(2.0);
        var flashWaitsForHost = replicatedFlash.WaitsForAuthoritativeDetonation
            && !replicatedFlash.HasDetonated;
        replicatedFlash.ApplyAuthoritativeDetonation(replicatedFlashPosition);
        var flashPlayerGrouped = _player.IsInGroup(FlashbangGrenade.TargetGroupName);
        var flashTargetAlpha = _hud.FlashbangOverlayAlphaForDiagnostics;
        var flashTargetApplied = flashWaitsForHost
            && replicatedFlash.HasDetonated
            && replicatedFlash.GlobalPosition.DistanceTo(replicatedFlashPosition) <= 0.001f
            && flashPlayerGrouped
            && replicatedFlash.AppliedTargetCountForDiagnostics >= 1
            && _hud.IsFlashbangOverlayVisible
            && flashTargetAlpha >= 0.99f;
        _hud.ClearFlashbangExposure();
        replicatedFlash.QueueFree();
        var valid = executeSmoke.Kind == DemolitionAiUtilityKind.Smoke
            && retakeSmoke.Kind == DemolitionAiUtilityKind.Smoke
            && pushIncendiary.Kind == DemolitionAiUtilityKind.Incendiary
            && coverFrag.Kind == DemolitionAiUtilityKind.Fragmentation
            && channelHold.Kind == DemolitionAiUtilityKind.None
            && unsafeFire.Kind == DemolitionAiUtilityKind.None
            && safeFlash.Kind == DemolitionAiUtilityKind.Flashbang
            && unsafeFlash.Kind == DemolitionAiUtilityKind.None
            && flashResolverReady
            && flashThrowerSafetyReady
            && incendiaryCap
            && smokeCap
            && flashbangCap
            && overlapGuard
            && lowFrequency
            && fireTuning
            && requestContract
            && spawnContract
            && flashNetworkContract
            && flashDetonationContract
            && flashWaitsForHost
            && flashTargetApplied
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
            + $"post_round={postRoundProjectilesCleared}/{postRoundDamageBlocked} "
            + $"flash_plan={safeFlash.Kind}/{unsafeFlash.Kind} "
            + $"flash_resolver={flashResolverReady}:{flashFacing.Intensity:0.00}/{flashFacing.DurationSeconds:0.00}s/{flashAway.Intensity:0.00}/{flashAway.DurationSeconds:0.00}s/{flashBlocked.Intensity:0.00} "
            + $"flash_thrower_safe={flashThrowerSafetyReady} "
            + $"flash_network={flashNetworkContract}/{flashDetonationContract}/{flashWaitsForHost} "
            + $"flash_target={flashTargetApplied}:{flashPlayerGrouped}/{replicatedFlash.EligibleTargetCountForDiagnostics}/{replicatedFlash.AppliedTargetCountForDiagnostics}/{flashTargetAlpha:0.00} "
            + $"flash_cap={flashbangCap}:{MaximumActiveFlashbangGrenades} "
            + $"flash_cleanup={roundCleanup && postRoundProjectilesCleared}");
        GD.Print($"DEMOLITION_UTILITY_PASS valid={valid} flash_resolver={flashResolverReady} flash_thrower_safe={flashThrowerSafetyReady} flash_plan={safeFlash.Kind}/{unsafeFlash.Kind} flash_network={flashNetworkContract}/{flashDetonationContract}/{flashWaitsForHost} flash_target={flashTargetApplied} flash_cap={flashbangCap} flash_cleanup={roundCleanup && postRoundProjectilesCleared}");
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
        bool channeling = false,
        bool hasFlashbang = false,
        bool flashbangFriendlySafe = true)
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
            40.0f,
            hasFlashbang,
            flashbangFriendlySafe);
}
