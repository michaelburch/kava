using Kava.Core.Models;
using Xunit;

namespace Kava.Core.Tests;

public class PlaceholderTests
{
    [Fact]
    public void Account_Defaults_MatchExpectedState()
    {
        var account = new Account();

        Assert.NotEqual(Guid.Empty, Guid.Parse(account.AccountId));
        Assert.Equal(string.Empty, account.DisplayName);
        Assert.Equal(string.Empty, account.ServerBaseUrl);
        Assert.Equal(string.Empty, account.Username);
        Assert.Equal(string.Empty, account.CredentialReference);
        Assert.Null(account.LastSyncUtc);
        Assert.Null(account.SyncToken);
        Assert.True(account.IsEnabled);
        Assert.True(account.SupportsCalendars);
        Assert.True(account.SupportsContacts);
    }

    [Fact]
    public void Calendar_EffectiveColor_UsesLocalOverrideWhenPresent()
    {
        var calendar = new Calendar
        {
            Color = "#112233",
            LocalColor = "#445566",
        };

        Assert.Equal("#445566", calendar.EffectiveColor);
    }

    [Fact]
    public void Calendar_Defaults_MatchExpectedState()
    {
        var calendar = new Calendar();

        Assert.NotEqual(Guid.Empty, Guid.Parse(calendar.CalendarId));
        Assert.Equal(string.Empty, calendar.AccountId);
        Assert.Equal(string.Empty, calendar.DisplayName);
        Assert.Equal(string.Empty, calendar.CalDavUrl);
        Assert.Equal("#0078D4", calendar.Color);
        Assert.Equal(calendar.Color, calendar.EffectiveColor);
        Assert.Null(calendar.LocalColor);
        Assert.False(calendar.IsReadOnly);
        Assert.True(calendar.IsEnabled);
        Assert.Null(calendar.CTag);
        Assert.Null(calendar.SyncToken);
        Assert.Null(calendar.IcsUrl);
    }

    [Fact]
    public void CalendarEvent_Defaults_MatchExpectedState()
    {
        var calendarEvent = new CalendarEvent();

        Assert.NotEqual(Guid.Empty, Guid.Parse(calendarEvent.EventId));
        Assert.Equal(string.Empty, calendarEvent.CalendarId);
        Assert.Equal(string.Empty, calendarEvent.RemoteUid);
        Assert.Null(calendarEvent.RemoteResourceUrl);
        Assert.Null(calendarEvent.ETag);
        Assert.Equal(string.Empty, calendarEvent.Title);
        Assert.Null(calendarEvent.Description);
        Assert.Null(calendarEvent.Location);
        Assert.Equal(default, calendarEvent.Start);
        Assert.Equal(default, calendarEvent.End);
        Assert.Null(calendarEvent.TimeZoneId);
        Assert.False(calendarEvent.IsAllDay);
        Assert.Null(calendarEvent.RecurrenceRule);
        Assert.Null(calendarEvent.MeetingUrl);
        Assert.Null(calendarEvent.RawICalendarPayload);
        Assert.Null(calendarEvent.LastSeenUtc);
    }

    [Fact]
    public void Contact_Defaults_MatchExpectedState()
    {
        var contact = new Contact();

        Assert.NotEqual(Guid.Empty, Guid.Parse(contact.ContactId));
        Assert.Equal(string.Empty, contact.AddressBookId);
        Assert.Equal(string.Empty, contact.RemoteUid);
        Assert.Null(contact.RemoteResourceUrl);
        Assert.Null(contact.ETag);
        Assert.Equal(string.Empty, contact.FullName);
        Assert.Null(contact.FirstName);
        Assert.Null(contact.LastName);
        Assert.Null(contact.Organization);
        Assert.Empty(contact.Emails);
        Assert.Empty(contact.PhoneNumbers);
        Assert.Empty(contact.Addresses);
        Assert.Null(contact.PhotoUri);
        Assert.Null(contact.Notes);
        Assert.Null(contact.RawVCardPayload);
        Assert.Null(contact.LastSeenUtc);
    }

    [Fact]
    public void AddressBook_Defaults_MatchExpectedState()
    {
        var addressBook = new AddressBook();

        Assert.NotEqual(Guid.Empty, Guid.Parse(addressBook.AddressBookId));
        Assert.Equal(string.Empty, addressBook.AccountId);
        Assert.Equal(string.Empty, addressBook.DisplayName);
        Assert.Equal(string.Empty, addressBook.CardDavUrl);
        Assert.False(addressBook.IsReadOnly);
        Assert.True(addressBook.IsEnabled);
        Assert.Null(addressBook.CTag);
    }

    [Fact]
    public void ProviderType_ExposesSupportedValues()
    {
        var values = Enum.GetValues<ProviderType>();

        Assert.Equal([ProviderType.CalDav, ProviderType.IcsSubscription], values);
    }
}
