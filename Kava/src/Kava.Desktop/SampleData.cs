namespace Kava.Desktop;

public static class SampleData
{
    public static Dictionary<DateOnly, List<EventItem>> CreateSampleEvents()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var events = new Dictionary<DateOnly, List<EventItem>>();

        events[today] =
        [
            new EventItem
            {
                Title = "Team standup",
                TimeRange = "9:00 AM \u2013 9:15 AM",
                Subtitle = "Microsoft Teams Meeting",
                CalendarColor = "#4A90D9",
                MeetingUrl = "https://example.com/meeting",
            },
            new EventItem
            {
                Title = "Focus time",
                TimeRange = "10:00 AM \u2013 12:00 PM",
                CalendarColor = "#7B68EE",
            },
            new EventItem
            {
                Title = "Lunch with Sarah",
                TimeRange = "12:30 PM \u2013 1:30 PM",
                Subtitle = "Downtown Caf\u00e9",
                CalendarColor = "#E85D75",
            },
            new EventItem
            {
                Title = "Sprint planning",
                TimeRange = "2:00 PM \u2013 3:00 PM",
                Subtitle = "Microsoft Teams Meeting",
                CalendarColor = "#4A90D9",
                MeetingUrl = "https://example.com/meeting2",
            },
            new EventItem
            {
                Title = "Dentist appointment",
                TimeRange = "4:30 PM \u2013 5:30 PM",
                Subtitle = "123 Health St",
                CalendarColor = "#50C878",
            },
        ];

        events[today.AddDays(1)] =
        [
            new EventItem
            {
                Title = "Project deadline",
                IsAllDay = true,
                CalendarColor = "#E85D75",
            },
            new EventItem
            {
                Title = "1:1 with manager",
                TimeRange = "10:00 AM \u2013 10:30 AM",
                CalendarColor = "#4A90D9",
                MeetingUrl = "https://example.com/meeting3",
            },
            new EventItem
            {
                Title = "Design review",
                TimeRange = "2:00 PM \u2013 3:00 PM",
                Subtitle = "Conference Room B",
                CalendarColor = "#7B68EE",
            },
        ];

        events[today.AddDays(2)] =
        [
            new EventItem
            {
                Title = "Gym session",
                TimeRange = "7:00 AM \u2013 8:00 AM",
                CalendarColor = "#50C878",
            },
            new EventItem
            {
                Title = "Architecture sync",
                TimeRange = "11:00 AM \u2013 12:00 PM",
                Subtitle = "Microsoft Teams Meeting",
                CalendarColor = "#4A90D9",
                MeetingUrl = "https://example.com/meeting4",
            },
        ];

        // --- Past events (last year) ---

        var lastYear = today.AddYears(-1);

        events[lastYear.AddDays(2)] =
        [
            new EventItem
            {
                Title = "Lunch with Sarah",
                TimeRange = "12:00 PM \u2013 1:00 PM",
                Subtitle = "Lakeside Grill",
                CalendarColor = "#E85D75",
            },
            new EventItem
            {
                Title = "Team standup",
                TimeRange = "9:00 AM \u2013 9:15 AM",
                Subtitle = "Microsoft Teams Meeting",
                CalendarColor = "#4A90D9",
                MeetingUrl = "https://example.com/meeting-old1",
            },
        ];

        events[lastYear.AddDays(30)] =
        [
            new EventItem
            {
                Title = "Quarterly review",
                TimeRange = "10:00 AM \u2013 11:30 AM",
                Subtitle = "Board Room",
                CalendarColor = "#7B68EE",
            },
            new EventItem
            {
                Title = "Lunch with Sarah",
                TimeRange = "12:30 PM \u2013 1:30 PM",
                Subtitle = "Sushi Palace",
                CalendarColor = "#E85D75",
            },
        ];

        events[lastYear.AddDays(60)] =
        [
            new EventItem
            {
                Title = "Company picnic",
                IsAllDay = true,
                CalendarColor = "#50C878",
            },
            new EventItem
            {
                Title = "Sprint planning",
                TimeRange = "2:00 PM \u2013 3:00 PM",
                Subtitle = "Microsoft Teams Meeting",
                CalendarColor = "#4A90D9",
                MeetingUrl = "https://example.com/meeting-old2",
            },
        ];

        events[lastYear.AddDays(120)] =
        [
            new EventItem
            {
                Title = "Dentist appointment",
                TimeRange = "3:00 PM \u2013 4:00 PM",
                Subtitle = "456 Dental Ave",
                CalendarColor = "#50C878",
            },
        ];

        events[lastYear.AddDays(200)] =
        [
            new EventItem
            {
                Title = "Holiday party",
                TimeRange = "6:00 PM \u2013 10:00 PM",
                Subtitle = "Grand Ballroom",
                CalendarColor = "#E85D75",
            },
            new EventItem
            {
                Title = "1:1 with manager",
                TimeRange = "10:00 AM \u2013 10:30 AM",
                CalendarColor = "#4A90D9",
                MeetingUrl = "https://example.com/meeting-old3",
            },
        ];

        // --- Future events (next year) ---

        var nextYear = today.AddYears(1);

        events[nextYear.AddDays(-10)] =
        [
            new EventItem
            {
                Title = "Lunch with Sarah",
                TimeRange = "12:00 PM \u2013 1:00 PM",
                Subtitle = "Thai Garden",
                CalendarColor = "#E85D75",
            },
            new EventItem
            {
                Title = "Budget planning",
                TimeRange = "3:00 PM \u2013 4:30 PM",
                Subtitle = "Finance Room",
                CalendarColor = "#7B68EE",
            },
        ];

        events[nextYear.AddDays(5)] =
        [
            new EventItem
            {
                Title = "Team standup",
                TimeRange = "9:00 AM \u2013 9:15 AM",
                Subtitle = "Microsoft Teams Meeting",
                CalendarColor = "#4A90D9",
                MeetingUrl = "https://example.com/meeting-future1",
            },
            new EventItem
            {
                Title = "Design review",
                TimeRange = "2:00 PM \u2013 3:00 PM",
                Subtitle = "Conference Room A",
                CalendarColor = "#7B68EE",
            },
        ];

        events[nextYear.AddDays(45)] =
        [
            new EventItem
            {
                Title = "Conference keynote",
                TimeRange = "9:00 AM \u2013 12:00 PM",
                Subtitle = "Convention Center",
                CalendarColor = "#4A90D9",
            },
            new EventItem
            {
                Title = "Lunch with Sarah",
                TimeRange = "12:30 PM \u2013 1:30 PM",
                Subtitle = "Rooftop Bar",
                CalendarColor = "#E85D75",
            },
        ];

        events[nextYear.AddDays(90)] =
        [
            new EventItem
            {
                Title = "Sprint planning",
                TimeRange = "2:00 PM \u2013 3:00 PM",
                Subtitle = "Zoom Meeting",
                CalendarColor = "#4A90D9",
                MeetingUrl = "https://example.com/meeting-future2",
            },
            new EventItem
            {
                Title = "Gym session",
                TimeRange = "7:00 AM \u2013 8:00 AM",
                CalendarColor = "#50C878",
            },
        ];

        events[nextYear.AddDays(180)] =
        [
            new EventItem
            {
                Title = "Annual review",
                TimeRange = "10:00 AM \u2013 11:00 AM",
                Subtitle = "HR Office",
                CalendarColor = "#7B68EE",
            },
        ];

        // --- A few more in the current month (past days) ---

        events[today.AddDays(-5)] =
        [
            new EventItem
            {
                Title = "Lunch with Sarah",
                TimeRange = "12:00 PM \u2013 1:00 PM",
                Subtitle = "Pizza Place",
                CalendarColor = "#E85D75",
            },
            new EventItem
            {
                Title = "Code review",
                TimeRange = "3:00 PM \u2013 4:00 PM",
                Subtitle = "Microsoft Teams Meeting",
                CalendarColor = "#4A90D9",
                MeetingUrl = "https://example.com/meeting5",
            },
        ];

        events[today.AddDays(-15)] =
        [
            new EventItem
            {
                Title = "Team offsite",
                IsAllDay = true,
                CalendarColor = "#50C878",
            },
            new EventItem
            {
                Title = "Sprint planning",
                TimeRange = "2:00 PM \u2013 3:00 PM",
                Subtitle = "Microsoft Teams Meeting",
                CalendarColor = "#4A90D9",
                MeetingUrl = "https://example.com/meeting6",
            },
        ];

        return events;
    }
}
