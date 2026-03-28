using System.Runtime.InteropServices;
using H.NotifyIcon.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Kava.Windows;

public class TrayIconManager : IDisposable
{
    private TrayIcon? _trayIcon;
    private FlyoutWindow? _flyoutWindow;
    private bool _toggling;
    private readonly DispatcherQueue _dispatcher;

    public TrayIconManager()
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread();
    }

    public void Initialize()
    {
        _trayIcon = new TrayIcon();
        _trayIcon.ToolTip = "Kava";

        // Generate and set the icon (TrayIcon.Icon is an IntPtr HICON)
        var hIcon = GenerateIcon();
        if (hIcon != IntPtr.Zero)
        {
            _trayIcon.Icon = hIcon;
        }

        _trayIcon.MessageWindow.MouseEventReceived += OnMouseEvent;
        _trayIcon.Create();
    }

    private void OnMouseEvent(object? sender, MessageWindow.MouseEventReceivedEventArgs e)
    {
        switch (e.MouseEvent)
        {
            case MouseEvent.IconLeftMouseUp:
                _dispatcher.TryEnqueue(ToggleFlyout);
                break;
            case MouseEvent.IconRightMouseUp:
                ShowContextMenu();
                break;
        }
    }

    private void ShowContextMenu()
    {
        var menu = new PopupMenu();
        menu.Items.Add(new PopupMenuItem("Open Calendar", (_, _) =>
            _dispatcher.TryEnqueue(ShowFlyout)));
        menu.Items.Add(new PopupMenuItem("Sync Now", (_, _) => { /* TODO */ }));
        menu.Items.Add(new PopupMenuSeparator());
        menu.Items.Add(new PopupMenuItem("Exit", (_, _) =>
            _dispatcher.TryEnqueue(ExitApp)));

        GetCursorPos(out var pt);
        menu.Show(_trayIcon!.MessageWindow.Handle, pt.X, pt.Y);
    }

    private static IntPtr GenerateIcon()
    {
        try
        {
            using var bitmap = new System.Drawing.Bitmap(32, 32);
            using (var g = System.Drawing.Graphics.FromImage(bitmap))
            {
                g.Clear(System.Drawing.Color.FromArgb(0, 120, 212));
                using var font = new System.Drawing.Font("Segoe UI", 18, System.Drawing.FontStyle.Bold);
                using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.White);
                var size = g.MeasureString("K", font);
                g.DrawString("K", font, brush,
                    (32 - size.Width) / 2,
                    (32 - size.Height) / 2);
            }
            return bitmap.GetHicon();
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

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
        _flyoutWindow.Activate();
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
        Application.Current.Exit();
    }

    public void Dispose()
    {
        _trayIcon?.Dispose();
    }
}
