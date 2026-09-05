using MEFrpLauncherX.Plugin.Core;
using MEFrpLauncherX.Plugin.Services;
using YamlDotNet.Serialization;

namespace MEFrpLauncherX.Plugin;

[YamlStaticContext]
[YamlSerializable(typeof(PluginDefinition))]
[YamlSerializable(typeof(TriggerDefinition))]
[YamlSerializable(typeof(ActionDefinition))]
[YamlSerializable(typeof(RawPlugin))]
[YamlSerializable(typeof(RawPluginMeta))]
[YamlSerializable(typeof(ProxyTemplateDefinition))]
[YamlSerializable(typeof(ProxyTemplateIconDefinition))]
[YamlSerializable(typeof(ProxyTemplateCreateDefinition))]
[YamlSerializable(typeof(ProxyTemplateNodeFilterDefinition))]
[YamlSerializable(typeof(ProxyTemplateExtraTunnelDefinition))]
public partial class YamlModelStaticContext : StaticContext
{
    
}