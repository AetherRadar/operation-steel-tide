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
        Chinese["medical_hotkey"] = "B  \u533b\u7597";
        Chinese["medical_selector"] = "\u533b\u7597\u9009\u62e9\u8f6e\u76d8";
        Chinese["medical_wheel_hint"] = "\u6307\u5411\u5e76\u70b9\u51fb  //  \u677e\u5f00 B \u4f7f\u7528";
        Chinese["medical_hold_hint"] = "\u6309\u4f4f B \u6253\u5f00\u836f\u54c1\u8f6e\u76d8";
        Chinese["medical_boost"] = "\u80be\u4e0a\u817a\u7d20\u589e\u76ca";
        Chinese["medical_bandage"] = "\u6b62\u8840\u7ef7\u5e26";
        Chinese["medical_medkit"] = "\u6218\u5730\u6025\u6551\u5305";
        Chinese["medical_adrenaline"] = "\u80be\u4e0a\u817a\u7d20\u6ce8\u5c04\u5668";
        Chinese["medical_bandage_effect"] = "\u6062\u590d 32 \u751f\u547d  //  1.15 \u79d2";
        Chinese["medical_medkit_effect"] = "\u6062\u590d 72 \u751f\u547d  //  2.35 \u79d2";
        Chinese["medical_adrenaline_effect"] = "\u6062\u590d 12 \u751f\u547d\u548c\u4f53\u529b  //  14 \u79d2\u589e\u76ca";
        Chinese["medical_using_bandage"] = "\u6b63\u5728\u4f7f\u7528\u7ef7\u5e26";
        Chinese["medical_using_medkit"] = "\u6b63\u5728\u4f7f\u7528\u6025\u6551\u5305";
        Chinese["medical_using_adrenaline"] = "\u6b63\u5728\u6ce8\u5c04\u80be\u4e0a\u817a\u7d20";
        Chinese["medical_bandage_used"] = "\u6b62\u8840\u5b8c\u6210  //  \u4f24\u52bf\u5df2\u7a33\u5b9a";
        Chinese["medical_medkit_used"] = "\u6025\u6551\u5b8c\u6210  //  \u751f\u547d\u5df2\u6062\u590d";
        Chinese["medical_adrenaline_used"] = "\u80be\u4e0a\u817a\u7d20\u5df2\u6fc0\u6d3b  //  \u901f\u5ea6\u548c\u4f53\u529b\u589e\u5f3a";
        Chinese["medical_empty"] = "\u6ca1\u6709\u533b\u7597\u7269\u8d44";
        Chinese["medical_full"] = "\u72b6\u6001\u5df2\u7a33\u5b9a";
        Chinese["medical_interrupted"] = "\u7528\u836f\u88ab\u6253\u65ad";
        Chinese["medical_recovered"] = "\u627e\u5230\u533b\u7597\u7269\u8d44";
        Chinese["damage_incoming"] = "\u906d\u53d7\u653b\u51fb";
        Chinese["damage_front"] = "\u524d\u65b9";
        Chinese["damage_rear"] = "\u540e\u65b9";
        Chinese["damage_left"] = "\u5de6\u4fa7";
        Chinese["damage_right"] = "\u53f3\u4fa7";
        Chinese["damage_region_head"] = "\u5934\u90e8";
        Chinese["damage_region_torso"] = "\u8eaf\u5e72";
        Chinese["damage_region_limbs"] = "\u56db\u80a2";
        Chinese["damage_source_enemy"] = "\u654c\u65b9\u5e72\u5458";
        Chinese["damage_source_aircraft"] = "\u7a7a\u4e2d\u706b\u529b";
        Chinese["damage_source_explosion"] = "\u7206\u70b8\u51b2\u51fb";
        Chinese["damage_source_vehicle"] = "\u8f7d\u5177\u649e\u51fb";
        Chinese["damage_source_environment"] = "\u73af\u5883\u4f24\u5bb3";
        Chinese["you"] = "\u4f60";
        Chinese["minimap_title"] = "\u6218\u672f\u5c0f\u5730\u56fe";
        Chinese["minimap_deploy"] = "\u51fa\u751f\u70b9";
        Chinese["minimap_extract"] = "\u64a4\u79bb\u70b9";
        Chinese["minimap_relay"] = "\u901a\u4fe1\u4e2d\u7ee7";
        Chinese["minimap_manifest"] = "\u6e05\u5355\u70b9";
        Chinese["minimap_warehouse"] = "\u4ed3\u5e93";
        Chinese["minimap_radar"] = "\u96f7\u8fbe\u5854";
        Chinese["minimap_residential"] = "\u6f6e\u6c50\u4f4f\u533a";
        Chinese["minimap_command"] = "\u6307\u6325\u8231";
        Chinese["ammo_tier_tooltip"] = "\u5f39\u5323\u5185\u5f39\u836f\u7b49\u7ea7";
        Chinese["loadout_scavenger"] = "\u641c\u7d22\u8005 / \u4ec5\u5200\u5177";
        Chinese["loadout_m4a1"] = "M4A1 \u7a81\u51fb";
        Chinese["loadout_mp5"] = "MP5A5 \u8fd1\u6218";
        Chinese["loadout_m24"] = "M24 \u7cbe\u5bc6\u5c04\u624b";
        Chinese["loadout_standard_armor"] = "\u6807\u51c6\u91ce\u6218\u88c5\u5907";
        Chinese["loadout_heavy_armor"] = "\u91cd\u578b\u7a81\u51fb\u88c5\u5907";
        Chinese["loadout_insufficient"] = "\u4f59\u989d\u4e0d\u8db3  //  \u8bf7\u9009\u62e9\u66f4\u4fbf\u5b9c\u7684\u6574\u5907";
        Chinese["loadout_save_failed"] = "\u6863\u6848\u4fdd\u5b58\u5931\u8d25  //  \u5df2\u53d6\u6d88\u90e8\u7f72";
        Chinese["extraction_bank"] = "\u5df2\u5b58\u5165\u64a4\u79bb\u4ef7\u503c";
        Chinese["extraction_unbanked"] = "\u64a4\u79bb\u4ef7\u503c\u672a\u5165\u8d26";
        Chinese["profile_balance"] = "\u4e0b\u5c40\u6574\u5907\u4f59\u989d";
        Chinese["profile_save_warning"] = "\u6863\u6848\u4fdd\u5b58\u5931\u8d25  //  \u4ef7\u503c\u672a\u5b58\u5165";
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
