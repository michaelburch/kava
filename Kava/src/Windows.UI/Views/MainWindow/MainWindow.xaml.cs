using System;

namespace Core.Domain.Models
{
    public class Account
    {
        public Guid AccountId { get; set; }
        public string ProviderType { get; set; }
        public string DisplayName { get; set; }
        public string ServerBaseUrl { get; set; }
        public string Username { get; set; }
        public string CredentialReference { get; set; }
        public DateTime LastSyncUtc { get; set; }
        public string SyncToken { get; set; }
        public bool IsEnabled { get; set; }
        public bool SupportsCalendars { get; set; }
        public bool SupportsContacts { get; set; }
    }
}