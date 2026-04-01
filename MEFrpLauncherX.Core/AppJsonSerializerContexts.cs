using System.Text.Json;
using System.Text.Json.Serialization;
using MEFrpLauncherX.Core.MEFIntergrated;

namespace MEFrpLauncherX;

[JsonSerializable(typeof(InfoClasses.UserInfo))]
[JsonSerializable(typeof(JsonElement))]
public partial class AppJsonSerializerContext : JsonSerializerContext
{
}