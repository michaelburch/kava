using Microsoft.Data.Sqlite;

namespace Kava.Persistence;

public class KavaDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public KavaDatabase(string databasePath)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        InitializeSchema();
    }

    public SqliteConnection Connection => _connection;

    private void InitializeSchema()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Accounts (
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

            CREATE TABLE IF NOT EXISTS Calendars (
                CalendarId TEXT PRIMARY KEY,
                AccountId TEXT NOT NULL,
                DisplayName TEXT NOT NULL,
                CalDavUrl TEXT NOT NULL,
                Color TEXT NOT NULL DEFAULT '#0078D4',
                IsReadOnly INTEGER NOT NULL DEFAULT 0,
                IsEnabled INTEGER NOT NULL DEFAULT 1,
                CTag TEXT,
                SyncToken TEXT,
                FOREIGN KEY (AccountId) REFERENCES Accounts(AccountId) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Events (
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

            CREATE INDEX IF NOT EXISTS IX_Events_CalendarId_Start ON Events(CalendarId, StartUtc);
            CREATE INDEX IF NOT EXISTS IX_Events_RemoteUid ON Events(RemoteUid);

            CREATE TABLE IF NOT EXISTS AddressBooks (
                AddressBookId TEXT PRIMARY KEY,
                AccountId TEXT NOT NULL,
                DisplayName TEXT NOT NULL,
                CardDavUrl TEXT NOT NULL,
                IsReadOnly INTEGER NOT NULL DEFAULT 0,
                IsEnabled INTEGER NOT NULL DEFAULT 1,
                CTag TEXT,
                FOREIGN KEY (AccountId) REFERENCES Accounts(AccountId) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Contacts (
                ContactId TEXT PRIMARY KEY,
                AddressBookId TEXT NOT NULL,
                RemoteUid TEXT NOT NULL,
                RemoteResourceUrl TEXT,
                ETag TEXT,
                FullName TEXT NOT NULL,
                FirstName TEXT,
                LastName TEXT,
                Organization TEXT,
                Emails TEXT,
                PhoneNumbers TEXT,
                Addresses TEXT,
                PhotoUri TEXT,
                Notes TEXT,
                RawVCardPayload TEXT,
                LastSeenUtc TEXT,
                FOREIGN KEY (AddressBookId) REFERENCES AddressBooks(AddressBookId) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS IX_Contacts_AddressBookId ON Contacts(AddressBookId);
            """;
        cmd.ExecuteNonQuery();

        RunMigrations();
    }

    private void RunMigrations()
    {
        // Add SyncToken column to Calendars if it doesn't exist yet
        AddColumnIfMissing("Calendars", "SyncToken", "TEXT");

        // Remove duplicate events from before the unique index fix
        DeduplicateEvents();

        // Add LocalColor column to Calendars for user color overrides
        AddColumnIfMissing("Calendars", "LocalColor", "TEXT");

        // Add IcsUrl column for ICS subscription calendars
        AddColumnIfMissing("Calendars", "IcsUrl", "TEXT");

        // Create unique index after dedup so it doesn't fail on corrupt data
        using var idx = _connection.CreateCommand();
        idx.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS IX_Events_CalendarId_RemoteUid ON Events(CalendarId, RemoteUid)";
        idx.ExecuteNonQuery();
    }

    private void DeduplicateEvents()
    {
        using var cmd = _connection.CreateCommand();
        // Keep the most recently seen row per (CalendarId, RemoteUid), delete the rest
        cmd.CommandText = """
            DELETE FROM Events WHERE EventId NOT IN (
                SELECT EventId FROM (
                    SELECT EventId, ROW_NUMBER() OVER (
                        PARTITION BY CalendarId, RemoteUid
                        ORDER BY LastSeenUtc DESC, EventId
                    ) AS rn FROM Events
                ) WHERE rn = 1
            )
            """;
        cmd.ExecuteNonQuery();
    }

    private void AddColumnIfMissing(string table, string column, string type)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table})";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return;
        }
        reader.Close();

        using var alter = _connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {type}";
        alter.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
