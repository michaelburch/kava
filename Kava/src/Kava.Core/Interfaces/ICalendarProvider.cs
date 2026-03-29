using Kava.Core.Models;

namespace Kava.Core.Interfaces;

public interface ICalendarProvider
{
    Task<IReadOnlyList<Calendar>> DiscoverCalendarsAsync(Account account, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        Calendar calendar,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        CancellationToken cancellationToken = default);

    Task<SyncResult<CalendarEvent>> SyncEventsAsync(
        Calendar calendar,
        string? syncToken,
        CancellationToken cancellationToken = default);
}

public class SyncResult<T>
{
    public required IReadOnlyList<T> Changed { get; init; }
    public required IReadOnlyList<string> DeletedUids { get; init; }
    public string? NewSyncToken { get; init; }
    public string? NewCTag { get; set; }
}
