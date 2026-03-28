namespace Kava.Core.Models;

public class Contact
{
    public string ContactId { get; set; } = Guid.NewGuid().ToString();
    public string AddressBookId { get; set; } = string.Empty;
    public string RemoteUid { get; set; } = string.Empty;
    public string? RemoteResourceUrl { get; set; }
    public string? ETag { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Organization { get; set; }
    public List<string> Emails { get; set; } = [];
    public List<string> PhoneNumbers { get; set; } = [];
    public List<string> Addresses { get; set; } = [];
    public string? PhotoUri { get; set; }
    public string? Notes { get; set; }
    public string? RawVCardPayload { get; set; }
    public DateTime? LastSeenUtc { get; set; }
}
