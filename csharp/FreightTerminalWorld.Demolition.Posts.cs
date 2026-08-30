using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    // Posts are tactical anchors, not terminal states.  A short dwell keeps the squad
    // readable in cover, then a low-frequency patrol hop makes sure a mate cannot remain
    // in Hold forever after the opening lane is complete.  No route query is performed
    // here; movement still goes through the cached demolition route in SquadNavigation.
    private const float DemolitionPostHoldDuration = 3.8f;
    private const float DemolitionPostPatrolMinimumDistance = 2.5f;

    private readonly Dictionary<SquadMate, string> _demolitionSquadActivePostTargets = new();
    private readonly Dictionary<SquadMate, float> _demolitionSquadPostHoldTimers = new();
    private readonly Dictionary<SquadMate, int> _demolitionSquadPostPatrolSteps = new();

    private void UpdateDemolitionSquadPosts(
        float delta = 0.0f,
        bool ignoreEscort = false,
        bool ignoreThreat = false)
    {
        if (!_demolitionRoundActive)
        {
            return;
        }

        var layout = DemolitionLayout();
        for (var index = 0; index < _squadMates.Count; index++)
        {
            var mate = _squadMates[index];
            if (!IsInstanceValid(mate))
            {
                continue;
            }
            if (!_demolitionSquadAssignmentTargets.TryGetValue(mate, out var assignmentKey)
                || mate.IsDowned
                || mate.IsBodyBag)
            {
                ClearDemolitionSquadPostState(mate);
                continue;
            }

            // Objective movement and carrier escort are resolved by SquadMate every
            // frame.  A post watchdog must never overwrite either responsibility.
            if (HasDemolitionSquadObjectiveDuty(mate))
            {
                ClearDemolitionSquadPostState(mate);
                continue;
            }
            if (!ignoreEscort && TryGetDemolitionEscortTarget(mate, out _))
            {
                _demolitionSquadPostHoldTimers[mate] = DemolitionPostHoldDuration;
                continue;
            }

            if (!_demolitionSquadActivePostTargets.TryGetValue(mate, out var activeKey))
            {
                activeKey = assignmentKey;
                _demolitionSquadActivePostTargets[mate] = activeKey;
            }
            var activeTarget = layout.StrategyTarget(activeKey);

            // If another system briefly put a demolition mate back into Follow, restore
            // its assigned tactical lane.  A stale Move aimed at a superseded objective
            // is the same ownership loss even though its enum still says Move.
            if (mate.Order == SquadOrder.Follow
                || mate.Order == SquadOrder.Move
                    && !mate.DemolitionMoveTargets(activeTarget))
            {
                mate.SetOrder(SquadOrder.Move, activeTarget);
            }

            if (mate.Order == SquadOrder.Move)
            {
                if (mate.GlobalPosition.DistanceTo(activeTarget) <= 3.0f)
                {
                    mate.SetOrder(SquadOrder.Hold, mate.GlobalPosition);
                    _demolitionSquadPostHoldTimers[mate] = DemolitionPostHoldDuration;
                }
                continue;
            }

            if (mate.Order != SquadOrder.Hold)
            {
                continue;
            }

            // A post may have been left behind while the carrier crossed the map.  Return
            // to the authored anchor before beginning another patrol hop.
            var assignmentTarget = layout.StrategyTarget(assignmentKey);
            if (activeKey == assignmentKey
                && mate.GlobalPosition.DistanceTo(assignmentTarget) > 3.0f)
            {
                _demolitionSquadActivePostTargets[mate] = assignmentKey;
                _demolitionSquadPostHoldTimers[mate] = DemolitionPostHoldDuration;
                mate.SetOrder(SquadOrder.Move, assignmentTarget);
                continue;
            }

            // Under contact, keep the anchored cover position.  The combat layer still
            // acquires and fires; this only prevents a patrol hop from pulling a mate
            // away while a hostile is nearby.
            if (!ignoreThreat && ShouldYieldDemolitionSquadToCombat(mate))
            {
                _demolitionSquadPostHoldTimers[mate] = Mathf.Max(
                    _demolitionSquadPostHoldTimers.GetValueOrDefault(mate),
                    1.2f);
                continue;
            }

            if (delta <= 0.0f)
            {
                // Deterministic diagnostics intentionally observe the conversion to Hold
                // without advancing the live patrol clock.
                continue;
            }

            var remaining = _demolitionSquadPostHoldTimers.GetValueOrDefault(
                mate,
                DemolitionPostHoldDuration) - delta;
            if (remaining > 0.0f)
            {
                _demolitionSquadPostHoldTimers[mate] = remaining;
                continue;
            }

            var patrolStep = _demolitionSquadPostPatrolSteps.GetValueOrDefault(mate) + 1;
            var patrolKey = DemolitionPostPatrolTargetKey(assignmentKey, mate.SquadSlot, patrolStep);
            var patrolTarget = layout.StrategyTarget(patrolKey);
            if (patrolKey == activeKey
                || mate.GlobalPosition.DistanceTo(patrolTarget) < DemolitionPostPatrolMinimumDistance)
            {
                // Some authored anchors are intentionally close together (the two B
                // attack anchors on the compact arena are only about 1.4 m apart).
                // Do not turn that valid layout into a permanent Hold loop: choose a
                // second authored lane before giving up this patrol tick.
                if (!TryFindDemolitionPostPatrolAlternative(
                        layout,
                        assignmentKey,
                        activeKey,
                        mate.SquadSlot,
                        patrolStep,
                        out patrolKey,
                        out patrolTarget))
                {
                    _demolitionSquadPostHoldTimers[mate] = DemolitionPostHoldDuration;
                    _demolitionSquadPostPatrolSteps[mate] = patrolStep;
                    continue;
                }
            }

            _demolitionSquadPostPatrolSteps[mate] = patrolStep;
            _demolitionSquadActivePostTargets[mate] = patrolKey;
            _demolitionSquadPostHoldTimers[mate] = DemolitionPostHoldDuration;
            mate.SetOrder(SquadOrder.Move, patrolTarget);
        }
    }

    private static string DemolitionPostPatrolTargetKey(
        string assignmentKey,
        int squadSlot,
        int patrolStep)
    {
        var evenStep = patrolStep % 2 == 0;
        return assignmentKey switch
        {
            "attack_entry_a" => evenStep ? "attack_entry_a" : "attack_support_a",
            "attack_support_a" => evenStep ? "attack_support_a" : "attack_entry_a",
            "attack_entry_b" => evenStep ? "attack_entry_b" : "attack_support_b",
            "attack_support_b" => evenStep ? "attack_support_b" : "attack_entry_b",
            "attack_mid_recon" => evenStep
                ? "attack_mid_recon"
                : squadSlot % 2 == 0 ? "attack_support_b" : "attack_support_a",
            "defense_anchor_a" => evenStep ? "defense_anchor_a" : "defense_rotate_a",
            "defense_rotate_a" => evenStep ? "defense_rotate_a" : "defense_anchor_a",
            "defense_anchor_b" => evenStep ? "defense_anchor_b" : "defense_rotate_b",
            "defense_rotate_b" => evenStep ? "defense_rotate_b" : "defense_anchor_b",
            "defense_mid" => evenStep
                ? "defense_mid"
                : squadSlot % 2 == 0 ? "defense_rotate_b" : "defense_rotate_a",
            "retake_entry_a" => (patrolStep % 3) switch
            {
                1 => "retake_cover_a",
                2 => "retake_flank_a",
                _ => "retake_entry_a"
            },
            "retake_cover_a" => (patrolStep % 3) switch
            {
                1 => "retake_flank_a",
                2 => "retake_entry_a",
                _ => "retake_cover_a"
            },
            "retake_flank_a" => (patrolStep % 3) switch
            {
                1 => "retake_entry_a",
                2 => "retake_cover_a",
                _ => "retake_flank_a"
            },
            "retake_entry_b" => (patrolStep % 3) switch
            {
                1 => "retake_cover_b",
                2 => "retake_flank_b",
                _ => "retake_entry_b"
            },
            "retake_cover_b" => (patrolStep % 3) switch
            {
                1 => "retake_flank_b",
                2 => "retake_entry_b",
                _ => "retake_cover_b"
            },
            "retake_flank_b" => (patrolStep % 3) switch
            {
                1 => "retake_entry_b",
                2 => "retake_cover_b",
                _ => "retake_flank_b"
            },
            "postplant_guard_a" => (patrolStep % 3) switch
            {
                1 => "postplant_crossfire_a",
                2 => "postplant_lurk_a",
                _ => "postplant_guard_a"
            },
            "postplant_crossfire_a" => (patrolStep % 3) switch
            {
                1 => "postplant_lurk_a",
                2 => "postplant_guard_a",
                _ => "postplant_crossfire_a"
            },
            "postplant_lurk_a" => (patrolStep % 3) switch
            {
                1 => "postplant_guard_a",
                2 => "postplant_crossfire_a",
                _ => "postplant_lurk_a"
            },
            "postplant_guard_b" => (patrolStep % 3) switch
            {
                1 => "postplant_crossfire_b",
                2 => "postplant_lurk_b",
                _ => "postplant_guard_b"
            },
            "postplant_crossfire_b" => (patrolStep % 3) switch
            {
                1 => "postplant_lurk_b",
                2 => "postplant_guard_b",
                _ => "postplant_crossfire_b"
            },
            "postplant_lurk_b" => (patrolStep % 3) switch
            {
                1 => "postplant_guard_b",
                2 => "postplant_crossfire_b",
                _ => "postplant_lurk_b"
            },
            "site_a" => (patrolStep % 3) switch
            {
                1 => "postplant_guard_a",
                2 => "postplant_crossfire_a",
                _ => "postplant_lurk_a"
            },
            "site_b" => (patrolStep % 3) switch
            {
                1 => "postplant_guard_b",
                2 => "postplant_crossfire_b",
                _ => "postplant_lurk_b"
            },
            _ => assignmentKey
        };
    }

    private static bool TryFindDemolitionPostPatrolAlternative(
        DemolitionArenaLayout layout,
        string assignmentKey,
        string activeKey,
        int squadSlot,
        int patrolStep,
        out string patrolKey,
        out Vector3 patrolTarget)
    {
        var candidates = assignmentKey switch
        {
            "attack_entry_b" or "attack_support_b" => squadSlot % 2 == 0
                ? new[] { "attack_mid_recon", "attack_entry_a", "attack_support_a" }
                : new[] { "attack_mid_recon", "attack_support_a", "attack_entry_a" },
            _ => new[]
            {
                DemolitionPostPatrolTargetKey(assignmentKey, squadSlot, patrolStep + 1),
                DemolitionPostPatrolTargetKey(assignmentKey, squadSlot, patrolStep + 2),
                "attack_mid_recon"
            }
        };
        for (var index = 0; index < candidates.Length; index++)
        {
            var candidate = candidates[index];
            if (candidate == activeKey || candidate == assignmentKey)
            {
                continue;
            }
            var target = layout.StrategyTarget(candidate);
            if (target.DistanceTo(layout.StrategyTarget(activeKey)) < DemolitionPostPatrolMinimumDistance
                || target.DistanceTo(layout.StrategyTarget(assignmentKey)) < DemolitionPostPatrolMinimumDistance)
            {
                continue;
            }
            patrolKey = candidate;
            patrolTarget = target;
            return true;
        }
        patrolKey = activeKey;
        patrolTarget = layout.StrategyTarget(activeKey);
        return false;
    }

    internal static bool ValidateDemolitionPostPatrolLayout(DemolitionArenaLayout layout)
    {
        foreach (var assignmentKey in DemolitionArenaLayout.StrategyTargetKeys)
        {
            var activeKey = assignmentKey;
            for (var patrolStep = 1; patrolStep <= 6; patrolStep++)
            {
                var patrolKey = DemolitionPostPatrolTargetKey(
                    assignmentKey,
                    squadSlot: patrolStep % 3 + 1,
                    patrolStep: patrolStep);
                var patrolTarget = layout.StrategyTarget(patrolKey);
                if (patrolKey == activeKey
                    || patrolTarget.DistanceTo(layout.StrategyTarget(activeKey))
                        < DemolitionPostPatrolMinimumDistance)
                {
                    if (!TryFindDemolitionPostPatrolAlternative(
                            layout,
                            assignmentKey,
                            activeKey,
                            patrolStep % 3 + 1,
                            patrolStep,
                            out patrolKey,
                            out patrolTarget))
                    {
                        return false;
                    }
                }
                activeKey = patrolKey;
            }
        }
        return true;
    }

    private void ClearDemolitionSquadPostState(SquadMate mate)
    {
        _demolitionSquadActivePostTargets.Remove(mate);
        _demolitionSquadPostHoldTimers.Remove(mate);
        _demolitionSquadPostPatrolSteps.Remove(mate);
    }

    private void ClearDemolitionSquadPostStates()
    {
        _demolitionSquadActivePostTargets.Clear();
        _demolitionSquadPostHoldTimers.Clear();
        _demolitionSquadPostPatrolSteps.Clear();
    }

    private void ClearDemolitionSquadMateState(SquadMate mate)
    {
        _demolitionSquadAssignmentTargets.Remove(mate);
        ClearDemolitionSquadPostState(mate);
        _demolitionSquadCombatBreakoffs.Remove(mate);
        ClearDemolitionSquadRoute(mate);
        ClearDemolitionSquadRouteFallback(mate);
        ClearDemolitionEscortLifecycleState(mate);
        if (ReferenceEquals(_demolitionSquadObjectiveMate, mate))
        {
            _demolitionSquadObjectiveMate = null;
            _demolitionSquadPlantProgress = 0.0f;
            _demolitionSquadDefuseProgress = 0.0f;
        }
    }
}
