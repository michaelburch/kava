using Ical.Net;
using IcsCalendarEvent = Ical.Net.CalendarComponents.CalendarEvent;
using KavaEvent = Kava.Core.Models.CalendarEvent;

namespace Kava.Providers.CalDav;

public static class IcsParser
{
    public static List<KavaEvent> ParseEvents(string icsData, string calendarId)
    {
        var events = new List<KavaEvent>();

        var calendar = Calendar.Load(icsData);
        foreach (var vEvent in calendar.Events)
        {
            try
            {
                var evt = MapEvent(vEvent, calendarId);
                if (evt != null)
                    events.Add(evt);
            }
            catch
            {
                // Skip events that fail to parse
            }
        }

        return events;
    }

    private static KavaEvent? MapEvent(IcsCalendarEvent vEvent, string calendarId)
    {
        if (vEvent.DtStart == null) return null;
        if (string.IsNullOrEmpty(vEvent.Uid)) return null;

        var start = vEvent.DtStart.AsDateTimeOffset;
        var end = vEvent.DtEnd?.AsDateTimeOffset ?? start;
        var isAllDay = !vEvent.DtStart.HasTime;

        string? meetingUrl = null;
        var hangout = vEvent.Properties["X-GOOGLE-HANGOUT"];
        var teams = vEvent.Properties["X-MICROSOFT-SKYPETEAMSMEETINGURL"];
        if (hangout != null)
            meetingUrl = hangout.Value?.ToString();
        else if (teams != null)
            meetingUrl = teams.Value?.ToString();
        else if (vEvent.Location != null && Uri.IsWellFormedUriString(vEvent.Location, UriKind.Absolute))
            meetingUrl = vEvent.Location;

        return new KavaEvent
        {
            EventId = $"{calendarId}::{vEvent.Uid}",
            CalendarId = calendarId,
            RemoteUid = vEvent.Uid,
            Title = vEvent.Summary ?? "(No title)",
            Description = vEvent.Description,
            Location = vEvent.Location,
            Start = start,
            End = end,
            TimeZoneId = vEvent.DtStart.TzId,
            IsAllDay = isAllDay,
            RecurrenceRule = vEvent.RecurrenceRules?.FirstOrDefault()?.ToString(),
            MeetingUrl = meetingUrl,
        };
    }
}
