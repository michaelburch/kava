using Kava.Core.Models;

namespace Kava.Core.Interfaces;

public interface IContactProvider
{
    Task<IReadOnlyList<AddressBook>> DiscoverAddressBooksAsync(Account account, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Contact>> GetContactsAsync(AddressBook addressBook, CancellationToken cancellationToken = default);

    Task<SyncResult<Contact>> SyncContactsAsync(
        AddressBook addressBook,
        string? syncToken,
        CancellationToken cancellationToken = default);

    Task CreateContactAsync(AddressBook addressBook, Contact contact, CancellationToken cancellationToken = default);
    Task UpdateContactAsync(AddressBook addressBook, Contact contact, CancellationToken cancellationToken = default);
    Task DeleteContactAsync(AddressBook addressBook, string remoteUid, CancellationToken cancellationToken = default);
}
