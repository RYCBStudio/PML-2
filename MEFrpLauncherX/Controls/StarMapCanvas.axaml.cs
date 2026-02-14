using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;

namespace MEFrpLauncherX.Controls;

public partial class StarMapCanvas : UserControl
{
    // 星图配置（可根据需求调整）
    private const int NodeCount = 30; // 节点数量，越多越密集
    private const int MaxLineDistance = 150; // 节点最大连接距离
    private const double NodeSpeed = 0.5; // 节点移动速度
    private const double NodeMinRadius = 1; // 节点最小半径
    private const double NodeMaxRadius = 2.5; // 节点最大半径
    private const int AnimationFps = 15; // 动画帧率，越低越省资源
    private readonly TimeSpan _animationInterval = TimeSpan.FromMilliseconds(1000 / AnimationFps);

    // 节点实体类
    private class StarNode
    {
        public double X
        {
            get;
            set;
        }

        public double Y
        {
            get;
            set;
        }

        public double Vx
        {
            get;
            set;
        } // X轴速度

        public double Vy
        {
            get;
            set;
        } // Y轴速度

        public double Radius
        {
            get;
            set;
        }

        public SolidColorBrush Brush
        {
            get;
            set;
        }
    }

    private readonly List<StarNode> _nodes = new();
    private readonly DispatcherTimer _animationTimer;
    private bool _isNightMode; // 是否为夜间模式
    private readonly Random _random = new();

    public StarMapCanvas()
    {
        InitializeComponent();
        // 初始化动画定时器
        _animationTimer = new DispatcherTimer { Interval = _animationInterval };
        _animationTimer.Tick += AnimationTimer_Tick;
        // 窗口大小变化时重新生成节点
        SizeChanged += (s, e) => ResetStars();
        // 初始化夜间模式判断+节点
        CheckNightMode();
        ResetStars();
        // 启动定时器
        _animationTimer.Start();
    }

    // 夜间模式判断（23:00-7:00）
    private void CheckNightMode()
    {
        var hour = DateTime.Now.Hour;
        _isNightMode = hour is >= 23 or < 7;
        _isNightMode = true;// 测试
    }

    // 重置节点（窗口大小变化/初始化时）
    private void ResetStars()
    {
        _nodes.Clear();
        StarCanvas.Children.Clear();
        if (!_isNightMode || double.IsNaN(Width) || double.IsNaN(Height)) return;

        // 随机生成节点
        for (int i = 0; i < NodeCount; i++)
        {
            var node = new StarNode
            {
                X = _random.NextDouble() * Width,
                Y = _random.NextDouble() * Height,
                // 随机移动方向和速度
                Vx = (_random.NextDouble() - 0.5) * NodeSpeed,
                Vy = (_random.NextDouble() - 0.5) * NodeSpeed,
                Radius = _random.NextDouble() * (NodeMaxRadius - NodeMinRadius) + NodeMinRadius,
                // 淡蓝色系（贴合内网穿透工具科技感，可修改）
                Brush = new SolidColorBrush(Color.FromArgb(180, 100, 180, 255))
            };
            _nodes.Add(node);
        }
    }

    // 动画核心逻辑（每一帧更新）
    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        // 每秒重新判断一次夜间模式
        if (DateTime.Now.Second == 0) CheckNightMode();
        if (!_isNightMode)
        {
            StarCanvas.Children.Clear();
            return;
        }

        StarCanvas.Children.Clear();
        // 更新所有节点位置
        foreach (var node in _nodes)
        {
            // 边界碰撞反弹
            if (node.X < 0 || node.X > Width) node.Vx *= -1;
            if (node.Y < 0 || node.Y > Height) node.Vy *= -1;
            // 移动节点
            node.X += node.Vx;
            node.Y += node.Vy;
            // 绘制节点（小圆）
            var ellipse = new Ellipse
            {
                Width = node.Radius * 2,
                Height = node.Radius * 2,
                Fill = node.Brush
            };
            Canvas.SetLeft(ellipse, node.X - node.Radius);
            Canvas.SetTop(ellipse, node.Y - node.Radius);
            StarCanvas.Children.Add(ellipse);
        }

        // 绘制节点间的连接线条（距离小于阈值时显示）
        for (int i = 0; i < _nodes.Count; i++)
        {
            for (int j = i + 1; j < _nodes.Count; j++)
            {
                var node1 = _nodes[i];
                var node2 = _nodes[j];
                // 计算两点距离
                var distance = Math.Sqrt(Math.Pow(node2.X - node1.X, 2) + Math.Pow(node2.Y - node1.Y, 2));
                if (distance > MaxLineDistance) continue;

                // 线条透明度随距离衰减（越远越淡）
                var alpha = (byte)(255 * (1 - distance / MaxLineDistance) * 0.6);
                var lineBrush = new SolidColorBrush(Color.FromArgb(alpha, 100, 180, 255));
                // 绘制线条
                var line = new Line
                {
                    StartPoint = new Point(node1.X, node1.Y),
                    EndPoint = new Point(node2.X, node2.Y),
                    Stroke = lineBrush,
                    StrokeThickness = 0.5
                };
                StarCanvas.Children.Add(line);
            }
        }
    }

    // 手动控制星图显隐（可选，供主窗口调用）
    public void ToggleStarMap(bool isShow)
    {
        _isNightMode = isShow;
        ResetStars();
    }
}