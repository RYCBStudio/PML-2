// PortScannerService.cs

using System.Net.Sockets;
using System.Text;

namespace RYCB.PML2.Extensions.MinecraftExtension;

public class PortScannerService
{
    public async Task<List<ScanResult>> ScanPortsAsync(
        string ipAddress,
        int startPort,
        int endPort,
        int timeout = 200,
        int maxConcurrent = 100,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ScanResult>();
        var semaphore = new SemaphoreSlim(maxConcurrent);
        var tasks = new List<Task>();

        for (var port = startPort; port <= endPort; port++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await semaphore.WaitAsync(cancellationToken);

            var task = Task.Run(async () =>
            {
                try
                {
                    var result = await CheckPortAsync(ipAddress, port, timeout, cancellationToken);
                    if (result.IsOpen)
                    {
                        lock (results)
                        {
                            results.Add(result);
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken);

            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
        return results;
    }

    public async Task<List<ScanResult>> ScanCommonMCPortsAsync(
        string ipAddress = "127.0.0.1",
        CancellationToken cancellationToken = default)
    {
        // MC常用端口范围
        var commonPorts = new List<int>
        {
            25565, // 默认MC端口
            25560, 25561, 25562, 25563, 25564, 25566, 25567, 25568, 25569, 25570,
            25575, // RCON端口
            25577, 25578, 25579,
            25580, 25581, 25582, 25583, 25584, 25585
        };

        var results = new List<ScanResult>();
        var tasks = new List<Task<ScanResult>>();

        foreach (var port in commonPorts)
        {
            tasks.Add(CheckPortAsync(ipAddress, port, 200, cancellationToken));
        }

        var scanResults = await Task.WhenAll(tasks);
        foreach (var result in scanResults)
        {
            if (result.IsOpen)
            {
                results.Add(result);
            }
        }

        return results;
    }

    public async Task<ScanResult> CheckPortAsync(
        string ipAddress,
        int port,
        int timeout,
        CancellationToken cancellationToken)
    {
        var result = new ScanResult { Port = port };

        using (var client = new TcpClient())
        {
            try
            {
                var connectTask = client.ConnectAsync(ipAddress, port);
                var timeoutTask = Task.Delay(timeout, cancellationToken);

                var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                if (completedTask == connectTask && connectTask.IsCompletedSuccessfully)
                {
                    result.IsOpen = true;

                    // 尝试识别MC服务器
                    if (await IsMinecraftServerAsync(client, cancellationToken))
                    {
                        result.ServiceName = "Minecraft Server";
                        result.Description = "我的世界游戏服务器";
                    }
                    else
                    {
                        result.ServiceName = "未知服务";
                        result.Description = "开放端口";
                    }

                    client.Close();
                }
            }
            catch
            {
                result.IsOpen = false;
            }
        }

        return result;
    }

    private async Task<bool> IsMinecraftServerAsync(TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            // MC服务器握手包检测
            var stream = client.GetStream();
            stream.ReadTimeout = 500;
            stream.WriteTimeout = 500;

            // 发送握手包
            var handshake = CreateMinecraftHandshake();
            await stream.WriteAsync(handshake, 0, handshake.Length, cancellationToken);

            // 尝试读取响应
            var buffer = new byte[1024];
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

            // 简单的MC服务器响应检测
            if (bytesRead > 0)
            {
                var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                return response.Contains("Minecraft") ||
                       response.Contains("MC") ||
                       CheckMinecraftPacket(buffer, bytesRead);
            }
        }
        catch
        {
            // 忽略错误，不是MC服务器
        }

        return false;
    }

    private byte[] CreateMinecraftHandshake()
    {
        // 创建一个简单的MC握手包
        using (var ms = new MemoryStream())
        using (var writer = new BinaryWriter(ms))
        {
            // 包ID (Handshake = 0x00)
            writer.Write((byte)0x00);
            // 协议版本 (随便一个版本号)
            WriteVarInt(writer, 47);
            // 服务器地址长度和地址
            WriteString(writer, "localhost");
            // 端口
            writer.Write((ushort)25565);
            // Next state (1 for status)
            WriteVarInt(writer, 1);

            return ms.ToArray();
        }
    }

    private void WriteVarInt(BinaryWriter writer, int value)
    {
        do
        {
            var temp = (byte)(value & 0b01111111);
            value >>= 7;
            if (value != 0)
            {
                temp |= 0b10000000;
            }

            writer.Write(temp);
        } while (value != 0);
    }

    private void WriteString(BinaryWriter writer, string str)
    {
        var bytes = Encoding.UTF8.GetBytes(str);
        WriteVarInt(writer, bytes.Length);
        writer.Write(bytes);
    }

    private bool CheckMinecraftPacket(byte[] buffer, int length)
    {
        // 简单的MC包格式检查
        if (length < 3)
        {
            return false;
        }

        // 检查包长度字段
        try
        {
            var index = 0;
            var packetLength = ReadVarInt(buffer, ref index);
            return packetLength > 0 && packetLength < 1000;
        }
        catch
        {
            return false;
        }
    }

    private int ReadVarInt(byte[] buffer, ref int index)
    {
        var value = 0;
        var size = 0;
        int b;

        while (((b = buffer[index++]) & 0x80) == 0x80)
        {
            value |= (b & 0x7F) << (size++ * 7);
            if (size > 5)
            {
                throw new Exception("VarInt too big");
            }
        }

        return value | ((b & 0x7F) << (size * 7));
    }

    public class ScanResult
    {
        public int Port
        {
            get;
            set;
        }

        public bool IsOpen
        {
            get;
            set;
        }

        public string ServiceName
        {
            get;
            set;
        }

        public string Description
        {
            get;
            set;
        }
    }
}