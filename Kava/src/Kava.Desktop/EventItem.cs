namespace Kava.Desktop;

public class EventItem
{
    public string Title { get; init; } = string.Empty;
    public string TimeRange { get; init; } = string.Empty;
    public string? Subtitle { get; init; }
    public string CalendarColor { get; init; } = "#0078D4";
    public bool IsAllDay { get; init; }
    public string? MeetingUrl { get; init; }
}
