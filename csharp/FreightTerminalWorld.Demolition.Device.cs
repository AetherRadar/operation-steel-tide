using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const float DemolitionDevicePickupRadius = 1.8f;
    private readonly DemolitionDeviceLifecycle _demolitionDeviceLifecycle = new();
    private Vector3 _demolitionDeviceGroundPosition;
    private Vector3 _demolitionDeviceLastCarrierPosition;
    private OmniLight3D? _demolitionDeviceBeacon;
    private int _demolitionDetonationCount;

    private void BeginDemolitionDeviceRound()
    {
        var layout = DemolitionLayout();
        _demolitionDeviceGroundPosition = layout.AttackSpawn + Vector3.Up * 0.26f;
        _demolitionDeviceLastCarrierPosition = _demolitionDeviceGroundPosition;
        _demolitionDeviceLifecycle.BeginGrounded();
        BuildDemolitionDeviceVisual();
        AssignInitialDemolitionDevicePickupRunner();
        SyncDemolitionDeviceVisual();
    }

    private void BuildDemolitionDeviceVisual()
    {
        if (IsInstanceValid(_demolitionDevice))
        {
            return;
        }
        var orange = Mat(
            "demolition_device_orange",
            new Color(1.0f, 0.24f, 0.04f),
            0.16f,
            0.24f,
            new Color(1.0f, 0.05f, 0.01f));
        var dark = Mat(
            "demolition_device_dark",
            new Color(0.018f, 0.025f, 0.023f),
            0.72f,
            0.3f);
        _demolitionDevice = new Node3D
        {
            Name = $"DemolitionDevice_{_demolitionMatch.CurrentRound:00}",
            Position = _demolitionDeviceGroundPosition
        };
        _demolitionArena!.Root.AddChild(_demolitionDevice);
        OfficeBox(
            _demolitionDevice,
            "DeviceCase",
            Vector3.Zero,
            new Vector3(0.9f, 0.48f, 0.62f),
            dark);
        OfficeBox(
            _demolitionDevice,
            "DeviceScreen",
            new Vector3(0.0f, 0.1f, -0.33f),
            new Vector3(0.52f, 0.2f, 0.035f),
            orange);
        _demolitionDeviceBeacon = new OmniLight3D
        {
            Name = "DeviceBeacon",
            Position = new Vector3(0.0f, 0.45f, 0.0f),
            LightColor = new Color(1.0f, 0.12f, 0.02f),
            LightEnergy = 1.2f,
            OmniRange = 5.0f,
            ShadowEnabled = false
        };
        _demolitionDevice.AddChild(_demolitionDeviceBeacon);
    }

    private void AssignInitialDemolitionDevicePickupRunner()
    {
        var attackers = LivingDemolitionAttackers().ToList();
        var memberIds = attackers
            .Select(DemolitionMemberId)
            .Where(memberId => memberId is not null)
            .Select(memberId => memberId!)
            .ToArray();
        var selectedId = _demolitionDeviceLifecycle.AssignRandomPickupRunner(
            memberIds,
            _rng.Randi());
        ApplyDemolitionDevicePickupAssignment(ResolveDemolitionAttacker(selectedId));
    }

    private void EnsureDemolitionDevicePickupRunner()
    {
        if (!_demolitionDeviceLifecycle.IsGrounded)
        {
            return;
        }
        var runner = ResolveDemolitionAttacker(_demolitionDeviceLifecycle.PickupRunnerMemberId);
        if (IsLivingDemolitionAttacker(runner))
        {
            ApplyDemolitionDevicePickupAssignment(runner);
            return;
        }
        var replacement = NearestLivingDemolitionAttacker(_demolitionDeviceGroundPosition, runner);
        _demolitionDeviceLifecycle.ClearPickupRunner();
        if (replacement is not null
            && _demolitionDeviceLifecycle.AssignPickupRunner(DemolitionMemberId(replacement)))
        {
            ApplyDemolitionDevicePickupAssignment(replacement);
        }
    }

    private void ApplyDemolitionDevicePickupAssignment(Node3D? runner)
    {
        if (!IsLivingDemolitionAttacker(runner))
        {
            return;
        }
        if (runner is SquadMate mate)
        {
            _demolitionSquadObjectiveMate = mate;
            _demolitionSquadAssignmentTargets.Remove(mate);
            if (!mate.DemolitionMoveTargets(_demolitionDeviceGroundPosition))
            {
                mate.SetOrder(SquadOrder.Move, _demolitionDeviceGroundPosition);
            }
        }
        else if (runner is EnemyOperator enemy)
        {
            if (_demolitionCarrier != enemy)
            {
                var previousCarrier = _demolitionCarrier;
                _demolitionCarrier = enemy;
                ResetDemolitionOpponentRoute(previousCarrier);
                ResetDemolitionOpponentRoute(enemy);
            }
        }
    }

    private void UpdateDemolitionDeviceLifecycle()
    {
        if (!_demolitionRoundActive || _demolitionDevicePlanted)
        {
            return;
        }
        if (_demolitionDeviceLifecycle.IsCarried)
        {
            var carrier = ResolveDemolitionAttacker(_demolitionDeviceLifecycle.CarrierMemberId);
            if (!IsLivingDemolitionAttacker(carrier))
            {
                DropDemolitionDevice(carrier);
                return;
            }
            _demolitionDeviceLastCarrierPosition = carrier!.GlobalPosition;
            SyncDemolitionDeviceVisual();
            return;
        }
        if (!_demolitionDeviceLifecycle.IsGrounded)
        {
            return;
        }
        EnsureDemolitionDevicePickupRunner();
        var runner = ResolveDemolitionAttacker(_demolitionDeviceLifecycle.PickupRunnerMemberId);
        if (!IsLivingDemolitionAttacker(runner))
        {
            return;
        }
        if (HorizontalDistance(runner!.GlobalPosition, _demolitionDeviceGroundPosition)
                <= DemolitionDevicePickupRadius
            && Mathf.Abs(runner.GlobalPosition.Y - _demolitionDeviceGroundPosition.Y) < 2.0f)
        {
            TryPickupDemolitionDevice(runner);
        }
        SyncDemolitionDeviceVisual();
    }

    private bool TryPickupDemolitionDevice(Node3D runner)
    {
        var memberId = DemolitionMemberId(runner);
        if (memberId is null || !_demolitionDeviceLifecycle.TryPickup(memberId))
        {
            return false;
        }
        _demolitionDeviceLastCarrierPosition = runner.GlobalPosition;
        if (runner is EnemyOperator enemy)
        {
            _demolitionCarrier = enemy;
            ResetDemolitionOpponentRoute(enemy);
        }
        else if (runner is SquadMate mate)
        {
            _demolitionSquadObjectiveMate = mate;
            _demolitionSquadPlantProgress = 0.0f;
        }
        SyncDemolitionDeviceVisual();
        if (LocalDemolitionSide == DemolitionTeam.Attackers)
        {
            _hud.ShowRadioMessage(
                GameLocalization.Format(
                    "demolition_device_picked_up",
                    _languageSetting,
                    "{0} HAS THE DEVICE  //  MOVE TO SITE A OR B",
                    DemolitionMemberDisplayName(runner)),
                new Color(1.0f, 0.58f, 0.2f));
        }
        return true;
    }

    private void DropDemolitionDevice(Node3D? carrier)
    {
        var carrierId = _demolitionDeviceLifecycle.CarrierMemberId;
        if (carrierId is null)
        {
            return;
        }
        var dropOrigin = IsInstanceValid(carrier)
            ? carrier!.GlobalPosition
            : _demolitionDeviceLastCarrierPosition;
        _demolitionDeviceGroundPosition = dropOrigin + Vector3.Up * 0.26f;
        var replacement = NearestLivingDemolitionAttacker(dropOrigin, carrier);
        var replacementId = DemolitionMemberId(replacement);
        if (!_demolitionDeviceLifecycle.TryDrop(carrierId, replacementId))
        {
            return;
        }

        var previousEnemyCarrier = _demolitionCarrier;
        _demolitionCarrier = replacement as EnemyOperator;
        ResetDemolitionOpponentRoute(previousEnemyCarrier);
        ResetDemolitionOpponentRoute(_demolitionCarrier);
        _demolitionPlantProgress = 0.0f;
        _demolitionEnemyPlantProgress = 0.0f;
        _demolitionSquadPlantProgress = 0.0f;
        _demolitionSquadObjectiveMate = replacement as SquadMate;
        ApplyDemolitionDevicePickupAssignment(replacement);
        SyncDemolitionDeviceVisual();

        if (LocalDemolitionSide == DemolitionTeam.Attackers)
        {
            var replacementName = replacement is null
                ? GameLocalization.Get("demolition_no_carrier", _languageSetting, "NO CARRIER AVAILABLE")
                : DemolitionMemberDisplayName(replacement);
            _hud.ShowRadioMessage(
                GameLocalization.Format(
                    "demolition_device_dropped",
                    _languageSetting,
                    "DEVICE DROPPED  //  {0} TAKE OVER",
                    replacementName),
                new Color(1.0f, 0.3f, 0.18f));
        }
    }

    private void SyncDemolitionDeviceVisual()
    {
        if (!IsInstanceValid(_demolitionDevice))
        {
            return;
        }
        _demolitionDevice!.Visible = _demolitionDeviceLifecycle.Phase
            != DemolitionDevicePhase.Inactive
            && _demolitionDeviceLifecycle.Phase != DemolitionDevicePhase.Detonated;
        _demolitionDevice.Scale = Vector3.One;
        if (_demolitionDeviceLifecycle.IsGrounded)
        {
            _demolitionDevice.GlobalPosition = _demolitionDeviceGroundPosition;
            SetDemolitionDeviceBeacon(active: true, energy: 1.2f, range: 5.0f);
        }
        else if (_demolitionDeviceLifecycle.IsCarried)
        {
            var carrier = ResolveDemolitionAttacker(_demolitionDeviceLifecycle.CarrierMemberId);
            if (IsInstanceValid(carrier))
            {
                _demolitionDevice.GlobalPosition = carrier!.GlobalPosition
                    + Vector3.Up * 1.0f
                    + carrier.GlobalBasis.X * 0.36f;
            }
            SetDemolitionDeviceBeacon(active: false, energy: 0.0f, range: 5.0f);
        }
    }

    private void SetDemolitionDeviceBeacon(bool active, float energy, float range)
    {
        if (!IsInstanceValid(_demolitionDeviceBeacon))
        {
            return;
        }
        _demolitionDeviceBeacon!.Visible = active;
        _demolitionDeviceBeacon.LightEnergy = energy;
        _demolitionDeviceBeacon.OmniRange = range;
    }

    private IEnumerable<Node3D> LivingDemolitionAttackers()
    {
        if (LocalDemolitionSide == DemolitionTeam.Attackers)
        {
            if (!_player.IsDead)
            {
                yield return _player;
            }
            foreach (var mate in _squadMates.Where(mate => IsInstanceValid(mate)
                         && !mate.IsDowned
                         && !mate.IsBodyBag))
            {
                yield return mate;
            }
            yield break;
        }
        foreach (var opponent in _demolitionOpponents.Where(opponent => IsInstanceValid(opponent)
                     && !opponent.IsDead))
        {
            yield return opponent;
        }
    }

    private Node3D? NearestLivingDemolitionAttacker(Vector3 origin, Node3D? excluded)
        => LivingDemolitionAttackers()
            .Where(candidate => candidate != excluded)
            .OrderBy(candidate => candidate.GlobalPosition.DistanceSquaredTo(origin))
            .ThenBy(candidate => DemolitionMemberId(candidate), System.StringComparer.Ordinal)
            .FirstOrDefault();

    private bool IsLivingDemolitionAttacker(Node3D? actor)
        => actor switch
        {
            TacticalPlayer player => LocalDemolitionSide == DemolitionTeam.Attackers
                && !player.IsDead,
            SquadMate mate => LocalDemolitionSide == DemolitionTeam.Attackers
                && IsInstanceValid(mate)
                && !mate.IsDowned
                && !mate.IsBodyBag,
            EnemyOperator opponent => LocalDemolitionSide == DemolitionTeam.Defenders
                && IsInstanceValid(opponent)
                && !opponent.IsDead,
            _ => false
        };

    private static string? DemolitionMemberId(Node3D? actor)
        => actor switch
        {
            TacticalPlayer => "PLAYER",
            SquadMate mate => $"MATE:{mate.SquadSlot}",
            EnemyOperator opponent => opponent.Name.ToString(),
            _ => null
        };

    private Node3D? ResolveDemolitionAttacker(string? memberId)
    {
        if (string.IsNullOrWhiteSpace(memberId))
        {
            return null;
        }
        if (memberId == "PLAYER")
        {
            return LocalDemolitionSide == DemolitionTeam.Attackers ? _player : null;
        }
        if (memberId.StartsWith("MATE:", System.StringComparison.Ordinal)
            && int.TryParse(memberId.Substring(5), out var slot))
        {
            return LocalDemolitionSide == DemolitionTeam.Attackers
                ? _squadMates.FirstOrDefault(mate => IsInstanceValid(mate) && mate.SquadSlot == slot)
                : null;
        }
        return LocalDemolitionSide == DemolitionTeam.Defenders
            ? _demolitionOpponents.FirstOrDefault(opponent => IsInstanceValid(opponent)
                && string.Equals(opponent.Name.ToString(), memberId, System.StringComparison.Ordinal))
            : null;
    }

    private string DemolitionMemberDisplayName(Node3D actor)
        => actor switch
        {
            TacticalPlayer => GameLocalization.Get("you", _languageSetting, "YOU"),
            SquadMate mate => mate.Callsign,
            EnemyOperator opponent => opponent.Name.ToString(),
            _ => "OPERATOR"
        };

    private bool PlayerCarriesDemolitionDevice()
        => IsDemolitionNetworkClient
            ? _networkDeviceCarrierActorId == LocalDemolitionActorId
            : _demolitionDeviceLifecycle.IsCarried
                && _demolitionDeviceLifecycle.CarrierMemberId == "PLAYER";

    private bool IsDemolitionSquadDeviceObjectiveMate(SquadMate mate)
    {
        var memberId = DemolitionMemberId(mate);
        return !_demolitionDevicePlanted
            && LocalDemolitionSide == DemolitionTeam.Attackers
            && (memberId == _demolitionDeviceLifecycle.CarrierMemberId
                || memberId == _demolitionDeviceLifecycle.PickupRunnerMemberId);
    }

    internal bool ShouldPrioritizeDemolitionObjective(SquadMate mate, EnemyOperator? hostile)
    {
        if (!_demolitionMode || !_demolitionRoundActive || mate.IsDowned || mate.IsBodyBag)
        {
            return false;
        }
        var ownsAttackObjective = IsDemolitionSquadDeviceObjectiveMate(mate);
        var ownsDefuseObjective = _demolitionDevicePlanted
            && LocalDemolitionSide == DemolitionTeam.Defenders
            && mate == _demolitionSquadObjectiveMate;
        if (!ownsAttackObjective && !ownsDefuseObjective)
        {
            return false;
        }
        return hostile is null
            || !IsInstanceValid(hostile)
            || hostile.IsDead
            || mate.GlobalPosition.DistanceTo(hostile.GlobalPosition) >= DemolitionCombatEngageRange;
    }

    private bool TryUpdateDemolitionSquadDeviceObjective(float delta)
    {
        if (!_demolitionRoundActive
            || _demolitionDevicePlanted
            || LocalDemolitionSide != DemolitionTeam.Attackers)
        {
            return false;
        }
        if (_demolitionDeviceLifecycle.IsGrounded)
        {
            _demolitionSquadObjectiveMate = ResolveDemolitionAttacker(
                _demolitionDeviceLifecycle.PickupRunnerMemberId) as SquadMate;
            ApplyDemolitionDevicePickupAssignment(_demolitionSquadObjectiveMate);
            return true;
        }
        var carrier = ResolveDemolitionAttacker(_demolitionDeviceLifecycle.CarrierMemberId) as SquadMate;
        _demolitionSquadObjectiveMate = carrier;
        if (carrier is null || carrier.IsDowned || carrier.IsBodyBag)
        {
            _demolitionSquadPlantProgress = 0.0f;
            return true;
        }
        var layout = DemolitionLayout();
        var siteIndex = Mathf.Clamp(
            _demolitionAttackerPlan?.PrimarySiteIndex ?? _demolitionEnemyTargetSite,
            0,
            layout.SitePositions.Count - 1);
        if (_demolitionSquadObjectiveSite != siteIndex)
        {
            _demolitionSquadObjectiveSite = siteIndex;
            _demolitionSquadPlantProgress = 0.0f;
        }
        var site = layout.SitePositions[siteIndex];
        var flatSite = new Vector3(site.X, carrier.GlobalPosition.Y, site.Z);
        if (carrier.GlobalPosition.DistanceTo(flatSite) > 2.15f)
        {
            if (!carrier.DemolitionMoveTargets(site))
            {
                carrier.SetOrder(SquadOrder.Move, site);
            }
            _demolitionSquadPlantProgress = 0.0f;
            return true;
        }
        if (carrier.Order == SquadOrder.Move)
        {
            carrier.SetOrder(SquadOrder.Hold, carrier.GlobalPosition);
        }
        if (carrier.HasDemolitionThreatWithin(DemolitionChannelGuardRange))
        {
            return true;
        }
        _demolitionSquadPlantProgress = Mathf.Min(
            1.0f,
            _demolitionSquadPlantProgress + delta / DemolitionPlantDuration);
        if (_demolitionSquadPlantProgress >= 1.0f)
        {
            PlantDemolitionDevice(siteIndex, byPlayerTeam: true, carrier);
        }
        return true;
    }

    private void DetonateDemolitionDevice()
    {
        if (!_demolitionDeviceLifecycle.TryDetonate())
        {
            return;
        }
        _demolitionDetonationCount++;
        var position = IsInstanceValid(_demolitionDevice)
            ? _demolitionDevice!.GlobalPosition
            : DemolitionLayout().SitePositions[Mathf.Clamp(
                _demolitionActiveSite,
                0,
                DemolitionLayout().SitePositions.Count - 1)];
        SpawnExplosionEffect(position);
        if (IsInstanceValid(_demolitionDevice))
        {
            _demolitionDevice!.Visible = false;
        }
        SetDemolitionDeviceBeacon(active: false, energy: 0.0f, range: 5.0f);
    }

}
