using System.Reflection;
using Kava.Core.Models;
using Kava.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Core.Persistence.Tests.Repositories;

public sealed class EventRepositoryTests : IDisposable
{
    private readonly string _databasePath;

    public EventRepositoryTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"kava-persistence-{Guid.NewGuid():N}.db");
    }

    [Fact]
    public async Task AccountRepository_SupportsRoundTripAndDelete()
    {
        using var db = new KavaDatabase(_databasePath);
        var repository = new AccountRepository(db);
        var account = new Account
        {
            AccountId = "acc-1",
            ProviderType = ProviderType.CalDav,
            DisplayName = "Work",
            ServerBaseUrl = "https://calendar.example.com",
            Username = "michael",
            CredentialReference = "cred-1",
            LastSyncUtc = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc),
            SyncToken = "token-1",
            IsEnabled = true,
            SupportsCalendars = true,
            SupportsContacts = false,
        };

        await repository.UpsertAsync(account);

        var loaded = await repository.GetByIdAsync(account.AccountId);
        var all = await repository.GetAllAsync();

        Assert.NotNull(loaded);
        Assert.Single(all);
        Assert.Equal(account.DisplayName, loaded!.DisplayName);
        Assert.Equal(account.ProviderType, loaded.ProviderType);
        Assert.Equal(account.ServerBaseUrl, loaded.ServerBaseUrl);
        Assert.Equal(account.Username, loaded.Username);
        Assert.Equal(account.CredentialReference, loaded.CredentialReference);
        Assert.Equal(account.LastSyncUtc, loaded.LastSyncUtc);
        Assert.Equal(account.SyncToken, loaded.SyncToken);
        Assert.True(loaded.IsEnabled);
        Assert.True(loaded.SupportsCalendars);
        Assert.False(loaded.SupportsContacts);

        account.DisplayName = "Work Updated";
        account.IsEnabled = false;
        account.SupportsContacts = true;
        await repository.UpsertAsync(account);

        loaded = await repository.GetByIdAsync(account.AccountId);
        Assert.NotNull(loaded);
        Assert.Equal("Work Updated", loaded!.DisplayName);
        Assert.False(loaded.IsEnabled);
        Assert.True(loaded.SupportsContacts);

        await repository.DeleteAsync(account.AccountId);

        Assert.Null(await repository.GetByIdAsync(account.AccountId));
        Assert.Empty(await repository.GetAllAsync());
    }

    [Fact]
    public async Task CalendarRepository_SupportsRoundTripAndEnabledFilter()
    {
        using var db = new KavaDatabase(_databasePath);
        await SeedAccountAsync(db, "acc-1");

        var repository = new CalendarRepository(db);
        var calendar = new Calendar
        {
            CalendarId = "cal-1",
            AccountId = "acc-1",
            DisplayName = "Team",
            CalDavUrl = "https://calendar.example.com/team",
            Color = "#112233",
            LocalColor = "#445566",
            IsReadOnly = true,
            IsEnabled = true,
            CTag = "ctag-1",
            SyncToken = "sync-1",
            IcsUrl = "https://calendar.example.com/team.ics",
        };

        await repository.UpsertAsync(calendar);

        var accountCalendars = await repository.GetByAccountAsync("acc-1");
        var enabled = await repository.GetEnabledAsync();

        Assert.Single(accountCalendars);
        Assert.Single(enabled);
        Assert.Equal(calendar.DisplayName, accountCalendars[0].DisplayName);
        Assert.Equal(calendar.CalDavUrl, accountCalendars[0].CalDavUrl);
        Assert.Equal(calendar.Color, accountCalendars[0].Color);
        Assert.Equal(calendar.LocalColor, accountCalendars[0].LocalColor);
        Assert.Equal(calendar.LocalColor, accountCalendars[0].EffectiveColor);
        Assert.Equal(calendar.CTag, accountCalendars[0].CTag);
        Assert.Equal(calendar.SyncToken, accountCalendars[0].SyncToken);
        Assert.Equal(calendar.IcsUrl, accountCalendars[0].IcsUrl);
        Assert.True(accountCalendars[0].IsReadOnly);
        Assert.True(accountCalendars[0].IsEnabled);

        calendar.IsEnabled = false;
        calendar.LocalColor = null;
        await repository.UpsertAsync(calendar);

        accountCalendars = await repository.GetByAccountAsync("acc-1");
        enabled = await repository.GetEnabledAsync();

        Assert.Single(accountCalendars);
        Assert.Empty(enabled);
        Assert.Null(accountCalendars[0].LocalColor);
        Assert.Equal(calendar.Color, accountCalendars[0].EffectiveColor);
    }

    [Fact]
    public async Task CalendarRepository_ReadsLegacyRows_WithoutIcsUrlColumn()
    {
        CreateLegacyDatabase(_databasePath);

        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWrite,
        }.ToString());
        connection.Open();

        using (var insert = connection.CreateCommand())
        {
            insert.CommandText = "INSERT INTO Accounts (AccountId, ProviderType, DisplayName, ServerBaseUrl, Username) VALUES ('acc-legacy', 0, 'Legacy', 'https://calendar.example.com', 'michael');";
            insert.ExecuteNonQuery();
        }

        using (var insert = connection.CreateCommand())
        {
            insert.CommandText = "INSERT INTO Calendars (CalendarId, AccountId, DisplayName, CalDavUrl, Color, IsReadOnly, IsEnabled, CTag) VALUES ('cal-legacy', 'acc-legacy', 'Legacy Team', 'https://calendar.example.com/team', '#112233', 1, 1, 'ctag-legacy');";
            insert.ExecuteNonQuery();
        }

        using var select = connection.CreateCommand();
        select.CommandText = "SELECT * FROM Calendars WHERE CalendarId = 'cal-legacy'";
        using var reader = await select.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        var readCalendar = typeof(CalendarRepository).GetMethod("ReadCalendar", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(readCalendar);

        var calendar = Assert.IsType<Calendar>(readCalendar!.Invoke(null, [reader]));
        Assert.Equal("cal-legacy", calendar.CalendarId);
        Assert.Null(calendar.IcsUrl);
        Assert.Equal("ctag-legacy", calendar.CTag);
    }

    [Fact]
    public async Task EventRepository_SupportsRangeQueriesAndDeleteOperations()
    {
        using var db = new KavaDatabase(_databasePath);
        await SeedAccountAsync(db, "acc-1");
        await SeedCalendarAsync(db, "acc-1", "cal-1");
        await SeedCalendarAsync(db, "acc-1", "cal-2");

        var repository = new EventRepository(db);
        var start = new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero);
        var eventOne = new CalendarEvent
        {
            EventId = "evt-1",
            CalendarId = "cal-1",
            RemoteUid = "uid-1",
            RemoteResourceUrl = "https://calendar.example.com/events/1.ics",
            ETag = "etag-1",
            Title = "Standup",
            Description = "Daily sync",
            Location = "Room 1",
            Start = start,
            End = start.AddHours(1),
            TimeZoneId = "UTC",
            IsAllDay = false,
            RecurrenceRule = "FREQ=DAILY",
            MeetingUrl = "https://meet.example.com/standup",
            RawICalendarPayload = "BEGIN:VCALENDAR",
            LastSeenUtc = new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc),
        };
        var eventTwo = new CalendarEvent
        {
            EventId = "evt-2",
            CalendarId = "cal-2",
            RemoteUid = "uid-2",
            RemoteResourceUrl = "https://calendar.example.com/events/2.ics",
            Title = "Review",
            Start = start.AddDays(1),
            End = start.AddDays(1).AddHours(1),
            IsAllDay = false,
        };

        await repository.UpsertAsync(eventOne);
        await repository.UpsertAsync(eventTwo);

        var ranged = await repository.GetByDateRangeAsync(["cal-1", "cal-2"], start.AddMinutes(-30), start.AddDays(2));
        Assert.Equal(2, ranged.Count);
        Assert.Equal(["evt-1", "evt-2"], ranged.Select(evt => evt.EventId));

        var noCalendars = await repository.GetByDateRangeAsync([], start, start.AddDays(2));
        Assert.Empty(noCalendars);

        eventOne.Title = "Standup Updated";
        eventOne.LastSeenUtc = eventOne.LastSeenUtc!.Value.AddMinutes(10);
        await repository.UpsertAsync(eventOne);

        ranged = await repository.GetByDateRangeAsync(["cal-1"], start.AddMinutes(-30), start.AddHours(2));
        Assert.Single(ranged);
        Assert.Equal("Standup Updated", ranged[0].Title);
        Assert.Equal(eventOne.RemoteResourceUrl, ranged[0].RemoteResourceUrl);
        Assert.Equal(eventOne.ETag, ranged[0].ETag);
        Assert.Equal(eventOne.Description, ranged[0].Description);
        Assert.Equal(eventOne.Location, ranged[0].Location);
        Assert.Equal(eventOne.TimeZoneId, ranged[0].TimeZoneId);
        Assert.Equal(eventOne.RecurrenceRule, ranged[0].RecurrenceRule);
        Assert.Equal(eventOne.MeetingUrl, ranged[0].MeetingUrl);
        Assert.Equal(eventOne.RawICalendarPayload, ranged[0].RawICalendarPayload);
        Assert.Equal(eventOne.LastSeenUtc, ranged[0].LastSeenUtc);

        await repository.DeleteByRemoteUidsAsync("cal-1", ["uid-1"]);
        ranged = await repository.GetByDateRangeAsync(["cal-1", "cal-2"], start.AddMinutes(-30), start.AddDays(2));
        Assert.Single(ranged);
        Assert.Equal("evt-2", ranged[0].EventId);

        await repository.UpsertAsync(eventOne);
        await repository.DeleteByRemoteUrlsAsync("cal-1", [eventOne.RemoteResourceUrl!]);
        ranged = await repository.GetByDateRangeAsync(["cal-1", "cal-2"], start.AddMinutes(-30), start.AddDays(2));
        Assert.Single(ranged);
        Assert.Equal("evt-2", ranged[0].EventId);

        await repository.UpsertAsync(eventOne);
        await repository.DeleteByCalendarAsync("cal-2");
        ranged = await repository.GetByDateRangeAsync(["cal-1", "cal-2"], start.AddMinutes(-30), start.AddDays(2));
        Assert.Single(ranged);
        Assert.Equal("evt-1", ranged[0].EventId);

        await repository.DeleteByRemoteUidsAsync("cal-1", []);
        await repository.DeleteByRemoteUrlsAsync("cal-1", []);
    }

    [Fact]
    public void KavaDatabase_RunsCalendarMigrationsAndDeduplicatesLegacyEvents()
    {
        CreateLegacyDatabase(_databasePath);

        using var db = new KavaDatabase(_databasePath);
        using var command = db.Connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(Calendars)";
        using var reader = command.ExecuteReader();

        var columns = new List<string>();
        while (reader.Read())
            columns.Add(reader.GetString(1));

        Assert.Contains("SyncToken", columns);
        Assert.Contains("LocalColor", columns);
        Assert.Contains("IcsUrl", columns);

        reader.Close();

        command.CommandText = "SELECT EventId, LastSeenUtc FROM Events ORDER BY EventId";
        using var eventsReader = command.ExecuteReader();
        Assert.True(eventsReader.Read());
        Assert.Equal("evt-new", eventsReader.GetString(0));
        Assert.Equal("2026-04-01T09:05:00.0000000Z", eventsReader.GetString(1));
        Assert.False(eventsReader.Read());

        eventsReader.Close();

        command.CommandText = "PRAGMA index_list(Events)";
        using var indexes = command.ExecuteReader();
        var indexNames = new List<string>();
        while (indexes.Read())
            indexNames.Add(indexes.GetString(1));

        Assert.Contains("IX_Events_CalendarId_RemoteUid", indexNames);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_databasePath))
                File.Delete(_databasePath);
        }
        catch (IOException)
        {
        }
    }

    private static async Task SeedAccountAsync(KavaDatabase db, string accountId)
    {
        var repository = new AccountRepository(db);
        await repository.UpsertAsync(new Account
        {
            AccountId = accountId,
            ProviderType = ProviderType.CalDav,
            DisplayName = "Account",
            ServerBaseUrl = "https://calendar.example.com",
            Username = "michael",
            CredentialReference = "cred",
        });
    }

    private static async Task SeedCalendarAsync(KavaDatabase db, string accountId, string calendarId)
    {
        var repository = new CalendarRepository(db);
        await repository.UpsertAsync(new Calendar
        {
            CalendarId = calendarId,
            AccountId = accountId,
            DisplayName = calendarId,
            CalDavUrl = $"https://calendar.example.com/{calendarId}",
            Color = "#0078D4",
            IsEnabled = true,
        });
    }

    private static void CreateLegacyDatabase(string databasePath)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString());
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE Accounts (
                AccountId TEXT PRIMARY KEY,
                ProviderType INTEGER NOT NULL,
                DisplayName TEXT NOT NULL,
                ServerBaseUrl TEXT NOT NULL,
                Username TEXT NOT NULL,
                CredentialReference TEXT NOT NULL DEFAULT '',
                LastSyncUtc TEXT,
                SyncToken TEXT,
                IsEnabled INTEGER NOT NULL DEFAULT 1,
                SupportsCalendars INTEGER NOT NULL DEFAULT 1,
                SupportsContacts INTEGER NOT NULL DEFAULT 1
            );

            CREATE TABLE Calendars (
                CalendarId TEXT PRIMARY KEY,
                AccountId TEXT NOT NULL,
                DisplayName TEXT NOT NULL,
                CalDavUrl TEXT NOT NULL,
                Color TEXT NOT NULL DEFAULT '#0078D4',
                IsReadOnly INTEGER NOT NULL DEFAULT 0,
                IsEnabled INTEGER NOT NULL DEFAULT 1,
                CTag TEXT,
                FOREIGN KEY (AccountId) REFERENCES Accounts(AccountId) ON DELETE CASCADE
            );

            CREATE TABLE Events (
                EventId TEXT PRIMARY KEY,
                CalendarId TEXT NOT NULL,
                RemoteUid TEXT NOT NULL,
                RemoteResourceUrl TEXT,
                ETag TEXT,
                Title TEXT NOT NULL,
                Description TEXT,
                Location TEXT,
                StartUtc TEXT NOT NULL,
                EndUtc TEXT NOT NULL,
                TimeZoneId TEXT,
                IsAllDay INTEGER NOT NULL DEFAULT 0,
                RecurrenceRule TEXT,
                MeetingUrl TEXT,
                RawICalendarPayload TEXT,
                LastSeenUtc TEXT,
                FOREIGN KEY (CalendarId) REFERENCES Calendars(CalendarId) ON DELETE CASCADE
            );
            """;
        command.ExecuteNonQuery();

        command.CommandText = """
            INSERT INTO Accounts(AccountId, ProviderType, DisplayName, ServerBaseUrl, Username, CredentialReference, IsEnabled, SupportsCalendars, SupportsContacts)
            VALUES ('acc-1', 0, 'Legacy', 'https://calendar.example.com', 'michael', 'cred', 1, 1, 1);

            INSERT INTO Calendars(CalendarId, AccountId, DisplayName, CalDavUrl, Color, IsReadOnly, IsEnabled, CTag)
            VALUES ('cal-1', 'acc-1', 'Legacy Calendar', 'https://calendar.example.com/cal-1', '#0078D4', 0, 1, 'ctag');

            INSERT INTO Events(EventId, CalendarId, RemoteUid, Title, StartUtc, EndUtc, IsAllDay, LastSeenUtc)
            VALUES
                ('evt-old', 'cal-1', 'uid-1', 'Old copy', '2026-04-01T09:00:00.0000000Z', '2026-04-01T10:00:00.0000000Z', 0, '2026-04-01T09:00:00.0000000Z'),
                ('evt-new', 'cal-1', 'uid-1', 'New copy', '2026-04-01T09:00:00.0000000Z', '2026-04-01T10:00:00.0000000Z', 0, '2026-04-01T09:05:00.0000000Z');
            """;
        command.ExecuteNonQuery();
    }
}