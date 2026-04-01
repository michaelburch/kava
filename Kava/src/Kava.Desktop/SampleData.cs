namespace Kava.Desktop;

public static class SampleData
{
    private const string ExampleDomain = "example.com";
    private const string LunchWithSarahTitle = "Lunch with Sarah";
    private const string PersonalCalendarColor = "#E85D75";
    private const string SprintPlanningTimeRange = "2:00 PM \u2013 3:00 PM";
    private const string SprintPlanningTitle = "Sprint planning";
    private const string TeamsMeetingSubtitle = "Microsoft Teams Meeting";
    private const string WellnessCalendarColor = "#50C878";
    private const string WorkCalendarColor = "#4A90D9";
    private const string WorkFocusCalendarColor = "#7B68EE";

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
                Subtitle = TeamsMeetingSubtitle,
                CalendarColor = WorkCalendarColor,
                MeetingUrl = CreateMeetingUrl("meeting"),
            },
            new EventItem
            {
                Title = "Focus time",
                TimeRange = "10:00 AM \u2013 12:00 PM",
                CalendarColor = WorkFocusCalendarColor,
            },
            new EventItem
            {
                Title = LunchWithSarahTitle,
                TimeRange = "12:30 PM \u2013 1:30 PM",
                Subtitle = "Downtown Caf\u00e9",
                CalendarColor = PersonalCalendarColor,
            },
            new EventItem
            {
                Title = SprintPlanningTitle,
                TimeRange = SprintPlanningTimeRange,
                Subtitle = TeamsMeetingSubtitle,
                CalendarColor = WorkCalendarColor,
                MeetingUrl = CreateMeetingUrl("meeting2"),
            },
            new EventItem
            {
                Title = "Dentist appointment",
                TimeRange = "4:30 PM \u2013 5:30 PM",
                Subtitle = "123 Health St",
                CalendarColor = WellnessCalendarColor,
            },
        ];

        events[today.AddDays(1)] =
        [
            new EventItem
            {
                Title = "Project deadline",
                IsAllDay = true,
                CalendarColor = PersonalCalendarColor,
            },
            new EventItem
            {
                Title = "1:1 with manager",
                TimeRange = "10:00 AM \u2013 10:30 AM",
                CalendarColor = WorkCalendarColor,
                MeetingUrl = CreateMeetingUrl("meeting3"),
            },
            new EventItem
            {
                Title = "Design review",
                TimeRange = SprintPlanningTimeRange,
                Subtitle = "Conference Room B",
                CalendarColor = WorkFocusCalendarColor,
            },
        ];

        events[today.AddDays(2)] =
        [
            new EventItem
            {
                Title = "Gym session",
                TimeRange = "7:00 AM \u2013 8:00 AM",
                CalendarColor = WellnessCalendarColor,
            },
            new EventItem
            {
                Title = "Architecture sync",
                TimeRange = "11:00 AM \u2013 12:00 PM",
                Subtitle = TeamsMeetingSubtitle,
                CalendarColor = WorkCalendarColor,
                MeetingUrl = CreateMeetingUrl("meeting4"),
            },
        ];

        // --- Past events (last year) ---

        var lastYear = today.AddYears(-1);

        events[lastYear.AddDays(2)] =
        [
            new EventItem
            {
                Title = LunchWithSarahTitle,
                TimeRange = "12:00 PM \u2013 1:00 PM",
                Subtitle = "Lakeside Grill",
                CalendarColor = PersonalCalendarColor,
            },
            new EventItem
            {
                Title = "Team standup",
                TimeRange = "9:00 AM \u2013 9:15 AM",
                Subtitle = TeamsMeetingSubtitle,
                CalendarColor = WorkCalendarColor,
                MeetingUrl = CreateMeetingUrl("meeting-old1"),
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
                Title = LunchWithSarahTitle,
                TimeRange = "12:30 PM \u2013 1:30 PM",
                Subtitle = "Sushi Palace",
                CalendarColor = PersonalCalendarColor,
            },
        ];

        events[lastYear.AddDays(60)] =
        [
            new EventItem
            {
                Title = "Company picnic",
                IsAllDay = true,
                CalendarColor = WellnessCalendarColor,
            },
            new EventItem
            {
                Title = SprintPlanningTitle,
                TimeRange = SprintPlanningTimeRange,
                Subtitle = TeamsMeetingSubtitle,
                CalendarColor = WorkCalendarColor,
                MeetingUrl = CreateMeetingUrl("meeting-old2"),
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
                CalendarColor = PersonalCalendarColor,
            },
            new EventItem
            {
                Title = "1:1 with manager",
                TimeRange = "10:00 AM \u2013 10:30 AM",
                CalendarColor = "#4A90D9",
                MeetingUrl = CreateMeetingUrl("meeting-old3"),
            },
        ];

        // --- Future events (next year) ---

        var nextYear = today.AddYears(1);

        events[nextYear.AddDays(-10)] =
        [
            new EventItem
            {
                Title = LunchWithSarahTitle,
                TimeRange = "12:00 PM \u2013 1:00 PM",
                Subtitle = "Thai Garden",
                CalendarColor = PersonalCalendarColor,
            },
            new EventItem
            {
                Title = "Budget planning",
                TimeRange = "3:00 PM \u2013 4:30 PM",
                Subtitle = "Finance Room",
                CalendarColor = WorkFocusCalendarColor,
            },
        ];

        events[nextYear.AddDays(5)] =
        [
            new EventItem
            {
                Title = "Team standup",
                TimeRange = "9:00 AM \u2013 9:15 AM",
                Subtitle = TeamsMeetingSubtitle,
                CalendarColor = WorkCalendarColor,
                MeetingUrl = CreateMeetingUrl("meeting-future1"),
            },
            new EventItem
            {
                Title = "Design review",
                TimeRange = "2:00 PM \u2013 3:00 PM",
                Subtitle = "Conference Room A",
                CalendarColor = WorkFocusCalendarColor,
            },
        ];

        events[nextYear.AddDays(45)] =
        [
            new EventItem
            {
                Title = "Conference keynote",
                TimeRange = "9:00 AM \u2013 12:00 PM",
                Subtitle = "Convention Center",
                CalendarColor = WorkCalendarColor,
            },
            new EventItem
            {
                Title = LunchWithSarahTitle,
                TimeRange = "12:30 PM \u2013 1:30 PM",
                Subtitle = "Rooftop Bar",
                CalendarColor = PersonalCalendarColor,
            },
        ];

        events[nextYear.AddDays(90)] =
        [
            new EventItem
            {
                Title = SprintPlanningTitle,
                TimeRange = SprintPlanningTimeRange,
                Subtitle = "Zoom Meeting",
                CalendarColor = WorkCalendarColor,
                MeetingUrl = CreateMeetingUrl("meeting-future2"),
            },
            new EventItem
            {
                Title = "Gym session",
                TimeRange = "7:00 AM \u2013 8:00 AM",
                CalendarColor = WellnessCalendarColor,
            },
        ];

        events[nextYear.AddDays(180)] =
        [
            new EventItem
            {
                Title = "Annual review",
                TimeRange = "10:00 AM \u2013 11:00 AM",
                Subtitle = "HR Office",
                CalendarColor = WorkFocusCalendarColor,
            },
        ];

        // --- A few more in the current month (past days) ---

        events[today.AddDays(-5)] =
        [
            new EventItem
            {
                Title = LunchWithSarahTitle,
                TimeRange = "12:00 PM \u2013 1:00 PM",
                Subtitle = "Pizza Place",
                CalendarColor = PersonalCalendarColor,
            },
            new EventItem
            {
                Title = "Code review",
                TimeRange = "3:00 PM \u2013 4:00 PM",
                Subtitle = TeamsMeetingSubtitle,
                CalendarColor = WorkCalendarColor,
                MeetingUrl = CreateMeetingUrl("meeting5"),
            },
        ];

        events[today.AddDays(-15)] =
        [
            new EventItem
            {
                Title = "Team offsite",
                IsAllDay = true,
                CalendarColor = WellnessCalendarColor,
            },
            new EventItem
            {
                Title = SprintPlanningTitle,
                TimeRange = SprintPlanningTimeRange,
                Subtitle = TeamsMeetingSubtitle,
                CalendarColor = WorkCalendarColor,
                MeetingUrl = CreateMeetingUrl("meeting6"),
            },
        ];

        return events;
    }

    private static string CreateMeetingUrl(string path) => $"https://{ExampleDomain}/{path}";
}
