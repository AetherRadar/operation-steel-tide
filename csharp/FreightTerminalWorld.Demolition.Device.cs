using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private const float DemolitionDevicePickupRadius = 1.8f;
    private const float DemolitionDeviceGroundHeight = 0.02f;
    private const float DemolitionDeviceCarryHeight = 0.80f;
    private const float DemolitionDeviceCarryOffset = 0.25f;
    private const float DemolitionDeviceCarryBackOffset = 0.02f;
    private readonly DemolitionDeviceLifecycle _demolitionDeviceLifecycle = new();
    private Vector3 _demolitionDeviceGroundPosition;
    private Vector3 _demolitionDeviceLastCarrierPosition;
    private OmniLight3D? _demolitionDeviceBeacon;
    private int _demolitionDetonationCount;

    private void BeginDemolitionDeviceRound()
    {
        var layout = DemolitionLayout();
        _demolitionDeviceGroundPosition = layout.AttackSpawn + Vector3.Up * DemolitionDeviceGroundHeight;
        _demolitionDeviceLastCarrierPosition = _demolitionDeviceGroundPosition;
        _demolitionDeviceLifecycle.BeginGrounded();
        BuildDemolitionDeviceVisual();
        AssignInitialDemolitionDeviceCarrier();
        SyncDemolitionDeviceVisual();
    }

    private void BuildDemolitionDeviceVisual()
    {
        if (IsInstanceValid(_demolitionDevice))
        {
            return;
        }
        _demolitionDevice = new Node3D
        {
            Name = $"DemolitionDevice_{_demolitionMatch.CurrentRound:00}",
            Position = _demolitionDeviceGroundPosition
        };
        _demolitionArena!.Root.AddChild(_demolitionDevice);
        var authoredVisual = CombatModelLibrary.InstantiateDemolitionDevice();
        _demolitionDevice.AddChild(authoredVisual.Root);
        _demolitionDeviceBeacon = new OmniLight3D
        {
            Name = "DeviceBeacon",
            Position = new Vector3(0.0f, 0.15f, 0.0f),
            LightColor = new Color(1.0f, 0.32f, 0.025f),
            LightEnergy = 2.4f,
            OmniRange = 6.0f,
            ShadowEnabled = false
        };
        _demolitionDevice.AddChild(_demolitionDeviceBeacon);
    }

    private void AssignInitialDemolitionDeviceCarrier()
    {
        var attackers = LivingDemolitionAttackers().ToList();
        // The opening device belongs to an autonomous teammate whenever one is
        // available.  Randomly handing it to the human made every escort fall back to
        // the player formation and left the AI carrier assignment undefined at spawn.
        var automatedAttackers = attackers
            .Where(IsAutomatedDemolitionAttacker)
            .ToList();
        var assignmentPool = automatedAttackers.Count > 0 ? automatedAttackers : attackers;
        var memberIds = assignmentPool
            .Select(DemolitionMemberId)
            .Where(memberId => memberId is not null)
            .Select(memberId => memberId!)
            .ToArray();
        var selectedId = _demolitionDeviceLifecycle.AssignRandomPickupRunner(
            memberIds,
            _rng.Randi());
        var selectedCarrier = ResolveDemolitionAttacker(selectedId);
        if (selectedCarrier is not null && TryPickupDemolitionDevice(selectedCarrier))
        {
            return;
        }
        ApplyDemolitionDevicePickupAssignment(selectedCarrier);
    }

    private void EnsureDemolitionDevicePickupRunner()
    {
        if (!_demolitionDeviceLifecycle.IsGrounded)
        {
            return;
        }
        var runner = ResolveDemolitionAttacker(_demolitionDeviceLifecycle.PickupRunnerMemberId);
        if (IsDemolitionDeviceRunnerEligible(runner))
        {
            ApplyDemolitionDevicePickupAssignment(runner);
            return;
        }
        // Grounded hand-offs must prefer an autonomous runner.  Assigning a distant
        // living player is a no-op (we cannot drive the player to the device) and used
        // to make every AI escort that player while the device stayed on the floor.
        var replacement = NearestLivingAutomatedDemolitionAttacker(
                _demolitionDeviceGroundPosition,
                runner)
            ?? (IsDemolitionDeviceRunnerEligible(runner) ? runner : null)
            ?? NearestLivingDemolitionAttackerWithinPickupRange(
                _demolitionDeviceGroundPosition,
                runner);
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
            ClearDemolitionSquadPostState(mate);
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
            _demolitionSquadAssignmentTargets.Remove(mate);
            ClearDemolitionSquadPostState(mate);
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
        // 掉在墙内则向最近空旷侧探1.5m，避免全员对墙罚站
        dropOrigin = FindFreeDemolitionDropPosition(dropOrigin);
        _demolitionDeviceGroundPosition = dropOrigin + Vector3.Up * DemolitionDeviceGroundHeight;
        var replacement = NearestLivingAutomatedDemolitionAttacker(dropOrigin, carrier)
            ?? NearestLivingDemolitionAttackerWithinPickupRange(dropOrigin, carrier);
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
        // 立即重算策略，不等1.5s，避免新Runner拿包后全员原地
        _demolitionStrategyRemaining = 0.0f;
    }

    private Vector3 FindFreeDemolitionDropPosition(Vector3 origin)
    {
        var layout = DemolitionLayout();
        // 若掉落点在墙体内，向四方向侧探1.5m找空旷点
        if (!IsPointInsideDemolitionGeometry(origin, layout))
        {
            return origin;
        }
        var offsets = new Vector3[] { new(1.5f, 0, 0), new(-1.5f, 0, 0), new(0, 0, 1.5f), new(0, 0, -1.5f) };
        foreach (var offset in offsets)
        {
            var candidate = origin + offset;
            if (!IsPointInsideDemolitionGeometry(candidate, layout))
            {
                return candidate;
            }
        }
        return origin;
    }

    private bool IsPointInsideDemolitionGeometry(Vector3 point, DemolitionArenaLayout layout)
    {
        const float horizontalPadding = 0.6f;
        const float bodyProbeHeight = 0.9f;
        var probePoint = point + Vector3.Up * bodyProbeHeight;

        // Probe at body height so walkable floors and low steps do not displace the dropped device.
        foreach (var box in layout.CollisionBoxes)
        {
            var basis = new Basis(Quaternion.FromEuler(box.Rotation));
            if (PointInsideExpandedDemolitionBox(
                    probePoint,
                    box.Center,
                    basis,
                    box.Size * 0.5f,
                    horizontalPadding))
            {
                return true;
            }
        }
        foreach (var prop in layout.Props)
        {
            var propBasis = new Basis(Vector3.Up, prop.Yaw);
            for (var pieceIndex = 0; pieceIndex < prop.CollisionPieceCount; pieceIndex++)
            {
                var piece = prop.CollisionPieceAt(pieceIndex);
                var center = prop.Position + propBasis * (piece.Offset * prop.Scale);
                var basis = propBasis * new Basis(Quaternion.FromEuler(piece.Rotation));
                var half = piece.Size * Mathf.Abs(prop.Scale) * 0.5f;
                if (PointInsideExpandedDemolitionBox(
                        probePoint,
                        center,
                        basis,
                        half,
                        horizontalPadding))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool PointInsideExpandedDemolitionBox(
        Vector3 point,
        Vector3 center,
        Basis basis,
        Vector3 half,
        float horizontalPadding)
    {
        var local = basis.Inverse() * (point - center);
        return Mathf.Abs(local.X) < half.X + horizontalPadding
            && Mathf.Abs(local.Y) < half.Y
            && Mathf.Abs(local.Z) < half.Z + horizontalPadding;
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
            _demolitionDevice.GlobalBasis = Basis.Identity;
            SetDemolitionDeviceBeacon(active: true, energy: 2.4f, range: 6.0f);
        }
        else if (_demolitionDeviceLifecycle.IsCarried)
        {
            var carrier = ResolveDemolitionAttacker(_demolitionDeviceLifecycle.CarrierMemberId);
            if (IsInstanceValid(carrier))
            {
                _demolitionDevice.GlobalPosition = carrier!.GlobalPosition
                    + Vector3.Up * DemolitionDeviceCarryHeight
                    + carrier.GlobalBasis.X * DemolitionDeviceCarryOffset
                    + carrier.GlobalBasis.Z * DemolitionDeviceCarryBackOffset;
                _demolitionDevice.GlobalBasis = carrier.GlobalBasis
                    * new Basis(Vector3.Up, Mathf.Pi * 0.5f);
            }
            SetDemolitionDeviceBeacon(active: true, energy: 0.8f, range: 2.8f);
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

    private Node3D? NearestLivingAutomatedDemolitionAttacker(Vector3 origin, Node3D? excluded)
        => LivingDemolitionAttackers()
            .Where(candidate => candidate != excluded && IsAutomatedDemolitionAttacker(candidate))
            .OrderBy(candidate => candidate.GlobalPosition.DistanceSquaredTo(origin))
            .ThenBy(candidate => DemolitionMemberId(candidate), System.StringComparer.Ordinal)
            .FirstOrDefault();

    private Node3D? NearestLivingDemolitionAttackerWithinPickupRange(
        Vector3 origin,
        Node3D? excluded)
        => LivingDemolitionAttackers()
            .Where(candidate => candidate != excluded
                && !IsAutomatedDemolitionAttacker(candidate)
                && HorizontalDistance(candidate.GlobalPosition, origin) <= DemolitionDevicePickupRadius
                && Mathf.Abs(candidate.GlobalPosition.Y - origin.Y) < 2.0f)
            .OrderBy(candidate => candidate.GlobalPosition.DistanceSquaredTo(origin))
            .ThenBy(candidate => DemolitionMemberId(candidate), System.StringComparer.Ordinal)
            .FirstOrDefault();

    private static bool IsAutomatedDemolitionAttacker(Node3D? actor)
        => actor switch
        {
            TacticalPlayer => false,
            SquadMate mate => !mate.IsNetworkProxy && !mate.IsHumanProxy,
            EnemyOperator opponent => !opponent.IsNetworkProxy && !opponent.IsHumanProxy,
            _ => false
        };

    private bool IsDemolitionDeviceRunnerEligible(Node3D? runner)
        => IsLivingDemolitionAttacker(runner)
            && (IsAutomatedDemolitionAttacker(runner!)
                || IsWithinDemolitionDevicePickupRange(runner!));

    private bool IsWithinDemolitionDevicePickupRange(Node3D runner)
        => HorizontalDistance(runner.GlobalPosition, _demolitionDeviceGroundPosition)
                <= DemolitionDevicePickupRadius
            && Mathf.Abs(runner.GlobalPosition.Y - _demolitionDeviceGroundPosition.Y) < 2.0f;

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
        if (TryParseDemolitionMateSlot(memberId, out var slot))
        {
            if (LocalDemolitionSide != DemolitionTeam.Attackers)
            {
                return null;
            }
            for (var index = 0; index < _squadMates.Count; index++)
            {
                var mate = _squadMates[index];
                if (IsInstanceValid(mate) && mate.SquadSlot == slot)
                {
                    return mate;
                }
            }
            return null;
        }
        if (LocalDemolitionSide != DemolitionTeam.Defenders)
        {
            return null;
        }
        for (var index = 0; index < _demolitionOpponents.Count; index++)
        {
            var opponent = _demolitionOpponents[index];
            if (IsInstanceValid(opponent)
                && string.Equals(
                    opponent.Name.ToString(),
                    memberId,
                    StringComparison.Ordinal))
            {
                return opponent;
            }
        }
        return null;
    }

    private static bool TryParseDemolitionMateSlot(string? memberId, out int slot)
    {
        slot = -1;
        return memberId is not null
            && memberId.StartsWith("MATE:", System.StringComparison.Ordinal)
            && int.TryParse(memberId.AsSpan(5), out slot);
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
        return !_demolitionDevicePlanted
            && LocalDemolitionSide == DemolitionTeam.Attackers
            && ((TryParseDemolitionMateSlot(
                        _demolitionDeviceLifecycle.CarrierMemberId,
                        out var carrierSlot)
                    && carrierSlot == mate.SquadSlot)
                || (TryParseDemolitionMateSlot(
                        _demolitionDeviceLifecycle.PickupRunnerMemberId,
                        out var runnerSlot)
                    && runnerSlot == mate.SquadSlot));
    }

    private bool HasDemolitionSquadObjectiveDuty(SquadMate mate)
        => IsDemolitionSquadDeviceObjectiveMate(mate)
            || ReferenceEquals(_demolitionSquadObjectiveMate, mate);

    /// <summary>
    /// Resolves the live device leader without assuming that the leader is an AI mate.
    /// During the grounded phase the pickup runner is the tactical leader; once picked
    /// up, the carrier is.  Keeping this lookup in one place prevents followers from
    /// silently falling back to the human formation anchor when the device belongs to
    /// the player or when a hand-off is still being applied.
    /// </summary>
    private Node3D? ResolveDemolitionDeviceLeader()
    {
        if (!_demolitionMode
            || !_demolitionRoundActive
            || _demolitionDevicePlanted
            || LocalDemolitionSide != DemolitionTeam.Attackers)
        {
            return null;
        }

        var memberId = _demolitionDeviceLifecycle.IsCarried
            ? _demolitionDeviceLifecycle.CarrierMemberId
            : _demolitionDeviceLifecycle.IsGrounded
                ? _demolitionDeviceLifecycle.PickupRunnerMemberId
                : null;
        // A grounded player assignment is not a leader yet: unlike an AI runner, the
        // game cannot move the player to the device.  Once the player actually picks it
        // up, the carried phase resolves them normally.
        if (_demolitionDeviceLifecycle.IsGrounded && memberId == "PLAYER")
        {
            return null;
        }
        var resolved = ResolveDemolitionAttacker(memberId);
        if (IsLivingDemolitionAttacker(resolved))
        {
            return resolved;
        }
        return null;
    }

    internal bool TryGetDemolitionObjectiveDestination(
        SquadMate mate,
        out Vector3 destination)
    {
        destination = default;
        if (!ShouldPrioritizeDemolitionObjective(mate))
        {
            return false;
        }

        var layout = DemolitionLayout();
        if (_demolitionDeviceLifecycle.IsGrounded)
        {
            destination = _demolitionDeviceGroundPosition;
            return true;
        }

        if (_demolitionDeviceLifecycle.IsCarried)
        {
            var siteIndex = Mathf.Clamp(
                _demolitionAttackerPlan?.PrimarySiteIndex ?? _demolitionEnemyTargetSite,
                0,
                layout.SitePositions.Count - 1);
            destination = layout.SitePositions[siteIndex];
            return true;
        }

        if (_demolitionDevicePlanted
            && LocalDemolitionSide == DemolitionTeam.Defenders
            && _demolitionActiveSite >= 0)
        {
            destination = layout.SitePositions[
                Mathf.Clamp(_demolitionActiveSite, 0, layout.SitePositions.Count - 1)];
            return true;
        }

        return false;
    }

    private bool ShouldPrioritizeDemolitionObjective(SquadMate mate)
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
        if (ownsAttackObjective)
        {
            // The device runner/carrier owns the execute. Contact may trigger
            // opportunistic fire, but it must never replace the route to the device
            // or plant site with a generic combat maneuver.
            _demolitionSquadCombatBreakoffs.Remove(mate);
            return true;
        }
        if (ShouldHardCommitDemolitionSquadObjective(mate))
        {
            _demolitionSquadCombatBreakoffs.Remove(mate);
            return true;
        }
        return !ShouldYieldDemolitionSquadToCombat(mate);
    }

    internal bool TryGetDemolitionEscortTarget(
        SquadMate mate,
        out Node3D leader,
        out bool objectivePriority)
    {
        leader = null!;
        objectivePriority = false;
        if (!_demolitionMode
            || !_demolitionRoundActive
            || mate.IsDowned
            || mate.IsBodyBag
            || _demolitionDevicePlanted)
        {
            return false;
        }
        if (IsDemolitionSquadDeviceObjectiveMate(mate)
            || LocalDemolitionSide != DemolitionTeam.Attackers)
        {
            return false;
        }

        var resolved = ResolveDemolitionDeviceLeader();
        if (resolved is null || resolved == mate)
        {
            return false;
        }
        leader = resolved;
        objectivePriority = !ShouldYieldDemolitionSquadToCombat(mate);
        return true;
    }

    internal bool TryGetDemolitionEscortTarget(SquadMate mate, out Node3D leader)
        => TryGetDemolitionEscortTarget(mate, out leader, out _);

    private bool ShouldHardCommitDemolitionSquadObjective(SquadMate mate)
    {
        if (!_demolitionRoundActive
            || mate != _demolitionSquadObjectiveMate
            || mate.IsDowned
            || mate.IsBodyBag)
        {
            return false;
        }

        var layout = DemolitionLayout();
        var siteIndex = _demolitionDevicePlanted
            ? _demolitionActiveSite
            : _demolitionSquadObjectiveSite;
        if (siteIndex < 0 || siteIndex >= layout.SitePositions.Count)
        {
            return false;
        }
        var distance = HorizontalDistance(mate.GlobalPosition, layout.SitePositions[siteIndex]);
        return _demolitionDevicePlanted
            ? DemolitionStrategyPlanner.RequiresUrgentDefuseCommit(
                _demolitionRemaining,
                distance,
                _demolitionSquadDefuseProgress)
            : DemolitionStrategyPlanner.RequiresUrgentPlantCommit(
                _demolitionRemaining,
                distance,
                _demolitionSquadPlantProgress);
    }

    private static bool IsAutonomousDemolitionSquadMate(SquadMate mate)
        => !mate.IsHumanProxy && !mate.IsNetworkProxy;

    private bool ShouldYieldDemolitionSquadToCombat(SquadMate mate)
    {
        if (!IsInstanceValid(mate) || mate.IsDowned || mate.IsBodyBag)
        {
            _demolitionSquadCombatBreakoffs.Remove(mate);
            return false;
        }

        var alreadyYielding = _demolitionSquadCombatBreakoffs.Contains(mate);
        var range = alreadyYielding
            ? DemolitionCombatResumeRange
            : DemolitionCombatEngageRange;
        if (mate.HasDemolitionThreatWithin(range))
        {
            _demolitionSquadCombatBreakoffs.Add(mate);
            return true;
        }
        _demolitionSquadCombatBreakoffs.Remove(mate);
        return false;
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
        if (_demolitionSquadObjectiveMate != carrier)
        {
            _demolitionSquadPlantProgress = 0.0f;
        }
        _demolitionSquadObjectiveMate = carrier;
        if (carrier is null || carrier.IsDowned || carrier.IsBodyBag)
        {
            _demolitionSquadPlantProgress = 0.0f;
            return true;
        }
        if (!IsAutonomousDemolitionSquadMate(carrier))
        {
            // Remote humans own movement and planting through their input/RPC stream.
            // The world coordinator must track their ownership without driving them.
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
        if (carrier.IsRevivingFriendly)
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
