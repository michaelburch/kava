using Kava.Core.Models;

namespace Kava.Providers.CalDav;

/// <summary>
/// Fetches and parses read-only ICS calendar feeds (webcal:// or https:// URLs).
/// </summary>
public static class IcsSubscriptionProvider
{
    private static readonly HttpClient SharedClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    /// <summary>
    /// Downloads an ICS feed and returns all parsed events for the given calendar.
    /// Uses ETag/Last-Modified for conditional requests when available.
    /// Returns null if the feed has not changed (304 Not Modified).
    /// </summary>
    public static async Task<IcsSubscriptionResult?> FetchAsync(
        Calendar calendar, CancellationToken ct = default)
    {
        var url = NormalizeUrl(calendar.IcsUrl!);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        // Conditional request using stored ETag (stored in SyncToken field)
        if (!string.IsNullOrEmpty(calendar.SyncToken))
            request.Headers.TryAddWithoutValidation("If-None-Match", calendar.SyncToken);

        using var response = await SharedClient.SendAsync(request, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
            return null;

        response.EnsureSuccessStatusCode();

        var icsData = await response.Content.ReadAsStringAsync(ct);
        var events = IcsParser.ParseEvents(icsData, calendar.CalendarId);
        var etag = response.Headers.ETag?.Tag;

        return new IcsSubscriptionResult
        {
            Events = events,
            NewETag = etag,
        };
    }

    private static string NormalizeUrl(string url)
    {
        // webcal:// is just http:// or https:// by convention
        if (url.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase))
            return "https://" + url[9..];
        return url;
    }
}

public class IcsSubscriptionResult
{
    public required IReadOnlyList<CalendarEvent> Events { get; init; }
    public string? NewETag { get; init; }
}
