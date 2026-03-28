namespace Kava.Core.Models;

public class Account
{
    public string AccountId { get; set; } = Guid.NewGuid().ToString();
    public ProviderType ProviderType { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string ServerBaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string CredentialReference { get; set; } = string.Empty;
    public DateTime? LastSyncUtc { get; set; }
    public string? SyncToken { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool SupportsCalendars { get; set; } = true;
    public bool SupportsContacts { get; set; } = true;
}
