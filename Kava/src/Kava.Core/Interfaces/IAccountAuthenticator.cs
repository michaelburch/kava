using Kava.Core.Models;

namespace Kava.Core.Interfaces;

public interface IAccountAuthenticator
{
    Task<bool> ValidateAsync(Account account, string credential, CancellationToken cancellationToken = default);
}
