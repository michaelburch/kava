namespace Kava.Core.Interfaces;

public interface ICredentialStore
{
    Task SaveCredentialAsync(string accountId, string credential);
    Task<string?> GetCredentialAsync(string accountId);
    Task DeleteCredentialAsync(string accountId);
}
