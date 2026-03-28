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
    private bool _toggling;

    public TrayIconManager(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        _lifetime = lifetime;
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
        openItem.Click += (_, _) => Dispatcher.UIThread.Post(ShowFlyout);
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

    private static WindowIcon CreateIcon()
    {
        var pixelSize = new PixelSize(32, 32);
        using var bitmap = new RenderTargetBitmap(pixelSize, new Vector(96, 96));
        using (var ctx = bitmap.CreateDrawingContext())
        {
            ctx.DrawRectangle(
                new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                null,
                new Rect(0, 0, 32, 32));

            var text = new FormattedText(
                "K",
                System.Globalization.CultureInfo.InvariantCulture,
                Avalonia.Media.FlowDirection.LeftToRight,
                new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold),
                20, Brushes.White);
            ctx.DrawText(text, new Point((32 - text.Width) / 2, (32 - text.Height) / 2));
        }

        using var ms = new MemoryStream();
        bitmap.Save(ms);
        ms.Position = 0;
        return new WindowIcon(ms);
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
        _trayIcon?.Dispose();
        _lifetime.Shutdown();
    }

    public void Dispose()
    {
        _trayIcon?.Dispose();
    }
}
