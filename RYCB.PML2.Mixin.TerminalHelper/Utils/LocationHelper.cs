using System.Device.Location;
using System.Net;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using Newtonsoft.Json;
using RestSharp;

namespace RYCB.PML2.Mixin.TerminalHelper.Utils;

public class LocationHelper
{
    public async Task<LocationCoordinate> GetLocationAsync()
    {
        using var watcher = new GeoCoordinateWatcher();
        try
        {
            var cancel = new CancellationTokenSource();
            cancel.CancelAfter(TimeSpan.FromSeconds(10));
            await Task.Run(() =>
            {
                watcher.TryStart(false, TimeSpan.FromSeconds(10));
                while (watcher.Status != GeoPositionStatus.Ready && !cancel.IsCancellationRequested)
                {
                }
            }, cancel.Token);
            var coord = watcher.Position.Location;
            var locationCoordinate = new LocationCoordinate
            {
                Longitude = coord.Longitude,
                Latitude = coord.Latitude
            };
            if (double.IsNaN(locationCoordinate.Latitude) || double.IsNaN(locationCoordinate.Longitude))
            {
                throw new InvalidOperationException("获取的位置信息无效，可能是定位服务未开启或系统不支持");
            }

            return locationCoordinate;
        }
        finally
        {
            watcher.Stop();
        }
    }

    private static RestClient CreateClient(string endpoint)
    {
        return new RestClient(new RestClientOptions(endpoint)
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            UserAgent = OperatingSystem.IsAndroid() ? "RYCB-PML2/Android 0.0.1" : "RYCB-PML2/Desktop 2.1.0",
            Timeout = TimeSpan.FromSeconds(10),
        });
    }

    private static RestRequest CreateRequest(Method method = Method.Get, bool withAuthorization = true)
    {
        var request = new RestRequest { Method = method };
        if (method != Method.Get)
        {
            request.AddHeader("Content-Type", "application/json");
        }

        return request;
    }

    private static void HandleResponse(RestResponse response)
    {
        if (response == null)
        {
            return;
        }

        if ((int)response.StatusCode != 200)
        {
            Growl.Error(response.ErrorMessage);
        }
    }

    private static async Task<T> ExecuteRequestAsync<T>(RestRequest request, string endpoint,
        string operationName)
    {
        App.CurrentLogger.LogDebug($"GET {endpoint}", port: EnumLogPort.Server,
            module: EnumLogModule.Custom, customModuleName: "API");
        App.CurrentLogger.Log($"正在获取{operationName}", port: EnumLogPort.Client, module: EnumLogModule.Net);
        MainWindowViewModel.Instance?.AppMessage = $"正在获取{operationName}";

        using var client = CreateClient(endpoint);

        var response = await client.ExecuteAsync(request).ConfigureAwait(false);
        App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);

        if (string.IsNullOrEmpty(response.Content))
        {
            return default;
        }

        var result = JsonConvert.DeserializeObject<T>(response.Content) ?? default;

        HandleResponse(response);
        return result;
    }

    public async Task<LocationNameInfo> GetLocationNameAsync(LocationCoordinate locationCoordinate)
    {
        return await ExecuteRequestAsync<LocationNameInfo>(CreateRequest(withAuthorization: false),
            "https://weatherapi.market.xiaomi.com/wtr-v3/location/city/geo?" +
            $"longitude={locationCoordinate.Longitude}" +
            $"&latitude={locationCoordinate.Latitude}&locale=zh_cn", "位置信息");
    }
}