using Kava.Core.Models;
using Microsoft.Data.Sqlite;

namespace Kava.Persistence;

public class CalendarRepository
{
    private readonly KavaDatabase _db;

    public CalendarRepository(KavaDatabase db)
    {
        _db = db;
    }

    public async Task<List<Calendar>> GetByAccountAsync(string accountId)
    {
        var calendars = new List<Calendar>();
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Calendars WHERE AccountId = @accountId";
        cmd.Parameters.AddWithValue("@accountId", accountId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            calendars.Add(ReadCalendar(reader));
        }
        return calendars;
    }

    public async Task<List<Calendar>> GetEnabledAsync()
    {
        var calendars = new List<Calendar>();
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Calendars WHERE IsEnabled = 1";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            calendars.Add(ReadCalendar(reader));
        }
        return calendars;
    }

    public async Task UpsertAsync(Calendar calendar)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Calendars (CalendarId, AccountId, DisplayName, CalDavUrl, Color, LocalColor, IsReadOnly, IsEnabled, CTag, SyncToken, IcsUrl)
            VALUES (@id, @accountId, @name, @url, @color, @localColor, @ro, @enabled, @ctag, @syncToken, @icsUrl)
            ON CONFLICT(CalendarId) DO UPDATE SET
                DisplayName = @name, CalDavUrl = @url, Color = @color, LocalColor = @localColor,
                IsReadOnly = @ro, IsEnabled = @enabled, CTag = @ctag, SyncToken = @syncToken, IcsUrl = @icsUrl
            """;
        cmd.Parameters.AddWithValue("@id", calendar.CalendarId);
        cmd.Parameters.AddWithValue("@accountId", calendar.AccountId);
        cmd.Parameters.AddWithValue("@name", calendar.DisplayName);
        cmd.Parameters.AddWithValue("@url", calendar.CalDavUrl);
        cmd.Parameters.AddWithValue("@color", calendar.Color);
        cmd.Parameters.AddWithValue("@localColor", (object?)calendar.LocalColor ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ro", calendar.IsReadOnly ? 1 : 0);
        cmd.Parameters.AddWithValue("@enabled", calendar.IsEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@ctag", (object?)calendar.CTag ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@syncToken", (object?)calendar.SyncToken ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@icsUrl", (object?)calendar.IcsUrl ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    private static Calendar ReadCalendar(SqliteDataReader reader)
    {
        var cal = new Calendar
        {
            CalendarId = reader.GetString(reader.GetOrdinal("CalendarId")),
            AccountId = reader.GetString(reader.GetOrdinal("AccountId")),
            DisplayName = reader.GetString(reader.GetOrdinal("DisplayName")),
            CalDavUrl = reader.GetString(reader.GetOrdinal("CalDavUrl")),
            Color = reader.GetString(reader.GetOrdinal("Color")),
            LocalColor = TryGetOptionalString(reader, "LocalColor"),
            IsReadOnly = reader.GetInt32(reader.GetOrdinal("IsReadOnly")) == 1,
            IsEnabled = reader.GetInt32(reader.GetOrdinal("IsEnabled")) == 1,
            CTag = reader.IsDBNull(reader.GetOrdinal("CTag")) ? null : reader.GetString(reader.GetOrdinal("CTag")),
            SyncToken = TryGetOptionalString(reader, "SyncToken"),
        };

        cal.IcsUrl = TryGetOptionalString(reader, "IcsUrl");

        return cal;
    }

    private static string? TryGetOptionalString(SqliteDataReader reader, string columnName)
    {
        try
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
