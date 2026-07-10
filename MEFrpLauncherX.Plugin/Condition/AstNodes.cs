using System.Text.RegularExpressions;
using ExecutionContext =  MEFrpLauncherX.Plugin.Core.ExecutionContext;

namespace MEFrpLauncherX.Plugin.Condition;

public interface ICondition
{
    bool Evaluate(ExecutionContext ctx);
}

public partial class CompareCondition : ICondition
{
    public string LeftPath { get; set; } = "";
    public string Operator { get; set; } = "";
    public object RightValue { get; set; } = new();

    public bool Evaluate(ExecutionContext ctx)
    {
        var leftVal = PropertyAccessor.GetValue(ctx, LeftPath);
        return Operator switch
        {
            "-eq" => Equals(leftVal, RightValue),
            "-ne" => !Equals(leftVal, RightValue),
            "-gt" => Compare(leftVal, RightValue) > 0,
            "-lt" => Compare(leftVal, RightValue) < 0,
            "-ge" => Compare(leftVal, RightValue) >= 0,
            "-le" => Compare(leftVal, RightValue) <= 0,
            "-like" => LikeMatch(leftVal?.ToString(), RightValue?.ToString()),
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

    [GeneratedRegex("...", RegexOptions.IgnoreCase)]
    private static partial Regex LikeRegex();
}

public class LogicCondition : ICondition
{
    public ICondition Left { get; set; }
    public string Operator { get; set; } = "";
    public ICondition Right { get; set; }

    public bool Evaluate(ExecutionContext ctx) =>
        Operator == "-and" ? Left.Evaluate(ctx) && Right.Evaluate(ctx)
            : Left.Evaluate(ctx) || Right.Evaluate(ctx);
}