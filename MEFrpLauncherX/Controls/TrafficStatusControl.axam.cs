using System;
using System.Diagnostics;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using MEFrpLauncherX.Core.MEFIntergrated;
using ScottPlot;
using ScottPlot.Plottables;

// ReSharper disable All

namespace MEFrpLauncherX.Controls;

public partial class TrafficStatusControl : UserControl
{
    private bool init = false;
    private Crosshair CrosshairTool;
    private Tooltip Tooltip;

    public TrafficStatusControl()
    {
        InitializeComponent();
        InitializePlot();
        InitializeHoverTools();
        this.Loaded += (sender, args) => init = true;
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs? e)
    {
        // 阻止事件继续传播到父级控件
        base.OnPointerWheelChanged(e);
        e?.Handled = true;
    }

    private void InitializePlot()
    {
        // 设置图表基本属性
        avaPlot.Plot.Title("网络流量统计");
        avaPlot.Plot.Axes.DateTimeTicksBottom();
        avaPlot.Plot.ShowLegend(Alignment.UpperLeft);

        // 添加右侧坐标轴用于显示流量
        avaPlot.Plot.Axes.Right.MinimumSize = 50;
        avaPlot.Plot.Axes.Left.MinimumSize = 50;
        avaPlot.Plot.Axes.Bottom.MinimumSize = 40;

        // 设置坐标轴标签
        avaPlot.Plot.Axes.Left.Label.Text = "流量 (MB)";
        avaPlot.Plot.Axes.Bottom.Label.Text = "日期";
        avaPlot.Plot.Benchmark.IsVisible = false;
    }

    private void InitializeHoverTools()
    {
        // 创建十字准星工具
        CrosshairTool = avaPlot.Plot.Add.Crosshair(0, 0);

        // Configure crosshair appearance
        CrosshairTool.TextColor = Colors.White;
        CrosshairTool.TextBackgroundColor = CrosshairTool.HorizontalLine.Color;
        CrosshairTool.IsVisible = false; // start hidden

        avaPlot.Refresh();

        // Keep PointerExited in place (you already have it)
        // avaPlot.PointerExited += (s, e) =>
        // {
        //     CrosshairTool.IsVisible = false;
        //     avaPlot.Refresh();
        // };
    }

    private (DateTime Date, double InMB, double OutMB, double TotalMB, int Index)? FindNearestDataPoint(
        double xCoordinate)
    {
        try
        {
            // 将OADate转换回DateTime进行比较
            var targetDate = DateTime.FromOADate(xCoordinate);

            // 找到最近的日期索引
            var nearestIndex = -1;
            var minDifference = double.MaxValue;

            for (var i = 0; i < dateValues.Length; i++)
            {
                var currentDate = DateTime.FromOADate(dateValues[i]);
                var difference = Math.Abs((currentDate - targetDate).TotalDays);

                if (difference < minDifference)
                {
                    minDifference = difference;
                    nearestIndex = i;
                }
            }

            // 如果距离太远，不显示提示
            if (nearestIndex == -1 || minDifference > 1.0) // 超过1天不显示
                return null;

            return (DateTime.FromOADate(dateValues[nearestIndex]),
                trafficInMB[nearestIndex],
                trafficOutMB[nearestIndex],
                totalTrafficMB[nearestIndex],
                nearestIndex);
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Error(ex);
            return default;
        }
    }

    private void UpdateTooltip((DateTime Date, double InMB, double OutMB, double TotalMB, int Index) data,
        Coordinates coordinates)
    {
        // Use the crosshair's line texts to display the same information.
        // Keeping this method centralizes tooltip formatting so you can reuse it elsewhere.
        CrosshairTool.VerticalLine.Text = data.Date.ToString("yyyy-MM-dd");
        CrosshairTool.HorizontalLine.Text =
            $"入站: {data.InMB:F2} MB  出站: {data.OutMB:F2} MB  总计: {data.TotalMB:F2} MB";
        CrosshairTool.IsVisible = true;
    }

    // 添加类级别变量来存储数据
    private double[] dateValues;
    private double[] trafficInMB;
    private double[] trafficOutMB;
    private double[] totalTrafficMB;

    public void UpdateTrafficData(InfoClasses.TrafficStatus trafficData)
    {
        if (trafficData?.dates == null)
            return;

        // 清除之前的图表数据
        avaPlot.Plot.Clear();

        // 将日期字符串转换为DateTime数组
        var dates = trafficData.dates.Select(DateTime.Parse).ToArray();

        // 转换为OADate格式（ScottPlot需要的格式）
        dateValues = dates.Select(d => d.ToOADate()).ToArray();

        // 将流量数据从字节转换为MB（假设原始数据是字节）
        trafficInMB = trafficData.trafficIn.Select(b => b / (1024.0 * 1024.0)).ToArray();
        trafficOutMB = trafficData.trafficOut.Select(b => b / (1024.0 * 1024.0)).ToArray();
        totalTrafficMB = trafficData.totalTraffic.Select(b => b / (1024.0 * 1024.0)).ToArray();

        // 添加入站流量曲线（蓝色）
        var spIn = avaPlot.Plot.Add.Scatter(dateValues, trafficInMB);
        spIn.LegendText = "入站流量";
        spIn.Color = Colors.DodgerBlue;
        spIn.LineWidth = 2;
        spIn.MarkerSize = 5;

        // 添加出站流量曲线（红色）
        var spOut = avaPlot.Plot.Add.Scatter(dateValues, trafficOutMB);
        spOut.LegendText = "出站流量";
        spOut.Color = Colors.OrangeRed;
        spOut.LineWidth = 2;
        spOut.MarkerSize = 5;

        // 添加总流量曲线（绿色）
        var spTotal = avaPlot.Plot.Add.Scatter(dateValues, totalTrafficMB);
        spTotal.LegendText = "总流量";
        spTotal.Color = Colors.LimeGreen;
        spTotal.LineWidth = 2;
        spTotal.MarkerSize = 5;

        avaPlot.PointerMoved += (s, e) =>
        {
            /*
            // determine where the mouse is and get the nearest point
            var pos = e.GetPosition(avaPlot);
            Pixel mousePixel = new(pos.X, pos.Y);
            Coordinates mouseLocation = avaPlot.Plot.GetCoordinates(mousePixel);

            // find the nearest data point (method already returns null if too far)
            var nearest = FindNearestDataPoint(mouseLocation.X);

            if (nearest != null)
            {
                // show and place the crosshair on the nearest data point
                var dataX = nearest.Value.Date.ToOADate();
                var dataY = nearest.Value.TotalMB; // use total for crosshair Y; change if you prefer another series

                CrosshairTool.MarkerShape = MarkerShape.OpenCircleWithDot;
                CrosshairTool.IsVisible = true;
                CrosshairTool.Position = new Coordinates(dataX, dataY);

                // show text on the crosshair lines (date and summary)
                CrosshairTool.VerticalLine.Text = $"{nearest.Value.Date:yyyy-MM-dd}";
                CrosshairTool.HorizontalLine.Text =
                    $"In: {nearest.Value.InMB:F2} MB  Out: {nearest.Value.OutMB:F2} MB  Total: {nearest.Value.TotalMB:F2} MB";

                Debug.WriteLine(
                    $"Show at {nearest.Value.Date:yyyy-MM-dd} ( coord: {CrosshairTool.Position.X}, {CrosshairTool.Position.Y}" +
                    $" Real: {CrosshairTool.Position.AreReal})",
                    "TrafficScott");

                // also call UpdateTooltip if you want to keep that separate logic (it now updates crosshair text)
                UpdateTooltip(nearest.Value, new Coordinates(dataX, dataY));
            }
            else
            {
                // hide the crosshair when no nearby point was found
                if (CrosshairTool.IsVisible)
                {
                    CrosshairTool.IsVisible = false;
                }
            }

            avaPlot.Refresh();
            */
            var pos = e.GetPosition(avaPlot);
            Pixel mousePixel = new(pos.X, pos.Y);
            Coordinates mouseLocation = avaPlot.Plot.GetCoordinates(mousePixel);
            if (mouseLocation.X > 0 && mouseLocation.Y > 0)
            {
                //
                DataPoint nearest = spTotal.Data.GetNearest(mouseLocation, avaPlot.Plot.LastRender);

                Debug.WriteLine(
                    $"Show at ( coord: {CrosshairTool.Position.X}, {CrosshairTool.Position.Y}" +
                    $" Real: {CrosshairTool.Position.AreReal})",
                    "TrafficScott");
                if (nearest.IsReal)
                {
                    CrosshairTool.IsVisible = true;
                    CrosshairTool.Position = nearest.Coordinates;
                    avaPlot.Refresh();
                    CrosshairTool.HorizontalLine.Text =
                        $"Selected Index={nearest.Index}, X={nearest.X:0.##}, Y={nearest.Y:0.##}";
                }

                // hide the crosshair when no point is selected
                if (!nearest.IsReal && CrosshairTool.IsVisible)
                {
                    CrosshairTool.IsVisible = false;
                    avaPlot.Refresh();
                    //CrosshairTool.Text = $"No point selected";
                }
            }
        };

        // 设置底部坐标轴为日期格式
        avaPlot.Plot.Axes.DateTimeTicksBottom();

        // change figure colors
        avaPlot.Plot.FigureBackground.Color = Colors.Transparent;
        avaPlot.Plot.DataBackground.Color = Colors.Transparent;

        if (App.Current?.ActualThemeVariant == ThemeVariant.Dark)
        {
            // change axis and grid colors
            avaPlot.Plot.Axes.Color(Color.FromHex("#d7d7d7"));
            avaPlot.Plot.Grid.MajorLineColor = Color.FromHex("#404040");

            // change legend colors
            avaPlot.Plot.Legend.BackgroundColor = Color.FromHex("#404040");
            avaPlot.Plot.Legend.FontColor = Color.FromHex("#d7d7d7");
            avaPlot.Plot.Legend.OutlineColor = Color.FromHex("#d7d7d7");
        }

        // 根据数据范围自动调整坐标轴
        avaPlot.Plot.Axes.AntiAlias(true);
        avaPlot.Plot.Axes.AutoScale();
        avaPlot.Plot.Font.Automatic();

        // 添加图例
        avaPlot.Plot.ShowLegend(Alignment.UpperLeft);

        // 刷新图表
        avaPlot.Refresh();
    }

    private async void UpdateData(object? sender, SelectionChangedEventArgs e)
    {
        if (!init) return;
        if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem comboBoxItem)
        {
            var days = int.Parse(comboBoxItem?.Tag.ToString());
            var data = await MEFApiConverter.GetTrafficStatusAsync(days);
            UpdateTrafficData(data?.data);
        }
    }
}