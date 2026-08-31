using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace OperationSteelTide;

public enum DemolitionStrategyPhase
{
    Opening,
    PostPlant
}

public enum DemolitionOpeningPattern
{
    FullExecute,
    SplitPressure
}

public enum DemolitionDuty
{
    Entry,
    Support,
    Recon,
    AnchorA,
    AnchorB,
    MidControl,
    Rotate,
    Retake,
    Flank,
    Defuse,
    CoverDefuser,
    SiteGuard,
    Crossfire,
    Lurk
}

public readonly record struct DemolitionAgentSnapshot(
    string MemberId,
    DemolitionTeam Team,
    OperatorRole Role,
    float HealthRatio,
    float WeaponRange,
    bool Alive,
    bool Downed,
    float PositionX,
    float PositionZ);

public readonly record struct DemolitionAssignment(
    string MemberId,
    DemolitionDuty Duty,
    int SiteIndex,
    string TargetKey,
    string Reason);

public sealed record DemolitionStrategyPlan(
    DemolitionTeam Team,
    DemolitionStrategyPhase Phase,
    int PrimarySiteIndex,
    DemolitionOpeningPattern OpeningPattern,
    IReadOnlyList<DemolitionAssignment> Assignments,
    string Callout);

/// <summary>
/// Pure deterministic team planner. It considers role, health, weapon range, survival,
/// and position before assigning opening or retake duties.
/// </summary>
public sealed class DemolitionStrategyPlanner
{
    private const float AttackerSiteThreatRadius = 38.0f;
    private const float AttackerSiteThreatPenalty = 52.0f;
    private const float AttackerSiteSwitchPenalty = 40.0f;
    private const float AttackerRoundSitePreference = 24.0f;
    private const float AttackerUrgentThresholdSeconds = 35.0f;
    private const float AttackerPlantAndSettleSeconds = 5.4f;
    private const float AttackerEstimatedMoveSpeed = 5.1f;
    internal const float PlantDurationSeconds = 3.4f;
    internal const float PlantMoveSpeed = 5.1f;
    internal const float PlantStoppingDistance = 2.15f;
    internal const float PlantCommitBufferSeconds = 1.5f;
    internal const float DefuseDurationSeconds = 7.0f;
    internal const float DefuseMoveSpeed = 5.3f;
    internal const float DefuseStoppingDistance = 2.15f;
    internal const float DefuseCommitBufferSeconds = 1.25f;

    public DemolitionStrategyPlan Plan(
        DemolitionTeam team,
        DemolitionStrategyPhase phase,
        IReadOnlyList<DemolitionAgentSnapshot> members,
        int plantedSiteIndex = -1,
        IReadOnlyList<DemolitionAgentSnapshot>? knownOpponents = null,
        int strategySeed = 0,
        IReadOnlyList<Vector2>? siteCenters = null,
        float remainingSeconds = 100.0f,
        string? objectiveMemberId = null,
        int committedSiteIndex = -1,
        IReadOnlyList<float>? objectiveRouteLengths = null,
        bool lockCommittedSite = false)
    {
        siteCenters ??= DemolitionArenaLayout.LocalSiteCenters;
        var available = members
            .Where(member => member.Team == team && member.Alive && !member.Downed)
            .OrderBy(member => member.MemberId, StringComparer.Ordinal)
            .ToList();
        if (available.Count == 0)
        {
            return new DemolitionStrategyPlan(team, phase, Math.Max(0, plantedSiteIndex),
                DemolitionOpeningPattern.FullExecute,
                Array.Empty<DemolitionAssignment>(), "NO OPERATORS AVAILABLE");
        }

        if (team == DemolitionTeam.Attackers)
        {
            return phase == DemolitionStrategyPhase.PostPlant && plantedSiteIndex >= 0
                ? PlanAttackerPostPlant(available, plantedSiteIndex)
                : PlanAttackerOpening(
                    available,
                    knownOpponents,
                    strategySeed,
                    siteCenters,
                    remainingSeconds,
                    objectiveMemberId,
                    committedSiteIndex,
                    objectiveRouteLengths,
                    lockCommittedSite);
        }
        return phase == DemolitionStrategyPhase.PostPlant && plantedSiteIndex >= 0
            ? PlanDefenderRetake(available, plantedSiteIndex, siteCenters, remainingSeconds)
            : PlanDefenderOpening(available, knownOpponents, siteCenters);
    }

    /// <summary>
    /// Estimates the last safe start for a defuse without pathfinding. Runtime callers
    /// pass the already known actor/site distance, keeping the per-frame decision O(1).
    /// </summary>
    internal static float EstimateDefuseCompletionSeconds(
        float distanceToDevice,
        float channelProgress)
    {
        var travelDistance = Math.Max(0.0f, distanceToDevice - DefuseStoppingDistance);
        var travelSeconds = travelDistance / DefuseMoveSpeed;
        var channelSeconds = (1.0f - Math.Clamp(channelProgress, 0.0f, 1.0f))
            * DefuseDurationSeconds;
        return travelSeconds + channelSeconds;
    }

    internal static bool RequiresUrgentDefuseCommit(
        float secondsRemaining,
        float distanceToDevice,
        float channelProgress)
        => secondsRemaining <= EstimateDefuseCompletionSeconds(
                distanceToDevice,
                channelProgress)
            + DefuseCommitBufferSeconds;

    internal static float EstimatePlantCompletionSeconds(
        float distanceToSite,
        float channelProgress)
    {
        var travelDistance = Math.Max(0.0f, distanceToSite - PlantStoppingDistance);
        var travelSeconds = travelDistance / PlantMoveSpeed;
        var channelSeconds = (1.0f - Math.Clamp(channelProgress, 0.0f, 1.0f))
            * PlantDurationSeconds;
        return travelSeconds + channelSeconds;
    }

    internal static bool RequiresUrgentPlantCommit(
        float secondsRemaining,
        float distanceToSite,
        float channelProgress)
        => secondsRemaining <= EstimatePlantCompletionSeconds(
                distanceToSite,
                channelProgress)
            + PlantCommitBufferSeconds;

    /// <summary>
    /// Chooses an execute around the device runner rather than the team centroid. Actual
    /// authored-route cost constrains a stable per-round site preference, known defenders
    /// can justify a rotation, and the committed site adds enough hysteresis to prevent a
    /// 1.5-second strategy refresh from causing a cross-map U-turn.
    /// Under clock pressure a reachable commitment is preserved and only an impossible
    /// plant is redirected to the shortest site that can still finish.
    /// </summary>
    private static int ChooseAttackerSite(
        List<DemolitionAgentSnapshot> members,
        IReadOnlyList<DemolitionAgentSnapshot>? knownOpponents,
        IReadOnlyList<Vector2> siteCenters,
        float remainingSeconds = 100.0f,
        int strategySeed = 0,
        string? objectiveMemberId = null,
        int committedSiteIndex = -1,
        IReadOnlyList<float>? objectiveRouteLengths = null,
        bool lockCommittedSite = false)
    {
        var objective = members.FirstOrDefault(member => string.Equals(
            member.MemberId,
            objectiveMemberId,
            StringComparison.Ordinal));
        var hasObjective = !string.IsNullOrWhiteSpace(objective.MemberId);
        var originX = hasObjective ? objective.PositionX : members.Average(member => member.PositionX);
        var originZ = hasObjective ? objective.PositionZ : members.Average(member => member.PositionZ);
        var routeLengths = new float[siteCenters.Count];
        for (var site = 0; site < siteCenters.Count; site++)
        {
            var center = siteCenters[site];
            var dx = originX - center.X;
            var dz = originZ - center.Y;
            var geometricLength = MathF.Sqrt(dx * dx + dz * dz);
            if (objectiveRouteLengths is null)
            {
                routeLengths[site] = geometricLength;
            }
            else if (site < objectiveRouteLengths.Count
                && float.IsFinite(objectiveRouteLengths[site])
                && objectiveRouteLengths[site] >= 0.0f)
            {
                routeLengths[site] = objectiveRouteLengths[site];
            }
            else
            {
                routeLengths[site] = float.PositiveInfinity;
            }
        }

        var committedSite = committedSiteIndex >= 0 && committedSiteIndex < siteCenters.Count
            ? committedSiteIndex
            : -1;
        if (lockCommittedSite && committedSite >= 0)
        {
            return committedSite;
        }
        if (remainingSeconds < AttackerUrgentThresholdSeconds)
        {
            if (committedSite >= 0 && CanFinishPlant(routeLengths[committedSite], remainingSeconds))
            {
                return committedSite;
            }

            var feasibleSite = -1;
            var feasibleLength = float.PositiveInfinity;
            for (var site = 0; site < routeLengths.Length; site++)
            {
                if (CanFinishPlant(routeLengths[site], remainingSeconds)
                    && routeLengths[site] < feasibleLength)
                {
                    feasibleSite = site;
                    feasibleLength = routeLengths[site];
                }
            }
            if (feasibleSite >= 0)
            {
                return feasibleSite;
            }
        }

        var hash = Math.Abs((long)strategySeed * 0x9e3779b1L);
        var seededSite = (int)(hash % Math.Max(1, siteCenters.Count));
        var scores = new float[siteCenters.Count];
        for (var site = 0; site < scores.Length; site++)
        {
            scores[site] = routeLengths[site]
                + (committedSite < 0
                    ? site == seededSite
                        ? -AttackerRoundSitePreference
                        : AttackerRoundSitePreference
                    : 0.0f)
                + (committedSite >= 0 && site != committedSite
                    ? AttackerSiteSwitchPenalty
                    : 0.0f);
            if (knownOpponents is null)
            {
                continue;
            }
            var center = siteCenters[site];
            foreach (var opponent in knownOpponents
                .GroupBy(opponent => opponent.MemberId, StringComparer.Ordinal)
                .Select(group => group.First()))
            {
                var dx = opponent.PositionX - center.X;
                var dz = opponent.PositionZ - center.Y;
                var distance = MathF.Sqrt(dx * dx + dz * dz);
                if (distance < AttackerSiteThreatRadius)
                {
                    scores[site] += AttackerSiteThreatPenalty
                        * (1.0f - distance / AttackerSiteThreatRadius);
                }
            }
        }

        var selected = 0;
        for (var site = 1; site < scores.Length; site++)
        {
            if (scores[site] < scores[selected])
            {
                selected = site;
            }
        }
        return selected;
    }

    private static bool CanFinishPlant(float routeLength, float remainingSeconds)
        => routeLength / AttackerEstimatedMoveSpeed + AttackerPlantAndSettleSeconds
            <= remainingSeconds;

    /// <summary>The site currently under contact, driving pre-plant defensive rotation.</summary>
    private static int ThreatenedSite(
        IReadOnlyList<DemolitionAgentSnapshot>? knownOpponents,
        IReadOnlyList<Vector2> siteCenters)
    {
        if (knownOpponents is null || knownOpponents.Count == 0)
        {
            return -1;
        }
        var best = -1;
        var bestDistance = float.PositiveInfinity;
        for (var site = 0; site < siteCenters.Count; site++)
        {
            foreach (var opponent in knownOpponents)
            {
                if (!IsNearSite(opponent, site, 34.0f, siteCenters))
                {
                    continue;
                }
                var center = siteCenters[site];
                var dx = opponent.PositionX - center.X;
                var dz = opponent.PositionZ - center.Y;
                var distance = dx * dx + dz * dz;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = site;
                }
            }
        }
        return best;
    }

    private static bool IsNearSite(
        DemolitionAgentSnapshot member,
        int site,
        float radius,
        IReadOnlyList<Vector2> siteCenters)
    {
        var center = siteCenters[site];
        var dx = member.PositionX - center.X;
        var dz = member.PositionZ - center.Y;
        return dx * dx + dz * dz <= radius * radius;
    }

    private static DemolitionOpeningPattern ChooseOpeningPattern(
        IReadOnlyCollection<DemolitionAgentSnapshot> members,
        int strategySeed)
    {
        if (members.Count < 4)
        {
            return DemolitionOpeningPattern.FullExecute;
        }

        // A round-stable seed prevents the live 1.5 second strategy refresh from making
        // operators reverse course. Two rounds commit as a group, then one applies 3-2
        // pressure, giving the opponent readable but non-repetitive team behavior.
        var cycle = Math.Abs((long)strategySeed) % 3L;
        return cycle == 0L
            ? DemolitionOpeningPattern.SplitPressure
            : DemolitionOpeningPattern.FullExecute;
    }

    private static DemolitionStrategyPlan PlanAttackerOpening(
        List<DemolitionAgentSnapshot> members,
        IReadOnlyList<DemolitionAgentSnapshot>? knownOpponents,
        int strategySeed,
        IReadOnlyList<Vector2> siteCenters,
        float remainingSeconds = 100.0f,
        string? objectiveMemberId = null,
        int committedSiteIndex = -1,
        IReadOnlyList<float>? objectiveRouteLengths = null,
        bool lockCommittedSite = false)
    {
        var primarySite = ChooseAttackerSite(
            members,
            knownOpponents,
            siteCenters,
            remainingSeconds,
            strategySeed,
            objectiveMemberId,
            committedSiteIndex,
            objectiveRouteLengths,
            lockCommittedSite);
        var openingPattern = remainingSeconds < 25.0f ? DemolitionOpeningPattern.FullExecute : ChooseOpeningPattern(members, strategySeed);

        var entry = members
            .OrderByDescending(member => EntryScore(member))
            .ThenBy(member => member.MemberId, StringComparer.Ordinal)
            .First();
        var remaining = members.Where(member => member.MemberId != entry.MemberId).ToList();
        var assignments = new List<DemolitionAssignment>
        {
            new(entry.MemberId, DemolitionDuty.Entry, primarySite,
                primarySite == 0 ? "attack_entry_a" : "attack_entry_b",
                $"entry score {EntryScore(entry):0.00}: health {entry.HealthRatio:0.00}, range {entry.WeaponRange:0}")
        };

        if (openingPattern == DemolitionOpeningPattern.SplitPressure && remaining.Count >= 3)
        {
            var recon = remaining
                .OrderByDescending(member => ReconScore(member))
                .ThenBy(member => member.MemberId, StringComparer.Ordinal)
                .First();
            remaining.RemoveAll(member => member.MemberId == recon.MemberId);
            assignments.Add(new DemolitionAssignment(
                recon.MemberId,
                DemolitionDuty.Recon,
                primarySite,
                "attack_mid_recon",
                $"recon score {ReconScore(recon):0.00}: role {recon.Role}, range {recon.WeaponRange:0}"));

            var secondarySite = 1 - primarySite;
            var flanker = remaining
                .OrderByDescending(member => member.HealthRatio * 0.55f + member.WeaponRange / 300.0f)
                .ThenBy(member => member.MemberId, StringComparer.Ordinal)
                .First();
            remaining.RemoveAll(member => member.MemberId == flanker.MemberId);
            assignments.Add(new DemolitionAssignment(
                flanker.MemberId,
                DemolitionDuty.Flank,
                secondarySite,
                secondarySite == 0 ? "attack_entry_a" : "attack_entry_b",
                $"secondary pressure at health {flanker.HealthRatio:0.00}, range {flanker.WeaponRange:0}"));
        }

        foreach (var member in remaining)
        {
            assignments.Add(new DemolitionAssignment(
                member.MemberId,
                DemolitionDuty.Support,
                primarySite,
                primarySite == 0 ? "attack_support_a" : "attack_support_b",
                $"support preserves {member.Role} at health {member.HealthRatio:0.00}"));
        }

        return new DemolitionStrategyPlan(
            DemolitionTeam.Attackers,
            DemolitionStrategyPhase.Opening,
            primarySite,
            openingPattern,
            assignments,
            openingPattern == DemolitionOpeningPattern.FullExecute
                ? primarySite == 0 ? "GROUP EXECUTE A  //  FIVE COMMIT" : "GROUP EXECUTE B  //  FIVE COMMIT"
                : primarySite == 0 ? "3-2 SPLIT  //  HIT A  //  PRESSURE B" : "3-2 SPLIT  //  HIT B  //  PRESSURE A");
    }

    private static DemolitionStrategyPlan PlanAttackerPostPlant(
        List<DemolitionAgentSnapshot> members,
        int plantedSiteIndex)
    {
        var guard = members
            .OrderByDescending(member => member.HealthRatio + (member.Role == OperatorRole.Medic ? 0.16f : 0.0f))
            .ThenBy(member => member.MemberId, StringComparer.Ordinal)
            .First();
        var remaining = members.Where(member => member.MemberId != guard.MemberId).ToList();
        var crossfire = remaining
            .OrderByDescending(member => member.WeaponRange * Math.Max(0.35f, member.HealthRatio))
            .ThenBy(member => member.MemberId, StringComparer.Ordinal)
            .FirstOrDefault();
        var assignments = new List<DemolitionAssignment>
        {
            new(guard.MemberId, DemolitionDuty.SiteGuard, plantedSiteIndex,
                plantedSiteIndex == 0 ? "postplant_guard_a" : "postplant_guard_b",
                $"site guard health {guard.HealthRatio:0.00}, role {guard.Role}")
        };
        if (!string.IsNullOrEmpty(crossfire.MemberId))
        {
            assignments.Add(new DemolitionAssignment(
                crossfire.MemberId,
                DemolitionDuty.Crossfire,
                plantedSiteIndex,
                plantedSiteIndex == 0 ? "postplant_crossfire_a" : "postplant_crossfire_b",
                $"long crossfire range {crossfire.WeaponRange:0}"));
        }
        foreach (var member in remaining.Where(member => member.MemberId != crossfire.MemberId))
        {
            assignments.Add(new DemolitionAssignment(
                member.MemberId,
                DemolitionDuty.Lurk,
                plantedSiteIndex,
                plantedSiteIndex == 0 ? "postplant_lurk_a" : "postplant_lurk_b",
                $"late contact lurk at health {member.HealthRatio:0.00}"));
        }
        var siteName = plantedSiteIndex == 0 ? "A" : "B";
        return new DemolitionStrategyPlan(
            DemolitionTeam.Attackers,
            DemolitionStrategyPhase.PostPlant,
            plantedSiteIndex,
            DemolitionOpeningPattern.FullExecute,
            assignments,
            $"HOLD {siteName}  //  CROSSfire SET  //  WATCH ROTATE");
    }

    private static DemolitionStrategyPlan PlanDefenderOpening(
        List<DemolitionAgentSnapshot> members,
        IReadOnlyList<DemolitionAgentSnapshot>? knownOpponents,
        IReadOnlyList<Vector2> siteCenters)
    {
        var threatened = ThreatenedSite(knownOpponents, siteCenters);
        var assignments = new List<DemolitionAssignment>(members.Count);
        var ordered = members
            .OrderByDescending(member => member.WeaponRange)
            .ThenByDescending(member => member.HealthRatio)
            .ThenBy(member => member.MemberId, StringComparer.Ordinal)
            .ToList();

        for (var index = 0; index < ordered.Count; index++)
        {
            var member = ordered[index];
            var (duty, site, target) = DefenderOpeningPost(index, threatened);
            assignments.Add(new DemolitionAssignment(
                member.MemberId,
                duty,
                site,
                target,
                $"opening rank {index + 1}: health {member.HealthRatio:0.00}, range {member.WeaponRange:0}"));
        }

        return new DemolitionStrategyPlan(
            DemolitionTeam.Defenders,
            DemolitionStrategyPhase.Opening,
            threatened >= 0 ? threatened : 0,
            DemolitionOpeningPattern.FullExecute,
            assignments,
            threatened switch
            {
                0 => "CONTACT A  //  THREE STRONG  //  B ANCHOR",
                1 => "CONTACT B  //  THREE STRONG  //  A ANCHOR",
                _ => "HOLD BOTH SITES  //  ONE MID  //  FLEX ROTATE"
            });
    }

    private static (DemolitionDuty Duty, int Site, string Target) DefenderOpeningPost(int index, int threatened)
    {
        if (threatened >= 0)
        {
            var weakSite = 1 - threatened;
            return index switch
            {
                0 => (DemolitionDuty.MidControl, threatened, "defense_mid"),
                1 or 2 => threatened == 0
                    ? (DemolitionDuty.AnchorA, 0, "defense_anchor_a")
                    : (DemolitionDuty.AnchorB, 1, "defense_anchor_b"),
                3 => weakSite == 0
                    ? (DemolitionDuty.AnchorA, 0, "defense_anchor_a")
                    : (DemolitionDuty.AnchorB, 1, "defense_anchor_b"),
                _ => (DemolitionDuty.Rotate, threatened,
                    threatened == 0 ? "defense_rotate_a" : "defense_rotate_b")
            };
        }

        var duty = index switch
        {
            0 => DemolitionDuty.MidControl,
            1 or 3 => DemolitionDuty.AnchorA,
            2 or 4 => DemolitionDuty.AnchorB,
            _ => DemolitionDuty.Rotate
        };
        var site = duty == DemolitionDuty.AnchorA ? 0 : duty == DemolitionDuty.AnchorB ? 1 : index % 2;
        var target = duty switch
        {
            DemolitionDuty.AnchorA => "defense_anchor_a",
            DemolitionDuty.AnchorB => "defense_anchor_b",
            DemolitionDuty.MidControl => "defense_mid",
            _ => index % 2 == 0 ? "defense_rotate_a" : "defense_rotate_b"
        };
        return (duty, site, target);
    }

    private static DemolitionStrategyPlan PlanDefenderRetake(
        List<DemolitionAgentSnapshot> members,
        int plantedSiteIndex,
        IReadOnlyList<Vector2> siteCenters,
        float remainingSeconds)
    {
        var site = siteCenters[Math.Clamp(plantedSiteIndex, 0, siteCenters.Count - 1)];
        var siteX = site.X;
        var siteZ = site.Y;
        var nearestDistance = members.Min(member => Math.Sqrt(DistanceSquared(member, siteX, siteZ)));
        var urgent = RequiresUrgentDefuseCommit(
            remainingSeconds,
            (float)nearestDistance,
            channelProgress: 0.0f);
        var defuser = members
            .OrderBy(member => DistanceSquared(member, siteX, siteZ)
                + (urgent ? 0.0f : (1.0f - member.HealthRatio) * 90.0f))
            .ThenBy(member => member.MemberId, StringComparer.Ordinal)
            .First();
        var remaining = members.Where(member => member.MemberId != defuser.MemberId).ToList();
        var cover = remaining
            .OrderByDescending(member => member.WeaponRange * Math.Max(0.35f, member.HealthRatio))
            .ThenBy(member => member.MemberId, StringComparer.Ordinal)
            .FirstOrDefault();

        var assignments = new List<DemolitionAssignment>
        {
            new(defuser.MemberId, DemolitionDuty.Defuse, plantedSiteIndex,
                plantedSiteIndex == 0 ? "site_a" : "site_b",
                urgent
                    ? $"last-chance defuser: shortest travel {Math.Sqrt(DistanceSquared(defuser, siteX, siteZ)):0.0}"
                    : $"closest viable defuser: health {defuser.HealthRatio:0.00}, distance {Math.Sqrt(DistanceSquared(defuser, siteX, siteZ)):0.0}")
        };
        if (!string.IsNullOrEmpty(cover.MemberId))
        {
            assignments.Add(new DemolitionAssignment(
                cover.MemberId,
                DemolitionDuty.CoverDefuser,
                plantedSiteIndex,
                plantedSiteIndex == 0 ? "retake_cover_a" : "retake_cover_b",
                $"best cover weapon: range {cover.WeaponRange:0}, health {cover.HealthRatio:0.00}"));
        }

        var retakers = remaining.Where(member => member.MemberId != cover.MemberId).ToList();
        for (var index = 0; index < retakers.Count; index++)
        {
            var member = retakers[index];
            var flank = index % 3 == 2 && member.HealthRatio >= 0.5f;
            assignments.Add(new DemolitionAssignment(
                member.MemberId,
                flank ? DemolitionDuty.Flank : DemolitionDuty.Retake,
                plantedSiteIndex,
                flank
                    ? plantedSiteIndex == 0 ? "retake_flank_a" : "retake_flank_b"
                    : plantedSiteIndex == 0 ? "retake_entry_a" : "retake_entry_b",
                flank
                    ? $"healthy flanker at {member.HealthRatio:0.00}"
                    : $"retake pressure with range {member.WeaponRange:0}"));
        }

        var siteName = plantedSiteIndex == 0 ? "A" : "B";
        return new DemolitionStrategyPlan(
            DemolitionTeam.Defenders,
            DemolitionStrategyPhase.PostPlant,
            plantedSiteIndex,
            DemolitionOpeningPattern.FullExecute,
            assignments,
            $"RETAKE {siteName}  //  COVER DEFUSER  //  FLANK LATE");
    }

    private static float EntryScore(DemolitionAgentSnapshot member)
    {
        var role = member.Role == OperatorRole.Assault ? 0.42f : member.Role == OperatorRole.Medic ? -0.08f : 0.06f;
        var closeRange = Math.Clamp((135.0f - member.WeaponRange) / 135.0f, -0.3f, 0.55f);
        return member.HealthRatio * 0.82f + role + closeRange;
    }

    private static float ReconScore(DemolitionAgentSnapshot member)
    {
        var role = member.Role == OperatorRole.Recon ? 0.72f : member.Role == OperatorRole.Medic ? 0.08f : 0.0f;
        return role + member.WeaponRange / 260.0f + member.HealthRatio * 0.24f;
    }

    private static float DistanceSquared(DemolitionAgentSnapshot member, float x, float z)
    {
        var dx = member.PositionX - x;
        var dz = member.PositionZ - z;
        return dx * dx + dz * dz;
    }
}
