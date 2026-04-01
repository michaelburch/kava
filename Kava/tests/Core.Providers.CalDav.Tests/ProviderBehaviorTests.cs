using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using Kava.Core.Models;
using Kava.Providers.CalDav;
using Xunit;

namespace Core.Providers.CalDav.Tests;

public sealed class ProviderBehaviorTests
{
    private static readonly string SampleIcs = string.Join("\r\n",
        "BEGIN:VCALENDAR",
        "VERSION:2.0",
        "BEGIN:VEVENT",
        "UID:event-1",
        "SUMMARY:Standup",
        "DESCRIPTION:Daily sync",
        "LOCATION:https://meet.example.com/standup",
        "DTSTART:20260401T090000Z",
        "DTEND:20260401T093000Z",
        "END:VEVENT",
        "END:VCALENDAR",
        string.Empty);

    [Fact]
    public void IcsParser_ParsesEventsAndExtractsMeetingUrl()
    {
        var events = IcsParser.ParseEvents(SampleIcs, "cal-1");

        var evt = Assert.Single(events);
        Assert.Equal("cal-1::event-1", evt.EventId);
        Assert.Equal("cal-1", evt.CalendarId);
        Assert.Equal("event-1", evt.RemoteUid);
        Assert.Equal("Standup", evt.Title);
        Assert.Equal("Daily sync", evt.Description);
        Assert.Equal("https://meet.example.com/standup", evt.Location);
        Assert.Equal("https://meet.example.com/standup", evt.MeetingUrl);
        Assert.False(evt.IsAllDay);
    }

    [Fact]
    public void IcsParser_SkipsInvalidEventsAndHandlesAllDayDefaults()
    {
        var ics = string.Join("\r\n",
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "BEGIN:VEVENT",
            "UID:all-day-1",
            "DTSTART;VALUE=DATE:20260402",
            "END:VEVENT",
            "BEGIN:VEVENT",
            "UID:missing-start-should-be-skipped",
            "END:VEVENT",
            "END:VCALENDAR",
            string.Empty);

        var events = IcsParser.ParseEvents(ics, "cal-2");

        var evt = Assert.Single(events);
        Assert.Equal("(No title)", evt.Title);
        Assert.True(evt.IsAllDay);
        Assert.Equal(evt.Start, evt.End);
        Assert.Null(evt.MeetingUrl);
    }

    [Fact]
    public void CreateHttpClient_RequiresHttps()
    {
        var account = new Account
        {
            ServerBaseUrl = "http://calendar.example.com",
            Username = "michael",
        };

        var ex = Assert.Throws<InvalidOperationException>(() => CalDavProvider.CreateHttpClient(account, "secret"));
        Assert.Equal("CalDAV server URL must use HTTPS.", ex.Message);
    }

    [Fact]
    public async Task DiscoverCalendarsAsync_FollowsPrincipalAndCalendarHomeDiscovery()
    {
        using var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.MultiStatus, "<d:multistatus xmlns:d=\"DAV:\"><d:response><d:propstat><d:prop><d:current-user-principal><d:href>/principals/user/</d:href></d:current-user-principal></d:prop></d:propstat></d:response></d:multistatus>");
        handler.Enqueue(HttpStatusCode.MultiStatus, "<d:multistatus xmlns:d=\"DAV:\" xmlns:c=\"urn:ietf:params:xml:ns:caldav\"><d:response><d:propstat><d:prop><c:calendar-home-set><d:href>/calendars/user/</d:href></c:calendar-home-set></d:prop></d:propstat></d:response></d:multistatus>");
        handler.Enqueue(HttpStatusCode.MultiStatus, "<d:multistatus xmlns:d=\"DAV:\" xmlns:c=\"urn:ietf:params:xml:ns:caldav\" xmlns:a=\"http://apple.com/ns/ical/\"><d:response><d:href>/calendars/user/work/</d:href><d:propstat><d:prop><d:displayname>Work</d:displayname><d:resourcetype><d:collection /><c:calendar /></d:resourcetype><a:calendar-color>#112233FF</a:calendar-color><d:current-user-privilege-set><d:privilege><d:write /></d:privilege></d:current-user-privilege-set></d:prop></d:propstat></d:response><d:response><d:href>/calendars/user/readonly/</d:href><d:propstat><d:prop><d:displayname>Readonly</d:displayname><d:resourcetype><d:collection /><c:calendar /></d:resourcetype><d:current-user-privilege-set><d:privilege><d:read /></d:privilege></d:current-user-privilege-set></d:prop></d:propstat></d:response></d:multistatus>");

        var provider = new CalDavProvider(new HttpClient(handler));
        var calendars = await provider.DiscoverCalendarsAsync(new Account
        {
            AccountId = "acc-1",
            ServerBaseUrl = "https://calendar.example.com/root/",
        });

        Assert.Equal(2, calendars.Count);
        Assert.Equal("Work", calendars[0].DisplayName);
        Assert.Equal("#112233", calendars[0].Color);
        Assert.False(calendars[0].IsReadOnly);
        Assert.Equal("https://calendar.example.com/calendars/user/work/", calendars[0].CalendarId);

        Assert.Equal("Readonly", calendars[1].DisplayName);
        Assert.True(calendars[1].IsReadOnly);
        Assert.Equal("#0078D4", calendars[1].Color);
    }

    [Fact]
    public async Task GetEventsAsync_ParsesCalendarQueryResponse()
    {
        using var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.MultiStatus, $"<d:multistatus xmlns:d=\"DAV:\" xmlns:c=\"urn:ietf:params:xml:ns:caldav\"><d:response><d:href>/calendars/user/work/event-1.ics</d:href><d:propstat><d:prop><d:getetag>\"etag-1\"</d:getetag><c:calendar-data>{EscapeXml(SampleIcs)}</c:calendar-data></d:prop></d:propstat></d:response></d:multistatus>");

        var provider = new CalDavProvider(new HttpClient(handler));
        var events = await provider.GetEventsAsync(
            new Calendar { CalendarId = "cal-1", CalDavUrl = "https://calendar.example.com/calendars/user/work/" },
            new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 2, 0, 0, 0, TimeSpan.Zero));

        var evt = Assert.Single(events);
        Assert.Equal("/calendars/user/work/event-1.ics", evt.RemoteResourceUrl);
        Assert.Equal("etag-1", evt.ETag);
        Assert.NotNull(evt.RawICalendarPayload);
        Assert.NotNull(evt.LastSeenUtc);
    }

    [Fact]
    public async Task SyncEventsAsync_ReturnsEarlyWhenCtagIsUnchanged()
    {
        using var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.MultiStatus, "<d:multistatus xmlns:d=\"DAV:\" xmlns:cs=\"http://calendarserver.org/ns/\"><d:response><d:propstat><d:prop><cs:getctag>tag-1</cs:getctag></d:prop></d:propstat></d:response></d:multistatus>");

        var provider = new CalDavProvider(new HttpClient(handler));
        var result = await provider.SyncEventsAsync(new Calendar
        {
            CalDavUrl = "https://calendar.example.com/calendars/user/work/",
            CTag = "tag-1",
        }, "sync-1");

        Assert.Empty(result.Changed);
        Assert.Empty(result.DeletedUids);
        Assert.Equal("sync-1", result.NewSyncToken);
        Assert.Equal("tag-1", result.NewCTag);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task SyncEventsAsync_UsesSyncCollectionWhenTokenIsPresent()
    {
        using var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.MultiStatus, "<d:multistatus xmlns:d=\"DAV:\" xmlns:cs=\"http://calendarserver.org/ns/\"><d:response><d:propstat><d:prop><cs:getctag>tag-2</cs:getctag></d:prop></d:propstat></d:response></d:multistatus>");
        handler.Enqueue(HttpStatusCode.MultiStatus, $"<d:multistatus xmlns:d=\"DAV:\" xmlns:c=\"urn:ietf:params:xml:ns:caldav\"><d:sync-token>sync-2</d:sync-token><d:response><d:href>/calendars/user/work/deleted.ics</d:href><d:status>HTTP/1.1 404 Not Found</d:status></d:response><d:response><d:href>/calendars/user/work/event-1.ics</d:href><d:propstat><d:prop><d:getetag>\"etag-2\"</d:getetag><c:calendar-data>{EscapeXml(SampleIcs)}</c:calendar-data></d:prop></d:propstat></d:response></d:multistatus>");

        var provider = new CalDavProvider(new HttpClient(handler));
        var result = await provider.SyncEventsAsync(new Calendar
        {
            CalendarId = "cal-1",
            CalDavUrl = "https://calendar.example.com/calendars/user/work/",
            CTag = "old-tag",
        }, "sync-1");

        Assert.Single(result.Changed);
        Assert.Equal("/calendars/user/work/deleted.ics", Assert.Single(result.DeletedUids));
        Assert.Equal("sync-2", result.NewSyncToken);
        Assert.Equal("tag-2", result.NewCTag);
    }

    [Fact]
    public async Task SyncEventsAsync_FallsBackToFullSyncWhenSyncTokenIsRejected()
    {
        using var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.MultiStatus, "<d:multistatus xmlns:d=\"DAV:\" xmlns:cs=\"http://calendarserver.org/ns/\"><d:response><d:propstat><d:prop><cs:getctag>tag-3</cs:getctag></d:prop></d:propstat></d:response></d:multistatus>");
        handler.Enqueue(HttpStatusCode.Forbidden, string.Empty);
        handler.Enqueue(HttpStatusCode.MultiStatus, $"<d:multistatus xmlns:d=\"DAV:\" xmlns:c=\"urn:ietf:params:xml:ns:caldav\"><d:response><d:href>/calendars/user/work/event-1.ics</d:href><d:propstat><d:prop><d:getetag>\"etag-3\"</d:getetag><c:calendar-data>{EscapeXml(SampleIcs)}</c:calendar-data></d:prop></d:propstat></d:response></d:multistatus>");
        handler.Enqueue(HttpStatusCode.MultiStatus, "<d:multistatus xmlns:d=\"DAV:\"><d:response><d:propstat><d:prop><d:sync-token>sync-3</d:sync-token></d:prop></d:propstat></d:response></d:multistatus>");

        var provider = new CalDavProvider(new HttpClient(handler));
        var result = await provider.SyncEventsAsync(new Calendar
        {
            CalendarId = "cal-1",
            CalDavUrl = "https://calendar.example.com/calendars/user/work/",
            CTag = "old-tag",
        }, "sync-old");

        Assert.Single(result.Changed);
        Assert.Empty(result.DeletedUids);
        Assert.Equal("sync-3", result.NewSyncToken);
        Assert.Equal("tag-3", result.NewCTag);
    }

    [Fact]
    public async Task SetCalendarColorAsync_ReturnsTrueForMultiStatus()
    {
        using var handler = new StubHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.MultiStatus, "<d:multistatus xmlns:d=\"DAV:\" />");

        var provider = new CalDavProvider(new HttpClient(handler));
        var success = await provider.SetCalendarColorAsync("https://calendar.example.com/calendars/user/work/", "#123456");

        Assert.True(success);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("PROPPATCH", request.Method.Method);
        Assert.Contains("#123456", request.Body);
    }

    [Fact]
    public async Task IcsSubscriptionProvider_FetchAsync_ReturnsEvents()
    {
        await using var server = await LoopbackServer.StartAsync(async (request, stream) =>
        {
            Assert.Contains("GET /feed.ics HTTP/1.1", request);
            var body = Encoding.UTF8.GetBytes(SampleIcs);
            var header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nETag: \"etag-1\"\r\nContent-Type: text/calendar\r\nContent-Length: {body.Length}\r\n\r\n");
            await stream.WriteAsync(header.Concat(body).ToArray());
        });

        var result = await IcsSubscriptionProvider.FetchAsync(new Calendar
        {
            CalendarId = "cal-ics",
            IcsUrl = $"http://127.0.0.1:{server.Port}/feed.ics",
        });

        Assert.NotNull(result);
        Assert.Single(result!.Events);
        Assert.Equal("etag-1", result.NewETag?.Trim('"'));
    }

    [Fact]
    public async Task IcsSubscriptionProvider_FetchAsync_ReturnsNullForNotModified()
    {
        await using var server = await LoopbackServer.StartAsync(async (request, stream) =>
        {
            Assert.Contains("If-None-Match: \"etag-1\"", request);
            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 304 Not Modified\r\nContent-Length: 0\r\n\r\n"));
        });

        var result = await IcsSubscriptionProvider.FetchAsync(new Calendar
        {
            CalendarId = "cal-ics",
            IcsUrl = $"http://127.0.0.1:{server.Port}/feed.ics",
            SyncToken = "\"etag-1\"",
        });

        Assert.Null(result);
    }

    private static string EscapeXml(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private sealed class StubHttpMessageHandler : HttpMessageHandler, IDisposable
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public List<(HttpMethod Method, Uri Uri, string Body)> Requests { get; } = [];

        public void Enqueue(HttpStatusCode statusCode, string body)
        {
            _responses.Enqueue(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/xml"),
            });
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method, request.RequestUri!, body));

            var response = _responses.Dequeue();
            response.RequestMessage = request;
            return response;
        }

        public new void Dispose()
        {
            while (_responses.Count > 0)
                _responses.Dequeue().Dispose();
        }
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