using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const float DemolitionBuyDuration = 15.0f;
    private bool _demolitionBuyPhaseActive;
    private float _demolitionBuyRemaining;
    private bool _demolitionLocalBuyReady;
    private bool _demolitionPurchasePending;
    private readonly HashSet<long> _demolitionBuyReadyPeers = new();
    private readonly Dictionary<long, DemolitionEconomy> _demolitionRemoteEconomies = new();
    private int _demolitionNetworkFundsRound;
    private WeaponBuild _demolitionOpponentRoundWeapon = WeaponCatalog.Build(WeaponPlatform.P226, 0);
    private readonly List<SmokeGrenade> _activeSmokeGrenades = new();

    public bool IsDemolitionBuyPhaseActive => _demolitionBuyPhaseActive;
    public float DemolitionBuySecondsRemaining => _demolitionBuyRemaining;
    public bool IsDemolitionLocalBuyReady => _demolitionLocalBuyReady;

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

    private void BeginDemolitionBuyPhase(float secondsRemaining = DemolitionBuyDuration)
    {
        ClearAllRemoteMeleeTransientState();
        _demolitionBuyPhaseActive = true;
        _demolitionBuyRemaining = Mathf.Clamp(secondsRemaining, 0.0f, DemolitionBuyDuration);
        _demolitionRoundActive = false;
        _demolitionLocalBuyReady = false;
        _demolitionPurchasePending = false;
        _demolitionBuyReadyPeers.Clear();
        SetDemolitionActorsFrozen(true);
        _hud.HideDemolitionRoundResult();
        _hud.ShowDemolitionBuy(DemolitionBuyState());
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private void UpdateDemolitionBuyPhase(float delta)
    {
        if (!_demolitionBuyPhaseActive)
        {
            return;
        }
        if (IsDemolitionNetworkClient)
        {
            return;
        }
        _demolitionBuyRemaining = Mathf.Max(0.0f, _demolitionBuyRemaining - delta);
        if (!_demolitionLocalBuyReady)
        {
            _hud.UpdateDemolitionBuy(DemolitionBuyState());
        }
        if (_demolitionBuyRemaining <= 0.0f)
        {
            if (!_demolitionLocalBuyReady)
            {
                ProcessLocalDemolitionPurchase(DemolitionPurchaseSelection.Empty);
            }
            if (_demolitionBuyPhaseActive)
            {
                BeginDemolitionLivePhase();
            }
        }
    }

    private void OnDemolitionPurchaseRequested(
        string sidearmId,
        string primaryId,
        bool armorSelected,
        int grenadeCount,
        int smokeGrenadeCount)
    {
        if (!_demolitionMode || !_demolitionBuyPhaseActive || _missionEnded
            || _demolitionLocalBuyReady || _demolitionPurchasePending)
        {
            return;
        }

        var selection = new DemolitionPurchaseSelection(
            sidearmId,
            primaryId,
            armorSelected,
            grenadeCount,
            smokeGrenadeCount);
        if (IsDemolitionNetworkClient)
        {
            _demolitionPurchasePending = true;
            _squadNetwork.RequestDemolitionPurchase(_demolitionMatch.CurrentRound, selection);
            _hud.ShowRadioMessage(
                GameLocalization.Get(
                    "demolition_purchase_pending",
                    _languageSetting,
                    "PURCHASE SENT  //  WAITING FOR HOST"),
                new Color(0.55f, 0.86f, 0.72f));
            return;
        }

        ProcessLocalDemolitionPurchase(selection);
    }

    private void ProcessLocalDemolitionPurchase(DemolitionPurchaseSelection selection)
    {
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
        _demolitionLocalBuyReady = true;
        _demolitionPurchasePending = false;
        _hud.HideDemolitionBuy();
        _hud.ShowRadioMessage(
            GameLocalization.Format(
                hasFirearm ? "demolition_purchase" : "demolition_buy_knife_live",
                _languageSetting,
                hasFirearm ? "LOADOUT PURCHASED  //  -${0}" : "ROUND LIVE  //  KNIFE ONLY  //  ${0} SPENT",
                spent),
            new Color(0.55f, 0.86f, 0.72f));

        var networkMatch = _squadNetwork.IsOnline
            && _squadNetwork.IsDemolitionSession
            && _squadNetwork.DemolitionMatchStarted;
        if (!networkMatch)
        {
            BeginDemolitionLivePhase();
            return;
        }
        if (_squadNetwork.IsHost)
        {
            _demolitionBuyReadyPeers.Add(1);
            TryBeginNetworkDemolitionLivePhase();
        }
    }

    private void BeginDemolitionLivePhase()
    {
        ClearAllRemoteMeleeTransientState();
        _demolitionBuyPhaseActive = false;
        _demolitionBuyRemaining = 0.0f;
        _demolitionRoundActive = true;
        _demolitionRemaining = DemolitionRoundDuration;
        _hud.HideDemolitionBuy();
        SetDemolitionActorsFrozen(false);
        RefreshDemolitionStrategies(true);
        UpdateDemolitionRoundHud();
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private void TryBeginNetworkDemolitionLivePhase()
    {
        if (!_squadNetwork.IsHost || !_demolitionBuyPhaseActive
            || _demolitionBuyReadyPeers.Count < _squadNetwork.RegisteredDemolitionPlayerCount)
        {
            return;
        }
        BeginDemolitionLivePhase();
    }

    private void OnDemolitionNetworkPurchaseRequested(
        long peerId,
        int round,
        DemolitionPurchaseSelection selection)
    {
        if (!_squadNetwork.IsHost || !_demolitionMode || !_demolitionBuyPhaseActive
            || round != _demolitionMatch.CurrentRound
            || _demolitionBuyReadyPeers.Contains(peerId)
            || !_squadNetwork.TryGetDemolitionAssignment(peerId, out var team, out _, out _))
        {
            return;
        }

        var economy = RemoteDemolitionEconomy(peerId);
        var quote = DemolitionBuyCatalog.Quote(selection, economy.Funds);
        var spent = quote.Affordable ? economy.Spend(quote.TotalCost) : 0;
        var result = new DemolitionPurchaseNetworkResult(
            round,
            quote.Affordable && spent == quote.TotalCost,
            quote.Selection,
            quote.TotalCost,
            economy.Funds);
        _squadNetwork.SendDemolitionPurchaseResult(peerId, result);
        if (!result.Approved)
        {
            return;
        }
        _demolitionBuyReadyPeers.Add(peerId);
        TryBeginNetworkDemolitionLivePhase();
    }

    private void OnDemolitionPurchaseResult(DemolitionPurchaseNetworkResult result)
    {
        if (!IsDemolitionNetworkClient || result.Round != _demolitionMatch.CurrentRound)
        {
            return;
        }
        _demolitionPurchasePending = false;
        _demolitionNetworkFundsRound = Mathf.Max(_demolitionNetworkFundsRound, result.Round);
        _demolitionPlayerEconomy.ApplyNetworkFunds(result.RemainingFunds);
        if (!result.Approved)
        {
            if (_demolitionBuyPhaseActive)
            {
                _hud.UpdateDemolitionBuy(DemolitionBuyState());
                _hud.ShowLocalizedMessage(
                    "demolition_buy_insufficient_short",
                    "INSUFFICIENT FUNDS",
                    new Color(1.0f, 0.34f, 0.2f));
            }
            return;
        }

        var quote = new DemolitionPurchaseQuote(
            result.Selection,
            result.TotalCost,
            result.RemainingFunds,
            true);
        var loadout = DemolitionBuyCatalog.BuildLoadout(quote);
        _player.ApplyDemolitionRoundLoadout(
            loadout,
            quote.Selection.GrenadeCount,
            quote.Selection.SmokeGrenadeCount);
        CompleteDemolitionBuyPhase(result.TotalCost, quote.HasFirearm);
    }

    private void OnDemolitionFundsState(DemolitionFundsNetworkState state)
    {
        if (!IsDemolitionNetworkClient || state.Round < _demolitionNetworkFundsRound)
        {
            return;
        }
        _demolitionNetworkFundsRound = state.Round;
        _demolitionPlayerEconomy.ApplyNetworkFunds(state.Funds);
        if (_demolitionBuyPhaseActive && !_demolitionLocalBuyReady)
        {
            _hud.UpdateDemolitionBuy(DemolitionBuyState());
        }
    }

    private DemolitionEconomy RemoteDemolitionEconomy(long peerId)
    {
        if (!_demolitionRemoteEconomies.TryGetValue(peerId, out var economy))
        {
            economy = new DemolitionEconomy();
            _demolitionRemoteEconomies[peerId] = economy;
        }
        return economy;
    }

    private void InitializeRemoteDemolitionEconomies()
    {
        _demolitionRemoteEconomies.Clear();
        _demolitionNetworkFundsRound = 0;
        if (!_squadNetwork.IsHost)
        {
            return;
        }
        foreach (var member in _squadNetwork.DemolitionLobbyMembers())
        {
            if (!member.Host)
            {
                _demolitionRemoteEconomies[member.PeerId] = new DemolitionEconomy();
            }
        }
    }

    private void RecordRemoteDemolitionRoundEconomies(
        bool alphaWon,
        bool alphaObjectiveCompleted,
        bool bravoObjectiveCompleted)
    {
        if (!_squadNetwork.IsHost)
        {
            return;
        }
        foreach (var member in _squadNetwork.DemolitionLobbyMembers())
        {
            if (member.Host)
            {
                continue;
            }
            var memberWon = member.Team == DemolitionNetworkTeam.Alpha
                ? alphaWon
                : !alphaWon;
            var objectiveCompleted = member.Team == DemolitionNetworkTeam.Alpha
                ? alphaObjectiveCompleted
                : bravoObjectiveCompleted;
            RemoteDemolitionEconomy(member.PeerId).RecordRound(memberWon, objectiveCompleted);
        }
    }

    private void ResetRemoteDemolitionEconomies()
    {
        foreach (var economy in _demolitionRemoteEconomies.Values)
        {
            economy.Reset();
        }
    }

    private void BroadcastRemoteDemolitionFunds()
    {
        if (!_squadNetwork.IsHost)
        {
            return;
        }
        foreach (var member in _squadNetwork.DemolitionLobbyMembers())
        {
            if (!member.Host && _demolitionRemoteEconomies.TryGetValue(member.PeerId, out var economy))
            {
                _squadNetwork.SendDemolitionFundsState(
                    member.PeerId,
                    new DemolitionFundsNetworkState(_demolitionMatch.CurrentRound, economy.Funds));
            }
        }
    }

    private void ApplyDemolitionNetworkBuyFallback()
    {
        if (_demolitionLocalBuyReady)
        {
            return;
        }
        var quote = DemolitionBuyCatalog.Quote(
            DemolitionPurchaseSelection.Empty,
            _demolitionPlayerEconomy.Funds);
        var loadout = DemolitionBuyCatalog.BuildLoadout(quote);
        _player.ApplyDemolitionRoundLoadout(loadout, 0, 0);
        _demolitionLocalBuyReady = true;
        _demolitionPurchasePending = false;
        _hud.HideDemolitionBuy();
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
                if (frozen)
                {
                    mate.SetDemolitionRoundFrozenPose();
                }
                else
                {
                    mate.Velocity = Vector3.Zero;
                }
                mate.ProcessMode = frozen ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
            }
        }
        foreach (var opponent in _demolitionOpponents)
        {
            if (IsInstanceValid(opponent))
            {
                if (frozen)
                {
                    opponent.SetDemolitionRoundFrozenPose();
                }
                else
                {
                    opponent.Velocity = Vector3.Zero;
                }
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
        var humanBravoTeam = _squadNetwork.IsOnline
            && _squadNetwork.IsHost
            && _squadNetwork.IsDemolitionSession
            && _squadNetwork.DemolitionPlayerCount(DemolitionNetworkTeam.Bravo) > 0;
        if (!humanBravoTeam)
        {
            _demolitionOpponentEconomy.Spend(offer?.Price ?? 0);
        }
        _demolitionOpponentRoundWeapon = WeaponCatalog.Build(platform, 0);
    }

    public void ThrowSmokeGrenade(Vector3 origin, Vector3 direction, Node source)
        => ThrowSmokeGrenade(origin, direction, source, 14.0f, 5.0f);

    public void ThrowSmokeGrenade(Vector3 origin, Vector3 direction, Node source, float speed, float loft)
    {
        var grenade = new SmokeGrenade
        {
            Position = origin,
            OwnerBody = source
        };
        AddChild(grenade);
        grenade.Arm(direction, speed, loft);
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
