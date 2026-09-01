using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateDemolitionBuy()
    {
        await WaitFrames(3);
        var originalLanguage = _hud.CurrentLanguage;
        var openingPrimary = DemolitionBuyCatalog.Quote(
            new DemolitionPurchaseSelection(string.Empty, DemolitionBuyCatalog.M4A1Id, false, 0, 0),
            DemolitionEconomy.StartingFunds);
        var pistolQuote = DemolitionBuyCatalog.Quote(
            new DemolitionPurchaseSelection(DemolitionBuyCatalog.P226Id, string.Empty, false, 0, 0),
            DemolitionEconomy.StartingFunds);
        var combinedQuote = DemolitionBuyCatalog.Quote(
            new DemolitionPurchaseSelection(DemolitionBuyCatalog.P226Id, string.Empty, false, 1, 0),
            DemolitionEconomy.StartingFunds);
        var smokeQuote = DemolitionBuyCatalog.Quote(
            new DemolitionPurchaseSelection(DemolitionBuyCatalog.P226Id, string.Empty, false, 0, 1),
            DemolitionEconomy.StartingFunds);
        var gsh18Quote = DemolitionBuyCatalog.Quote(
            new DemolitionPurchaseSelection(DemolitionBuyCatalog.Gsh18Id, string.Empty, false, 0, 0),
            DemolitionEconomy.StartingFunds);
        var m24Quote = DemolitionBuyCatalog.Quote(
            new DemolitionPurchaseSelection(
                string.Empty,
                DemolitionBuyCatalog.M24Id,
                false,
                0,
                0,
                0),
            4300);
        var incendiaryQuote = DemolitionBuyCatalog.Quote(
            new DemolitionPurchaseSelection(string.Empty, string.Empty, false, 0, 0, 1),
            DemolitionBuyCatalog.IncendiaryGrenadePrice);
        var flashbangQuote = DemolitionBuyCatalog.Quote(
            new DemolitionPurchaseSelection(string.Empty, string.Empty, false, 0, 0, 0, 1),
            DemolitionBuyCatalog.FlashbangGrenadePrice);
        var flashbangClampQuote = DemolitionBuyCatalog.Quote(
            new DemolitionPurchaseSelection(string.Empty, string.Empty, false, 0, 0, 0, 99),
            DemolitionBuyCatalog.MaximumFlashbangGrenades
                * DemolitionBuyCatalog.FlashbangGrenadePrice);
        var legacyPurchasePayload = SquadNetwork.DecodeLegacyDemolitionPurchase(
            DemolitionBuyCatalog.P226Id,
            string.Empty,
            false,
            0,
            0,
            1);
        var purchasePayloadV2 = SquadNetwork.DecodeDemolitionPurchaseV2(
            DemolitionBuyCatalog.P226Id,
            string.Empty,
            false,
            0,
            0,
            1,
            1);
        var pistolLoadout = DemolitionBuyCatalog.BuildLoadout(pistolQuote);
        var gsh18Loadout = DemolitionBuyCatalog.BuildLoadout(gsh18Quote);
        var m24Loadout = DemolitionBuyCatalog.BuildLoadout(m24Quote);
        var domainReady = !openingPrimary.Affordable
            && pistolQuote.Affordable
            && pistolQuote.TotalCost == 500
            && pistolQuote.RemainingFunds == 300
            && !combinedQuote.Affordable
            && smokeQuote.Affordable
            && smokeQuote.TotalCost == DemolitionEconomy.StartingFunds
            && smokeQuote.Selection.SmokeGrenadeCount == 1
            && pistolLoadout.Weapon is null
            && pistolLoadout.Sidearm?.Platform == WeaponPlatform.P226
            && pistolLoadout.BodyArmorId == "armor_none"
            && gsh18Quote.Affordable
            && gsh18Quote.TotalCost == 650
            && gsh18Quote.RemainingFunds == 150
            && gsh18Loadout.Sidearm?.Platform == WeaponPlatform.GSh18
            && gsh18Loadout.SidearmReserveAmmo == 54
            && m24Quote.Affordable
            && m24Quote.TotalCost == 4300
            && m24Loadout.Weapon?.Platform == WeaponPlatform.M24
            && m24Loadout.ReserveAmmo == 25
            && incendiaryQuote.Affordable
            && incendiaryQuote.Selection.IncendiaryGrenadeCount == 1;
        var flashBuyReady = flashbangQuote.Affordable
            && flashbangQuote.TotalCost == DemolitionBuyCatalog.FlashbangGrenadePrice
            && flashbangQuote.RemainingFunds == 0
            && flashbangQuote.Selection.FlashbangGrenadeCount == 1
            && flashbangClampQuote.Affordable
            && flashbangClampQuote.Selection.FlashbangGrenadeCount
                == DemolitionBuyCatalog.MaximumFlashbangGrenades
            && flashbangClampQuote.TotalCost
                == DemolitionBuyCatalog.MaximumFlashbangGrenades
                    * DemolitionBuyCatalog.FlashbangGrenadePrice;
        var flashPurchaseRpcReady = legacyPurchasePayload.FlashbangGrenadeCount == 0
            && legacyPurchasePayload.IncendiaryGrenadeCount == 1
            && !SquadNetwork.UsesDemolitionPurchaseV2(legacyPurchasePayload)
            && purchasePayloadV2.FlashbangGrenadeCount == 1
            && purchasePayloadV2.IncendiaryGrenadeCount == 1
            && SquadNetwork.UsesDemolitionPurchaseV2(purchasePayloadV2);

        const string scenePath = "res://ui/DemolitionBuyView.tscn";
        var packedScene = HudPackedSceneCache.Load(scenePath);
        var probe = packedScene?.Instantiate<DemolitionBuyView>();
        var purchaseRequests = 0;
        var purchaseRequestsWithFlash = 0;
        var requestedSidearm = string.Empty;
        var requestedPrimary = string.Empty;
        var requestedArmor = true;
        var requestedGrenades = -1;
        var requestedSmokeGrenades = -1;
        var requestedIncendiaryGrenades = -1;
        var requestedFlashbangGrenades = -1;
        if (probe is not null)
        {
            probe.Visible = false;
            _hud.AddChild(probe);
            probe.PurchaseRequested += (
                sidearmId,
                primaryId,
                armorSelected,
                grenadeCount,
                smokeGrenadeCount,
                incendiaryGrenadeCount) =>
            {
                purchaseRequests++;
                requestedSidearm = sidearmId;
                requestedPrimary = primaryId;
                requestedArmor = armorSelected;
                requestedGrenades = grenadeCount;
                requestedSmokeGrenades = smokeGrenadeCount;
                requestedIncendiaryGrenades = incendiaryGrenadeCount;
            };
            probe.PurchaseRequestedWithFlash += (
                sidearmId,
                primaryId,
                armorSelected,
                grenadeCount,
                smokeGrenadeCount,
                incendiaryGrenadeCount,
                flashbangGrenadeCount) =>
            {
                purchaseRequestsWithFlash++;
                requestedFlashbangGrenades = flashbangGrenadeCount;
            };
            probe.SetLanguage("zh");
            probe.BeginRound(new DemolitionBuySnapshot(
                1,
                0,
                0,
                DemolitionTeam.Attackers,
                DemolitionEconomy.StartingFunds + 400,
                DemolitionBuyDuration,
                DemolitionBuyDuration,
                false));
            probe.SelectSidearmForDiagnostics(DemolitionBuyCatalog.P226Id);
            probe.SetSmokeGrenadesForDiagnostics(1);
            probe.SetFlashbangGrenadesForDiagnostics(1);
            probe.PressConfirmForDiagnostics();
        }
        var sceneReady = probe is not null
            && probe.SceneFilePath == scenePath
            && probe.UiReady
            && probe.IntentSignalsConnected
            && probe.LanguageMatches("zh")
            && probe.IsSidearmOfferEnabled(DemolitionBuyCatalog.P226Id)
            && probe.IsSidearmOfferEnabled(DemolitionBuyCatalog.Gsh18Id)
            && !probe.IsPrimaryOfferEnabled(DemolitionBuyCatalog.Mp5Id)
            && !probe.IsPrimaryOfferEnabled(DemolitionBuyCatalog.M4A1Id)
            && !probe.IsPrimaryOfferEnabled(DemolitionBuyCatalog.M24Id)
            && probe.CurrentQuote.TotalCost == 1150
            && probe.CurrentQuote.RemainingFunds == 50
            && probe.CurrentSelection.FlashbangGrenadeCount == 1
            && purchaseRequests == 1
            && purchaseRequestsWithFlash == 1
            && requestedSidearm == DemolitionBuyCatalog.P226Id
            && string.IsNullOrEmpty(requestedPrimary)
            && !requestedArmor
            && requestedGrenades == 0
            && requestedSmokeGrenades == 1
            && requestedIncendiaryGrenades == 0
            && requestedFlashbangGrenades == 1;
        var sceneFlashCount = probe?.CurrentSelection.FlashbangGrenadeCount ?? -1;
        if (probe is not null)
        {
            probe.SelectPrimaryForDiagnostics(DemolitionBuyCatalog.M4A1Id);
        }
        var unaffordableBlocked = probe is not null
            && !probe.CurrentQuote.Affordable
            && !probe.ConfirmEnabled;
        probe?.QueueFree();

        _hud.SetLanguage("en");
        _hud.ShowDemolitionBuy(new DemolitionBuySnapshot(
            3,
            1,
            1,
            DemolitionTeam.Defenders,
            3300,
            8.5f,
            DemolitionBuyDuration,
            false));
        var hudReady = _hud.IsDemolitionBuyVisible
            && _hud.DemolitionBuyUiReady
            && _hud.DemolitionBuyUsesPackedScene
            && _hud.DemolitionBuyIntentSignalsReady
            && _hud.DemolitionBuyLanguageReady
            && _hud.DemolitionBuyDisplayedFunds == 3300
            && Mathf.IsEqualApprox(_hud.DemolitionBuyDisplayedSeconds, 8.5f);
        _hud.HideDemolitionBuy();
        _hud.ShowOperationsOffice();

        var smoke = new SmokeGrenade
        {
            Position = Vector3.Zero,
            OwnerBody = _player
        };
        AddChild(smoke);
        smoke.Arm(Vector3.Forward);
        smoke.BeginGroundFuseForDiagnostics();
        smoke._PhysicsProcess(2.0);
        var smokeCenter = smoke.GlobalPosition + Vector3.Up * 1.45f;
        var smokeInnerEdge = smokeCenter + Vector3.Forward * (SmokeGrenade.CloudRadius - 0.1f);
        var smokeOuterEdge = smokeCenter + Vector3.Forward * (SmokeGrenade.CloudRadius + 0.2f);
        var smokeReady = smoke.IsDeployed
            && smoke.RemainingDuration > 12.0f
            && smoke.CloudVisualCount == SmokeGrenade.VisualLobeCount
            && smoke.IsInGroup(SmokeGrenade.ActiveGroupName)
            && smoke.OwnerCollisionExcluded
            && IsLineObscuredBySmoke(smokeCenter + Vector3.Left * 8.0f, smokeCenter + Vector3.Right * 8.0f)
            && IsLineObscuredBySmoke(smokeInnerEdge + Vector3.Left, smokeInnerEdge + Vector3.Right)
            && !IsLineObscuredBySmoke(smokeOuterEdge + Vector3.Left, smokeOuterEdge + Vector3.Right)
            && !IsLineObscuredBySmoke(smokeCenter + Vector3.Forward * 12.0f, smokeCenter + Vector3.Forward * 18.0f);
        smoke.QueueFree();

        var incendiary = new IncendiaryGrenade
        {
            Position = Vector3.Zero,
            OwnerBody = _player
        };
        AddChild(incendiary);
        incendiary.Arm(Vector3.Forward);
        incendiary.BeginGroundFuseForDiagnostics(Vector3.Zero, Vector3.Up);
        incendiary._PhysicsProcess(0.5);
        var incendiaryReady = incendiary.IsBurning
            && incendiary.RemainingDuration >= 7.0f
            && incendiary.ParticleEmitterCount == 1
            && incendiary.IsInGroup(IncendiaryGrenade.ActiveGroupName)
            && ActiveIncendiaryCountForDiagnostics <= 4;
        var incendiaryTickProbe = new Node { Name = "IncendiaryOverlapTickProbe" };
        AddChild(incendiaryTickProbe);
        var overlapDamageGuard = TryAcquireIncendiaryDamageTickForDiagnostics(incendiaryTickProbe)
            && !TryAcquireIncendiaryDamageTickForDiagnostics(incendiaryTickProbe);
        incendiary.QueueFree();
        incendiaryTickProbe.QueueFree();
        ClearDemolitionUtilityProjectiles();
        _hud.SetLanguage(originalLanguage);
        await WaitFrames(3);

        var valid = domainReady
            && flashBuyReady
            && flashPurchaseRpcReady
            && sceneReady
            && unaffordableBlocked
            && hudReady
            && smokeReady
            && incendiaryReady
            && overlapDamageGuard;
        GD.Print($"DEMOLITION_BUY_CHECK valid={valid} domain={domainReady} scene={sceneReady} signals={purchaseRequests}/{purchaseRequestsWithFlash} primary_locked={!openingPrimary.Affordable} pistol_cost={pistolQuote.TotalCost} pistol_balance={pistolQuote.RemainingFunds} gsh18_cost={gsh18Quote.TotalCost} gsh18_platform={gsh18Loadout.Sidearm?.Platform} m24={m24Loadout.Weapon?.Platform}:{m24Quote.TotalCost} smoke_cost={DemolitionBuyCatalog.SmokeGrenadePrice} smoke={smokeReady}:{SmokeGrenade.CloudRadius:0.0}m/{SmokeGrenade.VisualLobeCount}/{SmokeGrenade.VisualOpacity:0.00} incendiary={incendiaryReady}:{DemolitionBuyCatalog.IncendiaryGrenadePrice} overlap_guard={overlapDamageGuard} combo_blocked={unaffordableBlocked} hud={hudReady} flash_buy={flashBuyReady}:{flashbangQuote.TotalCost}/{flashbangQuote.Selection.FlashbangGrenadeCount} flash_ui={sceneReady}:{sceneFlashCount}/{requestedFlashbangGrenades} flash_rpc={flashPurchaseRpcReady}:{legacyPurchasePayload.FlashbangGrenadeCount}/{purchasePayloadV2.FlashbangGrenadeCount}");
        GD.Print($"DEMOLITION_BUY_PASS valid={valid} flash_buy={flashBuyReady} flash_ui={sceneReady} flash_rpc={flashPurchaseRpcReady}");
        GetTree().Paused = false;
        await WaitFrames(30);
        GetTree().Quit(valid ? 0 : 2);
    }

    private async void CaptureDemolitionBuy()
    {
        await WaitFrames(3);
        _hud.ShowDemolitionBuy(new DemolitionBuySnapshot(
            7,
            3,
            3,
            DemolitionTeam.Attackers,
            4100,
            11.8f,
            DemolitionBuyDuration,
            false));
        _hud.SelectDemolitionBuySidearmForDiagnostics(DemolitionBuyCatalog.Gsh18Id);
        _hud.SelectDemolitionBuyPrimaryForDiagnostics(DemolitionBuyCatalog.M4A1Id);
        _hud.SetDemolitionBuyGrenadesForDiagnostics(1);
        _hud.SetDemolitionBuySmokeGrenadesForDiagnostics(1);
        _hud.SetDemolitionBuyIncendiaryGrenadesForDiagnostics(1);
        _hud.SetDemolitionBuyFlashbangGrenadesForDiagnostics(1);
        await WaitFrames(18);
        SaveViewportImage("res://demolition_buy_validation.png");
        GD.Print("DEMOLITION_BUY_CAPTURE path=demolition_buy_validation.png");
        GetTree().Paused = false;
        GetTree().Quit();
    }
}
