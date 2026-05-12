using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace MEFrpLauncherX.Core;

public static class StringUtils
{
    public static string Base64Encode(string plainText)
    {
        var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
        return Convert.ToBase64String(plainTextBytes);
    }

    public static string EncodeToBase64(this string plainText)
    {
        var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
        return Convert.ToBase64String(plainTextBytes);
    }

    public static string Base64Decode(string base64EncodedData)
    {
        var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
        return Encoding.UTF8.GetString(base64EncodedBytes);
    }
}

public static class IEnumerableUtils
{
}

public static class DeviceIdHelper
{
    // 存储设备ID的本地路径（跨平台）
    private static readonly string _deviceIdPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "Config",
        "device.id");

    /// <summary>
    ///     获取设备唯一ID（优先读本地，没有则生成）
    /// </summary>
    public static string GetDeviceUniqueId()
    {
        // 1. 先读本地存储的ID
        if (File.Exists(_deviceIdPath))
        {
            try
            {
                return File.ReadAllText(_deviceIdPath).Trim();
            }
            catch
            {
                /* 读取失败，重新生成 */
            }
        }

        // 2. 生成新ID
        var deviceId = GenerateDeviceId();

        // 3. 保存到本地
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_deviceIdPath));
            File.WriteAllText(_deviceIdPath, deviceId);
        }
        catch
        {
            /* 保存失败不影响，下次重新生成 */
        }

        return deviceId;
    }

    /// <summary>
    ///     生成跨平台设备唯一ID
    /// </summary>
    private static string GenerateDeviceId()
    {
        try
        {
            // 收集硬件特征（不同系统取不同标识）
            var hardwareInfo = GetPlatformSpecificHardwareInfo();

            // 哈希加密（不可逆，避免泄露原始硬件信息）
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(hardwareInfo));

            // 转成32位字符串（简洁、唯一）
            return BitConverter.ToString(hashBytes)
                .Replace("-", "")
                .Substring(0, 32)
                .ToLower();
        }
        catch
        {
            // 极端情况：读不到硬件信息 → 生成GUID（重启后不变，因为存本地）
            return Guid.NewGuid().ToString("N");
        }
    }

    /// <summary>
    ///     按系统获取硬件特征
    /// </summary>
    private static string GetPlatformSpecificHardwareInfo()
    {
        var os = Environment.OSVersion.Platform switch
        {
            PlatformID.Win32NT => "Windows",
            PlatformID.Unix => "Linux",
            PlatformID.MacOSX => "MacOS",
            _ => "Other"
        };
        if (OperatingSystem.IsMacOS())
        {
            os = "MacOS";
        }

        return os switch
        {
            // Windows：取主板ID + 网卡MAC
            "Windows" => GetWindowsHardwareId(),
            // macOS：取系统序列号
            "MacOS" => GetMacOsHardwareId(),
            // Linux：取机器ID（/etc/machine-id）
            "Linux" => GetLinuxHardwareId(),
            _ => Guid.NewGuid().ToString()
        };
    }

    #region 各系统具体实现

    private static string GetWindowsHardwareId()
    {
        try
        {
            // 主板ID
            var mbId = ExecuteCommand("wmic", "baseboard get serialnumber");
            // 网卡MAC（排除虚拟网卡）
            var macId = ExecuteCommand("wmic", "nic where netenabled=true get macaddress");

            return $"{mbId}_{macId}";
        }
        catch
        {
            return Guid.NewGuid().ToString();
        }
    }

    private static string GetMacOsHardwareId()
    {
        try
        {
            // macOS 系统序列号
            return ExecuteCommand("ioreg", "-l | grep IOPlatformSerialNumber");
        }
        catch
        {
            return Guid.NewGuid().ToString();
        }
    }

    private static string GetLinuxHardwareId()
    {
        try
        {
            // Linux 机器ID（系统生成，永久唯一）
            return File.ReadAllText("/etc/machine-id").Trim();
        }
        catch
        {
            return Guid.NewGuid().ToString();
        }
    }

    // 执行系统命令
    private static string ExecuteCommand(string fileName, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        // 清理输出，只保留有效字符
        return string.Join("", output.Where(c => !char.IsWhiteSpace(c))).Trim();
    }

    #endregion
}