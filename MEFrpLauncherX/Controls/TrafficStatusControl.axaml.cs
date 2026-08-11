using System;
using System.Collections.Generic;
using Avalonia.Collections;
using Avalonia.Controls;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.MEFIntegrated;
using ReactiveUI;

namespace MEFrpLauncherX.Controls;

public partial class TrafficStatusControl : UserControl
{
    private readonly TrafficStatusControlViewModel _vm;

    public TrafficStatusControl()
    {
        InitializeComponent();
        _vm = new TrafficStatusControlViewModel();
        DataContext = _vm;
    }

    /*
    public void UpdateTrafficData(InfoClasses.TrafficStatus data)
    {
        _vm.XAxes = new Axis() { Labels = data.dates };
        _vm.Series.Clear();
#if DEBUG
        data.trafficIn = [214, 25, 236, 2346, 2346, 234, 44];
        data.trafficOut = [2124, 215, 26, 346, 234, 24, 44];
        data.totalTraffic = [1, 0, 0, 0, 0, 0];
#endif
        _vm.Series.AddRange(
        [
            new LineSeries<int>()
            {
                Values = new AvaloniaList<int>(data.trafficIn),
                YToolTipLabelFormatter = x => $"入站流量: {ProcessFileSize(x.Model)}"
            },
            new LineSeries<int>()
            {
                Values = new AvaloniaList<int>(data.trafficOut),
                YToolTipLabelFormatter = x => $"出站流量: {ProcessFileSize(x.Model)}"
            },
            new LineSeries<int>()
            {
                Values = new AvaloniaList<int>(data.totalTraffic),
                YToolTipLabelFormatter = x => $"总流量: {ProcessFileSize(x.Model)}"
            }
        ]);
    }
    */

    public void UpdateTrafficData(InfoClasses.TrafficStatus data) => _vm.UpdateTrafficData(data);

    // ... existing code ...


    /// <summary>
    ///     根据<paramref name="fileSize" />的大小自动返回对应的文件大小值。
    ///     <br />
    ///     如：若<paramref name="fileSize" />32743879328,则返回30.50GB；
    ///     返回值的数值范围为1~1000。
    /// </summary>
    /// <param name="fileSize">文件大小，单位为Bytes</param>
    /// <returns>处理后的文件大小值。</returns>
    private string ProcessFileSize(double fileSize)
    {
        string[] sizeUnits = ["B", "KB", "MB", "GB", "TB"];
        var size = fileSize;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < sizeUnits.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{Math.Round(size, 2)}{sizeUnits[unitIndex]}";
    }
}

public class TrafficStatusControlViewModel : ViewModelBase
{
    private InfoClasses.TrafficStatus _data;

    public TrafficStatusControlViewModel()
    {
        if (Design.IsDesignMode)
        {
            Series =
            [
                new LineSeries<int>
                {
                    Values = [1, 1, 4, 5, 1, 4],
                    Name = "Test111"
                }
            ];
            XAxes =
            [
                new Axis
                {
                    Labels = ["2026-3-1", "2026-3-2", "2026-3-3", "2026-3-4", "2026-3-5", "2026-3-6"]
                }
            ];
        }
        else
        {
            Series = [];
        }

        SelectedPeriod = 7;
        YAxes = [new Axis { Labeler = ProcessFileSize }];
        ZoomMode = ZoomAndPanMode.X;
    }

    public AvaloniaList<ISeries> Series
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public IList<Axis> XAxes
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public IList<Axis> YAxes
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public int SelectedType
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            UpdateTrafficData(_data, value);
        }
    }

    public List<int> Periods
    {
        get;
    } = [7, 14, 30];

    public int SelectedPeriod
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            UpdateTrafficData(_data, SelectedType, true, value);
        }
    }

    public ZoomAndPanMode ZoomMode
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ZoomAndPanMode[] ZoomModes
    {
        get;
    } = [ZoomAndPanMode.None, ZoomAndPanMode.X, ZoomAndPanMode.Y, ZoomAndPanMode.Both];

    public bool IsLoading
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public async void UpdateTrafficData(InfoClasses.TrafficStatus? data, int type = 0, bool shouldLoadNew = false,
        int period = 7)
    {
        IsLoading = true;
        if (Design.IsDesignMode)
        {
            Core.App.CurrentLogger?.Log("流量数据为 null", module: EnumLogModule.Net, type: EnumLogType.Warn);
            return;
        }

        data ??= (await MEFrpApiConverter.GetTrafficStatusAsync(period)).data;
//
#if DEBUG
        data ??= new InfoClasses.TrafficStatus();
        data.trafficIn = [
            15242871936,        // 14.2 GB
            452890,             // 452 KB
            1024,               // 1 KB
            26843545600,        // 25 GB
            8912,               // 8.7 KB
            1099511627776,      // 1 TB（超大）
            64                  // 64 Bytes（极小）
        ];

        data.trafficOut = [
            8765432112,         // 8.16 GB
            2310456,            // 2.2 MB
            512,                // 512 Bytes
            52428800,           // 50 MB
            102400,             // 100 KB
            549755813888,       // 512 GB
            128                 // 128 Bytes
        ];

        data.totalTraffic = [
            24008304048,        // 22.36 GB (14.2+8.16)
            2763346,            // 2.64 MB
            1536,               // 1.5 KB
            26895974400,        // 25.05 GB
            111312,             // 108.7 KB
            1649267441664,      // 1.5 TB (1TB+512GB)
            192                 // 192 Bytes
        ];
#endif
        if (shouldLoadNew)
        {
            var r = await MEFrpApiConverter.GetTrafficStatusAsync(period);
            if (r.code == 200)
            {
                data = r.data;
            }
            else
            {
                Core.App.CurrentLogger?.Log("获取流量失败", module: EnumLogModule.Net, type: EnumLogType.Warn);
                IsLoading = false;
                return;
            }
        }

        _data = data;

        if (data == null)
        {
            Core.App.CurrentLogger?.Log("流量数据为 null", module: EnumLogModule.Net, type: EnumLogType.Warn);
            XAxes = [new Axis { Labels = [] }];
            Series = [];
            IsLoading = false;
            return;
        }

        var dates = data.dates ?? [];
        var trafficIn = data.trafficIn ?? [];
        var trafficOut = data.trafficOut ?? [];
        var totalTraffic = data.totalTraffic ?? [];

        if (dates.Length == 0)
        {
            Core.App.CurrentLogger?.Log("日期数据为空，无法显示图表", module: EnumLogModule.Net, type: EnumLogType.Warn);
            XAxes = [new Axis { Labels = [] }];
            Series = [];
            IsLoading = false;
            return;
        }

        if (trafficIn.Length == 0 && trafficOut.Length == 0 && totalTraffic.Length == 0)
        {
            Core.App.CurrentLogger?.Log("所有流量数据均为空，无法显示图表", module: EnumLogModule.Net, type: EnumLogType.Warn);
            XAxes = [new Axis { Labels = dates }];
            Series = [];
            IsLoading = false;
            return;
        }

        XAxes = [new Axis { Labels = dates }];
        var newSeries = new AvaloniaList<ISeries>();

        if (trafficIn.Length > 0)
        {
            switch (type)
            {
                case 0:
                    newSeries.Add(new LineSeries<long>
                    {
                        Values = new AvaloniaList<long>(trafficIn),
                        YToolTipLabelFormatter = x => ProcessFileSize(x.Model),
                        Name = Languages.Text_Traffic_Inbound
                    }); break;
                case 1:
                    newSeries.Add(new ColumnSeries<long>
                    {
                        Values = new AvaloniaList<long>(trafficIn),
                        YToolTipLabelFormatter = x => ProcessFileSize(x.Model),
                        Name = Languages.Text_Traffic_Inbound
                    }); break;
            }
        }

        if (trafficOut.Length > 0)
        {
            switch (type)
            {
                case 0:
                    newSeries.Add(new LineSeries<long>
                    {
                        Values = new AvaloniaList<long>(trafficOut),
                        YToolTipLabelFormatter = x => ProcessFileSize(x.Model),
                        Name = Languages.Text_Traffic_Outbound
                    }); break;
                case 1:
                    newSeries.Add(new ColumnSeries<long>
                    {
                        Values = new AvaloniaList<long>(trafficOut),
                        YToolTipLabelFormatter = x => ProcessFileSize(x.Model),
                        Name = Languages.Text_Traffic_Outbound
                    }); break;
            }
        }

        if (totalTraffic.Length > 0)
        {
            switch (type)
            {
                case 0:
                    newSeries.Add(new LineSeries<long>
                    {
                        Values = new AvaloniaList<long>(totalTraffic),
                        YToolTipLabelFormatter = x => ProcessFileSize(x.Model),
                        Name = Languages.Text_Traffic_Total
                    }); break;
                case 1:
                    newSeries.Add(new ColumnSeries<long>
                    {
                        Values = new AvaloniaList<long>(totalTraffic),
                        YToolTipLabelFormatter = x => ProcessFileSize(x.Model),
                        Name = Languages.Text_Traffic_Total
                    }); break;
            }
        }

        Series = newSeries;
        IsLoading = false;
        Core.App.CurrentLogger?.Log($"图表已更新，日期数：{dates.Length}, 系列数：{newSeries.Count}", module: EnumLogModule.Net);
    }

    /// <summary>
    ///     根据<paramref name="fileSize" />的大小自动返回对应的文件大小值。
    ///     <br />
    ///     如：若<paramref name="fileSize" />32743879328,则返回30.50GB；
    ///     返回值的数值范围为1~1000。
    /// </summary>
    /// <param name="fileSize">文件大小，单位为Bytes</param>
    /// <returns>处理后的文件大小值。</returns>
    private string ProcessFileSize(double fileSize)
    {
        string[] sizeUnits = ["B", "KB", "MB", "GB", "TB"];
        var size = fileSize;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < sizeUnits.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{Math.Round(size, 2)}{sizeUnits[unitIndex]}";
    }

    public void ClearZoom() => ZoomMode = ZoomAndPanMode.None;
}