using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using ExecutionContext = MEFrpLauncherX.Plugin.Core.ExecutionContext;

namespace MEFrpLauncherX.Plugin.Condition;

public interface ICondition
{
    bool Evaluate(ExecutionContext ctx);
}

/// <summary>
///     值表达式节点（26.3.1 M4）：常量 / 路径 / 算术表达式，供比较操作符两侧使用。
/// </summary>
public interface IValueNode
{
    object? Evaluate(ExecutionContext ctx);
}

/// <summary>路径取值节点（ctx.data.xxx / ctx.variables.xxx）</summary>
public class PathValueNode : IValueNode
{
    public string Path { get; set; } = "";

    public object? Evaluate(ExecutionContext ctx) => PropertyAccessor.GetValue(ctx, Path);
}

/// <summary>常量节点（数字 / 字符串字面量）</summary>
public class LiteralValueNode : IValueNode
{
    public object Value { get; set; } = "";

    public object? Evaluate(ExecutionContext ctx) => Value;
}

/// <summary>算术表达式节点：支持 + - * / 与括号，数值类型为主（26.3.1 M4）</summary>
public class ArithmeticNode : IValueNode
{
    public IValueNode Left { get; set; } = null!;

    public string Operator { get; set; } = "";

    public IValueNode Right { get; set; } = null!;

    public object? Evaluate(ExecutionContext ctx)
    {
        var l = ToNumber(Left.Evaluate(ctx));
        var r = ToNumber(Right.Evaluate(ctx));
        return Operator switch
        {
            "+" => l + r,
            "-" => l - r,
            "*" => l * r,
            "/" => l / r,
            _ => throw new InvalidOperationException($"不支持的算术运算符: {Operator}")
        };
    }

    private static double ToNumber(object? value)
    {
        if (value is double d) return d;
        if (value is int i) return i;
        if (value is long l) return l;
        if (value is decimal m) return (double)m;
        if (value is string s && double.TryParse(s, out var parsed)) return parsed;
        throw new InvalidOperationException($"算术表达式要求数值操作数, 实际为: {value?.GetType().Name ?? "null"}");
    }
}

/// <summary>内置函数调用节点（26.3.1 M5）：len / lower / upper / coalesce / min / max / now</summary>
public class FunctionCallNode : IValueNode
{
    public string Name { get; set; } = "";

    public List<IValueNode> Arguments { get; set; } = [];

    public object? Evaluate(ExecutionContext ctx)
    {
        var args = Arguments.Select(a => a.Evaluate(ctx)).ToArray();
        return ExpressionFunctions.Invoke(Name, args);
    }
}

/// <summary>
///     表达式内置函数库 v1（26.3.1 M5）。
///     函数名小写；参数按需取值（字符串 / 数值 / 集合）。未知函数或参数错误抛出异常，由调用方转为日志，不崩 UI。
/// </summary>
public static class ExpressionFunctions
{
    public static object? Invoke(string name, object?[] args)
    {
        return name switch
        {
            "len" => Len(args),
            "lower" => Lower(args),
            "upper" => Upper(args),
            "coalesce" => Coalesce(args),
            "min" => MinMax(args, min: true),
            "max" => MinMax(args, min: false),
            "now" => DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            _ => throw new InvalidOperationException($"未知函数: {name}")
        };
    }

    private static int Len(object?[] args)
    {
        if (args.Length != 1) throw new InvalidOperationException("len 需要 1 个参数");
        return args[0] switch
        {
            null => 0,
            string s => s.Length,
            ICollection c => c.Count,
            IEnumerable e => e.Cast<object>().Count(),
            _ => args[0]?.ToString()?.Length ?? 0
        };
    }

    private static string Lower(object?[] args)
    {
        if (args.Length != 1) throw new InvalidOperationException("lower 需要 1 个参数");
        return args[0]?.ToString()?.ToLowerInvariant() ?? "";
    }

    private static string Upper(object?[] args)
    {
        if (args.Length != 1) throw new InvalidOperationException("upper 需要 1 个参数");
        return args[0]?.ToString()?.ToUpperInvariant() ?? "";
    }

    private static object? Coalesce(object?[] args)
    {
        if (args.Length == 0) throw new InvalidOperationException("coalesce 至少需要 1 个参数");
        return args.FirstOrDefault(a => a != null && !(a is string s && s.Length == 0));
    }

    private static double MinMax(object?[] args, bool min)
    {
        if (args.Length < 2) throw new InvalidOperationException("min/max 至少需要 2 个参数");
        var nums = args.Select(ToNumber).ToArray();
        return min ? nums.Min() : nums.Max();
    }

    private static double ToNumber(object? value)
    {
        if (value is double d) return d;
        if (value is int i) return i;
        if (value is long l) return l;
        if (value is decimal m) return (double)m;
        if (value is string s && double.TryParse(s, out var parsed)) return parsed;
        throw new InvalidOperationException($"min/max 要求数值参数, 实际为: {value?.GetType().Name ?? "null"}");
    }
}

public partial class CompareCondition : ICondition
{
    public IValueNode Left { get; set; } = null!;

    public string Operator { get; set; } = "";

    public IValueNode Right { get; set; } = null!;

    public bool Evaluate(ExecutionContext ctx)
    {
        var leftVal = Left.Evaluate(ctx);
        var rightVal = Right.Evaluate(ctx);
        return Operator switch
        {
            "-eq" or "==" => ValueEquals(leftVal, rightVal),
            "-ne" or "!=" => !ValueEquals(leftVal, rightVal),
            "-gt" or ">" => Compare(leftVal, rightVal) > 0,
            "-lt" or "<" => Compare(leftVal, rightVal) < 0,
            "-ge" or ">=" => Compare(leftVal, rightVal) >= 0,
            "-le" or "<=" => Compare(leftVal, rightVal) <= 0,
            "-like" => LikeMatch(leftVal?.ToString(), rightVal?.ToString()),
            "-notlike" => !LikeMatch(leftVal?.ToString(), rightVal?.ToString()),
            "-contains" => ContainsCompare(leftVal, rightVal),
            "-notcontains" => !ContainsCompare(leftVal, rightVal),
            "-in" => InCompare(leftVal, rightVal),
            "-notin" => !InCompare(leftVal, rightVal),
            _ => false
        };
    }

    private static bool IsNumeric(object? v) => v is int or long or double or float or decimal;

    /// <summary>数值类型归一后比较（double 9 与 int 9 视为相等，26.3.1 M4）</summary>
    private static bool ValueEquals(object? a, object? b) => IsNumeric(a) && IsNumeric(b)
        ? Convert.ToDouble(a, System.Globalization.CultureInfo.InvariantCulture) ==
          Convert.ToDouble(b, System.Globalization.CultureInfo.InvariantCulture)
        : Equals(a, b);

    private int Compare(object? a, object? b) => IsNumeric(a) && IsNumeric(b)
        ? Convert.ToDouble(a, System.Globalization.CultureInfo.InvariantCulture)
            .CompareTo(Convert.ToDouble(b, System.Globalization.CultureInfo.InvariantCulture))
        : Comparer<object>.Default.Compare(a, b);

    private bool LikeMatch(string? input, string? pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return string.IsNullOrEmpty(input);
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return LikeRegex().IsMatch(input ?? "");
    }

    private bool ContainsCompare(object? a, object? b)
    {
        if (a == null || b == null) return false;
        if (a is string strA) return strA.Contains(b.ToString() ?? "");
        if (a is IEnumerable list)
        {
            return list.Cast<object>().Any(item => Equals(item, b));
        }
        return false;
    }

    private bool InCompare(object? a, object? b)
    {
        if (a == null || b == null) return false;
        if (b is string strB) return strB.Contains(a.ToString() ?? "");
        if (b is IEnumerable list)
        {
            return list.Cast<object>().Any(item => Equals(item, a));
        }
        return false;
    }

    [GeneratedRegex("...", RegexOptions.IgnoreCase)]
    private static partial Regex LikeRegex();
}

public class LogicCondition : ICondition
{
    public ICondition Left
    {
        get;
        set;
    }

    public string Operator
    {
        get;
        set;
    } = "";

    public ICondition Right
    {
        get;
        set;
    }

    public bool Evaluate(ExecutionContext ctx) => Operator switch
    {
        "-and" or "&&" => Left.Evaluate(ctx) && Right.Evaluate(ctx),
        "-or" or "||" => Left.Evaluate(ctx) || Right.Evaluate(ctx),
        "-xor" or "^" or "^|" => Left.Evaluate(ctx) ^ Right.Evaluate(ctx),
        _ => false
    };
}