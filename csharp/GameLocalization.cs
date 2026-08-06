using System.Collections.Generic;

namespace OperationSteelTide;

public static class GameLocalization
{
    private static readonly Dictionary<string, string> Chinese = new()
    {
        ["operation"] = "钢铁潮汐行动",
        ["deployment_objective"] = "越过部署线",
        ["secure_terminal"] = "控制货运站",
        ["hostiles"] = "敌军",
        ["vital"] = "生命",
        ["armor"] = "护甲",
        ["grenade"] = "手雷",
        ["auto"] = "全自动",
        ["semi"] = "半自动",
        ["online"] = "在线",
        ["local"] = "本地",
        ["ready"] = "就绪",
        ["undetected"] = "未被发现",
        ["alerted"] = "已暴露",
        ["suspicion"] = "警觉",
        ["suspicion_high"] = "高度警觉",
        ["deployment"] = "部署",
        ["infiltration"] = "潜入",
        ["contact"] = "接敌",
        ["combat"] = "交战",
        ["extraction"] = "撤离",
        ["complete"] = "完成",
        ["failed"] = "失败",
        ["disable_relay"] = "关闭通信中继",
        ["download_manifest"] = "下载货运清单",
        ["applying_armor"] = "装配护甲板",
        ["fire_auto"] = "射击模式  //  全自动",
        ["fire_semi"] = "射击模式  //  半自动",
        ["light_on"] = "枪灯  //  开启",
        ["light_off"] = "枪灯  //  关闭",
        ["armor_secured"] = "护甲板装配完成",
        ["ammo_recovered"] = "已回收弹药",
        ["armor_recovered"] = "已回收备用护甲",
        ["enemy_network"] = "敌方通信网络已激活",
        ["relay_offline"] = "中继已关闭  //  响应延迟",
        ["qrf_inbound"] = "敌方快速反应部队  //  7秒后到达",
        ["qrf_deployed"] = "快速反应部队抵达  //  三个目标",
        ["pause_title"] = "战术暂停",
        ["look_sensitivity"] = "视角灵敏度",
        ["render_quality"] = "画面质量",
        ["language"] = "语言",
        ["fullscreen"] = "全屏",
        ["resume"] = "继续行动",
        ["redeploy"] = "重新部署",
        ["exit"] = "退出游戏",
        ["performance"] = "性能",
        ["balanced"] = "平衡",
        ["cinematic"] = "电影级",
        ["mission_complete"] = "任务完成",
        ["terminal_secured"] = "货运站已控制",
        ["operator_down"] = "干员阵亡",
        ["press_enter"] = "按回车键重新部署",
        ["knife"] = "近战",
        ["tactical_knife"] = "战术刀",
        ["knife_ready"] = "战术刀已就绪",
        ["primary_ready"] = "主武器已就绪",
        ["search"] = "搜索",
        ["open_loot"] = "打开",
        ["field_inventory"] = "战地装备管理",
        ["searched_gear"] = "目标随身装备",
        ["equipped_backpack"] = "当前装备 / 个人背包",
        ["equipped_loadout"] = "当前人物装备",
        ["close"] = "关闭",
        ["take"] = "拿取",
        ["equip"] = "替换",
        ["use"] = "使用",
        ["use_install"] = "使用 / 安装",
        ["empty"] = "已搜空",
        ["backpack_empty"] = "背包为空",
        ["backpack_full"] = "背包已满",
        ["item_stored"] = "物品已放入背包",
        ["part_installed"] = "武器零件已安装",
        ["weapon_equipped"] = "主武器已替换",
        ["primary_weapon"] = "主武器",
        ["personal_backpack"] = "个人背包物品",
        ["details"] = "详情",
        ["weapon_details"] = "枪械详情",
        ["final_stats"] = "整枪最终属性",
        ["fitted_parts"] = "当前枪械配件",
        ["empty_slot"] = "空槽位",
        ["select_primary"] = "切换到主武器",
        ["select_knife"] = "切换到战术刀",
        ["helmet"] = "头盔",
        ["body_armor"] = "防弹衣",
        ["backpack"] = "背包",
        ["equipment_replaced"] = "装备已替换",
        ["pack_too_small"] = "请先腾空物品再更换此背包",
        ["stance_blocked"] = "上方空间不足",
        ["helmet_impact"] = "头盔吸收了冲击",
        ["armor_impact"] = "防弹衣吸收了冲击"
    };

    static GameLocalization()
    {
        Chinese["squad_ready"] = "\u5c0f\u961f\u5df2\u5c31\u7eea  //  F1 \u8ddf\u968f  F2 \u6212\u5907  F3 \u79fb\u52a8  H \u6280\u80fd";
        Chinese["medic_spray"] = "\u533b\u7597\u55b7\u96fe  //  \u4f24\u52bf\u5df2\u7a33\u5b9a";
        Chinese["squad_revive"] = "\u533b\u7597\u55b7\u96fe  //  \u961f\u53cb\u5df2\u6551\u8d77";
        Chinese["recon_scan"] = "\u8109\u51b2\u4fa6\u5bdf  //  \u654c\u4eba\u5df2\u6807\u8bb0";
        Chinese["player_left"] = "\u961f\u53cb\u5df2\u65ad\u5f00  //  AI \u63a5\u7ba1\u69fd\u4f4d";
        Chinese["player_downed"] = "\u4f60\u5df2\u5012\u5730  //  AI \u533b\u7597\u6b63\u5728\u6551\u63f4";
        Chinese["player_revived"] = "\u5df2\u6551\u8d77  //  \u91cd\u8fd4\u6218\u6597";
    }

    private static readonly Dictionary<string, string> ChineseObjectives = new()
    {
        ["DISABLE THE COMMUNICATIONS RELAY"] = "关闭通信中继",
        ["RECOVER THE SHIPPING MANIFEST"] = "获取货运清单",
        ["REACH THE EXTRACTION ZONE"] = "前往撤离区域",
        ["BYPASS THE CUSTOMS TERMINAL"] = "绕过海关终端",
        ["RECOVER THE SILENT LEDGER"] = "获取秘密账本",
        ["DISABLE THE GANTRY RELAY"] = "关闭龙门架中继",
        ["SECURE THE CRANE CONTROL LOG"] = "取得起重机控制日志"
    };

    public static bool IsChinese(string language) => language == "zh";

    public static string Get(string key, string language, string english)
    {
        return IsChinese(language) && Chinese.TryGetValue(key, out var translated) ? translated : english;
    }

    public static string Objective(string objective, string language)
    {
        return IsChinese(language) && ChineseObjectives.TryGetValue(objective, out var translated)
            ? translated
            : objective;
    }

    public static string Phase(string phase, string language)
    {
        return Get(phase.ToLowerInvariant(), language, phase);
    }
}
