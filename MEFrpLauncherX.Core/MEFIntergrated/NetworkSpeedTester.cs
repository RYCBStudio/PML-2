using System.Diagnostics;

namespace MEFrpLauncherX.Core.MEFIntergrated;

public class NetworkSpeedTester
{
    private readonly HttpClient _httpClient;

    public NetworkSpeedTester()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(5); // 设置较短的超时时间
    }

    /// <summary>
    ///     快速测试多个网址并返回最优网址的索引
    /// </summary>
    /// <param name="urls">要测试的网址数组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>最优网址的索引（从0开始），如果全部失败返回-1</returns>
    public async Task<int> FindFastestUrlIndexAsync(IEnumerable<string> urls,
        CancellationToken cancellationToken = default) =>
        await FindFastestUrlIndexAsync(cancellationToken, urls.ToArray());

    /// <summary>
    ///     快速测试多个网址并返回最优网址的索引
    /// </summary>
    /// <param name="urls">要测试的网址数组</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>最优网址的索引（从0开始），如果全部失败返回-1</returns>
    public async Task<int> FindFastestUrlIndexAsync(CancellationToken cancellationToken = default,
        params string[] urls)
    {
        if (urls == null || urls.Length == 0)
        {
            return -1;
        }

        if (urls.Length == 1)
        {
            return 0;
        }

        var tasks = new List<Task<UrlTestResult>>();

        // 并行测试所有网址
        for (var i = 0; i < urls.Length; i++)
        {
            var index = i;
            tasks.Add(TestUrlWithIndexAsync(urls[index], index, cancellationToken));
        }

        try
        {
            // 等待所有测试完成或超时
            var results = await Task.WhenAll(tasks);

            // 过滤掉失败的测试
            var successfulResults = new List<UrlTestResult>();
            foreach (var result in results)
            {
                if (result is { IsSuccessful: true })
                {
                    successfulResults.Add(result);
                }
            }

            if (successfulResults.Count == 0)
            {
                return -1;
            }

            // 按响应时间排序，选择最快的
            successfulResults.Sort((a, b) => a.ResponseTimeMs.CompareTo(b.ResponseTimeMs));

            return successfulResults[0].Index;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    ///     测试单个网址并返回结果
    /// </summary>
    private async Task<UrlTestResult> TestUrlWithIndexAsync(string url, int index,
        CancellationToken cancellationToken)
    {
        try
        {
            // 确保URL格式正确
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "https://" + url;
            }

            var stopwatch = Stopwatch.StartNew();

            // 使用HEAD方法快速测试
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            stopwatch.Stop();

            response.EnsureSuccessStatusCode();

            return new UrlTestResult
            {
                Index = index,
                Url = url,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                IsSuccessful = true
            };
        }
        catch
        {
            return new UrlTestResult
            {
                Index = index,
                Url = url,
                ResponseTimeMs = long.MaxValue,
                IsSuccessful = false
            };
        }
    }

    public void Dispose() => _httpClient?.Dispose();
}

internal class UrlTestResult
{
    public int Index
    {
        get;
        set;
    }

    public string Url
    {
        get;
        set;
    }

    public long ResponseTimeMs
    {
        get;
        set;
    }

    public bool IsSuccessful
    {
        get;
        set;
    }
}