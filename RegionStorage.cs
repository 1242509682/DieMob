using Newtonsoft.Json;
using TShockAPI;
using static DieMob.Plugin;

namespace DieMob;

internal static class RegionStorage
{
    private static readonly object Lock = new object();
    public static List<DieMobRegion> Regions { get; private set; } = new();

    #region 读取数据
    public static void Load(int worldID)
    {
        lock (Lock)
        {
            if (!Directory.Exists(MainPath))
                Directory.CreateDirectory(MainPath);

            string file = CachePath(worldID);
            if (!File.Exists(file))
            {
                Regions.Clear();
                return;
            }

            try
            {
                var json = File.ReadAllText(file);
                var list = JsonConvert.DeserializeObject<List<DieMobRegion>>(json);
                if (list == null)
                {
                    TShock.Log.ConsoleError($"区域文件 {file} 格式错误，将使用空列表。");
                    Regions.Clear();
                    return;
                }

                // 直接加载所有数据，不验证区域是否存在（避免启动时误删）
                Regions = list;
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"读取区域文件失败: {ex.Message}");
                Regions.Clear();
            }
        }
    }
    #endregion

    #region 保存数据
    public static void Save(int worldID)
    {
        lock (Lock)
        {
            if (!Directory.Exists(MainPath))
                Directory.CreateDirectory(MainPath);

            string file = CachePath(worldID);

            if (Regions.Count == 0)
            {
                // 列表为空，删除文件（如果存在）
                if (File.Exists(file))
                    File.Delete(file);
                return;
            }

            File.WriteAllText(file, JsonConvert.SerializeObject(Regions, Formatting.Indented));
        }
    }
    #endregion

    #region 添加或更新数据
    public static void AddOrUpdate(DieMobRegion data, int worldId)
    {
        lock (Lock)
        {
            // 比较字符串
            var existing = Regions.FirstOrDefault(r => r.RegionName == data.RegionName);
            if (existing != null)
                Regions.Remove(existing);

            Regions.Add(data);
            Save(worldId);
        }
    }
    #endregion

    #region 移除单个数据
    public static void Remove(string regionName, int worldId)
    {
        lock (Lock)
        {
            // 比较字符串
            Regions.RemoveAll(r => r.RegionName == regionName);
            Save(worldId);
        }
    }
    #endregion
}