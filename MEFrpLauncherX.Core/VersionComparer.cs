using System.Text.RegularExpressions;

namespace MEFrpLauncherX.Core;

public enum ReleaseFlag
{
    None,
    Preview,
    RC
}

public class VersionInfo
{
    public int Major
    {
        get;
        set;
    }

    public int Minor
    {
        get;
        set;
    }

    public int Patch
    {
        get;
        set;
    }

    public int? Build
    {
        get;
        set;
    }

    public ReleaseFlag Flag
    {
        get;
        set;
    }

    public int? FlagNumber
    {
        get;
        set;
    }
}

public static class VersionComparer
{
    private static readonly Regex VersionRegex = new(
        @"^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:\.(?<build>\d+))?(?:\-(?<flag>preview|rc)(?<flagnum>\d+))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    /// <summary>
    ///     解析版本字符串
    /// </summary>
    /// <param name="versionString">版本字符串（格式：x.y.z.a(-FLAGn) 或 x.y.z(-FLAGn)）</param>
    /// <returns>解析后的VersionInfo对象</returns>
    public static VersionInfo ParseVersion(string versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString))
        {
            throw new ArgumentException("版本字符串不能为空", nameof(versionString));
        }

        var match = VersionRegex.Match(versionString);
        if (!match.Success)
        {
            throw new ArgumentException($"版本字符串格式无效: {versionString}", nameof(versionString));
        }

        var version = new VersionInfo
        {
            Major = int.Parse(match.Groups["major"].Value),
            Minor = int.Parse(match.Groups["minor"].Value),
            Patch = int.Parse(match.Groups["patch"].Value)
        };

        // 解析构建号（可选）
        if (match.Groups["build"].Success && !string.IsNullOrEmpty(match.Groups["build"].Value))
        {
            version.Build = int.Parse(match.Groups["build"].Value);
        }

        // 解析发布标志和标志编号（可选）
        if (match.Groups["flag"].Success && !string.IsNullOrEmpty(match.Groups["flag"].Value))
        {
            var flagStr = match.Groups["flag"].Value.ToLower();
            version.Flag = flagStr switch
            {
                "preview" => ReleaseFlag.Preview,
                "rc" => ReleaseFlag.RC,
                _ => ReleaseFlag.None
            };

            if (match.Groups["flagnum"].Success && !string.IsNullOrEmpty(match.Groups["flagnum"].Value))
            {
                version.FlagNumber = int.Parse(match.Groups["flagnum"].Value);
            }
            else
            {
                throw new ArgumentException($"发布标志必须包含编号: {versionString}", nameof(versionString));
            }
        }

        return version;
    }

    /// <summary>
    ///     比较两个版本号的大小
    /// </summary>
    /// <param name="version1">第一个版本字符串</param>
    /// <param name="version2">第二个版本字符串</param>
    /// <returns>
    ///     -1: version1 &lt; version2<p />
    ///     <p>0: version1 = version2</p>
    ///     1: version1 > version2<p />
    /// </returns>
    public static int CompareVersions(string version1, string version2)
    {
        var v1 = ParseVersion(version1);
        var v2 = ParseVersion(version2);

        // 1. 比较主版本号
        var comparison = v1.Major.CompareTo(v2.Major);
        if (comparison != 0)
        {
            return comparison;
        }

        // 2. 比较次版本号
        comparison = v1.Minor.CompareTo(v2.Minor);
        if (comparison != 0)
        {
            return comparison;
        }

        // 3. 比较修订号
        comparison = v1.Patch.CompareTo(v2.Patch);
        if (comparison != 0)
        {
            return comparison;
        }

        // 4. 比较构建号（如果存在）
        comparison = CompareNullableInt(v1.Build, v2.Build);
        if (comparison != 0)
        {
            return comparison;
        }

        // 5. 比较发布标志
        comparison = CompareReleaseFlags(v1.Flag, v2.Flag);
        if (comparison != 0)
        {
            return comparison;
        }

        // 6. 比较标志编号
        comparison = CompareNullableInt(v1.FlagNumber, v2.FlagNumber);
        return comparison;
    }

    /// <summary>
    ///     比较可空整数
    /// </summary>
    private static int CompareNullableInt(int? a, int? b)
    {
        if (!a.HasValue && !b.HasValue)
        {
            return 0;
        }

        if (!a.HasValue)
        {
            return -1; // 没有值的版本号比有值的更小（如：1.0.0 < 1.0.0.1）
        }

        if (!b.HasValue)
        {
            return 1;
        }

        return a.Value.CompareTo(b.Value);
    }

    /// <summary>
    ///     比较发布标志（正式版 > RC > Preview）
    /// </summary>
    private static int CompareReleaseFlags(ReleaseFlag flag1, ReleaseFlag flag2)
    {
        if (flag1 == flag2)
        {
            return 0;
        }

        // 权重：None（正式版）= 2, RC = 1, Preview = 0
        var weight1 = flag1 switch
        {
            ReleaseFlag.None => 2,
            ReleaseFlag.RC => 1,
            ReleaseFlag.Preview => 0,
            _ => 0
        };

        var weight2 = flag2 switch
        {
            ReleaseFlag.None => 2,
            ReleaseFlag.RC => 1,
            ReleaseFlag.Preview => 0,
            _ => 0
        };

        return weight1.CompareTo(weight2);
    }

    /// <summary>
    ///     判断版本1是否大于版本2
    /// </summary>
    public static bool IsGreaterThan(string version1, string version2) => CompareVersions(version1, version2) > 0;

    /// <summary>
    ///     判断版本1是否小于版本2
    /// </summary>
    public static bool IsLessThan(string version1, string version2) => CompareVersions(version1, version2) < 0;

    /// <summary>
    ///     判断版本1是否等于版本2
    /// </summary>
    public static bool IsEqualTo(string version1, string version2) => CompareVersions(version1, version2) == 0;
}