using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace Kava.Desktop;

public class TrayIconManager : IDisposable
{
    private readonly IClassicDesktopStyleApplicationLifetime _lifetime;
    private TrayIcon? _trayIcon;
    private FlyoutWindow? _flyoutWindow;
    private MainWindow? _mainWindow;
    private bool _toggling;

    public static TrayIconManager? Instance { get; private set; }

    public TrayIconManager(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        _lifetime = lifetime;
        Instance = this;
    }

    public void Initialize()
    {
        _trayIcon = new TrayIcon
        {
            ToolTipText = "Kava",
            Icon = CreateIcon(),
            Menu = CreateMenu(),
        };
        _trayIcon.Clicked += (_, _) => Dispatcher.UIThread.Post(ToggleFlyout);
        _trayIcon.IsVisible = true;
    }

    private NativeMenu CreateMenu()
    {
        var menu = new NativeMenu();

        var openItem = new NativeMenuItem("Open Calendar");
        openItem.Click += (_, _) => Dispatcher.UIThread.Post(ShowMainWindow);
        menu.Add(openItem);

        var syncItem = new NativeMenuItem("Sync Now");
        syncItem.Click += (_, _) => { /* TODO */ };
        menu.Add(syncItem);

        menu.Add(new NativeMenuItemSeparator());

        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (_, _) => Dispatcher.UIThread.Post(ExitApp);
        menu.Add(exitItem);

        return menu;
    }

    public static WindowIcon CreateIcon()
    {
        var pixelSize = new PixelSize(32, 32);
        using var bitmap = new RenderTargetBitmap(pixelSize, new Vector(96, 96));
        using (var ctx = bitmap.CreateDrawingContext())
        {
            var pen = new Pen(Brushes.White, 1.8);
            ctx.DrawGeometry(null, pen, BuildKavaShape(16, 16, 22, 18, 5, -15));
        }

        using var ms = new MemoryStream();
        bitmap.Save(ms);
        ms.Position = 0;
        return new WindowIcon(ms);
    }

    /// <summary>
    /// Builds the Kava logo shape: a 5-sided figure with 3 rounded corners
    /// (top-left, top-right, bottom-right) and a flat angled edge at bottom-left,
    /// rotated by the given angle.
    /// </summary>
    private static StreamGeometry BuildKavaShape(
        double cx, double cy, double w, double h, double r, double angleDeg)
    {
        var hw = w / 2;
        var hh = h / 2;
        var cut = 6.0; // how far the angled cut intrudes
        var rad = angleDeg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);

        Point Rot(double x, double y)
        {
            var dx = x - cx;
            var dy = y - cy;
            return new Point(cx + dx * cos - dy * sin, cy + dx * sin + dy * cos);
        }

        var geo = new StreamGeometry();
        using (var sgc = geo.Open())
        {
            // Top-left corner (rounded)
            sgc.BeginFigure(Rot(cx - hw + r, cy - hh), false);

            // Top edge → top-right corner (rounded)
            sgc.LineTo(Rot(cx + hw - r, cy - hh));
            sgc.ArcTo(Rot(cx + hw, cy - hh + r), new Size(r, r), 0, false, SweepDirection.Clockwise);

            // Right edge → bottom-right corner (rounded)
            sgc.LineTo(Rot(cx + hw, cy + hh - r));
            sgc.ArcTo(Rot(cx + hw - r, cy + hh), new Size(r, r), 0, false, SweepDirection.Clockwise);

            // Bottom edge → angled bottom-left (2 points = flat diagonal)
            sgc.LineTo(Rot(cx - hw + cut, cy + hh));       // bottom edge ends
            sgc.LineTo(Rot(cx - hw, cy + hh - cut));       // angled edge to left side

            // Left edge → top-left corner (rounded)
            sgc.LineTo(Rot(cx - hw, cy - hh + r));
            sgc.ArcTo(Rot(cx - hw + r, cy - hh), new Size(r, r), 0, false, SweepDirection.Clockwise);

            sgc.EndFigure(true);
        }

        return geo;
    }

    private void ToggleFlyout()
    {
        if (_toggling) return;
        _toggling = true;
        try
        {
            if (_flyoutWindow != null)
            {
                CloseFlyout();
            }
            else
            {
                ShowFlyout();
            }
        }
        finally
        {
            _toggling = false;
        }
    }

    private void ShowFlyout()
    {
        if (_flyoutWindow != null) return;

        _flyoutWindow = new FlyoutWindow();
        _flyoutWindow.Closed += (_, _) => _flyoutWindow = null;
        _flyoutWindow.Show();
        _flyoutWindow.PositionNearTaskbar();
    }

    private void CloseFlyout()
    {
        var win = _flyoutWindow;
        _flyoutWindow = null;
        try { win?.Close(); } catch { }
    }

    private void ExitApp()
    {
        CloseFlyout();
        CloseMainWindow();
        _trayIcon?.Dispose();
        _lifetime.Shutdown();
    }

    public void ShowMainWindow()
    {
        CloseFlyout();

        if (_mainWindow != null)
        {
            _mainWindow.Activate();
            return;
        }

        _mainWindow = new MainWindow();
        _mainWindow.Closed += (_, _) => _mainWindow = null;
        _mainWindow.Show();
    }

    private void CloseMainWindow()
    {
        var win = _mainWindow;
        _mainWindow = null;
        try { win?.Close(); } catch { }
    }

    public void Dispose()
    {
        _trayIcon?.Dispose();
    }
}
