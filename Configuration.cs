using Newtonsoft.Json;
using static DieMob.Plugin;

namespace DieMob;

internal class Configuration
{
    #region 配置项成员
    [JsonProperty("插件开关", Order = 0)]
    public bool Enabled { get; set; } = true;
    [JsonProperty("更新间隔", Order = 1)]
    public int UpdateInterval { get; set; } = 1000;
    [JsonProperty("排斥力修正系数", Order = 2)]
    public float RepelPowerModifier { get; set; } = 1f;
    [JsonProperty("区域拉伸", Order = 3)]
    public int RegionStretch { get; set; } = 10;
    #endregion

    #region 预设参数方法
    public void SetDefault()
    {

    }
    #endregion

    #region 读取与创建配置文件方法
    public void Write()
    {
        string json = JsonConvert.SerializeObject(this, Formatting.Indented);
        File.WriteAllText(Paths, json);
    }
    public static Configuration Read()
    {
        if (!File.Exists(Paths))
        {
            var NewConfig = new Configuration();
            NewConfig.SetDefault();
            NewConfig.Write();
            return NewConfig;
        }
        else
        {
            string jsonContent = File.ReadAllText(Paths);
            var config = JsonConvert.DeserializeObject<Configuration>(jsonContent)!;
            return config;
        }
    }
    #endregion
}