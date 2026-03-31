using System.Diagnostics;
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
            if (account.ProviderType == ProviderType.IcsSubscription)
            {
                // Sync ICS subscription calendars
                try
                {
                    var calendars = await _calendars.GetByAccountAsync(account.AccountId);
                    var icsProvider = new IcsSubscriptionProvider();
                    foreach (var cal in calendars.Where(c => c.IsEnabled && !string.IsNullOrEmpty(c.IcsUrl)))
                    {
                        var result = await icsProvider.FetchAsync(cal, ct);
                        if (result == null) { synced++; continue; } // 304 Not Modified

                        // Replace all events for this subscription calendar
                        await _events.DeleteByCalendarAsync(cal.CalendarId);
                        foreach (var evt in result.Events)
                            await _events.UpsertAsync(evt);

                        if (result.NewETag != null)
                            cal.SyncToken = result.NewETag;
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
                continue;
            }

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

                // Re-discover calendars to pick up server-side changes (color, name)
                var existingCalendars = await _calendars.GetByAccountAsync(account.AccountId);
                var serverCalendars = await provider.DiscoverCalendarsAsync(account, ct);
                var existingMap = existingCalendars.ToDictionary(c => c.CalendarId);
                foreach (var serverCal in serverCalendars)
                {
                    if (existingMap.TryGetValue(serverCal.CalendarId, out var existing))
                    {
                        if (!string.Equals(existing.Color, serverCal.Color, StringComparison.OrdinalIgnoreCase))
                            Debug.WriteLine($"[Sync] Color changed on server for '{existing.DisplayName}': {existing.Color} → {serverCal.Color}");
                        // Update server-managed properties, preserve local settings (LocalColor, IsEnabled)
                        existing.DisplayName = serverCal.DisplayName;
                        existing.Color = serverCal.Color;
                        existing.IsReadOnly = serverCal.IsReadOnly;
                        // LocalColor is intentionally NOT overwritten — it's a user override
                        await _calendars.UpsertAsync(existing);
                    }
                    else
                    {
                        // New calendar discovered on the server
                        await _calendars.UpsertAsync(serverCal);
                    }
                }

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
        var colorMap = enabledCalendars.ToDictionary(c => c.CalendarId, c => c.EffectiveColor);

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

    /// <summary>
    /// Adds an ICS subscription. Creates a pseudo-account and a single calendar entry,
    /// then does the initial fetch. Returns null on success or an error message.
    /// </summary>
    public async Task<string?> AddSubscriptionAsync(
        string displayName, string icsUrl, string color, CancellationToken ct = default)
    {
        // Normalize webcal:// to https://
        var normalizedUrl = icsUrl.Trim();
        if (normalizedUrl.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase))
            normalizedUrl = "https://" + normalizedUrl[9..];

        if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != "https" && uri.Scheme != "http"))
            return "Please enter a valid URL (https:// or webcal://).";

        try
        {
            // Create a subscription account
            var account = new Account
            {
                ProviderType = ProviderType.IcsSubscription,
                DisplayName = displayName,
                ServerBaseUrl = normalizedUrl,
                Username = string.Empty,
                SupportsContacts = false,
            };

            var calendar = new Calendar
            {
                CalendarId = account.AccountId, // 1:1 account→calendar for subscriptions
                AccountId = account.AccountId,
                DisplayName = displayName,
                CalDavUrl = normalizedUrl,
                IcsUrl = normalizedUrl,
                Color = color,
                IsReadOnly = true,
            };

            // Validate by doing the initial fetch
            var provider = new IcsSubscriptionProvider();
            var result = await provider.FetchAsync(calendar, ct);
            if (result == null)
                return "The URL did not return any calendar data.";

            // Persist
            await _accounts.UpsertAsync(account);
            await _calendars.UpsertAsync(calendar);

            // Store fetched events
            foreach (var evt in result.Events)
                await _events.UpsertAsync(evt);

            if (result.NewETag != null)
                calendar.SyncToken = result.NewETag;
            account.LastSyncUtc = DateTime.UtcNow;
            await _calendars.UpsertAsync(calendar);
            await _accounts.UpsertAsync(account);

            return null;
        }
        catch (HttpRequestException ex)
        {
            return $"Failed to fetch calendar: {ex.Message}";
        }
        catch (TaskCanceledException)
        {
            return "Connection timed out.";
        }
        catch (Exception ex)
        {
            return $"Failed to subscribe: {ex.Message}";
        }
    }

    public async Task UpdateCalendarColorAsync(string calendarId, string color)
    {
        var accounts = await _accounts.GetAllAsync();
        foreach (var account in accounts)
        {
            var cals = await _calendars.GetByAccountAsync(account.AccountId);
            var cal = cals.FirstOrDefault(c => c.CalendarId == calendarId);
            if (cal == null) continue;

            // Try to push the color to the server
            var password = await _credentials.GetCredentialAsync(account.AccountId);
            if (password != null && !cal.IsReadOnly)
            {
                try
                {
                    using var httpClient = CalDavProvider.CreateHttpClient(account, password);
                    var provider = new CalDavProvider(httpClient);
                    Debug.WriteLine($"[Color] PROPPATCH sending color: {color}");
                    if (await provider.SetCalendarColorAsync(cal.CalDavUrl, color))
                    {
                        // Verify the server actually applied the change
                        var serverCalendars = await provider.DiscoverCalendarsAsync(account, default);
                        var updated = serverCalendars.FirstOrDefault(c => c.CalendarId == cal.CalendarId);
                        Debug.WriteLine($"[Color] Server returned color after PROPPATCH: {updated?.Color ?? "(calendar not found)"}");
                        if (updated != null && string.Equals(updated.Color, color, StringComparison.OrdinalIgnoreCase))
                        {
                            // Server accepted — update server color and clear local override
                            Debug.WriteLine($"[Color] Verified — server color matches");
                            cal.Color = color;
                            cal.LocalColor = null;
                            await _calendars.UpsertAsync(cal);
                            return;
                        }
                        Debug.WriteLine($"[Color] Server did NOT apply color — storing as local override");
                    }
                }
                catch
                {
                    // Server push failed — fall through to local-only override
                }
            }

            // Server push failed or read-only calendar — store as local override
            cal.LocalColor = color;
            await _calendars.UpsertAsync(cal);
            return;
        }
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
            CalendarId = evt.CalendarId,
            CalendarColor = color,
            IsAllDay = evt.IsAllDay,
            MeetingUrl = evt.MeetingUrl,
        };
    }
}
