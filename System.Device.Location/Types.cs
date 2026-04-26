namespace System.Device.Location;

public class WarnSolution
{
}

/// <summary>
///     代表一个位置坐标
/// </summary>
public record LocationCoordinate
{
    /// <summary>
    ///     经度
    /// </summary>
    public double Longitude
    {
        get;
        set;
    } = 0;

    /// <summary>
    ///     纬度
    /// </summary>
    public double Latitude
    {
        get;
        set;
    } = 0;
}

public class LocationNameInfo
{
    /// <summary>
    ///     详细地名
    /// </summary>
    public string affiliation
    {
        get;
        set;
    }

    /// <summary>
    ///     中国天气ID
    /// </summary>
    public string key
    {
        get;
        set;
    }

    public string latitude
    {
        get;
        set;
    }

    public string locationKey
    {
        get;
        set;
    }

    public string longitude
    {
        get;
        set;
    }

    public string name
    {
        get;
        set;
    }

    public int status
    {
        get;
        set;
    }

    public int timeZoneShift
    {
        get;
        set;
    }
}