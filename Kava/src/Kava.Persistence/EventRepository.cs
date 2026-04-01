using System.Globalization;
using System.Text.Json;
using Kava.Core.Models;
using Microsoft.Data.Sqlite;

namespace Kava.Persistence;

public class EventRepository
{
    private const string CalendarIdParameter = "@calId";
    private const string CalendarIdsJsonParameter = "@calendarIdsJson";
    private const string RemoteUidsJsonParameter = "@remoteUidsJson";
    private const string ResourceUrlsJsonParameter = "@resourceUrlsJson";

    private readonly KavaDatabase _db;

    public EventRepository(KavaDatabase db)
    {
        _db = db;
    }

    public async Task<List<CalendarEvent>> GetByDateRangeAsync(
        IEnumerable<string> calendarIds,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd)
    {
        var events = new List<CalendarEvent>();
        var ids = calendarIds.ToList();
        if (ids.Count == 0) return events;

        using var cmd = _db.Connection.CreateCommand();

        cmd.CommandText = """
            SELECT * FROM Events
            WHERE EXISTS (
                SELECT 1
                FROM json_each(@calendarIdsJson)
                WHERE value = Events.CalendarId
            )
              AND EndUtc >= @start AND StartUtc <= @end
            ORDER BY StartUtc
            """;
        cmd.Parameters.AddWithValue(CalendarIdsJsonParameter, JsonSerializer.Serialize(ids));
        cmd.Parameters.AddWithValue("@start", rangeStart.UtcDateTime.ToString("O"));
        cmd.Parameters.AddWithValue("@end", rangeEnd.UtcDateTime.ToString("O"));

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            events.Add(ReadEvent(reader));
        }
        return events;
    }

    public async Task UpsertAsync(CalendarEvent evt)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Events (EventId, CalendarId, RemoteUid, RemoteResourceUrl, ETag, Title, Description, Location,
                StartUtc, EndUtc, TimeZoneId, IsAllDay, RecurrenceRule, MeetingUrl, RawICalendarPayload, LastSeenUtc)
            VALUES (@id, @calId, @uid, @url, @etag, @title, @desc, @loc,
                @start, @end, @tz, @allday, @rrule, @meeting, @raw, @seen)
            ON CONFLICT(CalendarId, RemoteUid) DO UPDATE SET
                RemoteResourceUrl = @url, ETag = @etag, Title = @title,
                Description = @desc, Location = @loc, StartUtc = @start, EndUtc = @end,
                TimeZoneId = @tz, IsAllDay = @allday, RecurrenceRule = @rrule,
                MeetingUrl = @meeting, RawICalendarPayload = @raw, LastSeenUtc = @seen
            """;
        cmd.Parameters.AddWithValue("@id", evt.EventId);
        cmd.Parameters.AddWithValue(CalendarIdParameter, evt.CalendarId);
        cmd.Parameters.AddWithValue("@uid", evt.RemoteUid);
        cmd.Parameters.AddWithValue("@url", (object?)evt.RemoteResourceUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@etag", (object?)evt.ETag ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@title", evt.Title);
        cmd.Parameters.AddWithValue("@desc", (object?)evt.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@loc", (object?)evt.Location ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@start", evt.Start.UtcDateTime.ToString("O"));
        cmd.Parameters.AddWithValue("@end", evt.End.UtcDateTime.ToString("O"));
        cmd.Parameters.AddWithValue("@tz", (object?)evt.TimeZoneId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@allday", evt.IsAllDay ? 1 : 0);
        cmd.Parameters.AddWithValue("@rrule", (object?)evt.RecurrenceRule ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@meeting", (object?)evt.MeetingUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@raw", (object?)evt.RawICalendarPayload ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@seen", (object?)evt.LastSeenUtc?.ToString("O") ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteByRemoteUidsAsync(string calendarId, IEnumerable<string> remoteUids)
    {
        var uids = remoteUids.ToList();
        if (uids.Count == 0) return;

        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = $"""
            DELETE FROM Events
            WHERE CalendarId = {CalendarIdParameter}
              AND EXISTS (
                  SELECT 1
                  FROM json_each({RemoteUidsJsonParameter})
                  WHERE value = Events.RemoteUid
              )
            """;
        cmd.Parameters.AddWithValue(CalendarIdParameter, calendarId);
        cmd.Parameters.AddWithValue(RemoteUidsJsonParameter, JsonSerializer.Serialize(uids));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteByRemoteUrlsAsync(string calendarId, IEnumerable<string> resourceUrls)
    {
        var urls = resourceUrls.ToList();
        if (urls.Count == 0) return;

        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = $"""
            DELETE FROM Events
            WHERE CalendarId = {CalendarIdParameter}
              AND EXISTS (
                  SELECT 1
                  FROM json_each({ResourceUrlsJsonParameter})
                  WHERE value = Events.RemoteResourceUrl
              )
            """;
        cmd.Parameters.AddWithValue(CalendarIdParameter, calendarId);
        cmd.Parameters.AddWithValue(ResourceUrlsJsonParameter, JsonSerializer.Serialize(urls));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteByCalendarAsync(string calendarId)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = $"DELETE FROM Events WHERE CalendarId = {CalendarIdParameter}";
        cmd.Parameters.AddWithValue(CalendarIdParameter, calendarId);
        await cmd.ExecuteNonQueryAsync();
    }

    private static CalendarEvent ReadEvent(SqliteDataReader reader) => new()
    {
        EventId = reader.GetString(reader.GetOrdinal("EventId")),
        CalendarId = reader.GetString(reader.GetOrdinal("CalendarId")),
        RemoteUid = reader.GetString(reader.GetOrdinal("RemoteUid")),
        RemoteResourceUrl = reader.IsDBNull(reader.GetOrdinal("RemoteResourceUrl")) ? null : reader.GetString(reader.GetOrdinal("RemoteResourceUrl")),
        ETag = reader.IsDBNull(reader.GetOrdinal("ETag")) ? null : reader.GetString(reader.GetOrdinal("ETag")),
        Title = reader.GetString(reader.GetOrdinal("Title")),
        Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
        Location = reader.IsDBNull(reader.GetOrdinal("Location")) ? null : reader.GetString(reader.GetOrdinal("Location")),
        Start = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("StartUtc")), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        End = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("EndUtc")), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        TimeZoneId = reader.IsDBNull(reader.GetOrdinal("TimeZoneId")) ? null : reader.GetString(reader.GetOrdinal("TimeZoneId")),
        IsAllDay = reader.GetInt32(reader.GetOrdinal("IsAllDay")) == 1,
        RecurrenceRule = reader.IsDBNull(reader.GetOrdinal("RecurrenceRule")) ? null : reader.GetString(reader.GetOrdinal("RecurrenceRule")),
        MeetingUrl = reader.IsDBNull(reader.GetOrdinal("MeetingUrl")) ? null : reader.GetString(reader.GetOrdinal("MeetingUrl")),
        RawICalendarPayload = reader.IsDBNull(reader.GetOrdinal("RawICalendarPayload")) ? null : reader.GetString(reader.GetOrdinal("RawICalendarPayload")),
        LastSeenUtc = reader.IsDBNull(reader.GetOrdinal("LastSeenUtc"))
            ? null
            : DateTime.Parse(reader.GetString(reader.GetOrdinal("LastSeenUtc")), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
    };
}
