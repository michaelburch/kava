using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace Kava.Desktop;

internal static class ThemeHelper
{
    public static IBrush Brush(string key)
    {
        var app = Application.Current!;
        if (app.Resources.TryGetResource(key, app.ActualThemeVariant, out var value) && value is IBrush brush)
            return brush;
        return Brushes.Magenta; // Fallback for debugging
    }

    public static bool IsDark =>
        Application.Current?.ActualThemeVariant != ThemeVariant.Light;
}
