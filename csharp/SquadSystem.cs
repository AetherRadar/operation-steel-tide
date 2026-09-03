using Godot;

namespace OperationSteelTide;

public enum OperatorRole
{
    Assault,
    Medic,
    Recon,
    Scavenger,
    Locksmith
}

public enum OperatorVisualId
{
    Garrison = 0,
    Viper = 1,
    FemaleFieldOperator = Viper,
    Heron = 2,
    Lynx = 3,
    Magpie = 4,
    Jackal = 5
}

public enum SquadOrder
{
    Follow,
    Hold,
    Move
}

public enum SquadSessionMode
{
    Local,
    Host,
    Join
}

public static class SquadCommandRules
{
    public static bool CanExposeAiCommands(
        bool demolitionMode,
        bool networkMatch,
        bool squadDeployed,
        int locallyAuthoritativeAiCount)
        => !demolitionMode
            && !networkMatch
            && squadDeployed
            && locallyAuthoritativeAiCount > 0;
}

public interface ISquadCombatant
{
    Node3D CombatNode { get; }
    bool CombatDead { get; }
    bool CombatDowned { get; }
    bool ReviveUsed { get; }
    bool CanBeRevived { get; }
    float CombatHealth { get; }
    float CombatMaxHealth { get; }
    Vector3 HitPoint(HitRegion region);
    bool TakeCombatDamage(float amount, Vector3 hitPosition, Node? attacker = null);
    void RestoreHealth(float amount);
    /// <summary>Successful teammate revive. Returns false if revive-used or not downed.</summary>
    bool TryReceiveRevive(float healAmount);
    /// <summary>Convert an eligible downed operator into the permanent KIA state.</summary>
    bool TryFinishDowned(Node? attacker = null);
}

public readonly record struct OperatorRoleSpec(
    string Name,
    string Callsign,
    string SkillName,
    string Description,
    Color Accent,
    float MaxHealth,
    float MovementMultiplier,
    float FireIntervalMultiplier,
    float ReloadMultiplier,
    float SkillCooldown,
    float SkillDuration,
    OperatorVisualId VisualId,
    int BackpackCapacityBonus,
    float SearchDurationMultiplier);

public readonly record struct SquadMemberView(
    string Callsign,
    OperatorRole Role,
    float Health,
    float MaxHealth,
    bool IsHuman,
    bool IsDown,
    SquadOrder Order,
    float SkillCooldown,
    float SkillCooldownDuration);

public static class OperatorRoles
{
    public static readonly OperatorRole[] ExtractionRoles =
    {
        OperatorRole.Assault,
        OperatorRole.Medic,
        OperatorRole.Recon,
        OperatorRole.Scavenger,
        OperatorRole.Locksmith
    };

    public static readonly OperatorRole[] CombatRoles =
    {
        OperatorRole.Assault,
        OperatorRole.Medic,
        OperatorRole.Recon
    };

    public static OperatorRoleSpec Spec(OperatorRole role) => role switch
    {
        OperatorRole.Medic => new OperatorRoleSpec(
            "MEDIC",
            "HERON",
            "MEDICAL SPRAY",
            "Spray trauma medicine onto yourself or a nearby teammate.",
            new Color(0.28f, 0.9f, 0.58f),
            112.0f,
            1.0f,
            1.0f,
            1.0f,
            18.0f,
            1.25f,
            OperatorVisualId.Heron,
            1,
            0.92f),
        OperatorRole.Recon => new OperatorRoleSpec(
            "RECON",
            "LYNX",
            "PULSE SCANNER",
            "Raise the scanner and reveal hostile movement through cover.",
            new Color(0.28f, 0.72f, 1.0f),
            100.0f,
            1.04f,
            1.0f,
            0.94f,
            24.0f,
            2.1f,
            OperatorVisualId.Lynx,
            0,
            0.94f),
        OperatorRole.Scavenger => new OperatorRoleSpec(
            "SCAVENGER",
            "MAGPIE",
            "FORTUNE FINDER",
            "Appraise and mark the richest nearby loot; carries four extra stacks.",
            new Color(0.96f, 0.76f, 0.24f),
            102.0f,
            1.03f,
            1.0f,
            0.96f,
            22.0f,
            2.0f,
            OperatorVisualId.Magpie,
            4,
            0.72f),
        OperatorRole.Locksmith => new OperatorRoleSpec(
            "LOCKSMITH",
            "JACKAL",
            "SKELETON KEY",
            "Bypass locks and search containers rapidly; carries two extra stacks.",
            new Color(0.72f, 0.55f, 1.0f),
            108.0f,
            1.02f,
            0.98f,
            0.9f,
            26.0f,
            9.0f,
            OperatorVisualId.Jackal,
            2,
            0.78f),
        _ => new OperatorRoleSpec(
            "ASSAULT",
            "VIPER",
            "COMBAT OVERDRIVE",
            "Boost movement, rate of fire, reload speed, and weapon handling.",
            new Color(1.0f, 0.58f, 0.2f),
            125.0f,
            1.08f,
            0.93f,
            0.9f,
            28.0f,
            10.0f,
            OperatorVisualId.Viper,
            0,
            1.0f)
    };

    public static string RoleName(OperatorRole role, string language)
        => GameLocalization.Get($"operator_role_{role.ToString().ToLowerInvariant()}", language, Spec(role).Name);

    public static string SkillName(OperatorRole role, string language)
        => GameLocalization.Get($"operator_skill_{role.ToString().ToLowerInvariant()}", language, Spec(role).SkillName);

    public static string Description(OperatorRole role, string language)
        => GameLocalization.Get($"operator_description_{role.ToString().ToLowerInvariant()}", language, Spec(role).Description);

    public static string Callsign(OperatorRole role) => Spec(role).Callsign;

    public static string OrderName(SquadOrder order, string language)
    {
        var chinese = GameLocalization.IsChinese(language);
        return order switch
        {
            SquadOrder.Hold => chinese ? "\u539f\u5730\u6212\u5907" : "HOLD",
            SquadOrder.Move => chinese ? "\u79fb\u52a8\u81f3\u6807\u8bb0" : "MOVE",
            _ => chinese ? "\u8ddf\u968f" : "FOLLOW"
        };
    }
}
