using System.Collections;
using ExecutionContext = MEFrpLauncherX.Plugin.Core.ExecutionContext;

namespace MEFrpLauncherX.Plugin.Condition;

public static class PropertyAccessor
{
    public static object? GetValue(object? root, string path)
    {
        if (root == null || string.IsNullOrEmpty(path)) return null;
        var parts = path.Split('.');
        object? current = root;

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (current == null) return null;
            if (current is Dictionary<string, object> dict)
            {
                current = dict.GetValueOrDefault(part);
            }
            else if (current is IList list && int.TryParse(part, out var index) && index >= 0 && index < list.Count)
            {
                current = list[index];
            }
            else if (current is ExecutionContext ctx)
            {
                // 路径形如 ctx.data.xxx / ctx.variables.xxx（首个部分为 ctx，随后是 data/variables 段）
                current = parts.ElementAtOrDefault(i + 1) switch
                {
                    "data" => ctx.Data,
                    "variables" => ctx.Variables,
                    _ => null
                };
                if (current is Dictionary<string, object> ctx_dict)
                {
                    current = ctx_dict.GetValueOrDefault(parts.Last());
                    break;
                }
            }
            else
            {
                return null;
            }
        }
        return current;
    }
}