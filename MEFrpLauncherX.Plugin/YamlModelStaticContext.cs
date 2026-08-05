using MEFrpLauncherX.Plugin.Core;
using MEFrpLauncherX.Services;
using YamlDotNet.Serialization;

namespace MEFrpLauncherX.Plugin;

[YamlStaticContext]
[YamlSerializable(typeof(PluginDefinition))]
[YamlSerializable(typeof(TriggerDefinition))]
[YamlSerializable(typeof(ActionDefinition))]
[YamlSerializable(typeof(RawPlugin))]
[YamlSerializable(typeof(RawPluginMeta))]
public partial class YamlModelStaticContext : StaticContext
{
    
}