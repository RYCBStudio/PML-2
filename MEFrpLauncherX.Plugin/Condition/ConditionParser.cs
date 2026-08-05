namespace MEFrpLauncherX.Plugin.Condition;

public static class ConditionParser
{
    public static ICondition Parse(string expr)
    {
        var tokens = Tokenize(expr);
        var (ast, _) = ParseExpression(tokens, 0);
        return ast;
    }

    private static List<string> Tokenize(string expr)
    {
        // 简单分词：按空格分割，但保留引号内的空格
        var result = new List<string>();
        var current = "";
        var inQuotes = false;
        foreach (var ch in expr)
        {
            if (ch == '\'')
            {
                inQuotes = !inQuotes;
                current += ch;
            }
            else if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                if (!string.IsNullOrEmpty(current))
                {
                    result.Add(current);
                    current = "";
                }
            }
            else
            {
                current += ch;
            }
        }
        if (!string.IsNullOrEmpty(current)) result.Add(current);
        return result;
    }

    private static (ICondition, int) ParseExpression(List<string> tokens, int pos)
    {
        var (left, newPos) = ParseOr(tokens, pos);
        return (left, newPos);
    }

    private static (ICondition, int) ParseOr(List<string> tokens, int pos)
    {
        var (left, newPos) = ParseAnd(tokens, pos);
        while (newPos < tokens.Count && tokens[newPos] == "-or")
        {
            var (right, nextPos) = ParseAnd(tokens, newPos + 1);
            left = new LogicCondition { Left = left, Operator = "-or", Right = right };
            newPos = nextPos;
        }
        return (left, newPos);
    }

    private static (ICondition, int) ParseAnd(List<string> tokens, int pos)
    {
        var (left, newPos) = ParsePrimary(tokens, pos);
        while (newPos < tokens.Count && tokens[newPos] == "-and")
        {
            var (right, nextPos) = ParsePrimary(tokens, newPos + 1);
            left = new LogicCondition { Left = left, Operator = "-and", Right = right };
            newPos = nextPos;
        }
        return (left, newPos);
    }

    private static (ICondition, int) ParsePrimary(List<string> tokens, int pos)
    {
        if (tokens[pos] == "(")
        {
            var (inner, newPos) = ParseExpression(tokens, pos + 1);
            if (newPos < tokens.Count && tokens[newPos] == ")")
                return (inner, newPos + 1);
            throw new Exception("Mismatched parentheses");
        }
        if (tokens[pos] == "-not")
        {
            var (inner, newPos) = ParsePrimary(tokens, pos + 1);
            return (new NotCondition(inner), newPos);
        }
        // 比较表达式： leftPath op rightValue
        var leftPath = tokens[pos];
        var op = tokens[pos + 1];
        var rightVal = tokens[pos + 2];
        // 去除引号
        if (rightVal.StartsWith("'") && rightVal.EndsWith("'"))
            rightVal = rightVal.Substring(1, rightVal.Length - 2);
        object rightObj = int.TryParse(rightVal, out var intVal) ? intVal :
                          double.TryParse(rightVal, out var dblVal) ? dblVal : rightVal;
        return (new CompareCondition { LeftPath = leftPath, Operator = op, RightValue = rightObj }, pos + 3);
    }
}

public class NotCondition : ICondition
{
    private readonly ICondition _inner;
    public NotCondition(ICondition inner) => _inner = inner;
    public bool Evaluate(Core.ExecutionContext ctx) => !_inner.Evaluate(ctx);
}