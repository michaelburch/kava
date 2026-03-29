using Kava.Core.Interfaces;
using Kava.Core.Models;
using Kava.Persistence;
using Kava.Providers.CalDav;

namespace Kava.Desktop;

/// <summary>
/// Orchestrates CalDAV account operations: add, discover calendars, sync events, remove.
/// </summary>
public sealed class CalDavAccountService
{
    private readonly KavaDatabase _db;
    private readonly AccountRepository _accounts;
    private readonly CalendarRepository _calendars;
    private readonly EventRepository _events;
    private readonly ICredentialStore _credentials;

    /// <summary>In-memory cache of the last loaded events for instant flyout display.</summary>
    private Dictionary<DateOnly, List<EventItem>>? _cachedEvents;
    private DateOnly _cacheStart;
    private DateOnly _cacheEnd;

    public CalDavAccountService(
        KavaDatabase db,
        ICredentialStore credentials)
    {
        _db = db;
        _accounts = new AccountRepository(db);
        _calendars = new CalendarRepository(db);
        _events = new EventRepository(db);
        _credentials = credentials;
    }

    public Task<List<Account>> GetAccountsAsync() => _accounts.GetAllAsync();

    public Task<List<Calendar>> GetCalendarsAsync(string accountId) =>
        _calendars.GetByAccountAsync(accountId);

    /// <summary>
    /// Validates credentials, discovers calendars, and persists the account.
    /// Returns null on success, or an error message.
    /// </summary>
    public async Task<string?> AddAccountAsync(
        string displayName, string serverUrl, string username, string password,
        CancellationToken ct = default)
    {
        var account = new Account
        {
            ProviderType = ProviderType.CalDav,
            DisplayName = displayName,
            ServerBaseUrl = serverUrl.TrimEnd('/'),
            Username = username,
        };

        try
        {
            using var httpClient = CalDavProvider.CreateHttpClient(account, password);
            var provider = new CalDavProvider(httpClient);

            var calendars = await provider.DiscoverCalendarsAsync(account, ct);
            if (calendars.Count == 0)
                return "No calendars found. Check the server URL and credentials.";

            // Persist account
            await _accounts.UpsertAsync(account);
            await _credentials.SaveCredentialAsync(account.AccountId, password);

            // Persist discovered calendars
            foreach (var cal in calendars)
                await _calendars.UpsertAsync(cal);

            return null; // success
        }
        catch (HttpRequestException ex)
        {
            return ex.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized => "Invalid username or password.",
                System.Net.HttpStatusCode.Forbidden => "Access denied by the server.",
                System.Net.HttpStatusCode.NotFound => "Server URL not found.",
                _ => $"Connection failed: {ex.Message}",
            };
        }
        catch (TaskCanceledException)
        {
            return "Connection timed out.";
        }
        catch (Exception ex)
        {
            return $"Failed to add account: {ex.Message}";
        }
    }

    /// <summary>
    /// Syncs all enabled calendars for all accounts.
    /// When forceFullSync is true, ignores stored CTag/sync-token and does a full re-fetch.
    /// Returns (successCount, errorMessages).
    /// </summary>
    public async Task<(int synced, List<string> errors)> SyncAllAsync(
        CancellationToken ct = default, bool forceFullSync = false)
    {
        var accounts = await _accounts.GetAllAsync();
        var synced = 0;
        var errors = new List<string>();

        foreach (var account in accounts.Where(a => a.IsEnabled))
        {
            var password = await _credentials.GetCredentialAsync(account.AccountId);
            if (password == null)
            {
                errors.Add($"{account.DisplayName}: No stored credentials.");
                continue;
            }

            try
            {
                using var httpClient = CalDavProvider.CreateHttpClient(account, password);
                var provider = new CalDavProvider(httpClient);

                var calendars = await _calendars.GetByAccountAsync(account.AccountId);
                foreach (var cal in calendars.Where(c => c.IsEnabled))
                {
                    var syncToken = forceFullSync ? null : cal.SyncToken;
                    if (forceFullSync)
                        cal.CTag = null;

                    var result = await provider.SyncEventsAsync(cal, syncToken, ct);
                    foreach (var evt in result.Changed)
                        await _events.UpsertAsync(evt);

                    // Sync-collection returns resource hrefs for deletions
                    if (result.DeletedUids.Count > 0)
                        await _events.DeleteByRemoteUrlsAsync(cal.CalendarId, result.DeletedUids);

                    // Persist sync-token and CTag for next sync
                    if (result.NewSyncToken != null)
                        cal.SyncToken = result.NewSyncToken;
                    if (result.NewCTag != null)
                        cal.CTag = result.NewCTag;
                    await _calendars.UpsertAsync(cal);

                    synced++;
                }

                account.LastSyncUtc = DateTime.UtcNow;
                await _accounts.UpsertAsync(account);
            }
            catch (Exception ex)
            {
                errors.Add($"{account.DisplayName}: {ex.Message}");
            }
        }

        return (synced, errors);
    }

    /// <summary>
    /// Gets events for the given date range from the local database.
    /// Updates the in-memory cache.
    /// </summary>
    public async Task<Dictionary<DateOnly, List<EventItem>>> GetEventsAsync(
        DateOnly rangeStart, DateOnly rangeEnd)
    {
        var enabledCalendars = await _calendars.GetEnabledAsync();
        if (enabledCalendars.Count == 0)
        {
            _cachedEvents = new Dictionary<DateOnly, List<EventItem>>();
            return _cachedEvents;
        }

        var calIds = enabledCalendars.Select(c => c.CalendarId).ToList();
        var colorMap = enabledCalendars.ToDictionary(c => c.CalendarId, c => c.Color);

        var start = new DateTimeOffset(rangeStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var end = new DateTimeOffset(rangeEnd.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        var dbEvents = await _events.GetByDateRangeAsync(calIds, start, end);

        var result = new Dictionary<DateOnly, List<EventItem>>();
        foreach (var evt in dbEvents)
        {
            var date = DateOnly.FromDateTime(evt.Start.LocalDateTime);
            var item = MapToEventItem(evt, colorMap.GetValueOrDefault(evt.CalendarId, "#0078D4"));

            if (!result.TryGetValue(date, out var list))
            {
                list = [];
                result[date] = list;
            }
            list.Add(item);
        }

        _cachedEvents = result;
        _cacheStart = rangeStart;
        _cacheEnd = rangeEnd;
        return result;
    }

    /// <summary>
    /// Returns cached events instantly if available (for responsive flyout opening).
    /// Returns null if no cache exists yet.
    /// </summary>
    public Dictionary<DateOnly, List<EventItem>>? GetCachedEvents(DateOnly rangeStart, DateOnly rangeEnd)
    {
        if (_cachedEvents != null && rangeStart >= _cacheStart && rangeEnd <= _cacheEnd)
            return _cachedEvents;
        return null;
    }

    public async Task RemoveAccountAsync(string accountId)
    {
        await _credentials.DeleteCredentialAsync(accountId);
        await _accounts.DeleteAsync(accountId); // cascade deletes calendars + events
    }

    public async Task UpdateCalendarEnabledAsync(string calendarId, bool enabled)
    {
        // Read, modify, write
        var allCals = await _calendars.GetEnabledAsync();
        // We need all calendars not just enabled, get from any account
        var accounts = await _accounts.GetAllAsync();
        foreach (var account in accounts)
        {
            var cals = await _calendars.GetByAccountAsync(account.AccountId);
            var cal = cals.FirstOrDefault(c => c.CalendarId == calendarId);
            if (cal != null)
            {
                cal.IsEnabled = enabled;
                await _calendars.UpsertAsync(cal);
                return;
            }
        }
    }

    private static EventItem MapToEventItem(CalendarEvent evt, string color)
    {
        string timeRange;
        if (evt.IsAllDay)
        {
            timeRange = "All day";
        }
        else
        {
            var start = evt.Start.LocalDateTime;
            var end = evt.End.LocalDateTime;
            timeRange = $"{start:h:mm tt} \u2013 {end:h:mm tt}";
        }

        return new EventItem
        {
            Title = evt.Title,
            TimeRange = timeRange,
            Subtitle = evt.Location,
            CalendarColor = color,
            IsAllDay = evt.IsAllDay,
            MeetingUrl = evt.MeetingUrl,
        };
    }
}
