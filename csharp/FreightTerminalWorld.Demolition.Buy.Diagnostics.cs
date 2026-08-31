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
        var pistolLoadout = DemolitionBuyCatalog.BuildLoadout(pistolQuote);
        var gsh18Loadout = DemolitionBuyCatalog.BuildLoadout(gsh18Quote);
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
            && gsh18Loadout.SidearmReserveAmmo == 54;

        const string scenePath = "res://ui/DemolitionBuyView.tscn";
        var packedScene = HudPackedSceneCache.Load(scenePath);
        var probe = packedScene?.Instantiate<DemolitionBuyView>();
        var purchaseRequests = 0;
        var requestedSidearm = string.Empty;
        var requestedPrimary = string.Empty;
        var requestedArmor = true;
        var requestedGrenades = -1;
        var requestedSmokeGrenades = -1;
        if (probe is not null)
        {
            probe.Visible = false;
            _hud.AddChild(probe);
            probe.PurchaseRequested += (
                sidearmId,
                primaryId,
                armorSelected,
                grenadeCount,
                smokeGrenadeCount) =>
            {
                purchaseRequests++;
                requestedSidearm = sidearmId;
                requestedPrimary = primaryId;
                requestedArmor = armorSelected;
                requestedGrenades = grenadeCount;
                requestedSmokeGrenades = smokeGrenadeCount;
            };
            probe.SetLanguage("zh");
            probe.BeginRound(new DemolitionBuySnapshot(
                1,
                0,
                0,
                DemolitionTeam.Attackers,
                DemolitionEconomy.StartingFunds,
                DemolitionBuyDuration,
                DemolitionBuyDuration,
                false));
            probe.SelectSidearmForDiagnostics(DemolitionBuyCatalog.P226Id);
            probe.SetSmokeGrenadesForDiagnostics(1);
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
            && probe.CurrentQuote.TotalCost == 800
            && probe.CurrentQuote.RemainingFunds == 0
            && purchaseRequests == 1
            && requestedSidearm == DemolitionBuyCatalog.P226Id
            && string.IsNullOrEmpty(requestedPrimary)
            && !requestedArmor
            && requestedGrenades == 0
            && requestedSmokeGrenades == 1;
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
        var smokeReady = smoke.IsDeployed
            && smoke.RemainingDuration > 12.0f
            && smoke.CloudVisualCount == 24
            && smoke.IsInGroup(SmokeGrenade.ActiveGroupName)
            && smoke.OwnerCollisionExcluded
            && IsLineObscuredBySmoke(smokeCenter + Vector3.Left * 8.0f, smokeCenter + Vector3.Right * 8.0f)
            && !IsLineObscuredBySmoke(smokeCenter + Vector3.Forward * 12.0f, smokeCenter + Vector3.Forward * 18.0f);
        smoke.QueueFree();
        _hud.SetLanguage(originalLanguage);
        await WaitFrames(3);

        var valid = domainReady && sceneReady && unaffordableBlocked && hudReady && smokeReady;
        GD.Print($"DEMOLITION_BUY_CHECK valid={valid} domain={domainReady} scene={sceneReady} signals={purchaseRequests} primary_locked={!openingPrimary.Affordable} pistol_cost={pistolQuote.TotalCost} pistol_balance={pistolQuote.RemainingFunds} gsh18_cost={gsh18Quote.TotalCost} gsh18_platform={gsh18Loadout.Sidearm?.Platform} smoke_cost={DemolitionBuyCatalog.SmokeGrenadePrice} smoke={smokeReady} combo_blocked={unaffordableBlocked} hud={hudReady}");
        GD.Print($"DEMOLITION_BUY_PASS valid={valid}");
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
        await WaitFrames(18);
        SaveViewportImage("res://demolition_buy_validation.png");
        GD.Print("DEMOLITION_BUY_CAPTURE path=demolition_buy_validation.png");
        GetTree().Paused = false;
        GetTree().Quit();
    }
}
