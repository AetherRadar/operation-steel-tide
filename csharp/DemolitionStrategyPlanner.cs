using System;
using System.Collections.Generic;
using System.Linq;

namespace OperationSteelTide;

public enum DemolitionStrategyPhase
{
    Opening,
    PostPlant
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
    IReadOnlyList<DemolitionAssignment> Assignments,
    string Callout);

/// <summary>
/// Pure deterministic team planner. It considers role, health, weapon range, survival,
/// and position before assigning opening or retake duties.
/// </summary>
public sealed class DemolitionStrategyPlanner
{
    public DemolitionStrategyPlan Plan(
        DemolitionTeam team,
        DemolitionStrategyPhase phase,
        IReadOnlyList<DemolitionAgentSnapshot> members,
        int plantedSiteIndex = -1,
        IReadOnlyList<DemolitionAgentSnapshot>? knownOpponents = null)
    {
        var available = members
            .Where(member => member.Team == team && member.Alive && !member.Downed)
            .OrderBy(member => member.MemberId, StringComparer.Ordinal)
            .ToList();
        if (available.Count == 0)
        {
            return new DemolitionStrategyPlan(team, phase, Math.Max(0, plantedSiteIndex),
                Array.Empty<DemolitionAssignment>(), "NO OPERATORS AVAILABLE");
        }

        if (team == DemolitionTeam.Attackers)
        {
            return phase == DemolitionStrategyPhase.PostPlant && plantedSiteIndex >= 0
                ? PlanAttackerPostPlant(available, plantedSiteIndex)
                : PlanAttackerOpening(available, knownOpponents);
        }
        return phase == DemolitionStrategyPhase.PostPlant && plantedSiteIndex >= 0
            ? PlanDefenderRetake(available, plantedSiteIndex)
            : PlanDefenderOpening(available, knownOpponents);
    }

    /// <summary>
    /// Shared-intelligence site choice, the YaPB-style danger heuristic: with no contacts
    /// keep the loadout-based default, but once defenders are sighted around one site,
    /// attack the other.
    /// </summary>
    private static int ChooseAttackerSite(
        List<DemolitionAgentSnapshot> members,
        IReadOnlyList<DemolitionAgentSnapshot>? knownOpponents)
    {
        var averageRange = members.Average(member => member.WeaponRange);
        var reconWeight = members.Count(member => member.Role == OperatorRole.Recon) * 0.18f;
        var weakenedLeft = members.Count(member => member.PositionX < 0.0f && member.HealthRatio < 0.58f);
        var weakenedRight = members.Count(member => member.PositionX >= 0.0f && member.HealthRatio < 0.58f);
        var fallback = averageRange >= 135.0f || reconWeight > 0.0f && weakenedLeft <= weakenedRight ? 0 : 1;
        if (knownOpponents is null || knownOpponents.Count == 0)
        {
            return fallback;
        }
        var threats = new int[DemolitionArenaLayout.LocalSiteCenters.Length];
        foreach (var opponent in knownOpponents)
        {
            for (var site = 0; site < threats.Length; site++)
            {
                if (IsNearSite(opponent, site, 30.0f))
                {
                    threats[site]++;
                }
            }
        }
        if (threats[0] == threats[1])
        {
            return fallback;
        }
        return threats[0] < threats[1] ? 0 : 1;
    }

    /// <summary>The site currently under contact, driving pre-plant defensive rotation.</summary>
    private static int ThreatenedSite(IReadOnlyList<DemolitionAgentSnapshot>? knownOpponents)
    {
        if (knownOpponents is null || knownOpponents.Count == 0)
        {
            return -1;
        }
        var best = -1;
        var bestDistance = float.PositiveInfinity;
        for (var site = 0; site < DemolitionArenaLayout.LocalSiteCenters.Length; site++)
        {
            foreach (var opponent in knownOpponents)
            {
                if (!IsNearSite(opponent, site, 34.0f))
                {
                    continue;
                }
                var center = DemolitionArenaLayout.LocalSiteCenters[site];
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

    private static bool IsNearSite(DemolitionAgentSnapshot member, int site, float radius)
    {
        var center = DemolitionArenaLayout.LocalSiteCenters[site];
        var dx = member.PositionX - center.X;
        var dz = member.PositionZ - center.Y;
        return dx * dx + dz * dz <= radius * radius;
    }

    private static DemolitionStrategyPlan PlanAttackerOpening(
        List<DemolitionAgentSnapshot> members,
        IReadOnlyList<DemolitionAgentSnapshot>? knownOpponents = null)
    {
        var primarySite = ChooseAttackerSite(members, knownOpponents);

        var entry = members
            .OrderByDescending(member => EntryScore(member))
            .ThenBy(member => member.MemberId, StringComparer.Ordinal)
            .First();
        var remaining = members.Where(member => member.MemberId != entry.MemberId).ToList();
        var recon = remaining
            .OrderByDescending(member => ReconScore(member))
            .ThenBy(member => member.MemberId, StringComparer.Ordinal)
            .FirstOrDefault();

        var assignments = new List<DemolitionAssignment>
        {
            new(entry.MemberId, DemolitionDuty.Entry, primarySite,
                primarySite == 0 ? "attack_entry_a" : "attack_entry_b",
                $"entry score {EntryScore(entry):0.00}: health {entry.HealthRatio:0.00}, range {entry.WeaponRange:0}")
        };
        if (!string.IsNullOrEmpty(recon.MemberId))
        {
            assignments.Add(new DemolitionAssignment(
                recon.MemberId,
                DemolitionDuty.Recon,
                primarySite,
                "attack_mid_recon",
                $"recon score {ReconScore(recon):0.00}: role {recon.Role}, range {recon.WeaponRange:0}"));
        }

        foreach (var member in remaining.Where(member => member.MemberId != recon.MemberId))
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
            assignments,
            primarySite == 0 ? "EXECUTE A  //  RECON MID" : "EXECUTE B  //  RECON MID");
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
            assignments,
            $"HOLD {siteName}  //  CROSSfire SET  //  WATCH ROTATE");
    }

    private static DemolitionStrategyPlan PlanDefenderOpening(
        List<DemolitionAgentSnapshot> members,
        IReadOnlyList<DemolitionAgentSnapshot>? knownOpponents = null)
    {
        var threatened = ThreatenedSite(knownOpponents);
        var assignments = new List<DemolitionAssignment>(members.Count);
        var ordered = members
            .OrderByDescending(member => member.WeaponRange)
            .ThenByDescending(member => member.HealthRatio)
            .ThenBy(member => member.MemberId, StringComparer.Ordinal)
            .ToList();

        for (var index = 0; index < ordered.Count; index++)
        {
            var member = ordered[index];
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
                _ => threatened >= 0
                    ? (threatened == 0 ? "defense_rotate_a" : "defense_rotate_b")
                    : index % 2 == 0 ? "defense_rotate_a" : "defense_rotate_b"
            };
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
            0,
            assignments,
            "HOLD BOTH SITES  //  ONE MID  //  FLEX ROTATE");
    }

    private static DemolitionStrategyPlan PlanDefenderRetake(
        List<DemolitionAgentSnapshot> members,
        int plantedSiteIndex)
    {
        var site = DemolitionArenaLayout.LocalSiteCenters[
            Math.Clamp(plantedSiteIndex, 0, DemolitionArenaLayout.LocalSiteCenters.Length - 1)];
        var siteX = site.X;
        var siteZ = site.Y;
        var defuser = members
            .OrderBy(member => DistanceSquared(member, siteX, siteZ) + (1.0f - member.HealthRatio) * 90.0f)
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
                $"closest viable defuser: health {defuser.HealthRatio:0.00}, distance {Math.Sqrt(DistanceSquared(defuser, siteX, siteZ)):0.0}")
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
