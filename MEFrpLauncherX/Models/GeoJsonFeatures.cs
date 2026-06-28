using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Path = Avalonia.Controls.Shapes.Path;

namespace MEFrpLauncherX.Models;

public class GeoJsonFeature
{
    public string Type
    {
        get;
        set;
    }

    public FeatureProperties Properties
    {
        get;
        set;
    }

    public Geometry Geometry
    {
        get;
        set;
    }
}

public class FeatureProperties
{
    public string Name
    {
        get;
        set;
    }

    [JsonPropertyName("adcode")]
    public string AdCode
    {
        get;
        set;
    }
}

public class Geometry
{
    public string Type
    {
        get;
        set;
    }

    public List<List<List<List<double>>>> Coordinates
    {
        get;
        set;
    }
}

public class GeoJsonRoot
{
    public string Type
    {
        get;
        set;
    }

    public List<GeoJsonFeature> Features
    {
        get;
        set;
    }
}

public static class GeoJsonToPathConverter
{
    /// <summary>
    /// 从 GeoJSON 文件生成地区 Path 列表
    /// </summary>
    /// <param name="geoJsonPath">a.json 文件路径</param>
    /// <param name="imgWidth">地图图片宽度</param>
    /// <param name="imgHeight">地图图片高度</param>
    /// <returns>地区名称 -> Path 控件 的字典</returns>
    public static Dictionary<string, Path> ConvertToPaths(string geoJsonPath, double imgWidth, double imgHeight)
    {
        var json = File.ReadAllText(geoJsonPath);
        var root = JsonSerializer.Deserialize<GeoJsonRoot>(json, App.AppJsonSerializerContext.GeoJsonRoot);

        var result = new Dictionary<string, Path>();

        foreach (var feature in root.Features)
        {
            var regionName = feature.Properties.Name;
            var geometry = feature.Geometry;

            if (geometry.Type != "MultiPolygon")
                continue;

            var geometries = new List<PathGeometry>();

            // MultiPolygon 是多层嵌套： [多边形组][多边形环][点列表]
            foreach (var polygonGroup in geometry.Coordinates) // 每个省可能有多个不相连的区域（如岛屿）
            {
                foreach (var polygonRing in polygonGroup) // 外环 + 内洞（内洞可选）
                {
                    // 转换外环（第一个环通常是外边界）
                    var points = polygonRing.Select(p => GeoToPixel(p[0], p[1], imgWidth, imgHeight)).ToList();
                    if (points.Count < 3) continue;

                    var figure = new PathFigure
                    {
                        StartPoint = points[0],
                        IsClosed = true
                    };

                    for (int i = 1; i < points.Count; i++)
                    {
                        figure.Segments.Add(new LineSegment { Point = points[i] });
                    }

                    var pathGeometry = new PathGeometry();
                    pathGeometry.Figures.Add(figure);
                    geometries.Add(pathGeometry);
                }
            }

            // 合并多个几何体为一个 Path（处理岛屿）
            var combinedGeometry = new GeometryGroup();
            foreach (var geo in geometries)
                combinedGeometry.Children.Add(geo);

            var path = new Path
            {
                Data = combinedGeometry,
                Fill = Brushes.Transparent,
                Stroke = Brushes.Gray,
                StrokeThickness = 1,
                Tag = regionName,
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            result[regionName] = path;
        }

        return result;
    }

    private static Point GeoToPixel(double lon, double lat, double imgWidth, double imgHeight)
    {
        double x = (lon + 180) / 360.0 * imgWidth;
        double y = (90 - lat) / 180.0 * imgHeight;
        return new Point(x, y);
    }
}