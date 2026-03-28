using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WinColor = global::Windows.UI.Color;

namespace Kava.Windows;

public class EventItem
{
    public string Title { get; init; } = string.Empty;
    public string TimeRange { get; init; } = string.Empty;
    public string? Subtitle { get; init; }
    public string CalendarColor { get; init; } = "#0078D4";
    public bool IsAllDay { get; init; }
    public string? MeetingUrl { get; init; }

    public Brush CalendarColorBrush
    {
        get
        {
            try
            {
                var hex = CalendarColor.TrimStart('#');
                if (hex.Length == 6)
                {
                    var r = Convert.ToByte(hex[..2], 16);
                    var g = Convert.ToByte(hex[2..4], 16);
                    var b = Convert.ToByte(hex[4..6], 16);
                    return new SolidColorBrush(WinColor.FromArgb(255, r, g, b));
                }
            }
            catch { }
            return new SolidColorBrush(WinColor.FromArgb(255, 0, 120, 212));
        }
    }
}
