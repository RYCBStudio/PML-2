namespace MEFrpLauncherX.Core.Helpers;

public static class NodeLocationMapper
{
    public static readonly List<string> ChineseProvinces =
    [
        "北京市", "天津市", "上海市", "重庆市",
        "河北省", "山西省", "辽宁省", "吉林省", "黑龙江省",
        "江苏省", "浙江省", "安徽省", "福建省", "江西省", "山东省",
        "河南省", "湖北省", "湖南省", "广东省", "海南省", "四川省",
        "贵州省", "云南省", "陕西省", "甘肃省", "青海省", "台湾省",
        "内蒙古自治区", "广西壮族自治区", "西藏自治区", "宁夏回族自治区", "新疆维吾尔自治区",
        "香港特别行政区", "澳门特别行政区"
    ];
    
    private static readonly Dictionary<string, (double Lat, double Lon)> CityCoord = new()
    {
        // 中国大陆
        ["北京"] = (39.9042, 116.4074),
        ["上海"] = (31.2304, 121.4737),
        ["广州"] = (23.1291, 113.2644),
        ["深圳"] = (22.5431, 114.0579),
        ["成都"] = (30.5728, 104.0668),
        ["武汉"] = (30.5928, 114.3055),
        ["贵阳"] = (26.6477, 106.6302),
        ["大连"] = (38.9140, 121.6147),
        ["肇庆"] = (23.0516, 112.4656),
        ["襄阳"] = (32.0085, 112.1224),
        ["张家口"] = (40.7695, 114.8859),
        ["重庆"] = (29.4316, 106.9123),
        ["杭州"] = (30.2875, 120.1536),
        ["南京"] = (32.0415, 118.7674),
        ["西安"] = (34.3416, 108.9402),
        // 港澳台
        ["香港"] = (22.3193, 114.1694),
        ["台湾"] = (25.0330, 121.5654),   // 台北
        // 国际
        ["东京"] = (35.6895, 139.6917),
        ["首尔"] = (37.5665, 126.9780),
        ["新加坡"] = (1.3521, 103.8198),
        ["纽约"] = (40.7128, -74.0060),
        ["圣克拉拉"] = (37.3541, -121.9552),
        ["洛杉矶"] = (34.0522, -118.2437),
        ["弗里蒙特"] = (37.5485, -121.9886),
        ["圣何塞"] = (37.3382, -121.8863)
    };

    public static (double Lat, double Lon)? GetCoordinates(string nodeName)
    {
        foreach (var (city, coord) in CityCoord)
            if (nodeName.Contains(city))
                return coord;
        return null;
    }

    public static string GetCityName(string nodeName)
    {
        foreach (var city in CityCoord.Keys)
            if (nodeName.Contains(city))
                return city;
        return "未知";
    }
}