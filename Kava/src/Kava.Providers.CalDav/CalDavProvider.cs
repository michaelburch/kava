using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using Kava.Core.Interfaces;
using Kava.Core.Models;

namespace Kava.Providers.CalDav;

public class CalDavProvider : ICalendarProvider
{
    private static readonly XNamespace DavNs = "DAV:";
    private static readonly XNamespace CalDavNs = "urn:ietf:params:xml:ns:caldav";
    private static readonly XNamespace AppleNs = "http://apple.com/ns/ical/";

    private readonly HttpClient _httpClient;

    public CalDavProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public static HttpClient CreateHttpClient(Account account, string password)
    {
        var handler = new HttpClientHandler();
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(account.ServerBaseUrl)
        };
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{account.Username}:{password}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        return client;
    }

    public async Task<IReadOnlyList<Calendar>> DiscoverCalendarsAsync(
        Account account, CancellationToken cancellationToken = default)
    {
        // Step 1: Discover principal URL
        var principalUrl = await DiscoverPrincipalAsync(account.ServerBaseUrl, cancellationToken);
        if (principalUrl == null) return [];

        // Step 2: Discover calendar home set
        var calendarHomeUrl = await DiscoverCalendarHomeAsync(principalUrl, cancellationToken);
        if (calendarHomeUrl == null) return [];

        // Step 3: List calendars
        return await ListCalendarsAsync(account.AccountId, calendarHomeUrl, cancellationToken);
    }

    public async Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        Calendar calendar,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        CancellationToken cancellationToken = default)
    {
        var requestBody = new XDocument(
            new XElement(CalDavNs + "calendar-query",
                new XAttribute(XNamespace.Xmlns + "d", DavNs),
                new XAttribute(XNamespace.Xmlns + "c", CalDavNs),
                new XElement(DavNs + "prop",
                    new XElement(DavNs + "getetag"),
                    new XElement(CalDavNs + "calendar-data")),
                new XElement(CalDavNs + "filter",
                    new XElement(CalDavNs + "comp-filter",
                        new XAttribute("name", "VCALENDAR"),
                        new XElement(CalDavNs + "comp-filter",
                            new XAttribute("name", "VEVENT"),
                            new XElement(CalDavNs + "time-range",
                                new XAttribute("start", rangeStart.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'")),
                                new XAttribute("end", rangeEnd.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'"))))))));

        var response = await SendReportAsync(calendar.CalDavUrl, requestBody, cancellationToken);
        if (response == null) return [];

        return ParseEventsFromMultistatus(response, calendar.CalendarId);
    }

    public async Task<SyncResult<CalendarEvent>> SyncEventsAsync(
        Calendar calendar,
        string? syncToken,
        CancellationToken cancellationToken = default)
    {
        // For now, fall back to a full report for the current year range
        var start = new DateTimeOffset(DateTimeOffset.UtcNow.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddYears(1);
        var events = await GetEventsAsync(calendar, start, end, cancellationToken);

        return new SyncResult<CalendarEvent>
        {
            Changed = events,
            DeletedUids = [],
            NewSyncToken = null
        };
    }

    private async Task<string?> DiscoverPrincipalAsync(string baseUrl, CancellationToken ct)
    {
        var body = new XDocument(
            new XElement(DavNs + "propfind",
                new XElement(DavNs + "prop",
                    new XElement(DavNs + "current-user-principal"))));

        var response = await SendPropfindAsync(baseUrl, body, depth: 0, ct);
        if (response == null) return null;

        var href = response
            .Descendants(DavNs + "current-user-principal")
            .Descendants(DavNs + "href")
            .FirstOrDefault()?.Value;

        return href != null ? ResolveUrl(baseUrl, href) : null;
    }

    private async Task<string?> DiscoverCalendarHomeAsync(string principalUrl, CancellationToken ct)
    {
        var body = new XDocument(
            new XElement(DavNs + "propfind",
                new XAttribute(XNamespace.Xmlns + "c", CalDavNs),
                new XElement(DavNs + "prop",
                    new XElement(CalDavNs + "calendar-home-set"))));

        var response = await SendPropfindAsync(principalUrl, body, depth: 0, ct);
        if (response == null) return null;

        var href = response
            .Descendants(CalDavNs + "calendar-home-set")
            .Descendants(DavNs + "href")
            .FirstOrDefault()?.Value;

        return href != null ? ResolveUrl(principalUrl, href) : null;
    }

    private async Task<IReadOnlyList<Calendar>> ListCalendarsAsync(
        string accountId, string calendarHomeUrl, CancellationToken ct)
    {
        var body = new XDocument(
            new XElement(DavNs + "propfind",
                new XAttribute(XNamespace.Xmlns + "c", CalDavNs),
                new XAttribute(XNamespace.Xmlns + "a", AppleNs),
                new XElement(DavNs + "prop",
                    new XElement(DavNs + "displayname"),
                    new XElement(DavNs + "resourcetype"),
                    new XElement(AppleNs + "calendar-color"),
                    new XElement(DavNs + "current-user-privilege-set"))));

        var response = await SendPropfindAsync(calendarHomeUrl, body, depth: 1, ct);
        if (response == null) return [];

        var calendars = new List<Calendar>();
        foreach (var propResponse in response.Descendants(DavNs + "response"))
        {
            var href = propResponse.Element(DavNs + "href")?.Value;
            if (href == null) continue;

            var resourceType = propResponse.Descendants(DavNs + "resourcetype").FirstOrDefault();
            var isCalendar = resourceType?.Element(CalDavNs + "calendar") != null;
            if (!isCalendar) continue;

            var displayName = propResponse.Descendants(DavNs + "displayname").FirstOrDefault()?.Value ?? "Calendar";
            var colorValue = propResponse.Descendants(AppleNs + "calendar-color").FirstOrDefault()?.Value;
            var color = NormalizeColor(colorValue) ?? "#0078D4";

            var privileges = propResponse.Descendants(DavNs + "current-user-privilege-set")
                .Descendants(DavNs + "privilege")
                .Select(p => p.Elements().FirstOrDefault()?.Name.LocalName)
                .Where(n => n != null)
                .ToHashSet();
            var isReadOnly = !privileges.Contains("write");

            var calUrl = ResolveUrl(calendarHomeUrl, href);

            calendars.Add(new Calendar
            {
                CalendarId = calUrl, // Use URL as stable ID
                AccountId = accountId,
                DisplayName = displayName,
                CalDavUrl = calUrl,
                Color = color,
                IsReadOnly = isReadOnly,
            });
        }

        return calendars;
    }

    private IReadOnlyList<CalendarEvent> ParseEventsFromMultistatus(XDocument doc, string calendarId)
    {
        var events = new List<CalendarEvent>();

        foreach (var response in doc.Descendants(DavNs + "response"))
        {
            var href = response.Element(DavNs + "href")?.Value;
            var etag = response.Descendants(DavNs + "getetag").FirstOrDefault()?.Value;
            var calData = response.Descendants(CalDavNs + "calendar-data").FirstOrDefault()?.Value;

            if (calData == null) continue;

            var parsed = IcsParser.ParseEvents(calData, calendarId);
            foreach (var evt in parsed)
            {
                evt.RemoteResourceUrl = href;
                evt.ETag = etag?.Trim('"');
                evt.RawICalendarPayload = calData;
                evt.LastSeenUtc = DateTime.UtcNow;
                events.Add(evt);
            }
        }

        return events;
    }

    private async Task<XDocument?> SendPropfindAsync(
        string url, XDocument body, int depth, CancellationToken ct)
    {
        var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), url);
        request.Headers.Add("Depth", depth.ToString());
        request.Content = new StringContent(body.ToString(), Encoding.UTF8, "application/xml");

        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.MultiStatus)
            return null;

        var content = await response.Content.ReadAsStringAsync(ct);
        return XDocument.Parse(content);
    }

    private async Task<XDocument?> SendReportAsync(
        string url, XDocument body, CancellationToken ct)
    {
        var request = new HttpRequestMessage(new HttpMethod("REPORT"), url);
        request.Headers.Add("Depth", "1");
        request.Content = new StringContent(body.ToString(), Encoding.UTF8, "application/xml");

        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.MultiStatus)
            return null;

        var content = await response.Content.ReadAsStringAsync(ct);
        return XDocument.Parse(content);
    }

    private static string ResolveUrl(string baseUrl, string href)
    {
        if (Uri.TryCreate(href, UriKind.Absolute, out var abs))
            return abs.ToString();

        var baseUri = new Uri(baseUrl);
        return new Uri(baseUri, href).ToString();
    }

    private static string? NormalizeColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color)) return null;
        // Apple calendar colors may be #RRGGBBAA — strip the alpha
        if (color.StartsWith('#') && color.Length == 9)
            return color[..7];
        return color;
    }
}
