using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const float DemolitionBuyDuration = 15.0f;
    private bool _demolitionBuyPhaseActive;
    private float _demolitionBuyRemaining;
    private WeaponBuild _demolitionOpponentRoundWeapon = WeaponCatalog.Build(WeaponPlatform.P226, 0);
    private readonly List<SmokeGrenade> _activeSmokeGrenades = new();

    public bool IsDemolitionBuyPhaseActive => _demolitionBuyPhaseActive;
    public float DemolitionBuySecondsRemaining => _demolitionBuyRemaining;

    private DemolitionBuySnapshot DemolitionBuyState()
        => new(
            _demolitionMatch.CurrentRound,
            LocalDemolitionScore,
            OpposingDemolitionScore,
            LocalDemolitionSide,
            _demolitionPlayerEconomy.Funds,
            _demolitionBuyRemaining,
            DemolitionBuyDuration,
            _demolitionMatch.IsOvertime);

    private void BeginDemolitionBuyPhase()
    {
        _demolitionBuyPhaseActive = true;
        _demolitionBuyRemaining = DemolitionBuyDuration;
        _demolitionRoundActive = false;
        SetDemolitionActorsFrozen(true);
        _hud.ShowDemolitionBuy(DemolitionBuyState());
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private void UpdateDemolitionBuyPhase(float delta)
    {
        if (!_demolitionBuyPhaseActive)
        {
            return;
        }
        _demolitionBuyRemaining = Mathf.Max(0.0f, _demolitionBuyRemaining - delta);
        _hud.UpdateDemolitionBuy(DemolitionBuyState());
        if (_demolitionBuyRemaining <= 0.0f)
        {
            _hud.SubmitDemolitionBuyTimeout();
        }
    }

    private void OnDemolitionPurchaseRequested(
        string sidearmId,
        string primaryId,
        bool armorSelected,
        int grenadeCount,
        int smokeGrenadeCount)
    {
        if (!_demolitionMode || !_demolitionBuyPhaseActive || _missionEnded)
        {
            return;
        }

        var selection = new DemolitionPurchaseSelection(
            sidearmId,
            primaryId,
            armorSelected,
            grenadeCount,
            smokeGrenadeCount);
        var quote = DemolitionBuyCatalog.Quote(selection, _demolitionPlayerEconomy.Funds);
        if (!quote.Affordable)
        {
            _hud.ShowLocalizedMessage(
                "demolition_buy_insufficient_short",
                "INSUFFICIENT FUNDS",
                new Color(1.0f, 0.34f, 0.2f));
            return;
        }

        var spent = _demolitionPlayerEconomy.Spend(quote.TotalCost);
        if (spent != quote.TotalCost)
        {
            return;
        }
        var loadout = DemolitionBuyCatalog.BuildLoadout(quote);
        _player.ApplyDemolitionRoundLoadout(
            loadout,
            quote.Selection.GrenadeCount,
            quote.Selection.SmokeGrenadeCount);
        CompleteDemolitionBuyPhase(spent, quote.HasFirearm);
    }

    private void CompleteDemolitionBuyPhase(int spent, bool hasFirearm)
    {
        _demolitionBuyPhaseActive = false;
        _demolitionBuyRemaining = 0.0f;
        _demolitionRoundActive = true;
        _demolitionRemaining = DemolitionRoundDuration;
        _hud.HideDemolitionBuy();
        SetDemolitionActorsFrozen(false);
        RefreshDemolitionStrategies(true);
        UpdateDemolitionRoundHud();
        Input.MouseMode = Input.MouseModeEnum.Captured;
        _hud.ShowRadioMessage(
            GameLocalization.Format(
                hasFirearm ? "demolition_purchase" : "demolition_buy_knife_live",
                _languageSetting,
                hasFirearm ? "LOADOUT PURCHASED  //  -${0}" : "ROUND LIVE  //  KNIFE ONLY  //  ${0} SPENT",
                spent),
            new Color(0.55f, 0.86f, 0.72f));
    }

    private void SetDemolitionActorsFrozen(bool frozen)
    {
        _player.Velocity = Vector3.Zero;
        _player.UiLocked = frozen;
        _player.DisarmFireInput();
        _player.DisarmMovementInput();
        _player.ProcessMode = frozen ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
        if (!frozen)
        {
            _player.RestoreMovementInput();
        }
        foreach (var mate in _squadMates)
        {
            if (IsInstanceValid(mate))
            {
                mate.Velocity = Vector3.Zero;
                mate.ProcessMode = frozen ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
            }
        }
        foreach (var opponent in _demolitionOpponents)
        {
            if (IsInstanceValid(opponent))
            {
                opponent.Velocity = Vector3.Zero;
                opponent.ProcessMode = frozen ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
            }
        }
    }

    private void ResolveDemolitionOpponentBuy()
    {
        var funds = _demolitionOpponentEconomy.Funds;
        var offer = funds >= 3600
            ? DemolitionBuyCatalog.Primary(DemolitionBuyCatalog.ScarLId)
            : funds >= 3100
                ? DemolitionBuyCatalog.Primary(DemolitionBuyCatalog.M4A1Id)
                : funds >= 2900
                    ? DemolitionBuyCatalog.Primary(DemolitionBuyCatalog.Ak74Id)
                    : funds >= 1700
                        ? DemolitionBuyCatalog.Primary(DemolitionBuyCatalog.Mp5Id)
                        : DemolitionBuyCatalog.Sidearm(DemolitionBuyCatalog.P226Id);
        if (offer?.Platform is not WeaponPlatform platform)
        {
            platform = WeaponPlatform.P226;
        }
        _demolitionOpponentEconomy.Spend(offer?.Price ?? 0);
        _demolitionOpponentRoundWeapon = WeaponCatalog.Build(platform, 0);
    }

    public void ThrowSmokeGrenade(Vector3 origin, Vector3 direction, Node source)
    {
        var grenade = new SmokeGrenade
        {
            Position = origin,
            OwnerBody = source
        };
        AddChild(grenade);
        grenade.Arm(direction);
    }

    public bool IsLineObscuredBySmoke(Vector3 from, Vector3 to)
    {
        for (var index = _activeSmokeGrenades.Count - 1; index >= 0; index--)
        {
            var smoke = _activeSmokeGrenades[index];
            if (!IsInstanceValid(smoke))
            {
                _activeSmokeGrenades.RemoveAt(index);
                continue;
            }
            if (smoke.ObscuresSegment(from, to))
            {
                return true;
            }
        }
        return false;
    }

    internal void RegisterActiveSmokeGrenade(SmokeGrenade smoke)
    {
        if (!_activeSmokeGrenades.Contains(smoke))
        {
            _activeSmokeGrenades.Add(smoke);
        }
    }

    internal void UnregisterActiveSmokeGrenade(SmokeGrenade smoke)
        => _activeSmokeGrenades.Remove(smoke);
}
