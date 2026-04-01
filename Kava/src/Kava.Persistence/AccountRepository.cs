using System.Globalization;
using Kava.Core.Models;
using Microsoft.Data.Sqlite;

namespace Kava.Persistence;

public class AccountRepository
{
    private readonly KavaDatabase _db;

    public AccountRepository(KavaDatabase db)
    {
        _db = db;
    }

    public async Task<List<Account>> GetAllAsync()
    {
        var accounts = new List<Account>();
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Accounts";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            accounts.Add(ReadAccount(reader));
        }
        return accounts;
    }

    public async Task<Account?> GetByIdAsync(string accountId)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM Accounts WHERE AccountId = @id";
        cmd.Parameters.AddWithValue("@id", accountId);

        using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadAccount(reader) : null;
    }

    public async Task UpsertAsync(Account account)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Accounts (AccountId, ProviderType, DisplayName, ServerBaseUrl, Username, CredentialReference, LastSyncUtc, SyncToken, IsEnabled, SupportsCalendars, SupportsContacts)
            VALUES (@id, @type, @name, @url, @user, @cred, @sync, @token, @enabled, @cal, @contacts)
            ON CONFLICT(AccountId) DO UPDATE SET
                ProviderType = @type, DisplayName = @name, ServerBaseUrl = @url, Username = @user,
                CredentialReference = @cred, LastSyncUtc = @sync, SyncToken = @token,
                IsEnabled = @enabled, SupportsCalendars = @cal, SupportsContacts = @contacts
            """;
        cmd.Parameters.AddWithValue("@id", account.AccountId);
        cmd.Parameters.AddWithValue("@type", (int)account.ProviderType);
        cmd.Parameters.AddWithValue("@name", account.DisplayName);
        cmd.Parameters.AddWithValue("@url", account.ServerBaseUrl);
        cmd.Parameters.AddWithValue("@user", account.Username);
        cmd.Parameters.AddWithValue("@cred", account.CredentialReference);
        cmd.Parameters.AddWithValue("@sync", (object?)account.LastSyncUtc?.ToString("O") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@token", (object?)account.SyncToken ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@enabled", account.IsEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@cal", account.SupportsCalendars ? 1 : 0);
        cmd.Parameters.AddWithValue("@contacts", account.SupportsContacts ? 1 : 0);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(string accountId)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Accounts WHERE AccountId = @id";
        cmd.Parameters.AddWithValue("@id", accountId);
        await cmd.ExecuteNonQueryAsync();
    }

    private static Account ReadAccount(SqliteDataReader reader) => new()
    {
        AccountId = reader.GetString(reader.GetOrdinal("AccountId")),
        ProviderType = (ProviderType)reader.GetInt32(reader.GetOrdinal("ProviderType")),
        DisplayName = reader.GetString(reader.GetOrdinal("DisplayName")),
        ServerBaseUrl = reader.GetString(reader.GetOrdinal("ServerBaseUrl")),
        Username = reader.GetString(reader.GetOrdinal("Username")),
        CredentialReference = reader.GetString(reader.GetOrdinal("CredentialReference")),
        LastSyncUtc = reader.IsDBNull(reader.GetOrdinal("LastSyncUtc"))
            ? null
            : DateTime.Parse(reader.GetString(reader.GetOrdinal("LastSyncUtc")), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        SyncToken = reader.IsDBNull(reader.GetOrdinal("SyncToken")) ? null : reader.GetString(reader.GetOrdinal("SyncToken")),
        IsEnabled = reader.GetInt32(reader.GetOrdinal("IsEnabled")) == 1,
        SupportsCalendars = reader.GetInt32(reader.GetOrdinal("SupportsCalendars")) == 1,
        SupportsContacts = reader.GetInt32(reader.GetOrdinal("SupportsContacts")) == 1,
    };
}
