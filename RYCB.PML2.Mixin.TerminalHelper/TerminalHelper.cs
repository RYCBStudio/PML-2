using System.Text;
using System.Text.RegularExpressions;
using MEFrpLauncherX.Core.Languages;
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

    public Task<bool> InitializeAsync() => Task.Run(() => true);

    public Task<object?> ExecuteQueryAsync(string query, params object[] parameters) => null;

    public Task<int> ExecuteNonQueryAsync(string command, params object[] parameters) => null;

    public Task DisconnectAsync() => null;

    public static ISolution? GetSolution(string output)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine(output);
        if (output.Contains("失败: 端口不在允许范围内"))
        {
            return new ErrorSolution
            {
                Flag = "MT0005",
                Info = Languages.Text_TunnelError_InfoPortNotAllowed,
                Solution = [Languages.Text_TunnelError_SolutionCheckPort]
            };
        }

        if (output.Contains("流量已耗尽, 暂时无法启动隧道"))
        {
            return new ErrorSolution
            {
                Flag = "MT0006",
                Info = Languages.Text_TunnelError_InfoTrafficExhausted,
                Solution = [Languages.Text_TunnelError_SolutionCheckTraffic]
            };
        }

        if (output.Contains("隧道不存在"))
        {
            return new ErrorSolution
            {
                Flag = "MT0007",
                Info = Languages.Text_TunnelError_InfoTunnelNotExist,
                Solution = [Languages.Text_TunnelError_SolutionCheckTunnelExists]
            };
        }

        if (output.Contains("隧道当前在线, 请尝试使用强制下线隧道功能"))
        {
            return new ErrorSolution
            {
                Flag = "MT0004",
                Info = Languages.Text_TunnelError_InfoTunnelOnline,
                Solution = [Languages.Text_TunnelError_SolutionCheckTunnelExists]
            };
        }

        if (output.Contains("隧道已禁用, 请在网页端启用"))
        {
            return new ErrorSolution
            {
                Flag = "MT0008",
                Info = Languages.Text_TunnelError_InfoTunnelDisabled,
                Solution =
                [
                    Languages.Text_TunnelError_SolutionTunnelDisabledDetail,
                    Languages.Text_TunnelError_SolutionContactAdmin
                ]
            };
        }

        if (output.Contains("隧道所在节点不存在"))
        {
            return new ErrorSolution
            {
                Flag = "MT0009",
                Info = Languages.Text_TunnelError_InfoTunnelNodeNotExist,
                Solution = [Languages.Text_TunnelError_SolutionCheckTunnelNode]
            };
        }

        if (output.Contains("用户没有此节点的使用权限"))
        {
            return new ErrorSolution
            {
                Flag = "MT0010",
                Info = Languages.Text_TunnelError_InfoNoNodePermission,
                Solution = [Languages.Text_TunnelError_SolutionCheckPermission]
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
                    Info = Languages.Text_TunnelError_InfoConnectNodeFailed,
                    Solution =
                    [
                        Languages.Text_TunnelError_SolutionCheckNodeOnline,
                        Languages.Text_TunnelError_SolutionCheckNodeReachable
                    ]
                };
            }
        }

        if (output.Contains("connectex: No connection could be made because the target machine actively refused it."))
        {
            return new ErrorSolution
            {
                Flag = "MT0001",
                Info = Languages.Text_TunnelError_InfoCannotConnectLocal,
                Solution =
                [
                    Languages.Text_TunnelError_SolutionCheckServiceStarted,
                    Languages.Text_TunnelError_SolutionCheckPortOpen
                ]
            };
        }

        return null;
    }

    [GeneratedRegex(@"(\d{1,3}\.){3}\d{1,3}:\d{1,5}: i/o timeout")]
    private static partial Regex DetailedTcpError();
}