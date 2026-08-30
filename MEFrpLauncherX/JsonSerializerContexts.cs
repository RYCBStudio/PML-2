using System.Collections.Generic;
using System.Text.Json.Serialization;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.MEFIntegrated;
using MEFrpLauncherX.Core.Styling;
using MEFrpLauncherX.Services;
using MEFrpLauncherX.ViewModels;
using MEFrpLauncherX.Views;
using RYCB.PML2.MEFrpCaptchaLib;

namespace MEFrpLauncherX;

[JsonSerializable(typeof(InfoClasses.ApiInfo<InfoClasses.UserInfo>))]
[JsonSerializable(typeof(InfoClasses.ApiInfo<InfoClasses.ConfigInfo>))]
[JsonSerializable(typeof(InfoClasses.ApiInfo<object>))]
[JsonSerializable(typeof(InfoClasses.ApiInfo<InfoClasses.TrafficStatus>))]
[JsonSerializable(typeof(InfoClasses.ApiInfo<InfoClasses.FrpTokenInfo>))]
[JsonSerializable(typeof(InfoClasses.ApiInfo<InfoClasses.ProxyInfo>))]
[JsonSerializable(typeof(InfoClasses.ApiInfo<InfoClasses.NodesListInfo>))]
[JsonSerializable(typeof(InfoClasses.ApiInfo<InfoClasses.NodeNameList>))]
[JsonSerializable(typeof(InfoClasses.ApiInfo<InfoClasses.NodesStatusInfo>))]
[JsonSerializable(typeof(InfoClasses.ApiInfo<InfoClasses.PublicData>))]
[JsonSerializable(typeof(InfoClasses.ApiInfo<InfoClasses.SystemStatus>))]
[JsonSerializable(typeof(InfoClasses.ApiInfo<InfoClasses.ExtraUserInfo>))]
[JsonSerializable(typeof(InfoClasses.ConfigInfo))]
[JsonSerializable(typeof(InfoClasses.TrafficStatus))]
[JsonSerializable(typeof(InfoClasses.FrpTokenInfo))]
[JsonSerializable(typeof(InfoClasses.ProxyInfo))]
[JsonSerializable(typeof(InfoClasses.Nodes))]
[JsonSerializable(typeof(InfoClasses.Proxies))]
[JsonSerializable(typeof(InfoClasses.NodesListInfo))]
[JsonSerializable(typeof(InfoClasses.NodeInfo))]
[JsonSerializable(typeof(InfoClasses.NodeNameList))]
[JsonSerializable(typeof(InfoClasses.NodesStatusInfo))]
[JsonSerializable(typeof(InfoClasses.NodeStatus))]
[JsonSerializable(typeof(InfoClasses.PublicData))]
[JsonSerializable(typeof(InfoClasses.SystemStatus))]
[JsonSerializable(typeof(InfoClasses.UserInfo4Login))]
[JsonSerializable(typeof(InfoClasses.VaptchaInfo))]
[JsonSerializable(typeof(InfoClasses.LoginInfo))]
[JsonSerializable(typeof(InfoClasses.ExtraUserInfo))]
[JsonSerializable(typeof(StartupData))]
[JsonSerializable(typeof(NoticeData))]
[JsonSerializable(typeof(HitokotoResource))]
[JsonSerializable(typeof(FeedbackBody))]
[JsonSerializable(typeof(FeedbackResponse))]
[JsonSerializable(typeof(EmailBody))]
[JsonSerializable(typeof(SingleVersionInfo))]
[JsonSerializable(typeof(NoticeContent))]
[JsonSerializable(typeof(TunnelErrorInfosShell))]
[JsonSerializable(typeof(SingleApiInfo<TunnelErrorInfo>))]
[JsonSerializable(typeof(SingleApiInfo<NoticeContent[]>))]
[JsonSerializable(typeof(TunnelErrorInfo))]
[JsonSerializable(typeof(SplashPipeMessage))]
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(UpdateSettings))]
[JsonSerializable(typeof(BackgroundSettings))]
[JsonSerializable(typeof(PFSConfig))]
[JsonSerializable(typeof(ALPConfig))]
[JsonSerializable(typeof(List<ALPConfig>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<OnlineTheme>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(ThemeManifest))]
[JsonSerializable(typeof(RedeemInfo))]
[JsonSerializable(typeof(InfoClasses.CreateProxyRequestData))]
[JsonSerializable(typeof(ChallengeInfo))]
[JsonSerializable(typeof(AlistListFileRequestBody))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
public partial class AppJsonSerializerContext : JsonSerializerContext
{
}