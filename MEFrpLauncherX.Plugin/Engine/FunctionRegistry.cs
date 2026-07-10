using MEFrpLauncherX.Plugin.Core;

namespace MEFrpLauncherX.Plugin.Engine;

public class FunctionRegistry
{
    private readonly Dictionary<string, List<ActionDefinition>> _functions = new();

    public void Define(string name, List<ActionDefinition> actions) => _functions[name] = actions;
    public List<ActionDefinition>? Get(string name) => _functions.GetValueOrDefault(name);
}