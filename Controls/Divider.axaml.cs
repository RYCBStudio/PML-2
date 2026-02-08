// Divider.cs

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace MEFrpLauncherX.Controls
{
    public class Divider : ContentControl
    {
        public static readonly StyledProperty<IBrush> BackgroundProperty =
            Border.BackgroundProperty.AddOwner<Divider>();

        public static readonly StyledProperty<IBrush> ForegroundProperty =
            TextBlock.ForegroundProperty.AddOwner<Divider>();

        public static readonly StyledProperty<Orientation> OrientationProperty =
            StackPanel.OrientationProperty.AddOwner<Divider>();

        public static readonly StyledProperty<FontFamily> FontFamilyProperty =
            TextBlock.FontFamilyProperty.AddOwner<Divider>();

        private ContentPresenter? _contentPresenter;
        private Panel? _container;

        static Divider()
        {
            AffectsRender<Divider>(BackgroundProperty, ForegroundProperty, OrientationProperty);
        }

        public IBrush Background
        {
            get => GetValue(BackgroundProperty);
            set => SetValue(BackgroundProperty, value);
        }

        public IBrush Foreground
        {
            get => GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public Orientation Orientation
        {
            get => GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        public FontFamily FontFamily
        {
            get => GetValue(FontFamilyProperty);
            set => SetValue(FontFamilyProperty, value);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _contentPresenter = e.NameScope.Find<ContentPresenter>("PART_ContentPresenter");
            _container = e.NameScope.Find<Panel>("PART_Container");
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            
            if (Background == null)
            {
                return;
            }

            var bounds = Bounds;
            var centerX = bounds.Width / 2;
            var centerY = bounds.Height / 2;
            
            var lineBrush = Background;
            var linePen = new Pen(lineBrush);

            if (Orientation == Orientation.Horizontal)
            {
                // 如果有内容，在内容两侧绘制线条
                if (Content != null && _contentPresenter is { Bounds.Width: > 0 })
                {
                    var contentLeft = _contentPresenter.Bounds.Left;
                    var contentRight = _contentPresenter.Bounds.Right;

                    // 绘制左侧线条
                    context.DrawLine(linePen, 
                        new Point(0, centerY), 
                        new Point(contentLeft - 10, centerY));
                    
                    // 绘制右侧线条
                    context.DrawLine(linePen, 
                        new Point(contentRight + 10, centerY), 
                        new Point(bounds.Width, centerY));
                }
                else
                {
                    // 没有内容时绘制完整线条
                    context.DrawLine(linePen, 
                        new Point(0, centerY), 
                        new Point(bounds.Width, centerY));
                }
            }
            else
            {
                // 垂直方向的分割线
                if (Content != null && _contentPresenter is { Bounds.Height: > 0 })
                {
                    var contentTop = _contentPresenter.Bounds.Top;
                    var contentBottom = _contentPresenter.Bounds.Bottom;

                    // 绘制上方线条
                    context.DrawLine(linePen, 
                        new Point(centerX, 0), 
                        new Point(centerX, contentTop - 10));
                    
                    // 绘制下方线条
                    context.DrawLine(linePen, 
                        new Point(centerX, contentBottom + 10), 
                        new Point(centerX, bounds.Height));
                }
                else
                {
                    // 没有内容时绘制完整线条
                    context.DrawLine(linePen, 
                        new Point(centerX, 0), 
                        new Point(centerX, bounds.Height));
                }
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (_contentPresenter != null)
            {
                // 测量内容大小
                _contentPresenter.Measure(finalSize);
                var contentSize = _contentPresenter.DesiredSize;
                
                // 居中排列内容
                var x = (finalSize.Width - contentSize.Width) / 2;
                var y = (finalSize.Height - contentSize.Height) / 2;
                
                _contentPresenter.Arrange(new Rect(new Point(x, y), contentSize));
            }
            
            return finalSize;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var contentSize = new Size(0, 0);
            
            if (_contentPresenter != null)
            {
                _contentPresenter.Measure(availableSize);
                contentSize = _contentPresenter.DesiredSize;
            }

            // 确保最小尺寸，以便线条可见
            if (Orientation == Orientation.Horizontal)
            {
                return new Size(contentSize.Width + 20, Math.Max(2, contentSize.Height));
            }

            return new Size(Math.Max(2, contentSize.Width), contentSize.Height + 20);
        }
    }
}