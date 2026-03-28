namespace Kava.Core.Models;

public class CalendarEvent
{
    public string EventId { get; set; } = Guid.NewGuid().ToString();
    public string CalendarId { get; set; } = string.Empty;
    public string RemoteUid { get; set; } = string.Empty;
    public string? RemoteResourceUrl { get; set; }
    public string? ETag { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public string? TimeZoneId { get; set; }
    public bool IsAllDay { get; set; }
    public string? RecurrenceRule { get; set; }
    public string? MeetingUrl { get; set; }
    public string? RawICalendarPayload { get; set; }
    public DateTime? LastSeenUtc { get; set; }
}
