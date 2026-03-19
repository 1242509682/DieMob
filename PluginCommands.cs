using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using TShockAPI;
using TShockAPI.DB;
using static DieMob.Utils;

namespace DieMob;

internal static class PluginCommands
{
    #region 主指令方法
    public static void DieMobCommand(CommandArgs args)
    {
        if (args.Parameters.Count == 0)
        {
            ShowHelp(args);
            return;
        }

        string subCmd = args.Parameters[0].ToLower();

        switch (subCmd)
        {
            case "ls":
            case "list":
                HandleList(args);
                break;

            case "if":
            case "info":
                HandleInfo(args);
                break;

            case "rp":
            case "rminfo":
                HandleReplaceMobsInfo(args);
                break;

            case "up":
            case "mod":
                HandleMod(args);
                break;

            case "s":
            case "set":
                HandleSet(args);
                break;

            case "rs":
            case "reset":
            case "清空":
                HandleReset(args);
                break;

            default:
                ShowHelp(args);
                break;
        }
    }
    #endregion

    #region 帮助指令方法
    private static void ShowHelp(CommandArgs args)
    {
        var plr = args.Player;

        if (!plr.RealPlayer)
        {
            plr.SendMessage($"\n《{Plugin.PluginName}》\n", color);
            plr.SendMessage($"/dmb s ——添加与移除保护区", color);
            plr.SendMessage($"/dmb rp ——查看替换表", color);
            plr.SendMessage($"/dmb ls ——列出保护区", color);
            plr.SendMessage($"/dmb if ——查看保护区", color);
            plr.SendMessage($"/dmb up ——修改保护区", color);
            plr.SendMessage($"/dmb rs ——重置插件数据", color);
            plr.SendMessage($"/region ——区域指令帮助", color);
            plr.SendMessage($"/reload ——重载配置与数据", color);
        }
        else
        {
            plr.SendMessage("\n[i:3455][c/AD89D5:保][c/D68ACA:护][c/DF909A:区][c/E5A894:域][i:3454] " +
           "[i:3456][C/F2F2C7:重构] [C/BFDFEA:by] [c/00FFFF:羽学] [i:3459]", color);

            var mess = new StringBuilder();
            mess.AppendLine($"/dmb s ——添加与移除保护区");
            mess.AppendLine($"/dmb ls ——列出保护区");
            mess.AppendLine($"/dmb rp ——查看替换表");
            mess.AppendLine($"/dmb if ——查看保护区");
            mess.AppendLine($"/dmb up ——修改保护区");
            mess.AppendLine($"/dmb rs ——重置插件数据");
            mess.AppendLine($"/region ——区域指令帮助");
            mess.AppendLine($"/reload ——重载配置与数据");
            GradMess(plr, mess);
        }
    }
    #endregion

    #region 列出保护区域方法
    private static void HandleList(CommandArgs args)
    {
        var plr = args.Player;
        var toRemove = RegionStorage.Regions.Where(r => TShock.Regions.GetRegionByName(r.RegionName) == null).ToList();
        foreach (var r in toRemove)
            RegionStorage.Remove(r.RegionName, Main.worldID);

        if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out int page))
            page = 1;

        var lines = RegionStorage.Regions
            .Select(r =>
            {
                var reg = TShock.Regions.GetRegionByName(r.RegionName);
                return reg != null ? $"\n{r.RegionName} @ X:{reg.Area.X} Y:{reg.Area.Y}" : $"\n{r.RegionName} (区域已丢失)";
            })
            .ToList();

        if (lines.Count == 0)
        {
            plr.SendSuccessMessage($"\n保护区列表为空");
            plr.SendMessage($"查所有区域: /dmb s", color);
            plr.SendMessage($"添加保护区: /dmb s 区域名", color);
        }
        else
        {
            PaginationTools.SendPage(args.Player, page, PaginationTools.BuildLinesFromTerms(lines),
                new PaginationTools.Settings
                {
                    HeaderFormat = "\n保护区 ({0}/{1}):",
                    FooterFormat = "输入 /dmb list {0} 查看更多"
                });
        }
    }
    #endregion

    #region 查看指定区域参数
    private static void HandleInfo(CommandArgs args)
    {
        var plr = args.Player;
        if (args.Parameters.Count < 2)
        {
            plr.SendMessage("请指定区域名：/dmb if 区域名", color);
            return;
        }

        var data = RegionStorage.Regions.FirstOrDefault(r => r.RegionName == args.Parameters[1]);
        if (data == null)
        {
            plr.SendMessage($"\n区域 {args.Parameters[1]} 不在 保护区 列表中", color);
            plr.SendMessage($"请使用 /dmb s {args.Parameters[1]} 添加该区域 到 保护区表", color);
            return;
        }

        plr.SendMessage($"\n保护区: {data.RegionName}", color);
        plr.SendMessage($"类型: {data.Type}", color);
        plr.SendMessage($"影响友好NPC: {(data.AffectFriendlyNPCs ? "是" : "否")}", color);
        plr.SendMessage($"影响雕像刷怪: {(data.AffectStatueSpawns ? "是" : "否")}", color);
        plr.SendMessage($"替换怪物数: {data.ReplaceMobs.Count} 个。使用 '/dmb rp {data.RegionName} [页]' 查看列表", color);
        if (!string.IsNullOrEmpty(data.text))
            plr.SendMessage($"{data.text}", color);
    }
    #endregion

    #region 查看指定区域替换表
    private static void HandleReplaceMobsInfo(CommandArgs args)
    {
        var plr = args.Player;
        if (args.Parameters.Count < 2)
        {
            // 如果玩家在游戏内，显示附近85格内的NPC（去重）
            if (plr.RealPlayer)
            {
                var Tilepos = new Point((int)(plr.TPlayer.position.X / 16), (int)(plr.TPlayer.position.Y / 16));
                var npcTypes = new HashSet<int>(); // 用于去重
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    var npc = Main.npc[i];
                    if (npc.active && npc.type > 0)
                    {
                        int npcTileX = (int)(npc.position.X / 16);
                        int npcTileY = (int)(npc.position.Y / 16);
                        int dist = (int)Math.Sqrt(Math.Pow(npcTileX - Tilepos.X, 2) + Math.Pow(npcTileY - Tilepos.Y, 2));
                        if (dist <= 85)
                        {
                            npcTypes.Add(npc.type); // 自动去重
                        }
                    }
                }

                if (npcTypes.Count == 0)
                {
                    plr.SendMessage(TextGradient("\n您附近85格内没有找到任何NPC"), color);
                }
                else
                {
                    var npcList = npcTypes.Select(type => $"{Lang.GetNPCNameValue(type)}({type})").ToList();
                    npcList.Sort(); // 排序
                    plr.SendMessage(TextGradient($"\n您附近85格内的NPC类型 (共{npcTypes.Count}种):"), color);
                    var npcPages = SplitList(npcList, 5);
                    foreach (var page2 in npcPages)
                    {
                        plr.SendMessage(TextGradient(string.Join("  ", page2)), color);
                    }
                }
            }

            plr.SendMessage("\n请指定区域名：/dmb rp 区域名 [页]", color);
            return;
        }

        var region = RegionStorage.Regions.FirstOrDefault(r => r.RegionName == args.Parameters[1]);
        if (region == null)
        {
            plr.SendMessage($"\n区域 {args.Parameters[1]} 不在 保护区 列表中", color);
            plr.SendMessage($"请使用 /dmb s {args.Parameters[1]} 添加该区域 到 保护区表", color);
            return;
        }

        if (!int.TryParse(args.Parameters.ElementAtOrDefault(2), out int page) || page < 1)
            page = 1;

        var lines = region.ReplaceMobs.Select(kvp => 
        $"\n从 {Lang.GetNPCNameValue(kvp.Key)}({kvp.Key}) 替换为" +
        $" {Lang.GetNPCNameValue(kvp.Value)}({kvp.Value})").ToList();

        // 替换表为空的提示
        if (lines.Count == 0)
        {
            plr.SendMessage($"\n区域 '{region.RegionName}' 的替换表为空。", color);
            plr.SendMessage($"查看附近NPC：/dmb rp (无区域名)", color);
            plr.SendMessage($"添加替换规则：/dmb up {region.RegionName} r 原ID 新ID", color);
        }
        else
        {
            PaginationTools.SendPage(args.Player, page, PaginationTools.BuildLinesFromTerms(lines),
                new PaginationTools.Settings
                {
                    HeaderFormat = $"\n{region.RegionName} 替换列表 ({0}/{1}):",
                    FooterFormat = "输入 /dmb rp {0} {{0}} 查看更多"
                });
        }
    }
    #endregion

    #region 修改指定区域参数指令方法
    private static void HandleMod(CommandArgs args)
    {
        var plr = args.Player;
        if (args.Parameters.Count < 3)
        {
            plr.SendMessage("\n用法：/dmb up 区域名 选项 参数", color);
            plr.SendMessage("可用选项：", color);
            plr.SendMessage("1.类型(t) 杀死(k) | 击退(kb)", color);
            plr.SendMessage("2.友好(f)", color);
            plr.SendMessage("3.雕像(s)", color);
            plr.SendMessage("4.替换(r) 原ID [新ID] ——有2个ID则添加/更新，只有1个则删除", color);
            plr.SendMessage("5.描述(d) 文字", color);
            plr.SendMessage("6.显示描述占位符(zwf)", color);
            return;
        }

        var region = RegionStorage.Regions.FirstOrDefault(r => r.RegionName == args.Parameters[1]);
        if (region == null)
        {
            plr.SendMessage($"区域 {args.Parameters[1]} 不在 保护区 列表中", color);
            return;
        }

        string option = args.Parameters[2].ToLower();
        bool changed = false;

        switch (option)
        {
            case "1":
            case "类型":
            case "t":
            case "type":
                if (args.Parameters.Count > 3)
                {
                    string typeArg = args.Parameters[3].ToLower();
                    switch (typeArg)
                    {
                        case "杀死":
                        case "k":
                        case "kill":
                            region.Type = RegionType.杀死;
                            break;
                        case "击退":
                        case "kb":
                        case "repel":
                            region.Type = RegionType.击退;
                            break;
                        default:
                            plr.SendMessage("类型必须是 杀死/击退", color);
                            return;
                    }

                    plr.SendMessage($"\n区域类型已改为 {region.Type}", color);
                    changed = true;
                }
                break;

            case "2":
            case "f":
            case "友好":
                region.AffectFriendlyNPCs = !region.AffectFriendlyNPCs;
                plr.SendMessage($"\n影响友好NPC 已切换为 {(region.AffectFriendlyNPCs ? "启用" : "禁用")}", color);
                changed = true;
                break;

            case "3":
            case "s":
            case "雕像":
                region.AffectStatueSpawns = !region.AffectStatueSpawns;
                plr.SendMessage($"\n影响雕像刷怪 已切换为 {(region.AffectStatueSpawns ? "启用" : "禁用")}", color);
                changed = true;
                break;

            case "4":
            case "r":
            case "替换":
                if (args.Parameters.Count >= 4) // 至少需要原ID（索引3）
                {
                    if (args.Parameters.Count >= 5) // 有原ID和新ID（索引4存在）
                    {
                        // 添加/更新规则
                        if (int.TryParse(args.Parameters[3], out int fromId) &&
                            int.TryParse(args.Parameters[4], out int toId))
                        {
                            region.ReplaceMobs[fromId] = toId;
                            plr.SendMessage($"\n添加替换: {Lang.GetNPCNameValue(fromId)}({fromId}) -> {Lang.GetNPCNameValue(toId)}({toId})", color);
                            changed = true;
                        }
                        else
                        {
                            plr.SendMessage("\nID 必须是整数", color);
                        }
                    }
                    else if (args.Parameters.Count == 4) // 只有原ID
                    {
                        // 删除规则
                        if (int.TryParse(args.Parameters[3], out int delId))
                        {
                            if (region.ReplaceMobs.Remove(delId))
                            {
                                plr.SendMessage($"\n已移除替换: {Lang.GetNPCNameValue(delId)}({delId})", color);
                                changed = true;
                            }
                            else
                            {
                                plr.SendMessage($"\n{Lang.GetNPCNameValue(delId)}({delId}) 不存在于替换表中", color);
                            }
                        }
                        else
                        {
                            plr.SendMessage("ID 必须是整数", color);
                        }
                    }
                    else
                    {
                        plr.SendMessage("\n用法：/dmb up 区域名 r 原ID [新ID]", color);
                    }
                }
                else
                {
                    plr.SendMessage("\n用法：/dmb up 区域名 r 原ID [新ID]", color);
                }
                break;

            case "5":
            case "d":
            case "desc":
            case "描述":
                if (args.Parameters.Count > 3)
                {
                    string desc = string.Join(" ", args.Parameters.Skip(3));
                    region.text = desc;
                    plr.SendMessage($"\n保护区描述已设置为: {desc}", color);
                    changed = true;
                }
                else
                {
                    region.text = string.Empty;
                    plr.SendMessage("保护区描述已清空", color);
                    changed = true;
                }
                break;

            case "6":
            case "zwf":
            case "占位符":
            case "显示描述占位符":
                plr.SendMessage("\n示例:/dmb up 区域名 5 欢迎拿着{物品名}的{玩家名}进入本区域 ", color);
                plr.SendMessage("插件名、玩家名、ip、uuid、组名\n" +
                                "账号、武器类型、物品图标、物品名、当前入侵\n" +
                                "进度、生命、生命上限、魔力、魔力上限\n" +
                                "队伍、同队人数、同队玩家、别队人数、队伍统计\n" +
                                "服务器名、在线人数、在线玩家、服务器上限", color);
                break;

            default:
                plr.SendMessage("\n可用选项：", color);
                plr.SendMessage("1.类型(t) [杀死/击退]", color);
                plr.SendMessage("2.友好(f)", color);
                plr.SendMessage("3.雕像(s)", color);
                plr.SendMessage("4.替换(r) 原ID [新ID] ——有2个ID则添加/更新，只有1个则删除", color);
                plr.SendMessage("5.描述(d) 文字", color);
                plr.SendMessage("6.显示描述占位符(zwf)", color);
                return;
        }

        if (changed)
            RegionStorage.AddOrUpdate(region, Main.worldID);
    }
    #endregion

    #region 列出附近NPC
    private static List<List<string>> SplitList(List<string> source, int groupSize)
    {
        return source
            .Select((x, i) => new { Index = i, Value = x })
            .GroupBy(x => x.Index / groupSize)
            .Select(g => g.Select(x => x.Value).ToList())
            .ToList();
    }
    #endregion

    #region 设置区域指令方法
    private static void HandleSet(CommandArgs args)
    {
        var plr = args.Player;
        if (args.Parameters.Count < 2)
        {
            // 获取当前世界的所有区域名称
            var regionNames = TShock.Regions.Regions
                .Where(r => r.WorldID == Main.worldID.ToString())
                .Select(r => r.Name)
                .OrderBy(name => name)
                .ToList();

            if (regionNames.Count == 0)
            {
                plr.SendMessage("\n当前世界没有任何区域", color2);
            }
            else
            {
                plr.SendMessage("\n当前世界区域列表:", color2);
                plr.SendMessage(string.Join("\n", regionNames), color);
            }

            plr.SendMessage("\n请指定区域名：/dmb s 区域名", color2);
            plr.SendMessage("存在则移除,不在则添加", color);
            return;
        }

        var tsRegion = TShock.Regions.GetRegionByName(args.Parameters[1]);
        if (tsRegion == null)
        {
            plr.SendMessage($"\n区域 '{args.Parameters[1]}' 不存在", color);
            return;
        }

        if (RegionStorage.Regions.Any(r => r.RegionName == tsRegion.Name))
        {
            RegionStorage.Remove(tsRegion.Name, Main.worldID);
            plr.SendMessage($"\n区域 '{tsRegion.Name}' 已从 保护区 列表删除", color);
        }
        else
        {
            RegionStorage.AddOrUpdate(new DieMobRegion(tsRegion.Name), Main.worldID);
            plr.SendMessage($"\n区域 '{tsRegion.Name}' 已添加到 保护区 列表", color);
        }
    }
    #endregion

    #region 清空所有保护区
    private static void HandleReset(CommandArgs args)
    {
        var plr = args.Player;
        RegionStorage.Regions.Clear();
        RegionStorage.Save(Main.worldID);
        plr.SendMessage("所有保护区数据已重置", Color.OrangeRed);
    }
    #endregion
}