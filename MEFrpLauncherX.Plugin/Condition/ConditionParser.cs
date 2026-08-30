using System.Linq;

namespace MEFrpLauncherX.Plugin.Condition;

public static class ConditionParser
{
    public static ICondition Parse(string expr)
    {
        var tokens = Tokenize(expr);
        var (ast, _) = ParseExpression(tokens, 0);
        return ast;
    }

    /// <summary>解析值表达式（路径 / 字面量 / 算术 / 内置函数调用），供动作参数模板替换使用（26.3.1 M5）</summary>
    public static IValueNode ParseValue(string expr)
    {
        var tokens = Tokenize(expr);
        var (node, pos) = ParseValueExpression(tokens, 0);
        if (pos != tokens.Count) throw new Exception($"值表达式后有多余内容: {tokens[pos]}");
        return node;
    }

    /// <summary>是否比较运算符（比较表达式中的操作符 token）</summary>
    private static bool IsCompareOperator(string token) => token is
        "-eq" or "==" or "-ne" or "!=" or "-gt" or ">" or "-lt" or "<" or "-ge" or ">=" or "-le" or "<=" or
        "-like" or "-notlike" or "-contains" or "-notcontains" or "-in" or "-notin";

    private static List<string> Tokenize(string expr)
    {
        // 按空格切分（保留引号内空格），并把 ( ) + * / 及独立 - 切分为独立 token。
        // 连字符运算符（-and / -eq 等）保持为一个 token：'-' 后紧跟字母视为运算符前缀。
        var result = new List<string>();
        var current = "";
        var inQuotes = false;
        for (var i = 0; i < expr.Length; i++)
        {
            var ch = expr[i];
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
            else if (ch is '(' or ')' or '+' or '*' or '/' or ',')
            {
                if (!string.IsNullOrEmpty(current))
                {
                    result.Add(current);
                    current = "";
                }

                result.Add(ch.ToString());
            }
            else if (ch == '-' && !inQuotes)
            {
                // 减号（后跟数字 / 空格 / 括号 / 引号）与连字符运算符（-and 等）区分
                var next = i + 1 < expr.Length ? expr[i + 1] : '\0';
                if (char.IsLetter(next))
                {
                    // 连字符运算符：继续累积（-and / -eq / -not ...）
                    current += ch;
                }
                else
                {
                    // 减号：切为独立 token
                    if (!string.IsNullOrEmpty(current))
                    {
                        result.Add(current);
                        current = "";
                    }

                    result.Add("-");
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
        while (newPos < tokens.Count && tokens[newPos] is "-or" or "||")
        {
            var (right, nextPos) = ParseAnd(tokens, newPos + 1);
            left = new LogicCondition { Left = left, Operator = tokens[newPos], Right = right };
            newPos = nextPos;
        }

        return (left, newPos);
    }

    private static (ICondition, int) ParseAnd(List<string> tokens, int pos)
    {
        var (left, newPos) = ParsePrimary(tokens, pos);
        while (newPos < tokens.Count && tokens[newPos] is "-and" or "&&")
        {
            var (right, nextPos) = ParsePrimary(tokens, newPos + 1);
            left = new LogicCondition { Left = left, Operator = tokens[newPos], Right = right };
            newPos = nextPos;
        }

        return (left, newPos);
    }

    private static (ICondition, int) ParsePrimary(List<string> tokens, int pos)
    {
        if (tokens[pos] == "(")
        {
            // 优先尝试按算术值表达式解析（支持 (1 + 2) * 3 -eq 9 这类条件）；
            // 若括号内不是算术（而是嵌套条件），回退到条件表达式分支。
            try
            {
                var (val, valPos) = ParseValueExpression(tokens, pos);
                if (valPos < tokens.Count && IsCompareOperator(tokens[valPos]))
                {
                    var arithOp = tokens[valPos];
                    var (arithRight, arithNext) = ParseValueExpression(tokens, valPos + 1);
                    return (new CompareCondition { Left = val, Operator = arithOp, Right = arithRight }, arithNext);
                }
            }
            catch
            {
                // 算术解析失败：按条件括号处理
            }

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

        // 比较表达式： 左值 op 右值（两侧均支持算术表达式）
        var (left, p1) = ParseValueExpression(tokens, pos);
        if (p1 >= tokens.Count || !IsCompareOperator(tokens[p1]))
        {
            var found = p1 < tokens.Count ? tokens[p1] : "(结尾)";
            throw new Exception($"缺少比较运算符, 当前位置: {found}");
        }

        var opToken = tokens[p1];
        var (right, p2) = ParseValueExpression(tokens, p1 + 1);
        return (new CompareCondition { Left = left, Operator = opToken, Right = right }, p2);
    }

    /// <summary>解析值表达式：加减（低优先级）→ 乘除（高优先级）→ 原子</summary>
    private static (IValueNode, int) ParseValueExpression(List<string> tokens, int pos)
    {
        var (left, newPos) = ParseMulDiv(tokens, pos);
        while (newPos < tokens.Count && tokens[newPos] is "+" or "-")
        {
            var op = tokens[newPos];
            var (right, nextPos) = ParseMulDiv(tokens, newPos + 1);
            left = new ArithmeticNode { Left = left, Operator = op, Right = right };
            newPos = nextPos;
        }

        return (left, newPos);
    }

    private static (IValueNode, int) ParseMulDiv(List<string> tokens, int pos)
    {
        var (left, newPos) = ParseAtom(tokens, pos);
        while (newPos < tokens.Count && tokens[newPos] is "*" or "/")
        {
            var op = tokens[newPos];
            var (right, nextPos) = ParseAtom(tokens, newPos + 1);
            left = new ArithmeticNode { Left = left, Operator = op, Right = right };
            newPos = nextPos;
        }

        return (left, newPos);
    }

    private static (IValueNode, int) ParseAtom(List<string> tokens, int pos)
    {
        if (tokens[pos] == "(")
        {
            var (inner, newPos) = ParseValueExpression(tokens, pos + 1);
            if (newPos < tokens.Count && tokens[newPos] == ")")
                return (inner, newPos + 1);
            throw new Exception("Mismatched parentheses");
        }

        if (tokens[pos] == "-")
        {
            // 一元负号：-3 / -ctx.data.count
            var (inner, newPos) = ParseAtom(tokens, pos + 1);
            return (new ArithmeticNode { Left = new LiteralValueNode { Value = 0.0 }, Operator = "-", Right = inner },
                newPos);
        }

        var token = tokens[pos];
        // 带引号的是字符串字面量（如 '' / 'abc'），去除引号后作为常量；
        // 未带引号且能解析为数字的也是常量；其余视为上下文路径。
        var wasQuoted = token.Length >= 2 && token.StartsWith("'") && token.EndsWith("'");
        if (wasQuoted)
        {
            token = token.Substring(1, token.Length - 2);
            return (new LiteralValueNode { Value = token }, pos + 1);
        }

        if (int.TryParse(token, out var intVal)) return (new LiteralValueNode { Value = intVal }, pos + 1);
        if (double.TryParse(token, out var dblVal)) return (new LiteralValueNode { Value = dblVal }, pos + 1);
        // 函数调用：标识符后紧跟 ( 时解析为内置函数调用（26.3.1 M5）
        if (IsIdentifier(token) && pos + 1 < tokens.Count && tokens[pos + 1] == "(")
        {
            return ParseFunctionCall(tokens, pos);
        }

        // 否则视为上下文路径（ctx.data.xxx / ctx.variables.xxx）
        return (new PathValueNode { Path = token }, pos + 1);
    }

    /// <summary>函数名：以字母/下划线开头，其余为字母数字/下划线（不含点号，避免与 ctx.data 路径冲突）</summary>
    private static bool IsIdentifier(string token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        var first = token[0];
        if (!char.IsLetter(first) && first != '_') return false;
        return token.Skip(1).All(c => char.IsLetterOrDigit(c) || c == '_');
    }

    /// <summary>解析函数调用：name ( arg, arg, ... )</summary>
    private static (IValueNode, int) ParseFunctionCall(List<string> tokens, int pos)
    {
        var name = tokens[pos];
        var argPos = pos + 2; // 跳过 name 与 '('
        var args = new List<IValueNode>();
        if (argPos >= tokens.Count)
        {
            throw new Exception($"函数 {name} 括号不匹配");
        }

        if (tokens[argPos] != ")")
        {
            while (true)
            {
                var (arg, nextPos) = ParseValueExpression(tokens, argPos);
                args.Add(arg);
                argPos = nextPos;
                if (argPos >= tokens.Count)
                {
                    throw new Exception($"函数 {name} 括号不匹配");
                }

                if (tokens[argPos] == ")")
                {
                    argPos++;
                    break;
                }

                if (tokens[argPos] == ",")
                {
                    argPos++;
                    continue;
                }

                throw new Exception($"函数 {name} 参数分隔符错误: {tokens[argPos]}");
            }
        }
        else
        {
            argPos++; // 空参数列表 ()
        }

        return (new FunctionCallNode { Name = name, Arguments = args }, argPos);
    }
}

public class NotCondition : ICondition
{
    private readonly ICondition _inner;
    public NotCondition(ICondition inner) => _inner = inner;
    public bool Evaluate(Core.ExecutionContext ctx) => !_inner.Evaluate(ctx);
}
