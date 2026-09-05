using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;
using static DieMob.Utils;

namespace DieMob;

[ApiVersion(2, 1)]
public class Plugin : TerrariaPlugin
{
    #region 插件信息
    public static string PluginName => "怪物保护区"; // 插件名称
    public override string Name => PluginName;
    public override string Author => "Zaicon、羽学";
    public override Version Version => new(1, 0, 6);
    public override string Description => "为区域添加怪物保护选项";
    #endregion

    #region 文件路径
    public static readonly string MainPath = Path.Combine(TShock.SavePath, PluginName); // 主文件夹路径
    public static readonly string Paths = Path.Combine(MainPath, $"配置文件.json"); // 配置文件路径
    public static string CachePath(int worldID) => Path.Combine(MainPath, $"数据缓存_{worldID}.json"); // 缓存文件路径
    #endregion

    #region 注册与释放
    public Plugin(Main game) : base(game) => Order = 1;
    public override void Initialize()
    {
        ServerApi.Hooks.GameInitialize.Register(this, OnGameInitialize);
        ServerApi.Hooks.NpcAIUpdate.Register(this, OnNpcAIUpdate);
        ServerApi.Hooks.NpcKilled.Register(this, OnNpcKilled);
        RegionHooks.RegionEntered += OnRegionEntered;
        RegionHooks.RegionLeft += OnRegionLeft;
        RegionHooks.RegionDeleted += OnRegionDelete;
    }

    private void OnGameInitialize(EventArgs args)
    {
        LoadConfig(); // 加载配置文件
        GeneralHooks.ReloadEvent += ReloadConfig;
        Commands.ChatCommands.Add(new Command("diemob", PluginCommands.DieMobCommand, "diemob", "dmb"));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            GeneralHooks.ReloadEvent -= ReloadConfig;
            ServerApi.Hooks.GameInitialize.Deregister(this, OnGameInitialize);
            ServerApi.Hooks.NpcAIUpdate.Deregister(this, OnNpcAIUpdate);
            ServerApi.Hooks.NpcKilled.Deregister(this, OnNpcKilled);
            RegionHooks.RegionEntered -= OnRegionEntered;
            RegionHooks.RegionLeft -= OnRegionLeft;
            RegionHooks.RegionDeleted -= OnRegionDelete;
            Commands.ChatCommands.RemoveAll(x => x.CommandDelegate == PluginCommands.DieMobCommand);
        }
        base.Dispose(disposing);
    }
    #endregion

    #region 配置重载读取与写入方法
    internal static Configuration Config = new(); // 配置文件实例
    private static void ReloadConfig(ReloadEventArgs args)
    {
        LoadConfig();
        args.Player.SendMessage($"[{PluginName}]重新加载配置完毕。", color);
    }

    private static void LoadConfig()
    {
        // 创建主文件夹
        if (!Directory.Exists(MainPath))
            Directory.CreateDirectory(MainPath);

        Config = Configuration.Read();
        Config.Write();

        RegionStorage.Load(Main.worldID);
    }
    #endregion

    #region NPCAI更新事件
    private static Dictionary<int, DateTime> LastUpdate = new(); // NPC上次处理时间（用于频率限制）
    public static void OnNpcAIUpdate(NpcAiUpdateEventArgs args)
    {
        var npc = args.Npc;
        if (!Config.Enabled || npc is null || !npc.active) return;

        // 频率限制：每个NPC按配置间隔处理
        if (LastUpdate.TryGetValue(npc.whoAmI, out var last))
        {
            if ((DateTime.UtcNow - last).TotalMilliseconds < Config.UpdateInterval)
                return;
        }
        LastUpdate[npc.whoAmI] = DateTime.UtcNow;

        // 需要发送数据包
        bool needSend = false;

        foreach (var data in RegionStorage.Regions.ToArray())
        {
            if (data is null) continue;
            var region = TShock.Regions.GetRegionByName(data.RegionName);
            if (region == null)
            {
                // 区域已删除，自动清理
                RegionStorage.Remove(data.RegionName, Main.worldID);
                continue;
            }

            bool isFriendly = npc.friendly && data.AffectFriendlyNPCs && npc.type != NPCID.TargetDummy;
            bool isStatue = npc.SpawnedFromStatue && data.AffectStatueSpawns && npc.netID != NPCID.TargetDummy && npc.catchItem == 0;
            bool isNormal = !npc.friendly && !npc.SpawnedFromStatue && npc.type != NPCID.TargetDummy && npc.catchItem == 0;

            if (!(isFriendly || isStatue || isNormal)) continue;

            int tileX = (int)(npc.position.X / 16f);
            int tileY = (int)(npc.position.Y / 16f);

            if (!InArea(region, tileX, tileY)) continue;

            if (data.ReplaceMobs.TryGetValue(npc.type, out int newId))
            {
                npc.SetDefaults(newId);
                npc.type = newId;
                needSend = true;
            }
            else if (data.ReplaceMobs.TryGetValue(-100, out newId))
            {
                npc.SetDefaults(newId);
                npc.type = newId;
                needSend = true;
            }
            else if (data.Type == RegionType.击退)
            {
                var area = region.Area;
                int dx = area.Right - tileX < area.Width / 2 ? 10 : -10;
                int dy = area.Bottom - tileY < area.Height / 2 ? 10 : -10;
                npc.velocity = new Vector2(dx * Config.RepelPowerModifier, dy * Config.RepelPowerModifier);
                needSend = true;
            }
            else if (data.Type == RegionType.杀死)
            {
                npc.active = false;
                npc.type = 0;
                needSend = true;
            }
        }

        npc.netUpdate = needSend;
        args.Handled = needSend;
    }

    public static bool InArea(Region region, int x, int y)
    {
        return x >= region.Area.X && x <= region.Area.X + region.Area.Width + Config.RegionStretch &&
            y >= region.Area.Y && y <= region.Area.Y + region.Area.Height + Config.RegionStretch;
    }
    #endregion

    #region NPC死亡事件（清理字典）
    private void OnNpcKilled(NpcKilledEventArgs args)
    {
        if (LastUpdate.ContainsKey(args.npc.whoAmI))
            LastUpdate.Remove(args.npc.whoAmI);
    }
    #endregion

    #region 区域管理事件
    private void OnRegionEntered(RegionHooks.RegionEnteredEventArgs args)
    {
        var plr = args.Player;
        if (plr is null || !plr.IsLoggedIn || !Config.Enabled) return;
        var regionName = args.Region.Name;
        if (RegionStorage.Regions.Any(r => r.RegionName == regionName))
        {
            DieMobRegion? data = RegionStorage.Regions.FirstOrDefault(r => r.RegionName == regionName);
            if (data != null)
            {
                if (Config.Mess)
                {
                    if (!string.IsNullOrEmpty(data.Join))
                        plr.SendMessage(TextGradient($"\n{data.Join}", plr, data, regionName), color);

                    if (!string.IsNullOrEmpty(data.desc))
                        plr.SendMessage(TextGradient($"{data.desc}", plr, data, regionName), color);
                }
            }
        }
    }

    private void OnRegionLeft(RegionHooks.RegionLeftEventArgs args)
    {
        var plr = args.Player;
        if (plr is null || !plr.IsLoggedIn || !Config.Enabled || !Config.Mess) return;

        DieMobRegion? data = RegionStorage.Regions.FirstOrDefault(r => r.RegionName == args.Region.Name);
        if (data != null)
            if (!string.IsNullOrEmpty(data.Left))
                plr.SendMessage(TextGradient($"\n{data.Left}", plr, data, args.Region.Name), color);
    }

    private void OnRegionDelete(RegionHooks.RegionDeletedEventArgs args)
    {
        RegionStorage.Remove(args.Region.Name, Main.worldID);
    }
    #endregion

}