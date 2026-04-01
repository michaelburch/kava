using Kava.Core.Models;

namespace Kava.Desktop;

internal static class DesktopEventMapper
{
    private const string DefaultCalendarColor = "#0078D4";
    private const string AllDayLabel = "All day";

    internal static Dictionary<DateOnly, List<EventItem>> BuildEventLookup(
        IEnumerable<CalendarEvent> events,
        IReadOnlyDictionary<string, string> colorMap)
    {
        var result = new Dictionary<DateOnly, List<EventItem>>();

        foreach (var evt in events)
        {
            var date = DateOnly.FromDateTime(evt.Start.LocalDateTime);
            var item = MapToEventItem(evt, colorMap.GetValueOrDefault(evt.CalendarId, DefaultCalendarColor));

            if (!result.TryGetValue(date, out var list))
            {
                list = [];
                result[date] = list;
            }

            list.Add(item);
        }

        return result;
    }

    internal static EventItem MapToEventItem(CalendarEvent evt, string color) => new()
    {
        Title = evt.Title,
        TimeRange = FormatTimeRange(evt),
        Subtitle = evt.Location,
        CalendarId = evt.CalendarId,
        CalendarColor = color,
        IsAllDay = evt.IsAllDay,
        MeetingUrl = evt.MeetingUrl,
    };

    internal static string FormatTimeRange(CalendarEvent evt)
    {
        if (evt.IsAllDay)
            return AllDayLabel;

        var start = evt.Start.LocalDateTime;
        var end = evt.End.LocalDateTime;
        return $"{start:h:mm tt} \u2013 {end:h:mm tt}";
    }
}