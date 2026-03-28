namespace Kava.Windows;

/// <summary>
/// Provides realistic sample events for the flyout during development.
/// This will be replaced by data from the persistence/sync layer.
/// </summary>
public static class SampleData
{
    public static Dictionary<DateOnly, List<EventItem>> CreateSampleEvents()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var events = new Dictionary<DateOnly, List<EventItem>>();

        // Today's events
        events[today] =
        [
            new EventItem
            {
                Title = "Team standup",
                TimeRange = "9:00 AM – 9:15 AM",
                Subtitle = "Microsoft Teams Meeting",
                CalendarColor = "#4A90D9",
                MeetingUrl = "https://example.com/meeting",
            },
            new EventItem
            {
                Title = "Focus time",
                TimeRange = "10:00 AM – 12:00 PM",
                CalendarColor = "#7B68EE",
            },
            new EventItem
            {
                Title = "Lunch with Sarah",
                TimeRange = "12:30 PM – 1:30 PM",
                Subtitle = "Downtown Café",
                CalendarColor = "#E85D75",
            },
            new EventItem
            {
                Title = "Sprint planning",
                TimeRange = "2:00 PM – 3:00 PM",
                Subtitle = "Microsoft Teams Meeting",
                CalendarColor = "#4A90D9",
                MeetingUrl = "https://example.com/meeting2",
            },
            new EventItem
            {
                Title = "Dentist appointment",
                TimeRange = "4:30 PM – 5:30 PM",
                Subtitle = "123 Health St",
                CalendarColor = "#50C878",
            },
        ];

        // Tomorrow
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
                TimeRange = "10:00 AM – 10:30 AM",
                CalendarColor = "#4A90D9",
                MeetingUrl = "https://example.com/meeting3",
            },
            new EventItem
            {
                Title = "Design review",
                TimeRange = "2:00 PM – 3:00 PM",
                Subtitle = "Conference Room B",
                CalendarColor = "#7B68EE",
            },
        ];

        // Day after tomorrow
        events[today.AddDays(2)] =
        [
            new EventItem
            {
                Title = "Gym session",
                TimeRange = "7:00 AM – 8:00 AM",
                CalendarColor = "#50C878",
            },
            new EventItem
            {
                Title = "Architecture sync",
                TimeRange = "11:00 AM – 12:00 PM",
                Subtitle = "Microsoft Teams Meeting",
                CalendarColor = "#4A90D9",
                MeetingUrl = "https://example.com/meeting4",
            },
        ];

        // A day with no events is implicit (any day not in the dictionary)

        return events;
    }
}
