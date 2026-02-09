using System.Text;
using System.Text.RegularExpressions;
using MEFrpLauncherX.Core.Plugin;

namespace RYCB.PML2.Mixin.TerminalHelper;

public partial class TerminalHelper : ILogicalPlugin
{
    public string Name
    {
        get;
    } = "RYCB.PML2.Mixin.TerminalHelper";

    public string Description
    {
        get;
    } = "RYCB.PML2.Mixin.TerminalHelper";

    public Version Version
    {
        get;
    } = new(2, 2, 0, 2);

    public Task<bool> InitializeAsync()
    {
        return Task.Run(() => true);
    }

    public static ISolution? GetSolution(string output)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine(output);
        if (output.Contains("失败: 端口不在允许范围内"))
        {
            return new ErrorSolution
            {
                Flag = "MT0005",
                Info = "端口不在允许范围内",
                Solution = ["请检查端口是否在允许范围内"]
            };
        }

        if (output.Contains("流量已耗尽, 暂时无法启动隧道"))
        {
            return new ErrorSolution
            {
                Flag = "MT0006",
                Info = "流量已耗尽",
                Solution = ["请检查流量是否已耗尽"]
            };
        }

        if (output.Contains("隧道不存在"))
        {
            return new ErrorSolution
            {
                Flag = "MT0007",
                Info = "隧道不存在",
                Solution = ["请检查隧道是否存在"]
            };
        }

        if (output.Contains("隧道当前在线, 请尝试使用强制下线隧道功能"))
        {
            return new ErrorSolution
            {
                Flag = "MT0004",
                Info = "隧道在线",
                Solution = ["请检查隧道是否存在"]
            };
        }

        if (output.Contains("隧道已禁用, 请在网页端启用"))
        {
            return new ErrorSolution
            {
                Flag = "MT0008",
                Info = "隧道已禁用",
                Solution =
                [
                    "请检查隧道是否被禁用 (在强制下线隧道时会被禁用), 请重新启用",
                    "若非本人操作, 请联系管理员协助处理"
                ]
            };
        }

        if (output.Contains("隧道所在节点不存在"))
        {
            return new ErrorSolution
            {
                Flag = "MT0009",
                Info = "隧道所在节点不存在",
                Solution = ["请检查隧道所在节点是否存在"]
            };
        }

        if (output.Contains("用户没有此节点的使用权限"))
        {
            return new ErrorSolution
            {
                Flag = "MT0010",
                Info = "用户没有此节点的使用权限",
                Solution = ["请检查用户是否有此节点的使用权限"]
            };
        }

        if (output.Contains("连接节点失败:"))
        {
            var detailRegex = DetailedTcpError();
            var match = detailRegex.Match(output);
            if (match.Success)
            {
                return new ErrorSolution
                {

                    Flag = "MT0011",
                    Info = "连接节点失败",
                    Solution = ["请检查节点是否在线", "请检查节点是否可达"]
                };
            }
        }

        if (output.Contains("connectex: No connection could be made because the target machine actively refused it."))
        {
            return new ErrorSolution
            {
                Flag = "MT0001",
                Info = "无法连接到本地服务",
                Solution = ["请检查是否已打开服务", "请检查该端口是否已开放"]
            };
        }

        return null;
    }

    public Task<object?> ExecuteQueryAsync(string query, params object[] parameters)
    {
        return null;
    }

    public Task<int> ExecuteNonQueryAsync(string command, params object[] parameters)
    {
        return null;
    }

    public Task DisconnectAsync()
    {
        return null;
    }

    [GeneratedRegex(@"(\d{1,3}\.){3}\d{1,3}:\d{1,5}: i/o timeout")]
    private static partial Regex DetailedTcpError();
}