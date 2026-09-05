using Newtonsoft.Json;

namespace DieMob;

public enum RegionType
{
    杀死,
    击退,
}

public class DieMobRegion
{
    [JsonProperty("区域名称")]
    public string RegionName { get; set; } = string.Empty;
    [JsonProperty("处理类型(0杀死,1击退)")]
    public RegionType Type { get; set; } = RegionType.击退;
    [JsonProperty("替换表")]
    public Dictionary<int, int> ReplaceMobs { get; set; } = new();
    [JsonProperty("影响友方NPC")]
    public bool AffectFriendlyNPCs { get; set; } = false;
    [JsonProperty("影响雕像生成")]
    public bool AffectStatueSpawns { get; set; } = false;
    [JsonProperty("进入消息")]
    public string Join { get; set; } = "保护类型:{保护}\n影响友好NPC:{友好}\n影响雕像刷怪:{雕像}\n替换怪物数:{替换}个\n查看列表: /dmb rp {区域名}\n欢迎进入区域 [c/FFD700:{区域名}]";
    [JsonProperty("离开消息")]
    public string Left { get; set; } = "你已离开区域 [c/FFD700:{区域名}]";
    [JsonProperty("区域描述")]
    public string desc { get; set; } = string.Empty;

    public DieMobRegion() { }

    public DieMobRegion(string name)
    {
        RegionName = name;
        Type = RegionType.击退;
        ReplaceMobs = new Dictionary<int, int>();
        AffectFriendlyNPCs = false;
        AffectStatueSpawns = false;
    }
}
