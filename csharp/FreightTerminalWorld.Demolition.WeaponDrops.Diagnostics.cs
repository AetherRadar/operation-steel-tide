using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateDemolitionWeapons()
    {
        await WaitFrames(3);

        var opening = DemolitionBotLoadoutPlanner.Plan(
            DemolitionEconomy.StartingFunds,
            DemolitionSquadSize);
        var rifleRound = DemolitionBotLoadoutPlanner.Plan(3600, DemolitionSquadSize);
        var sniperRound = DemolitionBotLoadoutPlanner.Plan(
            DemolitionBotLoadoutPlanner.SniperFundsThreshold,
            DemolitionSquadSize);
        var sniperRoundPacked = DemolitionBotLoadoutNetworkCodec.Encode(sniperRound);
        var zeroAllocationCodecReady = sniperRoundPacked
            == DemolitionBotLoadoutNetworkCodec.EncodePlatforms(
                sniperRound[0]?.Platform,
                sniperRound[1]?.Platform,
                sniperRound[2]?.Platform,
                sniperRound[3]?.Platform,
                sniperRound[4]?.Platform);
        var postBuyFunds = DemolitionBotLoadoutPlanner.SniperFundsThreshold
            - DemolitionBuyCatalog.Primary(DemolitionBuyCatalog.M24Id)!.Price;
        var postBuyReplan = DemolitionBotLoadoutPlanner.Plan(
            postBuyFunds,
            DemolitionSquadSize);
        var authoritativePostBuyPlanReady = postBuyFunds == 0
            && postBuyReplan.All(build => build is null)
            && zeroAllocationCodecReady
            && DemolitionBotLoadoutNetworkCodec.IsValid(sniperRoundPacked)
            && DemolitionBotLoadoutNetworkCodec.Decode(sniperRoundPacked)
                .Count(build => build?.Platform == WeaponPlatform.M24) == 1
            && DemolitionBotLoadoutNetworkCodec.WeaponForSlot(sniperRoundPacked, 2)?.Platform
                == WeaponPlatform.M24;
        var firstDropId = AllocateDemolitionWeaponDropId();
        var replacementDropId = AllocateDemolitionWeaponDropId();
        var uniqueDropIdsReady = firstDropId != replacementDropId
            && replacementDropId == firstDropId + 1;
        var economyReady = opening.Count == DemolitionSquadSize
            && opening.All(build => build?.Platform == WeaponPlatform.P226)
            && rifleRound.All(build => build?.Platform == WeaponPlatform.ScarL)
            && sniperRound.Count(build => build?.Platform == WeaponPlatform.M24) == 1
            && sniperRound.Count(build => build?.Platform == WeaponPlatform.ScarL)
                == DemolitionSquadSize - 1
            && DemolitionBuyCatalog.Primary(DemolitionBuyCatalog.M24Id) is
            {
                Platform: WeaponPlatform.M24,
                Price: DemolitionBotLoadoutPlanner.SniperFundsThreshold,
                ReserveAmmo: 25
            };

        var remotePurchase = new EnemyOperator();
        remotePurchase.Loot.Add(new LootItem
        {
            Kind = LootItemKind.Weapon,
            Weapon = WeaponCatalog.Build(WeaponPlatform.P226, 0),
            Grade = LootGrade.Common
        });
        ApplyRemoteDemolitionCarriedWeapon(
            remotePurchase,
            WeaponCatalog.Build(WeaponPlatform.M24, 0));
        var remotePurchaseLootReady = remotePurchase.CarriedWeapon.Platform == WeaponPlatform.M24
            && remotePurchase.Loot.Count(item => item.Kind == LootItemKind.Weapon) == 1
            && remotePurchase.Loot.Single(item => item.Kind == LootItemKind.Weapon)
                .Weapon?.Platform == WeaponPlatform.M24;
        remotePurchase.Free();

        var fallbackQuote = DemolitionBuyCatalog.Quote(
            DemolitionPurchaseSelection.Empty,
            DemolitionEconomy.StartingFunds);
        var fallbackLoadout = DemolitionBuyCatalog.BuildLoadout(fallbackQuote);
        var fallbackSlots = CreateRemoteDemolitionWeaponSlots(fallbackQuote);
        var fallbackUtility = DemolitionRemoteUtilityInventoryForQuote(fallbackQuote);
        const long fallbackPeerId = 9_998;
        _demolitionRemotePurchasedWeapons[fallbackPeerId] = fallbackSlots;
        _demolitionRemoteUtilityInventories[fallbackPeerId] = fallbackUtility;
        var fallbackInventoryReady = fallbackQuote.Affordable
            && fallbackQuote.TotalCost == 0
            && fallbackQuote.RemainingFunds == DemolitionEconomy.StartingFunds
            && fallbackLoadout.Weapon is null
            && fallbackLoadout.Sidearm is null
            && fallbackSlots.Primary is null
            && fallbackSlots.Secondary is null
            && fallbackSlots.Sidearm is null
            && fallbackSlots.Carried is null
            && _demolitionRemotePurchasedWeapons.ContainsKey(fallbackPeerId)
            && _demolitionRemoteUtilityInventories[fallbackPeerId]
                == DemolitionBotUtilityInventory.Empty;
        _demolitionRemotePurchasedWeapons.Remove(fallbackPeerId);
        _demolitionRemoteUtilityInventories.Remove(fallbackPeerId);

        var fallbackActor = new EnemyOperator();
        fallbackActor.GrantFireablePrimaryForDiagnostics(
            WeaponCatalog.Build(WeaponPlatform.M24, 0));
        fallbackActor.Loot.Add(new LootItem
        {
            Kind = LootItemKind.Weapon,
            Weapon = WeaponCatalog.Build(WeaponPlatform.M24, 0),
            Grade = LootGrade.Common
        });
        ApplyRemoteDemolitionCarriedWeapon(fallbackActor, weapon: null);
        var fallbackActorReady = !fallbackActor.HasFireablePrimary
            && fallbackActor.Loot.All(item => item.Kind != LootItemKind.Weapon)
            && DetachDemolitionWeaponLoot(fallbackActor) is null;
        fallbackActor.Free();

        var corpse = new EnemyOperator();
        corpse.GrantFireablePrimaryForDiagnostics(
            WeaponCatalog.Build(WeaponPlatform.M24, 0));
        corpse.Loot.Add(new LootItem
        {
            Kind = LootItemKind.Weapon,
            Weapon = WeaponCatalog.Build(WeaponPlatform.P226, 0),
            Grade = LootGrade.Common
        });
        corpse.Loot.Add(new LootItem
        {
            Kind = LootItemKind.Ammunition,
            AmmoCaliber = AmmoCaliber.Sniper,
            Quantity = 15,
            Grade = LootGrade.Common
        });
        var detached = DetachDemolitionWeaponLoot(corpse);
        var corpseSplitReady = detached?.Weapon?.Platform == WeaponPlatform.M24
            && corpse.Loot.All(item => item.Kind != LootItemKind.Weapon)
            && corpse.Loot.Any(item => item.Kind == LootItemKind.Ammunition)
            && !corpse.HasFireablePrimary;
        corpse.Free();

        var pickup = new DroppedWeaponPickup
        {
            Name = "DemolitionWeaponDropDiagnostic",
            Position = new Vector3(240.0f, 60.0f, 240.0f)
        };
        pickup.Configure(detached!);
        pickup.ConfigureNetworkIdentity(
            Mathf.Max(1, _demolitionMatch.CurrentRound),
            dropId: 998,
            revision: 0);
        AddChild(pickup);
        await WaitFrames(2);
        var authoredDropReady = pickup.IsSearchable
            && pickup.PlatformForDiagnostics == WeaponPlatform.M24
            && pickup.UsesAuthoredWeaponVisualForDiagnostics
            && pickup.GetPhysicsProcessDeltaTime() >= 0.0
            && !pickup.IsPhysicsProcessing()
            && pickup.CollisionLayer == 0
            && pickup.CollisionMask == 0
            && !pickup.HasBlockingCollisionForDiagnostics
            && pickup.FindChildren("*", "OmniLight3D", recursive: true, owned: false).Count == 0;

        _player.ApplyColdStartUnarmed();
        _player.EquipFromLootToWeaponSlot(new LootItem
        {
            Kind = LootItemKind.Weapon,
            Weapon = WeaponCatalog.Build(WeaponPlatform.ScarL, 0),
            Grade = LootGrade.Common
        }, PlayerWeaponSlot.Primary);
        _player.EquipFromLootToWeaponSlot(new LootItem
        {
            Kind = LootItemKind.Weapon,
            Weapon = WeaponCatalog.Build(WeaponPlatform.ScarL, 0),
            Grade = LootGrade.Common
        }, PlayerWeaponSlot.Secondary);
        var equipped = TryPlayerEquipWeaponFromLootSource(pickup);
        var pickupTransferReady = equipped
            && _player.HasFireablePrimary
            && _player.EquippedWeapon.Platform == WeaponPlatform.M24
            && pickup.Loot.Count == 1
            && pickup.PlatformForDiagnostics == WeaponPlatform.ScarL
            && !pickup.IsQueuedForDeletion();
        var authoritativeRevisionReady = DemolitionWeaponDropNetworkRules.MatchesCurrentRevision(
                pickup,
                pickup.DemolitionRound,
                pickup.DropId,
                pickup.Revision)
            && !DemolitionWeaponDropNetworkRules.MatchesCurrentRevision(
                pickup,
                pickup.DemolitionRound,
                pickup.DropId,
                pickup.Revision - 1)
            && DemolitionWeaponDropNetworkRules.IsStatePayloadValid(
                CaptureDemolitionWeaponDropState(pickup));

        var friendly = _squadMates.FirstOrDefault(mate => IsInstanceValid(mate)
            && !mate.IsHumanProxy);
        var friendlyPistolVisualReady = false;
        var friendlySniperVisualReady = false;
        var singleShotReady = false;
        if (friendly is not null)
        {
            friendly.ConfigureDemolitionRoundLoadout(
                WeaponCatalog.Build(WeaponPlatform.P226, 0));
            friendlyPistolVisualReady = friendly.CarriedWeapon.Platform == WeaponPlatform.P226
                && friendly.AuthoredCarriedWeaponMatchesForDiagnostics;
            friendly.ConfigureDemolitionRoundLoadout(
                WeaponCatalog.Build(WeaponPlatform.M24, 0));
            friendlySniperVisualReady = friendly.CarriedWeapon.Platform == WeaponPlatform.M24
                && friendly.AuthoredCarriedWeaponMatchesForDiagnostics;
            singleShotReady = friendly.UsesSingleShotCadenceForDiagnostics
                && friendly.MinimumFireCooldownForDiagnostics
                    >= WeaponCatalog.Build(WeaponPlatform.M24, 0).Stats().FireInterval;
        }
        var friendlyDetached = friendly is null
            ? null
            : DetachDemolitionWeaponLoot(friendly);
        var friendlyDropReady = friendlyDetached?.Weapon?.Platform == WeaponPlatform.M24
            && friendly is not null
            && !friendly.HasFireablePrimary;

        var staleProxy = new EnemyOperator();
        staleProxy.GrantFireablePrimaryForDiagnostics(
            WeaponCatalog.Build(WeaponPlatform.P226, 0));
        var staleProxyReady = staleProxy.CarriedWeapon.Platform == WeaponPlatform.P226;
        var loadoutChanged = ApplyDemolitionAuthoritativeWeaponLoadouts(
            _demolitionMatch.CurrentRound,
            DemolitionBotLoadoutNetworkCodec.Encode(opening),
            sniperRoundPacked);
        var authoritativeProxyWeapon = DemolitionOpponentRoundWeaponForSlot(2);
        var existingProxyRefreshReady = staleProxyReady
            && loadoutChanged
            && authoritativeProxyWeapon?.Platform == WeaponPlatform.M24
            && !DemolitionActorWeaponMatches(staleProxy, authoritativeProxyWeapon);
        staleProxy.Free();
        var synchronizedSlotsReady = DemolitionOpponentPlatformsForDiagnostics.Count
                == DemolitionSquadSize
            && DemolitionOpponentPlatformsForDiagnostics.Count(
                platform => platform == WeaponPlatform.M24) == 1
            && DemolitionOpponentPlatformsForDiagnostics[2] == WeaponPlatform.M24
            && DemolitionOpponentRoundWeaponForSlot(1)?.Platform == WeaponPlatform.ScarL
            && DemolitionOpponentRoundWeaponForSlot(2)?.Platform == WeaponPlatform.M24;

        var pistolQuote = DemolitionBuyCatalog.Quote(
            new DemolitionPurchaseSelection(
                DemolitionBuyCatalog.P226Id,
                string.Empty,
                false,
                0,
                0,
                0),
            DemolitionEconomy.StartingFunds);
        _player.ApplyDemolitionRoundLoadout(
            DemolitionBuyCatalog.BuildLoadout(pistolQuote),
            0,
            0,
            0);
        var activePistolSnapshot = DemolitionWeaponPlatformForActor(_player);
        var selectedMelee = _player.SelectQuickSlot(PlayerQuickSlot.Melee, notify: false);
        var stowedPistolSnapshot = DemolitionWeaponPlatformForActor(_player);
        var hostPistolSnapshotReady = !_player.HasFireablePrimary
            && selectedMelee
            && !_player.HasActiveFirearm
            && activePistolSnapshot == WeaponPlatform.P226
            && stowedPistolSnapshot == WeaponPlatform.P226
            && DemolitionBotLoadoutNetworkCodec.WeaponForSlot(
                    DemolitionBotLoadoutNetworkCodec.EncodePlatforms(
                        stowedPistolSnapshot,
                        null,
                        null,
                        null,
                        null),
                    0)?.Platform == WeaponPlatform.P226;
        _player.SelectQuickSlot(PlayerQuickSlot.Sidearm, notify: false);
        var playerDetached = DetachDemolitionWeaponLoot(_player);
        var playerDropReady = playerDetached?.Weapon?.Platform == WeaponPlatform.P226
            && !_player.HasSidearmWeapon;
        const int duplicateActorId = 8_888;
        var actorDropDedupReady = _demolitionWeaponDropActorIds.Add(duplicateActorId)
            && !_demolitionWeaponDropActorIds.Add(duplicateActorId);
        _demolitionWeaponDropActorIds.Remove(duplicateActorId);
        var downedPlayerDoesNotDropReady = !TryResolveDemolitionWeaponDropActorId(
            _player,
            out _);
        var symmetricDropsReady = corpseSplitReady
            && friendlyDropReady
            && playerDropReady;

        _demolitionWeaponDrops.Add(pickup);
        _demolitionWeaponDropsById[pickup.DropId] = pickup;
        _lootSources.Add(pickup);
        ClearDemolitionWeaponDrops();
        var roundDropClearReady = _demolitionWeaponDrops.Count == 0
            && _demolitionWeaponDropsById.Count == 0
            && !_lootSources.Contains(pickup)
            && pickup.IsQueuedForDeletion();

        var demolitionBodies = _demolitionOpponents
            .Where(IsInstanceValid)
            .ToArray();
        foreach (var body in demolitionBodies)
        {
            if (!_lootSources.Contains(body))
            {
                _lootSources.Add(body);
            }
        }
        ClearDemolitionOpponents();
        var corpseSourceClearReady = demolitionBodies.All(body => !_lootSources.Contains(body));

        var valid = economyReady
            && authoritativePostBuyPlanReady
            && remotePurchaseLootReady
            && fallbackInventoryReady
            && fallbackActorReady
            && corpseSplitReady
            && authoredDropReady
            && pickupTransferReady
            && uniqueDropIdsReady
            && authoritativeRevisionReady
            && friendlyPistolVisualReady
            && friendlySniperVisualReady
            && friendlyDropReady
            && singleShotReady
            && synchronizedSlotsReady
            && existingProxyRefreshReady
            && hostPistolSnapshotReady
            && playerDropReady
            && actorDropDedupReady
            && downedPlayerDoesNotDropReady
            && symmetricDropsReady
            && roundDropClearReady
            && corpseSourceClearReady;
        GD.Print(
            $"DEMOLITION_WEAPONS_CHECK valid={valid} economy={economyReady} "
            + $"opening={string.Join(',', opening.Select(build => build?.Platform))} "
            + $"snipers={sniperRound.Count(build => build?.Platform == WeaponPlatform.M24)} "
            + $"postbuy_plan={authoritativePostBuyPlanReady} proxy_refresh={existingProxyRefreshReady} "
            + $"remote_purchase_loot={remotePurchaseLootReady} "
            + $"fallback={fallbackInventoryReady}/{fallbackActorReady} "
            + $"corpse_split={corpseSplitReady} authored_drop={authoredDropReady} "
            + $"transfer={pickupTransferReady} revision={authoritativeRevisionReady} "
            + $"unique_ids={uniqueDropIdsReady}:{firstDropId}/{replacementDropId} "
            + $"friendly_p226={friendlyPistolVisualReady} friendly_m24={friendlySniperVisualReady} "
            + $"symmetric_drops={symmetricDropsReady} dedup={actorDropDedupReady} "
            + $"downed_guard={downedPlayerDoesNotDropReady} "
            + $"single_shot={singleShotReady} synced_slots={synchronizedSlotsReady} "
            + $"host_p226_snapshot={hostPistolSnapshotReady} "
            + $"round_clear={roundDropClearReady} corpse_sources={corpseSourceClearReady} "
            + $"equipped={_player.EquippedWeapon.Platform}");
        GD.Print($"DEMOLITION_WEAPONS_PASS valid={valid}");
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void CaptureDemolitionWeaponDrop()
    {
        var viewport = new SubViewport
        {
            Name = "DemolitionWeaponDropCaptureViewport",
            Size = new Vector2I(1280, 900),
            OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Msaa3D = Viewport.Msaa.Msaa4X
        };
        AddChild(viewport);
        var stage = new Node3D { Name = "DemolitionWeaponDropCaptureStage" };
        viewport.AddChild(stage);
        stage.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.025f, 0.035f, 0.05f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.68f, 0.76f, 0.88f),
                AmbientLightEnergy = 1.15f
            }
        });
        stage.AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-48.0f, -34.0f, 0.0f),
            LightColor = new Color(1.0f, 0.88f, 0.7f),
            LightEnergy = 1.7f,
            ShadowEnabled = true
        });
        var floorMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.075f, 0.095f, 0.12f),
            Metallic = 0.15f,
            Roughness = 0.78f
        };
        stage.AddChild(new MeshInstance3D
        {
            Name = "DiagnosticFloor",
            Mesh = new PlaneMesh
            {
                Size = new Vector2(4.6f, 3.4f),
                Material = floorMaterial
            }
        });
        var camera = new Camera3D
        {
            Fov = 34.0f,
            Current = true
        };
        stage.AddChild(camera);
        camera.LookAtFromPosition(
            new Vector3(2.55f, 1.45f, 3.1f),
            new Vector3(0.0f, 0.3f, 0.0f));

        var pickup = new DroppedWeaponPickup
        {
            Name = "DemolitionM24DropCapture",
            Position = Vector3.Zero
        };
        pickup.Configure(new LootItem
        {
            Kind = LootItemKind.Weapon,
            Weapon = WeaponCatalog.Build(WeaponPlatform.M24, 0),
            Grade = LootGrade.Common
        });
        stage.AddChild(pickup);
        await WaitFrames(6);

        const string screenshotPath = "res://demolition_weapon_drop_validation.png";
        var image = viewport.GetTexture().GetImage();
        var absolutePath = ProjectSettings.GlobalizePath(screenshotPath);
        var saved = !image.IsEmpty()
            && image.SavePng(absolutePath) == Error.Ok
            && System.IO.File.Exists(absolutePath)
            && new System.IO.FileInfo(absolutePath).Length >= 10_000
            && pickup.UsesAuthoredWeaponVisualForDiagnostics;
        GD.Print(
            $"DEMOLITION_WEAPON_DROP_CAPTURE valid={saved} path={absolutePath} "
            + $"platform={pickup.PlatformForDiagnostics} authored={pickup.UsesAuthoredWeaponVisualForDiagnostics}");
        viewport.QueueFree();
        GetTree().Quit(saved ? 0 : 2);
    }
}
