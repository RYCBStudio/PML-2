using System.Collections;

namespace MEFrpLauncherX.Plugin.Condition;

public static class PropertyAccessor
{
    public static object? GetValue(object? root, string path)
    {
        if (root == null || string.IsNullOrEmpty(path)) return null;
        var parts = path.Split('.');
        object? current = root;

        foreach (var part in parts)
        {
            if (current == null) return null;
            if (current is Dictionary<string, object> dict)
            {
                current = dict.GetValueOrDefault(part);
            }
            else if (current is IList list && int.TryParse(part, out var index) && index >= 0 && index < list.Count)
            {
                current = list[index];
            }
            else
            {
                return null;
            }
        }
        return current;
    }
}