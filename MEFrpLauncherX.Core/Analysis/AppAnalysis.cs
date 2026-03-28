using System.Diagnostics;

namespace MEFrpLauncherX.Core.Analysis;

public static class AppAnalytics
{
    // 标记：是否已经初始化
    private static bool _isInitialized;

    // 标记：用户是否同意统计
    private static bool _isAnalyticsEnabled;

    private static string _dsn;
    private static string _appVersion;

    /// <summary>
    /// 预配置（程序启动就调用，不会马上上报）
    /// </summary>
    public static void Setup(string dsn, string appVersion)
    {
        _dsn = dsn;
        _appVersion = appVersion;
    }

    /// <summary>
    /// 用户同意隐私 → 启用统计
    /// </summary>
    public static void EnableAnalytics()
    {
        if (_isAnalyticsEnabled) return;

        _isAnalyticsEnabled = true;

        if (!_isInitialized)
        {
            InitSentry();
            _isInitialized = true;
        }
    }

    /// <summary>
    /// 用户拒绝 → 禁用统计
    /// </summary>
    public static void DisableAnalytics()
    {
        _isAnalyticsEnabled = false;
        SentrySdk.Close();
    }

    /// <summary>
    /// 真正初始化 Sentry
    /// </summary>
    private static void InitSentry()
    {
        SentrySdk.Init(o =>
        {
            o.Dsn = _dsn;
            o.Release = _appVersion;
#if DEBUG
            o.Debug = true;
#endif
            o.AutoSessionTracking = true;
#if RELEASE
            o.TracesSampleRate = 0.3;
#endif
        });
    }

    // ===================== 下面是原有埋点，全部加了开关判断 =====================

    public static void SetUserId(string userId, string username, string email)
    {
        if (!_isAnalyticsEnabled) return;

        SentrySdk.ConfigureScope(scope =>
        {
            scope.User = new SentryUser
            {
                Id = userId,
                Username = username,
                Email = email,
            };
        });
    }

    public static void TrackPage(string pageName)
    {
        if (!_isAnalyticsEnabled) return;
        SentrySdk.AddBreadcrumb($"页面：{pageName}", "page.view");
    }

    public static void TrackAction(string actionName, Dictionary<string, string> data = null)
    {
        if (!_isAnalyticsEnabled) return;
        SentrySdk.AddBreadcrumb(actionName, "user.action", data: data);
    }

    public static void TrackCost(string opName, Action action)
    {
        if (!_isAnalyticsEnabled)
        {
            action();
            return;
        }

        using var trans = SentrySdk.StartTransaction(opName, "app.operation");
        try
        {
            action();
        }
        catch (Exception ex)
        {
            trans.Finish(ex);
            CaptureException(ex, opName);
            throw;
        }
    }

    public static async Task<TimeSpan> TrackCostWithTimerAsync(string opName, Func<Task> task)
    {
        var sw = new Stopwatch();
        if (!_isAnalyticsEnabled)
        {
            sw.Start();
            await task();
            sw.Stop();
        }
        else
        {
            using var trans = SentrySdk.StartTransaction(opName, "app.operation");
            try
            {
                sw.Start();
                await task();
                sw.Stop();
            }
            catch (Exception ex)
            {
                trans.Finish(ex);
                CaptureException(ex, opName);
                throw;
            }
        }

        return sw.Elapsed;
    }

    public static async Task TrackCostAsync(string opName, Func<Task> task)
    {
        if (!_isAnalyticsEnabled)
        {
            await task();
            return;
        }

        using var trans = SentrySdk.StartTransaction(opName, "app.operation");
        try
        {
            await task();
        }
        catch (Exception ex)
        {
            trans.Finish(ex);
            CaptureException(ex, opName);
            throw;
        }
    }

    public static void CaptureException(Exception ex, string tag = null)
    {
        if (!_isAnalyticsEnabled || ex == null) return;

        var id = SentrySdk.CaptureException(ex, s =>
        {
            if (tag != null) s.SetTag("op", tag);
        });
        
    }
}