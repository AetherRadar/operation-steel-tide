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
        ["armor_impact"] = "防弹衣吸收了冲击",
        ["vehicle_entered"] = "载具  //  已上车",
        ["vehicle_exited"] = "载具  //  已下车",
        ["enter_vehicle"] = "上车",
        ["exit_vehicle"] = "下车",
        ["revive_exhausted"] = "救援次数已用尽  //  无法再次救起",
        ["squad_revive"] = "救援完成  //  队友已稳定",
        ["squadmate_kia"] = "队友阵亡  //  遗体袋可搜刮",
        ["backpack_button"] = "TAB  背包",
        ["backpack_value"] = "背包总估值",
        ["grade_common"] = "普通",
        ["grade_uncommon"] = "优良",
        ["grade_rare"] = "稀有",
        ["grade_epic"] = "史诗",
        ["grade_legendary"] = "传说",
        ["field_cache"] = "战地物资",
        ["rival_squad"] = "敌对干员小队",
        ["spawn_dispersed"] = "多点分散部署",
        ["extract_rank_title"] = "撤离物资排名",
        ["extract_rank_note"] = "遗体袋不计入队伍成绩",
        ["cold_start_unarmed"] = "冷启动  //  需搜刮枪械",
        ["civilian_down"] = "平民倒地  //  F 搜刮"
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
        Chinese["vehicle_blocked"] = "\u8f7d\u5177\u53d7\u963b  //  \u5012\u8f66\u8131\u56f0";
        Chinese["mate_reviving_you"] = "\u961f\u53cb\u6b63\u5728\u8d76\u6765\u6551\u63f4  //  \u575a\u6301\u4f4f";
        Chinese["weapon_m24"] = "M24 \u7cbe\u786e\u5c04\u624b\u6b65\u67aa";
        Chinese["weapon_mp5a5"] = "MP5A5 \u51b2\u950b\u67aa";
        Chinese["ammo_rifle"] = "\u6b65\u67aa\u5f39\u836f";
        Chinese["ammo_sniper"] = "7.62 \u6beb\u7c73\u7cbe\u786e\u5f39\u836f";
        Chinese["ammo_smg"] = "9 \u6beb\u7c73\u51b2\u950b\u67aa\u5f39\u836f";
        Chinese["knife_skin"] = "\u6218\u672f\u5200\u6d82\u88c5";
        Chinese["knife_skin_carbon"] = "\u78b3\u7ea4\u9ed1";
        Chinese["knife_skin_crimson"] = "\u8d64\u7ea2\u7535\u8def";
        Chinese["knife_skin_arctic"] = "\u6781\u5730\u51b0\u6676";
        Chinese["knife_skin_hazard"] = "\u8b66\u6212\u6761\u7eb9";
        Chinese["knife_skin_detail"] = "\u88c5\u5907\u540e\u6c38\u4e45\u66ff\u6362\u6218\u672f\u5200\u6d82\u88c5";
        Chinese["knife_skin_equipped"] = "\u6218\u672f\u5200\u6d82\u88c5\u5df2\u66ff\u6362";
        Chinese["civilian_medic_aid"] = "\u533b\u7597\u6551\u52a9  //  \u4f24\u52bf\u5df2\u7a33\u5b9a";
        Chinese["civilian_local_intel"] = "\u793e\u533a\u60c5\u62a5  //  \u654c\u4eba\u5df2\u6807\u8bb0";
        Chinese["civilian_field_repair"] = "\u73b0\u573a\u62a2\u4fee  //  \u8f7d\u5177\u6216\u88c5\u5907\u5df2\u7ef4\u62a4";
        Chinese["civilian_evac_supply"] = "\u64a4\u79bb\u7269\u8d44  //  \u5df2\u83b7\u5f97\u5f39\u836f";
        Chinese["resident_supplies"] = "\u5c45\u6c11\u8865\u7ed9\u5df2\u83b7\u5f97";
        Chinese["civilian_medic_request"] = "\u8bf7\u6c42\u533b\u7597\u6551\u52a9";
        Chinese["civilian_guard_request"] = "\u8bf7\u6c42\u793e\u533a\u60c5\u62a5";
        Chinese["civilian_repair_request"] = "\u8bf7\u6c42\u8f7d\u5177\u62a2\u4fee";
        Chinese["civilian_evac_request"] = "\u8bf7\u6c42\u64a4\u79bb\u7269\u8d44";
        Chinese["civilian_resident_request"] = "\u8bf7\u6c42\u5c45\u6c11\u8865\u7ed9";
        Chinese["residential_cache_medical"] = "\u793e\u533a\u533b\u7597\u67dc";
        Chinese["residential_cache_evac"] = "\u64a4\u79bb\u7269\u8d44\u67dc";
        Chinese["residential_cache_workshop"] = "\u7ef4\u4fee\u5de5\u5177\u67dc";
        Chinese["residential_cache_security"] = "\u793e\u533a\u5b89\u4fdd\u88c5\u5907\u67dc";
        Chinese["residential_cache_smuggler"] = "\u9690\u85cf\u8fdd\u7981\u54c1\u7bb1";
        Chinese["residential_cache_pantry"] = "\u793e\u533a\u50a8\u5907\u67dc";
        Chinese["residential_cache_family"] = "\u5c45\u6c11\u5e94\u6025\u50a8\u5907";
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
