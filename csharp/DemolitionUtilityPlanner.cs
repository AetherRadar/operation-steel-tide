using Godot;

namespace OperationSteelTide;

public enum DemolitionAiUtilityKind
{
    None,
    Fragmentation,
    Smoke,
    Incendiary
}

public readonly record struct DemolitionUtilityContext(
    DemolitionTeam Team,
    DemolitionStrategyPhase Phase,
    DemolitionDuty Duty,
    Vector3 ActorPosition,
    Vector3 ObjectivePosition,
    Vector3 ContactPosition,
    bool HasVisibleContact,
    bool IsObjectiveChanneling,
    bool HasFragmentation,
    bool HasSmoke,
    bool HasIncendiary,
    bool FragmentationFriendlySafe,
    bool IncendiaryFriendlySafe,
    float RemainingSeconds);

public readonly record struct DemolitionUtilityDecision(
    DemolitionAiUtilityKind Kind,
    Vector3 TargetPosition,
    string Reason)
{
    public static DemolitionUtilityDecision None
        => new(DemolitionAiUtilityKind.None, Vector3.Zero, "no utility window");
}

/// <summary>
/// Pure, deterministic demolition utility policy. Runtime visibility, friendly safety,
/// path clearance, inventory, cadence, and authority remain world responsibilities.
/// </summary>
public static class DemolitionUtilityPlanner
{
    public static DemolitionUtilityDecision Plan(DemolitionUtilityContext context)
    {
        if (context.IsObjectiveChanneling)
        {
            return DemolitionUtilityDecision.None;
        }

        var objectiveDistance = FlatDistance(context.ActorPosition, context.ObjectivePosition);
        var contactDistance = context.HasVisibleContact
            ? FlatDistance(context.ActorPosition, context.ContactPosition)
            : float.PositiveInfinity;
        var attacking = context.Team == DemolitionTeam.Attackers;
        var retaking = context.Duty is DemolitionDuty.Defuse
            or DemolitionDuty.CoverDefuser
            or DemolitionDuty.Retake;

        if (!attacking
            && context.Phase == DemolitionStrategyPhase.PostPlant
            && retaking
            && context.HasSmoke
            && objectiveDistance is >= 7.0f and <= 27.0f)
        {
            return new DemolitionUtilityDecision(
                DemolitionAiUtilityKind.Smoke,
                context.ObjectivePosition,
                "retake smoke blocks the device lane");
        }

        if (attacking
            && context.Phase == DemolitionStrategyPhase.Opening
            && context.Duty is DemolitionDuty.Entry
                or DemolitionDuty.Support
                or DemolitionDuty.Recon
                or DemolitionDuty.Flank
            && context.HasSmoke
            && objectiveDistance is >= 9.0f and <= 27.0f)
        {
            return new DemolitionUtilityDecision(
                DemolitionAiUtilityKind.Smoke,
                context.ObjectivePosition,
                "execute smoke covers the approach");
        }

        if (attacking
            && context.Phase == DemolitionStrategyPhase.PostPlant
            && context.Duty is DemolitionDuty.SiteGuard
                or DemolitionDuty.Crossfire
                or DemolitionDuty.Lurk
            && context.HasIncendiary
            && context.IncendiaryFriendlySafe
            && context.RemainingSeconds is >= 8.0f and <= 24.0f
            && objectiveDistance is >= 5.0f and <= 22.0f)
        {
            return new DemolitionUtilityDecision(
                DemolitionAiUtilityKind.Incendiary,
                context.ObjectivePosition,
                "post-plant incendiary delays the defuse");
        }

        if (context.HasVisibleContact
            && context.HasIncendiary
            && context.IncendiaryFriendlySafe
            && contactDistance is >= 7.0f and <= 22.0f
            && (!attacking
                || context.Phase == DemolitionStrategyPhase.PostPlant
                || context.Duty == DemolitionDuty.Entry))
        {
            return new DemolitionUtilityDecision(
                DemolitionAiUtilityKind.Incendiary,
                Grounded(context.ContactPosition),
                attacking
                    ? "incendiary denies the retake route"
                    : "incendiary stalls the confirmed push");
        }

        if (context.HasVisibleContact
            && context.HasFragmentation
            && context.FragmentationFriendlySafe
            && contactDistance is >= 9.0f and <= 26.0f
            && context.Duty is DemolitionDuty.Recon
                or DemolitionDuty.Flank
                or DemolitionDuty.MidControl
                or DemolitionDuty.Crossfire
                or DemolitionDuty.Lurk)
        {
            return new DemolitionUtilityDecision(
                DemolitionAiUtilityKind.Fragmentation,
                Grounded(context.ContactPosition),
                "fragmentation grenade pressures confirmed cover");
        }

        if (context.HasVisibleContact
            && context.HasSmoke
            && contactDistance is >= 10.0f and <= 30.0f
            && (attacking || retaking))
        {
            return new DemolitionUtilityDecision(
                DemolitionAiUtilityKind.Smoke,
                context.ActorPosition.Lerp(context.ContactPosition, 0.72f),
                attacking
                    ? "contact smoke masks the entry"
                    : "contact smoke masks the retake");
        }

        return DemolitionUtilityDecision.None;
    }

    private static float FlatDistance(Vector3 from, Vector3 to)
    {
        var offset = to - from;
        offset.Y = 0.0f;
        return offset.Length();
    }

    private static Vector3 Grounded(Vector3 point)
        => new(point.X, point.Y - 0.9f, point.Z);
}
