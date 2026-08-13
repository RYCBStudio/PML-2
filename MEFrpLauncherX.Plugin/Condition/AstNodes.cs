using System.Collections;
using System.Text.RegularExpressions;
using ExecutionContext = MEFrpLauncherX.Plugin.Core.ExecutionContext;

namespace MEFrpLauncherX.Plugin.Condition;

public interface ICondition
{
    bool Evaluate(ExecutionContext ctx);
}

public partial class CompareCondition : ICondition
{
    public string LeftPath
    {
        get;
        set;
    } = "";

    public string Operator
    {
        get;
        set;
    } = "";

    public object RightValue
    {
        get;
        set;
    } = new();

    public bool Evaluate(ExecutionContext ctx)
    {
        var leftVal = PropertyAccessor.GetValue(ctx, LeftPath);
        return Operator switch
        {
            "-eq" or "==" => Equals(leftVal, RightValue),
            "-ne" or "!=" => !Equals(leftVal, RightValue),
            "-gt" or ">" => Compare(leftVal, RightValue) > 0,
            "-lt" or "<" => Compare(leftVal, RightValue) < 0,
            "-ge" or ">=" => Compare(leftVal, RightValue) >= 0,
            "-le" or "<=" => Compare(leftVal, RightValue) <= 0,
            "-like" => LikeMatch(leftVal?.ToString(), RightValue?.ToString()),
            "-notlike" => !LikeMatch(leftVal?.ToString(), RightValue?.ToString()),
            "-contains" => ContainsCompare(leftVal, RightValue),
            "-notcontains" => !ContainsCompare(leftVal, RightValue),
            "-in" => InCompare(leftVal, RightValue),
            "-notin" => !InCompare(leftVal, RightValue),
            _ => false
        };
    }

    private int Compare(object? a, object? b) => Comparer<object>.Default.Compare(a, b);

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