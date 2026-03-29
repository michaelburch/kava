using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Kava.Core.Interfaces;

namespace Kava.Desktop;

/// <summary>
/// Stores credentials encrypted with DPAPI (Windows user-scoped).
/// Files are stored under the app data directory.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FileCredentialStore : ICredentialStore
{
    private readonly string _storePath;

    public FileCredentialStore(string appDataPath)
    {
        _storePath = Path.Combine(appDataPath, "credentials");
        Directory.CreateDirectory(_storePath);
    }

    public Task SaveCredentialAsync(string accountId, string credential)
    {
        var encrypted = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(credential),
            null,
            DataProtectionScope.CurrentUser);

        var filePath = GetFilePath(accountId);
        File.WriteAllBytes(filePath, encrypted);
        return Task.CompletedTask;
    }

    public Task<string?> GetCredentialAsync(string accountId)
    {
        var filePath = GetFilePath(accountId);
        if (!File.Exists(filePath))
            return Task.FromResult<string?>(null);

        var encrypted = File.ReadAllBytes(filePath);
        var decrypted = ProtectedData.Unprotect(
            encrypted,
            null,
            DataProtectionScope.CurrentUser);

        return Task.FromResult<string?>(Encoding.UTF8.GetString(decrypted));
    }

    public Task DeleteCredentialAsync(string accountId)
    {
        var filePath = GetFilePath(accountId);
        if (File.Exists(filePath))
            File.Delete(filePath);
        return Task.CompletedTask;
    }

    private string GetFilePath(string accountId)
    {
        // Use a hash of the account ID as filename to avoid path issues
        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(accountId)))[..16];
        return Path.Combine(_storePath, hash);
    }
}
