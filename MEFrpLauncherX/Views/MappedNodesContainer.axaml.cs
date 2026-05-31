using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts.Providers;
using Mapsui.Nts.Providers.Shapefile;
using Mapsui.Projections;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Styles.Thematics;
using Mapsui.Tiling.Layers;
using MEFrpLauncherX.Core;

#pragma warning disable CS0472 // 由于此类型的值永不等于 "null"，该表达式的结果始终相同

namespace MEFrpLauncherX.Views;

public partial class MappedNodesContainer : UserControl
{
    private bool _initialized;

    public MappedNodesContainer()
    {
        InitializeComponent();
        // 延迟到控件附加到可视化树后才加载地图（shapefile I/O 很慢）
        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    private async void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        AttachedToVisualTree -= OnAttachedToVisualTree;
        if (_initialized) return;
        _initialized = true;

        // 让 UI 先渲染，然后在后台加载地图
        await Task.Yield();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            AddGeoJson();
            AddShp(false);
            MapControl.Map.Navigator.CenterOnAndZoomTo(
                new MPoint(SphericalMercator.FromLonLat(110, 35)),
                MapControl.Map.Navigator.Resolutions[3]);
            MapControl.Info += OnMapInfo;
        }, DispatcherPriority.Background);
    }

    private async void OnMapInfo(object? sender, MapInfoEventArgs e)
    {
        var mapInfo = e.GetMapInfo(e.Map.Layers.FindLayer("Countries"));
        var (longitude, latitude) = SphericalMercator.ToLonLat(mapInfo.WorldPosition.X, mapInfo.WorldPosition.Y);
        var location = await RYCBApiConverter.GetLocationNameAsync(new()
        {
            Latitude = latitude, Longitude = longitude
        });
        System.Console.WriteLine(
            $"Map Info: ({mapInfo.ScreenPosition.X}, {mapInfo.ScreenPosition.Y}), ({longitude}, {latitude}), {location.FirstOrDefault()?.name}");
    }

    private void AddGeoJson()
    {
        MapControl.Map.Layers.Add(CreateCountriesLayer());
    }

    public static RasterizingTileLayer CreateCountriesLayer() => new(CreateCountriesGeoJsonLayer());

    private static Layer CreateCountriesGeoJsonLayer() => new("Countries")
    {
        DataSource = CreateCountriesProvider(false),
        Style = new StyleCollection
        {
            Styles =
            {
                //CreateCountriesStyle(),
                new CalloutStyle()
            }
        }
    };

    private void AddShp(bool isWorld)
    {
        string shpFilePath = $"{AppDomain.CurrentDomain.BaseDirectory}\\Config\\{(isWorld ? "World" : "China")}.shp";

        var countrySource = new ShapeFile(shpFilePath, false);

        MapControl.Map.Layers.Add(new RasterizingLayer(CreateCountryLayer(countrySource)));
    }

    private static Layer CreateCountryLayer(IProvider countrySource) => new()
    {
        Name = "Countries",
        DataSource = countrySource,
        Style = CreateCountryTheme()
    };

    private static GradientTheme CreateCountryTheme()
    {
        var min = new VectorStyle
        {
            Outline = new Pen
            {
                Color = Color.White
            }
        };

        var max = new VectorStyle
        {
            Outline = new Pen { Color = Color.White },
            Line = new Pen { Color = Color.White },
        };

        return new GradientTheme("POPDENS", 0, 400, min, max)
            { FillColorBlend = ColorBlend.TwoColors(Color.LightGray, Color.Red) };
    }

    private static ProjectingProvider CreateCountriesProvider(bool isWorld)
    {
        var path = $"{AppDomain.CurrentDomain.BaseDirectory}\\Config\\{(isWorld ? "World" : "China")}.json";

        var provider = new GeoJsonProvider(path) { CRS = "EPSG:4326" };
        return new ProjectingProvider(provider) { CRS = "EPSG:3857" };
    }

    private void SelectingItemsControl_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Option == null || Option.SelectedIndex == null)
        {
            return;
        }

        switch (Option.SelectedIndex)
        {
            case 0:
                MapControl.Map.Layers.Clear();
                AddGeoJson();
                AddShp(false);
                break;
            case 1:
                MapControl.Map.Layers.Clear();
                AddGeoJson();
                AddShp(true);
                break;
        }
    }
}