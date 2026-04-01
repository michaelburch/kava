using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia;
using Kava.Core.Interfaces;
using Kava.Core.Models;
using Kava.Persistence;
using System.Runtime.Versioning;
using Xunit;

using CalendarModel = Kava.Core.Models.Calendar;

namespace Kava.Desktop.Tests;

public sealed class DesktopLogicTests
{
    private const string SampleIcs = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nBEGIN:VEVENT\r\nUID:event-1\r\nSUMMARY:Standup\r\nDESCRIPTION:Daily sync\r\nLOCATION:https://meet.example.com/standup\r\nDTSTART:20260401T090000Z\r\nDTEND:20260401T093000Z\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";

    [Fact]
    public void AccountItem_AndCalendarInfo_HaveExpectedDefaults()
    {
        var item = new AccountItem();
        var calendar = new CalendarInfo();

        Assert.Equal(string.Empty, item.AccountId);
        Assert.Equal("Synced", item.Status);
        Assert.Empty(item.Calendars);

        Assert.Equal(string.Empty, calendar.CalendarId);
        Assert.Equal("#0078D4", calendar.Color);
        Assert.True(calendar.Enabled);
    }

    [Fact]
    public void EventItem_HasExpectedDefaults()
    {
        var item = new EventItem();

        Assert.Equal(string.Empty, item.Title);
        Assert.Equal(string.Empty, item.TimeRange);
        Assert.Equal(string.Empty, item.CalendarId);
        Assert.Equal("#0078D4", item.CalendarColor);
        Assert.False(item.IsAllDay);
        Assert.Null(item.Subtitle);
        Assert.Null(item.MeetingUrl);
    }

    [Fact]
    public void TryNormalizeCalDavServerUrl_AcceptsHttpsAndTrimsTrailingSlash()
    {
        var ok = DesktopInputNormalizer.TryNormalizeCalDavServerUrl(
            "https://calendar.example.com/root/",
            out var normalizedUrl,
            out var error);

        Assert.True(ok);
        Assert.Equal("https://calendar.example.com/root", normalizedUrl);
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void TryNormalizeCalDavServerUrl_RejectsNonHttps()
    {
        var ok = DesktopInputNormalizer.TryNormalizeCalDavServerUrl(
            "http://calendar.example.com/root",
            out var normalizedUrl,
            out var error);

        Assert.False(ok);
        Assert.Equal(string.Empty, normalizedUrl);
        Assert.Equal("Please enter a valid HTTPS server URL.", error);
    }

    [Fact]
    public void TryNormalizeSubscriptionUrl_NormalizesWebcal()
    {
        var ok = DesktopInputNormalizer.TryNormalizeSubscriptionUrl(
            "webcal://calendar.example.com/feed.ics",
            out var normalizedUrl,
            out var error);

        Assert.True(ok);
        Assert.Equal("https://calendar.example.com/feed.ics", normalizedUrl);
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void TryNormalizeSubscriptionUrl_RejectsInvalidScheme()
    {
        var ok = DesktopInputNormalizer.TryNormalizeSubscriptionUrl(
            "ftp://calendar.example.com/feed.ics",
            out var normalizedUrl,
            out var error);

        Assert.False(ok);
        Assert.Equal(string.Empty, normalizedUrl);
        Assert.Equal("Please enter a valid URL (https:// or webcal://).", error);
    }

    [Fact]
    public void FormatTimeRange_UsesAllDayLabelForAllDayEvents()
    {
        var evt = new CalendarEvent
        {
            IsAllDay = true,
            Start = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 4, 1, 23, 59, 0, TimeSpan.Zero),
        };

        Assert.Equal("All day", DesktopEventMapper.FormatTimeRange(evt));
    }

    [Fact]
    public void MapToEventItem_MapsDesktopFields()
    {
        var evt = new CalendarEvent
        {
            Title = "Standup",
            Location = "Room 1",
            CalendarId = "cal-1",
            IsAllDay = false,
            MeetingUrl = "https://meet.example.com/standup",
            Start = new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero),
        };

        var item = DesktopEventMapper.MapToEventItem(evt, "#112233");

        Assert.Equal("Standup", item.Title);
        Assert.Equal("Room 1", item.Subtitle);
        Assert.Equal("cal-1", item.CalendarId);
        Assert.Equal("#112233", item.CalendarColor);
        Assert.Equal("https://meet.example.com/standup", item.MeetingUrl);
        Assert.False(item.IsAllDay);
        Assert.Equal(DesktopEventMapper.FormatTimeRange(evt), item.TimeRange);
    }

    [Fact]
    public void BuildEventLookup_GroupsByLocalDateAndAppliesFallbackColor()
    {
        var events = new[]
        {
            new CalendarEvent
            {
                Title = "One",
                CalendarId = "cal-1",
                Start = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero),
                End = new DateTimeOffset(2026, 4, 1, 13, 0, 0, TimeSpan.Zero),
            },
            new CalendarEvent
            {
                Title = "Two",
                CalendarId = "cal-2",
                IsAllDay = true,
                Start = new DateTimeOffset(2026, 4, 2, 12, 0, 0, TimeSpan.Zero),
                End = new DateTimeOffset(2026, 4, 2, 12, 0, 0, TimeSpan.Zero),
            },
        };

        var lookup = DesktopEventMapper.BuildEventLookup(events, new Dictionary<string, string>
        {
            ["cal-1"] = "#abcdef",
        });

        Assert.Equal(2, lookup.Count);
        Assert.Equal("#abcdef", Assert.Single(lookup[new DateOnly(2026, 4, 1)]).CalendarColor);
        Assert.Equal("#0078D4", Assert.Single(lookup[new DateOnly(2026, 4, 2)]).CalendarColor);
    }

    [Fact]
    public void CreateDayButton_AppliesSharedShellProperties()
    {
        var selectedBackground = Brushes.Blue;
        var content = new TextBlock { Text = "1" };

        var button = DesktopUiFactory.CreateDayButton(
            new DateOnly(2026, 4, 1),
            isSelected: true,
            content,
            size: 30,
            cornerRadius: 15,
            selectedBackground);

        Assert.Equal(30, button.Width);
        Assert.Equal(30, button.Height);
        Assert.Equal(selectedBackground, button.Background);
        Assert.Equal(Brushes.Transparent, button.BorderBrush);
        Assert.Equal(content, button.Content);
        Assert.Equal(new DateOnly(2026, 4, 1), button.Tag);
    }

    [Fact]
    public void GetDayNumberForeground_UsesThemeResources_AndSelectedOverride()
    {
        EnsureTestApplication();

        Assert.Equal(Brushes.White, DesktopUiFactory.GetDayNumberForeground(isSelected: true, isToday: false));
        Assert.Equal(ThemeHelper.Brush("KavaAccent"), DesktopUiFactory.GetDayNumberForeground(isSelected: false, isToday: true));
        Assert.Equal(ThemeHelper.Brush("KavaTextSecondary"), DesktopUiFactory.GetDayNumberForeground(isSelected: false, isToday: false));
    }

    [Fact]
    public void CreateDayCellContent_AddsEventIndicator_WhenRequested()
    {
        EnsureTestApplication();

        var content = DesktopUiFactory.CreateDayCellContent(7, isSelected: false, isToday: true, hasEvents: true, fontSize: 13);

        Assert.Equal(2, content.Children.Count);
        Assert.IsType<TextBlock>(content.Children[0]);
        var dot = Assert.IsType<Avalonia.Controls.Shapes.Ellipse>(content.Children[1]);
        Assert.Equal(ThemeHelper.Brush("KavaAccent"), dot.Fill);
    }

    [Fact]
    public void CreateEventCard_BuildsTimedLayout_WithJoinButton()
    {
        EnsureTestApplication();

        var evt = new EventItem
        {
            Title = "Standup",
            TimeRange = "9:00 AM - 9:30 AM",
            Subtitle = "Room 1",
            CalendarColor = "#112233",
            MeetingUrl = "https://meet.example.com/standup",
        };

        var card = DesktopUiFactory.CreateEventCard(evt, isAllDay: false, DesktopUiFactory.MainEventCardStyle);

        var grid = Assert.IsType<Grid>(card.Child);
        Assert.Equal(4, grid.ColumnDefinitions.Count);
        Assert.Equal(3, grid.Children.Count);

        var textStack = Assert.IsType<StackPanel>(grid.Children[1]);
        Assert.Equal(3, textStack.Children.Count);
        Assert.Equal("Standup", Assert.IsType<TextBlock>(textStack.Children[0]).Text);
        Assert.Equal("9:00 AM - 9:30 AM", Assert.IsType<TextBlock>(textStack.Children[1]).Text);
        Assert.Equal("Room 1", Assert.IsType<TextBlock>(textStack.Children[2]).Text);

        var joinButton = Assert.IsType<Button>(grid.Children[2]);
        var joinLabel = Assert.IsType<TextBlock>(joinButton.Content);
        Assert.Equal("Join", joinLabel.Text);
    }

    [Fact]
    public void CreateEventCard_BuildsAllDayLayout_WithoutJoinButton()
    {
        EnsureTestApplication();

        var evt = new EventItem
        {
            Title = "Holiday",
            CalendarColor = "#112233",
        };

        var card = DesktopUiFactory.CreateEventCard(evt, isAllDay: true, DesktopUiFactory.FlyoutEventCardStyle);

        var grid = Assert.IsType<Grid>(card.Child);
        Assert.Equal(3, grid.ColumnDefinitions.Count);
        Assert.Equal(2, grid.Children.Count);

        var textStack = Assert.IsType<StackPanel>(grid.Children[1]);
        Assert.Equal(2, textStack.Children.Count);
        Assert.Equal("Holiday", Assert.IsType<TextBlock>(textStack.Children[0]).Text);
        Assert.Equal("All day", Assert.IsType<TextBlock>(textStack.Children[1]).Text);
    }

    [Fact]
    public void OpenMeetingUrl_IgnoresInvalidUri()
    {
        var method = typeof(DesktopUiFactory).GetMethod("OpenMeetingUrl", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        method!.Invoke(null, ["not-a-valid-uri"]);
    }

    [Fact]
    public void SampleData_CreateSampleEvents_ReturnsPastPresentAndFutureEvents()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var events = SampleData.CreateSampleEvents();

        Assert.NotEmpty(events);
        Assert.Contains(today, events.Keys);
        Assert.Contains(today.AddYears(-1).AddDays(2), events.Keys);
        Assert.Contains(today.AddYears(1).AddDays(5), events.Keys);
        Assert.Contains(events.SelectMany(static pair => pair.Value), evt => evt.IsAllDay);
        Assert.Contains(events.SelectMany(static pair => pair.Value), evt => evt.MeetingUrl?.StartsWith("https://example.com/", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void CrashLog_Write_AppendsMessageAndException()
    {
        var path = CrashLog.FilePath;
        var hadOriginal = File.Exists(path);
        var originalContents = hadOriginal ? File.ReadAllText(path) : null;

        try
        {
            CrashLog.Write("desktop test message", new InvalidOperationException("boom"));

            var contents = File.ReadAllText(path);
            Assert.Contains("desktop test message", contents, StringComparison.Ordinal);
            Assert.Contains("InvalidOperationException: boom", contents, StringComparison.Ordinal);
        }
        finally
        {
            if (hadOriginal)
            {
                File.WriteAllText(path, originalContents!);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task GetEventsAsync_GroupsEnabledCalendarEvents_AndCachesResults()
    {
        using var fixture = new DesktopServiceFixture();
        var service = fixture.CreateService();

        await fixture.SeedAccountAsync();
        await fixture.SeedCalendarAsync(new CalendarModel
        {
            CalendarId = "cal-enabled",
            AccountId = "acc-1",
            DisplayName = "Enabled",
            CalDavUrl = "https://calendar.example.com/enabled",
            Color = "#112233",
            LocalColor = "#334455",
            IsEnabled = true,
        });
        await fixture.SeedCalendarAsync(new CalendarModel
        {
            CalendarId = "cal-disabled",
            AccountId = "acc-1",
            DisplayName = "Disabled",
            CalDavUrl = "https://calendar.example.com/disabled",
            Color = "#556677",
            IsEnabled = false,
        });
        await fixture.SeedEventAsync(new CalendarEvent
        {
            EventId = "evt-1",
            CalendarId = "cal-enabled",
            RemoteUid = "uid-1",
            Title = "Enabled event",
            Location = "Room 1",
            Start = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 4, 1, 13, 0, 0, TimeSpan.Zero),
        });
        await fixture.SeedEventAsync(new CalendarEvent
        {
            EventId = "evt-2",
            CalendarId = "cal-disabled",
            RemoteUid = "uid-2",
            Title = "Disabled event",
            Start = new DateTimeOffset(2026, 4, 1, 14, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 4, 1, 15, 0, 0, TimeSpan.Zero),
        });

        var result = await service.GetEventsAsync(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 2));

        var dayEvents = Assert.Single(result[new DateOnly(2026, 4, 1)]);
        Assert.Equal("Enabled event", dayEvents.Title);
        Assert.Equal("#334455", dayEvents.CalendarColor);
        Assert.Equal(result, service.GetCachedEvents(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 2)));
        Assert.Null(service.GetCachedEvents(new DateOnly(2026, 3, 1), new DateOnly(2026, 4, 2)));
    }

    [Fact]
    public async Task UpdateCalendarEnabledAsync_PersistsEnabledState()
    {
        using var fixture = new DesktopServiceFixture();
        var service = fixture.CreateService();

        await fixture.SeedAccountAsync();
        await fixture.SeedCalendarAsync(new CalendarModel
        {
            CalendarId = "cal-1",
            AccountId = "acc-1",
            DisplayName = "Work",
            CalDavUrl = "https://calendar.example.com/work",
            IsEnabled = true,
        });

        await service.UpdateCalendarEnabledAsync("cal-1", false);

        var calendar = Assert.Single(await fixture.Calendars.GetByAccountAsync("acc-1"));
        Assert.False(calendar.IsEnabled);
    }

    [Fact]
    public async Task RemoveAccountAsync_DeletesCredentialAndAccount()
    {
        using var fixture = new DesktopServiceFixture();
        var service = fixture.CreateService();

        await fixture.SeedAccountAsync();
        await fixture.Credentials.SaveCredentialAsync("acc-1", "secret");

        await service.RemoveAccountAsync("acc-1");

        Assert.Empty(await fixture.Accounts.GetAllAsync());
        Assert.Equal(["acc-1"], fixture.Credentials.DeletedAccountIds);
    }

    [Fact]
    public async Task AddSubscriptionAsync_PersistsSubscription_AndFetchedEvents()
    {
        await using var server = await LoopbackServer.StartAsync(async (request, stream) =>
        {
            Assert.Contains("GET /calendar.ics HTTP/1.1", request, StringComparison.Ordinal);
            var body = Encoding.UTF8.GetBytes(SampleIcs);
            var header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nETag: \"etag-1\"\r\nContent-Type: text/calendar\r\nContent-Length: {body.Length}\r\n\r\n");
            await stream.WriteAsync(header.Concat(body).ToArray());
        });

        using var fixture = new DesktopServiceFixture();
        var service = fixture.CreateService();

        var error = await service.AddSubscriptionAsync(
            "Team feed",
            $"http://127.0.0.1:{server.Port}/calendar.ics",
            "#123456");

        Assert.Null(error);

        var accounts = await fixture.Accounts.GetAllAsync();
        var account = Assert.Single(accounts);
        Assert.Equal(ProviderType.IcsSubscription, account.ProviderType);
        Assert.Equal("Team feed", account.DisplayName);

        var calendar = Assert.Single(await fixture.Calendars.GetByAccountAsync(account.AccountId));
        Assert.Equal("#123456", calendar.Color);
        Assert.Equal(calendar.IcsUrl, calendar.CalDavUrl);
        Assert.Equal("\"etag-1\"", calendar.SyncToken);
        Assert.True(calendar.IsReadOnly);

        var storedEvents = await fixture.Events.GetByDateRangeAsync(
            [calendar.CalendarId],
            new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 2, 0, 0, 0, TimeSpan.Zero));

        Assert.Single(storedEvents);
        Assert.Equal("Standup", storedEvents[0].Title);
    }

    [Fact]
    public async Task AddSubscriptionAsync_RejectsInvalidUrl()
    {
        using var fixture = new DesktopServiceFixture();
        var service = fixture.CreateService();

        var error = await service.AddSubscriptionAsync("Bad feed", "ftp://example.com/feed.ics", "#123456");

        Assert.Equal("Please enter a valid URL (https:// or webcal://).", error);
        Assert.Empty(await fixture.Accounts.GetAllAsync());
    }

    [Fact]
    public async Task UpdateCalendarColorAsync_StoresLocalOverride_WhenServerPushIsNotPossible()
    {
        using var fixture = new DesktopServiceFixture();
        var service = fixture.CreateService();

        await fixture.SeedAccountAsync();
        await fixture.SeedCalendarAsync(new CalendarModel
        {
            CalendarId = "cal-1",
            AccountId = "acc-1",
            DisplayName = "Readonly",
            CalDavUrl = "https://calendar.example.com/work",
            Color = "#112233",
            IsReadOnly = true,
        });

        await service.UpdateCalendarColorAsync("cal-1", "#abcdef");

        var calendar = Assert.Single(await fixture.Calendars.GetByAccountAsync("acc-1"));
        Assert.Equal("#abcdef", calendar.LocalColor);
        Assert.Equal("#112233", calendar.Color);
    }

    [Fact]
    public async Task SyncAllAsync_SyncsEnabledIcsAccounts_AndUpdatesLastSync()
    {
        await using var server = await LoopbackServer.StartAsync(async (_, stream) =>
        {
            var body = Encoding.UTF8.GetBytes(SampleIcs);
            var header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nETag: \"etag-2\"\r\nContent-Type: text/calendar\r\nContent-Length: {body.Length}\r\n\r\n");
            await stream.WriteAsync(header.Concat(body).ToArray());
        });

        using var fixture = new DesktopServiceFixture();
        var service = fixture.CreateService();

        var account = new Account
        {
            AccountId = "ics-1",
            ProviderType = ProviderType.IcsSubscription,
            DisplayName = "Feed",
            ServerBaseUrl = $"http://127.0.0.1:{server.Port}/calendar.ics",
            Username = string.Empty,
            SupportsContacts = false,
        };

        await fixture.Accounts.UpsertAsync(account);
        await fixture.Calendars.UpsertAsync(new CalendarModel
        {
            CalendarId = account.AccountId,
            AccountId = account.AccountId,
            DisplayName = "Feed",
            CalDavUrl = account.ServerBaseUrl,
            IcsUrl = account.ServerBaseUrl,
            Color = "#123456",
            IsReadOnly = true,
            IsEnabled = true,
        });

        var result = await service.SyncAllAsync();

        Assert.Equal(1, result.synced);
        Assert.Empty(result.errors);

        var updatedAccount = Assert.Single(await fixture.Accounts.GetAllAsync());
        Assert.NotNull(updatedAccount.LastSyncUtc);

        var events = await fixture.Events.GetByDateRangeAsync(
            [account.AccountId],
            new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 2, 0, 0, 0, TimeSpan.Zero));
        Assert.Single(events);
    }

    [Fact]
    public async Task SyncAllAsync_ReturnsError_ForEnabledCalDavAccountWithoutCredentials()
    {
        using var fixture = new DesktopServiceFixture();
        var service = fixture.CreateService();

        await fixture.Accounts.UpsertAsync(new Account
        {
            AccountId = "acc-caldav",
            ProviderType = ProviderType.CalDav,
            DisplayName = "CalDAV",
            ServerBaseUrl = "https://calendar.example.com/root",
            Username = "michael",
            IsEnabled = true,
        });

        var result = await service.SyncAllAsync();

        Assert.Equal(0, result.synced);
        var error = Assert.Single(result.errors);
        Assert.Contains("CalDAV", error, StringComparison.Ordinal);
        Assert.Contains("No stored credentials.", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateDiscoveredCalendarsAsync_AddsNewCalendars_AndUpdatesExistingOnes()
    {
        using var fixture = new DesktopServiceFixture();
        var service = fixture.CreateService();
        var account = new Account
        {
            AccountId = "acc-1",
            ProviderType = ProviderType.CalDav,
            DisplayName = "Account",
            ServerBaseUrl = "https://calendar.example.com/root/",
            Username = "michael",
        };

        await fixture.Accounts.UpsertAsync(account);
        await fixture.Calendars.UpsertAsync(new CalendarModel
        {
            CalendarId = "https://calendar.example.com/calendars/user/work/",
            AccountId = account.AccountId,
            DisplayName = "Old name",
            CalDavUrl = "https://calendar.example.com/calendars/user/work/",
            Color = "#000000",
            IsReadOnly = false,
            IsEnabled = true,
        });

        using var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.MultiStatus, "<d:multistatus xmlns:d=\"DAV:\"><d:response><d:propstat><d:prop><d:current-user-principal><d:href>/principals/user/</d:href></d:current-user-principal></d:prop></d:propstat></d:response></d:multistatus>");
        handler.Enqueue(HttpStatusCode.MultiStatus, "<d:multistatus xmlns:d=\"DAV:\" xmlns:c=\"urn:ietf:params:xml:ns:caldav\"><d:response><d:propstat><d:prop><c:calendar-home-set><d:href>/calendars/user/</d:href></c:calendar-home-set></d:prop></d:propstat></d:response></d:multistatus>");
        handler.Enqueue(HttpStatusCode.MultiStatus, "<d:multistatus xmlns:d=\"DAV:\" xmlns:c=\"urn:ietf:params:xml:ns:caldav\" xmlns:a=\"http://apple.com/ns/ical/\"><d:response><d:href>/calendars/user/work/</d:href><d:propstat><d:prop><d:displayname>Work</d:displayname><d:resourcetype><d:collection /><c:calendar /></d:resourcetype><a:calendar-color>#112233FF</a:calendar-color><d:current-user-privilege-set><d:privilege><d:read /></d:privilege></d:current-user-privilege-set></d:prop></d:propstat></d:response><d:response><d:href>/calendars/user/personal/</d:href><d:propstat><d:prop><d:displayname>Personal</d:displayname><d:resourcetype><d:collection /><c:calendar /></d:resourcetype><a:calendar-color>#445566FF</a:calendar-color><d:current-user-privilege-set><d:privilege><d:write /></d:privilege></d:current-user-privilege-set></d:prop></d:propstat></d:response></d:multistatus>");

        var provider = new Kava.Providers.CalDav.CalDavProvider(new HttpClient(handler));
        var method = typeof(CalDavAccountService).GetMethod("UpdateDiscoveredCalendarsAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = Assert.IsType<Task>(method!.Invoke(service, [account, provider, CancellationToken.None])!, exactMatch: false);
        await task;

        var calendars = await fixture.Calendars.GetByAccountAsync(account.AccountId);
        Assert.Equal(2, calendars.Count);

        var updated = Assert.Single(calendars, c => c.CalendarId.EndsWith("/work/", StringComparison.Ordinal));
        Assert.Equal("Work", updated.DisplayName);
        Assert.Equal("#112233", updated.Color);
        Assert.True(updated.IsReadOnly);

        var added = Assert.Single(calendars, c => c.CalendarId.EndsWith("/personal/", StringComparison.Ordinal));
        Assert.Equal("Personal", added.DisplayName);
        Assert.Equal("#445566", added.Color);
        Assert.False(added.IsReadOnly);
    }

    [Fact]
    public async Task SyncEnabledCalendarsAsync_StoresChanges_DeletesRemovedUrls_AndUpdatesTokens()
    {
        using var fixture = new DesktopServiceFixture();
        var service = fixture.CreateService();

        await fixture.SeedAccountAsync();
        await fixture.SeedCalendarAsync(new CalendarModel
        {
            CalendarId = "cal-enabled",
            AccountId = "acc-1",
            DisplayName = "Enabled",
            CalDavUrl = "https://calendar.example.com/calendars/user/work/",
            Color = "#112233",
            IsEnabled = true,
            CTag = "old-tag",
            SyncToken = "sync-1",
        });
        await fixture.SeedCalendarAsync(new CalendarModel
        {
            CalendarId = "cal-disabled",
            AccountId = "acc-1",
            DisplayName = "Disabled",
            CalDavUrl = "https://calendar.example.com/calendars/user/disabled/",
            Color = "#445566",
            IsEnabled = false,
        });
        await fixture.SeedEventAsync(new CalendarEvent
        {
            EventId = "evt-old",
            CalendarId = "cal-enabled",
            RemoteUid = "uid-old",
            RemoteResourceUrl = "/calendars/user/work/deleted.ics",
            Title = "Old",
            Start = new DateTimeOffset(2026, 4, 1, 8, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero),
        });

        using var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.MultiStatus, "<d:multistatus xmlns:d=\"DAV:\" xmlns:cs=\"http://calendarserver.org/ns/\"><d:response><d:propstat><d:prop><cs:getctag>tag-2</cs:getctag></d:prop></d:propstat></d:response></d:multistatus>");
        handler.Enqueue(HttpStatusCode.MultiStatus, $"<d:multistatus xmlns:d=\"DAV:\" xmlns:c=\"urn:ietf:params:xml:ns:caldav\"><d:sync-token>sync-2</d:sync-token><d:response><d:href>/calendars/user/work/deleted.ics</d:href><d:status>HTTP/1.1 404 Not Found</d:status></d:response><d:response><d:href>/calendars/user/work/event-1.ics</d:href><d:propstat><d:prop><d:getetag>\"etag-2\"</d:getetag><c:calendar-data>{EscapeXml(SampleIcs)}</c:calendar-data></d:prop></d:propstat></d:response></d:multistatus>");

        var provider = new Kava.Providers.CalDav.CalDavProvider(new HttpClient(handler));
        var method = typeof(CalDavAccountService).GetMethod("SyncEnabledCalendarsAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = Assert.IsType<Task<int>>(method!.Invoke(service, ["acc-1", provider, false, CancellationToken.None])!, exactMatch: false);
        var synced = await task;

        Assert.Equal(1, synced);

        var enabledCalendar = Assert.Single(await fixture.Calendars.GetByAccountAsync("acc-1"), c => c.CalendarId == "cal-enabled");
        Assert.Equal("sync-2", enabledCalendar.SyncToken);
        Assert.Equal("tag-2", enabledCalendar.CTag);

        var events = await fixture.Events.GetByDateRangeAsync(
            ["cal-enabled"],
            new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 2, 0, 0, 0, TimeSpan.Zero));
        var evt = Assert.Single(events);
        Assert.Equal("cal-enabled::event-1", evt.EventId);
        Assert.Equal("Standup", evt.Title);
        Assert.DoesNotContain(events, e => e.RemoteResourceUrl == "/calendars/user/work/deleted.ics");
    }

    [Fact]
    public async Task SyncService_SyncNowAsync_RefreshesCache_AndRaisesEvent()
    {
        await using var server = await LoopbackServer.StartAsync(async (_, stream) =>
        {
            var body = Encoding.UTF8.GetBytes(SampleIcs);
            var header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nETag: \"etag-3\"\r\nContent-Type: text/calendar\r\nContent-Length: {body.Length}\r\n\r\n");
            await stream.WriteAsync(header.Concat(body).ToArray());
        });

        using var fixture = new DesktopServiceFixture();
        var service = fixture.CreateService();
        await fixture.Accounts.UpsertAsync(new Account
        {
            AccountId = "ics-2",
            ProviderType = ProviderType.IcsSubscription,
            DisplayName = "Feed",
            ServerBaseUrl = $"http://127.0.0.1:{server.Port}/calendar.ics",
            Username = string.Empty,
            SupportsContacts = false,
        });
        await fixture.Calendars.UpsertAsync(new CalendarModel
        {
            CalendarId = "ics-2",
            AccountId = "ics-2",
            DisplayName = "Feed",
            CalDavUrl = $"http://127.0.0.1:{server.Port}/calendar.ics",
            IcsUrl = $"http://127.0.0.1:{server.Port}/calendar.ics",
            Color = "#123456",
            IsReadOnly = true,
            IsEnabled = true,
        });

        using var sync = new SyncService(service, TimeSpan.FromMinutes(15));
        var completed = false;
        sync.SyncCompleted += () => completed = true;

        await sync.SyncNowAsync();

        Assert.True(completed);

        var today = DateOnly.FromDateTime(DateTime.Today);
        Assert.NotNull(service.GetCachedEvents(today.AddMonths(-6), today.AddMonths(6)));
    }

    [Fact]
    public async Task SyncService_RequestSync_CausesNextTickToRun()
    {
        await using var server = await LoopbackServer.StartAsync(async (_, stream) =>
        {
            var body = Encoding.UTF8.GetBytes(SampleIcs);
            var header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nETag: \"etag-4\"\r\nContent-Type: text/calendar\r\nContent-Length: {body.Length}\r\n\r\n");
            await stream.WriteAsync(header.Concat(body).ToArray());
        });

        using var fixture = new DesktopServiceFixture();
        var service = fixture.CreateService();
        await fixture.Accounts.UpsertAsync(new Account
        {
            AccountId = "ics-3",
            ProviderType = ProviderType.IcsSubscription,
            DisplayName = "Feed",
            ServerBaseUrl = $"http://127.0.0.1:{server.Port}/calendar.ics",
            Username = string.Empty,
            SupportsContacts = false,
        });
        await fixture.Calendars.UpsertAsync(new CalendarModel
        {
            CalendarId = "ics-3",
            AccountId = "ics-3",
            DisplayName = "Feed",
            CalDavUrl = $"http://127.0.0.1:{server.Port}/calendar.ics",
            IcsUrl = $"http://127.0.0.1:{server.Port}/calendar.ics",
            Color = "#123456",
            IsReadOnly = true,
            IsEnabled = true,
        });

        using var sync = new SyncService(service, TimeSpan.FromMinutes(15));
        sync.Start();
        sync.Stop();
        sync.RequestSync();

        var onTickAsync = typeof(SyncService).GetMethod("OnTickAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(onTickAsync);

        var task = Assert.IsType<Task>(onTickAsync!.Invoke(sync, [])!, exactMatch: false);
        await task;

        var events = await fixture.Events.GetByDateRangeAsync(
            ["ics-3"],
            new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 2, 0, 0, 0, TimeSpan.Zero));
        Assert.Single(events);
    }

    [SupportedOSPlatform("windows")]
    [Fact]
    public async Task FileCredentialStore_RoundTripsCredentialAndDeletesIt()
    {
        var root = Path.Combine(Path.GetTempPath(), $"kava-desktop-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var store = new FileCredentialStore(root);

            await store.SaveCredentialAsync("acc-1", "secret-value");
            var value = await store.GetCredentialAsync("acc-1");
            Assert.Equal("secret-value", value);

            await store.DeleteCredentialAsync("acc-1");
            Assert.Null(await store.GetCredentialAsync("acc-1"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class DesktopServiceFixture : IDisposable
    {
        private readonly string _root;

        public DesktopServiceFixture()
        {
            _root = Path.Combine(Path.GetTempPath(), $"kava-desktop-service-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_root);
            Database = new KavaDatabase(Path.Combine(_root, "kava.db"));
            Credentials = new FakeCredentialStore();
            Accounts = new AccountRepository(Database);
            Calendars = new CalendarRepository(Database);
            Events = new EventRepository(Database);
        }

        public KavaDatabase Database { get; }

        public FakeCredentialStore Credentials { get; }

        public AccountRepository Accounts { get; }

        public CalendarRepository Calendars { get; }

        public EventRepository Events { get; }

        public CalDavAccountService CreateService() => new(Database, Credentials);

        public Task SeedAccountAsync() => Accounts.UpsertAsync(new Account
        {
            AccountId = "acc-1",
            ProviderType = ProviderType.CalDav,
            DisplayName = "Account",
            ServerBaseUrl = "https://calendar.example.com/root",
            Username = "michael",
        });

        public Task SeedCalendarAsync(CalendarModel calendar) => Calendars.UpsertAsync(calendar);

        public Task SeedEventAsync(CalendarEvent evt) => Events.UpsertAsync(evt);

        public void Dispose()
        {
            Database.Dispose();

            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private sealed class FakeCredentialStore : ICredentialStore
    {
        private readonly Dictionary<string, string> _credentials = [];

        public List<string> DeletedAccountIds { get; } = [];

        public Task SaveCredentialAsync(string accountId, string credential)
        {
            _credentials[accountId] = credential;
            return Task.CompletedTask;
        }

        public Task<string?> GetCredentialAsync(string accountId)
        {
            _credentials.TryGetValue(accountId, out var value);
            return Task.FromResult<string?>(value);
        }

        public Task DeleteCredentialAsync(string accountId)
        {
            DeletedAccountIds.Add(accountId);
            _credentials.Remove(accountId);
            return Task.CompletedTask;
        }
    }

    private static string EscapeXml(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private sealed class StubHttpMessageHandler : HttpMessageHandler, IDisposable
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public void Enqueue(HttpStatusCode statusCode, string body)
        {
            _responses.Enqueue(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/xml"),
            });
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = _responses.Dequeue();
            response.RequestMessage = request;
            return Task.FromResult(response);
        }

        public new void Dispose()
        {
            while (_responses.Count > 0)
                _responses.Dequeue().Dispose();
        }
    }

    private static void EnsureTestApplication()
    {
        if (Application.Current == null)
            return;

        Application.Current.Resources["KavaAccent"] = Brushes.Red;
        Application.Current.Resources["KavaAction"] = Brushes.Green;
        Application.Current.Resources["KavaTextPrimary"] = Brushes.Black;
        Application.Current.Resources["KavaTextSecondary"] = Brushes.Gray;
        Application.Current.Resources["KavaTextTertiary"] = Brushes.DarkGray;
        Application.Current.Resources["KavaTextQuaternary"] = Brushes.DimGray;
        Application.Current.Resources["KavaCardBg"] = Brushes.Beige;
    }

    private sealed class LoopbackServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task _serverTask;

        private LoopbackServer(TcpListener listener, Task serverTask, int port)
        {
            _listener = listener;
            _serverTask = serverTask;
            Port = port;
        }

        public int Port { get; }

        public static async Task<LoopbackServer> StartAsync(Func<string, NetworkStream, Task> respondAsync)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;

            var serverTask = Task.Run(async () =>
            {
                using var client = await listener.AcceptTcpClientAsync();
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);

                var requestBuilder = new StringBuilder();
                while (true)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null)
                        break;

                    requestBuilder.AppendLine(line);
                    if (line.Length == 0)
                        break;
                }

                await respondAsync(requestBuilder.ToString(), stream);
                await stream.FlushAsync();
            });

            await Task.Yield();
            return new LoopbackServer(listener, serverTask, port);
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            await _serverTask;
        }
    }
}