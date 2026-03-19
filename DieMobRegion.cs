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
    [JsonProperty("保护区描述")]
    public string text { get; set; } = string.Empty;

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
