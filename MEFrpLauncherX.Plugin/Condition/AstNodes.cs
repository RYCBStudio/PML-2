using System.Collections;
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
    public string Path
    {
        get;
        set;
    } = "";

    public object? Evaluate(ExecutionContext ctx) => PropertyAccessor.GetValue(ctx, Path);
}

/// <summary>常量节点（数字 / 字符串字面量）</summary>
public class LiteralValueNode : IValueNode
{
    public object Value
    {
        get;
        set;
    } = "";

    public object? Evaluate(ExecutionContext ctx) => Value;
}

/// <summary>算术表达式节点：支持 + - * / 与括号，数值类型为主（26.3.1 M4）</summary>
public class ArithmeticNode : IValueNode
{
    public IValueNode Left
    {
        get;
        set;
    } = null!;

    public string Operator
    {
        get;
        set;
    } = "";

    public IValueNode Right
    {
        get;
        set;
    } = null!;

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
            "%" => l % r,
            "**" or "^" => Math.Pow(l, r),
            "//" => Math.Floor(l / r),
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
    public string Name
    {
        get;
        set;
    } = "";

    public List<IValueNode> Arguments
    {
        get;
        set;
    } = [];

    public object? Evaluate(ExecutionContext ctx)
    {
        var args = Arguments.Select(a => a.Evaluate(ctx)).ToArray();
        return ExpressionFunctions.Invoke(Name, args);
    }
}

/// <summary>
///     表达式内置函数库 v2（26.5）。
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
            "abs" => Abs(args),
            "round" => Round(args),
            "floor" => Floor(args),
            "ceil" => Ceiling(args),
            "sqrt" => Sqrt(args),
            "pow" => Pow(args),
            "ln" => Log(args, "e"),
            "log10" => Log(args, "10"),
            "log2" => Log(args, "2"),
            "log" => Log(args, "CUSTOM"),
            "sin" => Trigonometric(args, "sin"),
            "cos" => Trigonometric(args, "cos"),
            "tan" => Trigonometric(args, "tan"),
            "cot" => Trigonometric(args, "cot"),
            "sec" => Trigonometric(args, "sec"),
            "csc" => Trigonometric(args, "csc"),
            
            "datetime.now.strftime" => DateTimeNowStrftime(args),
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
        return args.FirstOrDefault(a => a != null && !(a is string { Length: 0 }));
    }

    private static double MinMax(object?[] args, bool min)
    {
        if (args.Length < 2) throw new InvalidOperationException("min/max 至少需要 2 个参数");
        var nums = args.Select(ToNumber).ToArray();
        return min ? nums.Min() : nums.Max();
    }

    private static double Abs(object?[] args)
    {
        if (args.Length != 1) throw new InvalidOperationException("abs 需要 1 个参数");
        return Math.Abs(ToNumber(args[0]));
    }
    
    private static double Round(object?[] args)
    {
        if (args.Length != 1) throw new InvalidOperationException("round 需要 1 个参数");
        return Math.Round(ToNumber(args[0]));
    }
    
    private static double Ceiling(object?[] args)
    {
        if (args.Length != 1) throw new InvalidOperationException("ceil 需要 1 个参数");
        return Math.Ceiling(ToNumber(args[0]));
    }
    
    private static double Floor(object?[] args)
    {
        if (args.Length != 1) throw new InvalidOperationException("floor 需要 1 个参数");
        return Math.Floor(ToNumber(args[0]));
    }
    
    private static double Sqrt(object?[] args)
    {
        if (args.Length != 1) throw new InvalidOperationException("sqrt 需要 1 个参数");
        return Math.Sqrt(ToNumber(args[0]));
    }
    
    private static double Pow(object?[] args)
    {
        if (args.Length != 2) throw new InvalidOperationException("pow 需要 2 个参数");
        return Math.Pow(ToNumber(args[0]), ToNumber(args[1]));
    }

    private static double Log(object?[] args, string baseNum)
    {
        if (!double.TryParse(baseNum, out var baseNumValue) && (
                !baseNum.Equals("e", StringComparison.OrdinalIgnoreCase) || !baseNum.Equals("CUSTOM")))
        {
            throw new InvalidOperationException("底数无效");
        }
        
        if (baseNum.Equals("CUSTOM", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 2) throw new InvalidOperationException("自定义底数的对数需要 2 个参数");
            baseNumValue = ToNumber(args[1]);
        }

        return baseNumValue switch
        {
            < 0 => throw new InvalidOperationException("log 底数需要大于 0"),
            10 => args.Length != 1
                ? throw new InvalidOperationException("log10 需要 1 个参数")
                : Math.Log10(ToNumber(args[0])),
            2 => args.Length != 1 ? throw new InvalidOperationException("log2 需要 1 个参数") : Math.Log2(ToNumber(args[0])),
            _ => Math.Log(ToNumber(args[0]), baseNumValue)
        };
    }

    private static double Trigonometric(object?[] args, string funcName)
    {
        if (args.Length != 1) throw new InvalidOperationException($"{funcName} 至少需要 1 个参数");
        if (args.Length == 2)
        {
            var isRadian = args[1]?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase);
            if (isRadian == true)
            {
                var value = args[0]?.ToString();
                if (value?.Contains("\\pi", StringComparison.OrdinalIgnoreCase) == true)
                {
                    value = value.Replace("\\pi", Math.PI.ToString(), StringComparison.OrdinalIgnoreCase);
                }
                return funcName switch
                {
                    "sin" => Math.Sin(ToNumber(value)),
                    "cos" => Math.Cos(ToNumber(value)),
                    "tan" => Math.Tan(ToNumber(value)),
                    "cot" => 1 / Math.Tan(ToNumber(value)),
                    "sec" => 1 / Math.Cos(ToNumber(value)),
                    "csc" => 1 / Math.Sin(ToNumber(value)),
                    _ => throw new InvalidOperationException($"未知三角函数: {funcName}")
                };  
            }
        }
        return funcName switch
        {
            "sin" => Math.Sin(ToNumber(args[0])),
            "cos" => Math.Cos(ToNumber(args[0])),
            "tan" => Math.Tan(ToNumber(args[0])),
            "cot" => 1 / Math.Tan(ToNumber(args[0])),
            "sec" => 1 / Math.Cos(ToNumber(args[0])),
            "csc" => 1 / Math.Sin(ToNumber(args[0])),
            _ => throw new InvalidOperationException($"未知三角函数: {funcName}")
        };
    }
    
    private static string DateTimeNowStrftime(object?[] args)
    {
        return DateTime.Now.ToString(args.Length == 0
            ? "yyyy-MM-dd HH:mm:ss"
            : args[0]?.ToString() ?? "");
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

public class CompareCondition : ICondition
{
    public IValueNode Left
    {
        get;
        set;
    } = null!;

    public string Operator
    {
        get;
        set;
    } = "";

    public IValueNode Right
    {
        get;
        set;
    } = null!;

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
            "-match" => RegexMatch(leftVal?.ToString(), rightVal?.ToString()),
            "-notmatch" => !RegexMatch(leftVal?.ToString(), rightVal?.ToString()),
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

    /// <summary>
    ///     通配符匹配（26.4）：<c>*</c> 匹配任意长度字符序列，<c>?</c> 匹配单个字符，其余字符按字面量处理。
    ///     整串匹配、大小写不敏感。
    /// </summary>
    private static bool LikeMatch(string? input, string? pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return string.IsNullOrEmpty(input);
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return Regex.IsMatch(input ?? "", regexPattern, RegexOptions.IgnoreCase);
    }

    /// <summary>
    ///     正则表达式匹配（26.4）：<paramref name="pattern" /> 为 .NET 正则表达式，子串匹配、无需书写首尾锚点。
    ///     大小写不敏感；正则非法时返回 <c>false</c>，不抛出异常。
    /// </summary>
    private static bool RegexMatch(string? input, string? pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return string.IsNullOrEmpty(input);
        try
        {
            return Regex.IsMatch(input ?? "", pattern, RegexOptions.IgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
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