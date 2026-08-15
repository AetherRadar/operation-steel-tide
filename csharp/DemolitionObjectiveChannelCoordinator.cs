using System;
using System.Collections.Generic;

namespace OperationSteelTide;

internal readonly record struct DemolitionObjectiveChannelState(
    string? CarrierMemberId,
    string? DefuserMemberId,
    float PlantProgress,
    float DefuseProgress,
    int CarrierSiteIndex,
    int ActiveSiteIndex);

internal sealed record DemolitionObjectiveChannelResolution(
    IReadOnlyList<DemolitionAssignment> Assignments,
    string? CarrierMemberId,
    string? DefuserMemberId,
    int CarrierSiteIndex,
    bool ResetPlantProgress,
    bool ResetDefuseProgress);

/// <summary>
/// Pure objective-channel coordinator for the AI-controlled demolition team.
/// It owns carrier and defuser continuity while the world maps member IDs to actors.
/// </summary>
internal sealed class DemolitionObjectiveChannelCoordinator
{
    public DemolitionObjectiveChannelResolution Resolve(
        DemolitionStrategyPlan plan,
        IReadOnlyCollection<string> aliveMemberIds,
        DemolitionObjectiveChannelState state)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(aliveMemberIds);

        var alive = new HashSet<string>(StringComparer.Ordinal);
        foreach (var memberId in aliveMemberIds)
        {
            if (!string.IsNullOrWhiteSpace(memberId))
            {
                alive.Add(memberId);
            }
        }

        var assignments = LiveAssignments(plan.Assignments, alive);
        var carrierPhase = plan.Team == DemolitionTeam.Attackers
            && plan.Phase == DemolitionStrategyPhase.Opening;
        var defuserPhase = plan.Team == DemolitionTeam.Defenders
            && plan.Phase == DemolitionStrategyPhase.PostPlant
            && state.ActiveSiteIndex >= 0;

        var preserveCarrier = carrierPhase
            && state.PlantProgress > 0.0f
            && IsAlive(state.CarrierMemberId, alive);
        var carrierSiteIndex = preserveCarrier && state.CarrierSiteIndex >= 0
            ? state.CarrierSiteIndex
            : plan.PrimarySiteIndex;
        var carrierMemberId = ResolveCarrier(
            assignments,
            alive,
            state,
            carrierPhase,
            carrierSiteIndex);
        var defuserMemberId = ResolveDefuser(
            assignments,
            alive,
            state,
            defuserPhase);

        var resetPlantProgress = state.PlantProgress > 0.0f
            && (!carrierPhase || !SameMember(carrierMemberId, state.CarrierMemberId));
        var resetDefuseProgress = state.DefuseProgress > 0.0f
            && (!defuserPhase || !SameMember(defuserMemberId, state.DefuserMemberId));

        return new DemolitionObjectiveChannelResolution(
            assignments.AsReadOnly(),
            carrierMemberId,
            defuserMemberId,
            carrierMemberId is null ? -1 : carrierSiteIndex,
            resetPlantProgress,
            resetDefuseProgress);
    }

    public static bool IsCarrierDuty(DemolitionDuty duty)
        => duty is DemolitionDuty.Entry
            or DemolitionDuty.Support
            or DemolitionDuty.Recon
            or DemolitionDuty.Flank;

    private static List<DemolitionAssignment> LiveAssignments(
        IReadOnlyList<DemolitionAssignment> plannedAssignments,
        HashSet<string> alive)
    {
        var assignments = new List<DemolitionAssignment>(plannedAssignments.Count);
        var assignedMembers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assignment in plannedAssignments)
        {
            if (alive.Contains(assignment.MemberId) && assignedMembers.Add(assignment.MemberId))
            {
                assignments.Add(assignment);
            }
        }
        return assignments;
    }

    private static string? ResolveCarrier(
        List<DemolitionAssignment> assignments,
        HashSet<string> alive,
        DemolitionObjectiveChannelState state,
        bool carrierPhase,
        int carrierSiteIndex)
    {
        if (!carrierPhase)
        {
            return null;
        }

        if (state.PlantProgress > 0.0f && IsAlive(state.CarrierMemberId, alive))
        {
            var carrierMemberId = state.CarrierMemberId!;
            EnsureCarrierAssignment(assignments, carrierMemberId, carrierSiteIndex);
            return carrierMemberId;
        }

        foreach (var assignment in assignments)
        {
            if (IsCarrierDuty(assignment.Duty))
            {
                return assignment.MemberId;
            }
        }
        return null;
    }

    private static string? ResolveDefuser(
        List<DemolitionAssignment> assignments,
        HashSet<string> alive,
        DemolitionObjectiveChannelState state,
        bool defuserPhase)
    {
        if (!defuserPhase)
        {
            return null;
        }

        var plannedDefuser = FindMemberWithDuty(assignments, DemolitionDuty.Defuse);
        if (state.DefuseProgress > 0.0f && IsAlive(state.DefuserMemberId, alive))
        {
            var activeDefuser = state.DefuserMemberId!;
            EnsureDefuserAssignment(assignments, activeDefuser, state.ActiveSiteIndex);
            if (plannedDefuser is not null && !SameMember(plannedDefuser, activeDefuser))
            {
                EnsureCoverAssignment(assignments, plannedDefuser, state.ActiveSiteIndex);
            }
            return activeDefuser;
        }

        return plannedDefuser;
    }

    private static void EnsureCarrierAssignment(
        List<DemolitionAssignment> assignments,
        string memberId,
        int siteIndex)
    {
        var index = FindAssignment(assignments, memberId);
        var normalizedSite = Math.Max(0, siteIndex);
        if (index >= 0 && IsCarrierDuty(assignments[index].Duty))
        {
            assignments[index] = assignments[index] with
            {
                SiteIndex = normalizedSite,
                TargetKey = normalizedSite == 0 ? "attack_support_a" : "attack_support_b",
                Reason = "continue in-progress plant channel"
            };
            return;
        }

        var assignment = new DemolitionAssignment(
            memberId,
            DemolitionDuty.Support,
            normalizedSite,
            normalizedSite == 0 ? "attack_support_a" : "attack_support_b",
            "continue in-progress plant channel");
        ReplaceOrAdd(assignments, index, assignment);
    }

    private static void EnsureDefuserAssignment(
        List<DemolitionAssignment> assignments,
        string memberId,
        int siteIndex)
    {
        var index = FindAssignment(assignments, memberId);
        var assignment = new DemolitionAssignment(
            memberId,
            DemolitionDuty.Defuse,
            siteIndex,
            siteIndex == 0 ? "site_a" : "site_b",
            "continue in-progress defuse channel");
        ReplaceOrAdd(assignments, index, assignment);
    }

    private static void EnsureCoverAssignment(
        List<DemolitionAssignment> assignments,
        string memberId,
        int siteIndex)
    {
        var index = FindAssignment(assignments, memberId);
        if (index < 0)
        {
            return;
        }
        assignments[index] = assignments[index] with
        {
            Duty = DemolitionDuty.CoverDefuser,
            SiteIndex = siteIndex,
            TargetKey = siteIndex == 0 ? "retake_cover_a" : "retake_cover_b",
            Reason = "cover in-progress defuse channel"
        };
    }

    private static int FindAssignment(
        IReadOnlyList<DemolitionAssignment> assignments,
        string memberId)
    {
        for (var index = 0; index < assignments.Count; index++)
        {
            if (SameMember(assignments[index].MemberId, memberId))
            {
                return index;
            }
        }
        return -1;
    }

    private static string? FindMemberWithDuty(
        IReadOnlyList<DemolitionAssignment> assignments,
        DemolitionDuty duty)
    {
        foreach (var assignment in assignments)
        {
            if (assignment.Duty == duty)
            {
                return assignment.MemberId;
            }
        }
        return null;
    }

    private static void ReplaceOrAdd(
        List<DemolitionAssignment> assignments,
        int index,
        DemolitionAssignment assignment)
    {
        if (index >= 0)
        {
            assignments[index] = assignment;
        }
        else
        {
            assignments.Add(assignment);
        }
    }

    private static bool IsAlive(string? memberId, HashSet<string> alive)
        => memberId is not null && alive.Contains(memberId);

    private static bool SameMember(string? left, string? right)
        => string.Equals(left, right, StringComparison.Ordinal);
}
