namespace Kava.Core.Models;

public class AddressBook
{
    public string AddressBookId { get; set; } = Guid.NewGuid().ToString();
    public string AccountId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string CardDavUrl { get; set; } = string.Empty;
    public bool IsReadOnly { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? CTag { get; set; }
}
