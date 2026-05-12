namespace RYCB.PML2.Mixin.TerminalHelper;

public interface ISolution
{
    /// <summary>
    ///     错误标记
    /// </summary>
    string Flag
    {
        get;
        set;
    }

    /// <summary>
    ///     错误信息
    /// </summary>
    string Info
    {
        get;
        set;
    }

    /// <summary>
    ///     解决方案
    /// </summary>
    List<string> Solution
    {
        get;
        set;
    }
}

public class ErrorSolution : ISolution
{
    public string Flag
    {
        get;
        set;
    }

    public string Info
    {
        get;
        set;
    }

    public List<string> Solution
    {
        get;
        set;
    }
}