using Godot;

namespace OperationSteelTide;

public enum OperatorRole
{
    Assault,
    Medic,
    Recon
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

public interface ISquadCombatant
{
    Node3D CombatNode { get; }
    bool CombatDead { get; }
    float CombatHealth { get; }
    float CombatMaxHealth { get; }
    Vector3 HitPoint(HitRegion region);
    bool TakeCombatDamage(float amount, Vector3 hitPosition, Node? attacker = null);
    void RestoreHealth(float amount);
}

public readonly record struct OperatorRoleSpec(
    string Name,
    string ChineseName,
    string SkillName,
    string ChineseSkillName,
    string Description,
    string ChineseDescription,
    Color Accent,
    float MaxHealth,
    float MovementMultiplier,
    float FireIntervalMultiplier,
    float ReloadMultiplier,
    float SkillCooldown,
    float SkillDuration);

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
    public static OperatorRoleSpec Spec(OperatorRole role) => role switch
    {
        OperatorRole.Medic => new OperatorRoleSpec(
            "MEDIC",
            "\u533b\u7597",
            "MEDICAL SPRAY",
            "\u533b\u7597\u55b7\u96fe",
            "Spray trauma medicine onto yourself or a nearby teammate.",
            "\u5411\u81ea\u5df1\u6216\u9644\u8fd1\u961f\u53cb\u55b7\u6d12\u6025\u6551\u836f\u5242\u3002",
            new Color(0.28f, 0.9f, 0.58f),
            112.0f,
            1.0f,
            1.0f,
            1.0f,
            18.0f,
            1.25f),
        OperatorRole.Recon => new OperatorRoleSpec(
            "RECON",
            "\u4fa6\u5bdf",
            "PULSE SCANNER",
            "\u8109\u51b2\u4fa6\u5bdf",
            "Raise the scanner and reveal hostile movement through cover.",
            "\u4e3e\u8d77\u4fa6\u5bdf\u8bbe\u5907\uff0c\u6807\u8bb0\u63a9\u4f53\u540e\u7684\u654c\u4eba\u3002",
            new Color(0.28f, 0.72f, 1.0f),
            100.0f,
            1.04f,
            1.0f,
            0.94f,
            24.0f,
            2.1f),
        _ => new OperatorRoleSpec(
            "ASSAULT",
            "\u6218\u58eb",
            "COMBAT OVERDRIVE",
            "\u6218\u6597\u8d85\u9a71",
            "Boost movement, rate of fire, reload speed, and weapon handling.",
            "\u77ed\u65f6\u63d0\u5347\u79fb\u52a8\u3001\u5c04\u901f\u3001\u6362\u5f39\u548c\u64cd\u63a7\u3002",
            new Color(1.0f, 0.58f, 0.2f),
            125.0f,
            1.08f,
            0.93f,
            0.9f,
            28.0f,
            10.0f)
    };

    public static string RoleName(OperatorRole role, string language)
    {
        var spec = Spec(role);
        return GameLocalization.IsChinese(language) ? spec.ChineseName : spec.Name;
    }

    public static string SkillName(OperatorRole role, string language)
    {
        var spec = Spec(role);
        return GameLocalization.IsChinese(language) ? spec.ChineseSkillName : spec.SkillName;
    }

    public static string Description(OperatorRole role, string language)
    {
        var spec = Spec(role);
        return GameLocalization.IsChinese(language) ? spec.ChineseDescription : spec.Description;
    }

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
