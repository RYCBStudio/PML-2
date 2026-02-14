using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;

namespace RYCB.PML2.Splash.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
    
    public MainWindow(string[]? args)
    {
        InitializeComponent();
        if (args?.Length == 1)
        {
            TbVersion.Text = args.First();
        }else if (args?.Length == 2)
        {
            switch (args[0])
            {
                case "--version" or "-v":
                    TbVersion.Text = args[1];
                    break;
                case "--back" or "-b" or "--background":
                    ImgBackground.Source = new Bitmap(args[1]);
                    break;
            }
        }
        else if (args?.Length == 4)
        {
            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--version" or "-v":
                        TbVersion.Text = args[i + 1];
                        continue;
                    case "--back" or "-b" or "--background":
                        ImgBackground.Source = new Bitmap(args[i + 1]);
                        continue;
                }
            }
        }
    }
    
    private bool _mouseDownForWindowMoving = false;
    private PointerPoint _originalPoint;

    private void InputElement_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_mouseDownForWindowMoving) return;

        PointerPoint currentPoint = e.GetCurrentPoint(this);
        Position = new PixelPoint(Position.X + (int)(currentPoint.Position.X - _originalPoint.Position.X),
            Position.Y + (int)(currentPoint.Position.Y - _originalPoint.Position.Y));
    }

    private void InputElement_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (WindowState == WindowState.Maximized || WindowState == WindowState.FullScreen) return;

        _mouseDownForWindowMoving = true;
        _originalPoint = e.GetCurrentPoint(this);
    }

    private void InputElement_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _mouseDownForWindowMoving = false;
    }
}