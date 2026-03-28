using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace Kava.Desktop;

public partial class MainWindow : Window
{
    private readonly Dictionary<DateOnly, List<EventItem>> _sampleEvents;
    private readonly Dictionary<DateOnly, Button> _dayButtons = new();

    private DateOnly _selectedDate = DateOnly.FromDateTime(DateTime.Today);
    private DateOnly _sidebarMonth;

    public MainWindow()
    {
        InitializeComponent();
        Icon = TrayIconManager.CreateIcon();

        _sampleEvents = SampleData.CreateSampleEvents();
        _sidebarMonth = new DateOnly(_selectedDate.Year, _selectedDate.Month, 1);

        BuildSidebarDowHeader();
        BuildSidebarMonth();
        SelectDate(_selectedDate);

        SearchBox.TextChanged += OnSearchTextChanged;
    }

    private bool _isSearching;

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        var query = SearchBox.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(query))
        {
            _isSearching = false;
            SelectDate(_selectedDate); // restore normal view
            return;
        }

        _isSearching = true;
        ShowSearchResults(query);
    }

    private void ShowSearchResults(string query)
    {
        MainEventList.Children.Clear();

        var results = _sampleEvents
            .SelectMany(kv => kv.Value.Select(ev => (Date: kv.Key, Event: ev)))
            .Where(x => x.Event.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                      || (x.Event.Subtitle?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(x => x.Date)
            .GroupBy(x => new DateOnly(x.Date.Year, x.Date.Month, 1));

        var anyResults = false;
        foreach (var monthGroup in results)
        {
            anyResults = true;

            // Month heading
            MainEventList.Children.Add(new TextBlock
            {
                Text = new DateTime(monthGroup.Key.Year, monthGroup.Key.Month, 1).ToString("MMMM yyyy"),
                FontSize = 16,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brush.Parse("#CCCCCC"),
                Margin = new Thickness(0, 12, 0, 4),
            });

            foreach (var (date, evt) in monthGroup)
            {
                // Date + event card
                var dateLabel = new TextBlock
                {
                    Text = date.ToString("ddd, MMM d"),
                    FontSize = 12,
                    Foreground = Brush.Parse("#888888"),
                    Margin = new Thickness(0, 4, 0, 0),
                };
                MainEventList.Children.Add(dateLabel);
                MainEventList.Children.Add(CreateEventCard(evt, isAllDay: evt.IsAllDay));
            }
        }

        if (!anyResults)
        {
            MainEventList.Children.Add(new TextBlock
            {
                Text = $"No events matching \"{query}\"",
                FontSize = 14,
                Foreground = Brush.Parse("#666666"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 60, 0, 0),
            });
        }

        MainDateHeading.Text = "Search Results";
        MainEmptyState.IsVisible = false;
    }

    private void BuildSidebarDowHeader()
    {
        SidebarDowHeader.ColumnDefinitions.Clear();
        SidebarDowHeader.Children.Clear();
        for (var c = 0; c < 7; c++)
            SidebarDowHeader.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

        string[] dayNames = ["Su", "Mo", "Tu", "We", "Th", "Fr", "Sa"];
        for (var c = 0; c < 7; c++)
        {
            var lbl = new TextBlock
            {
                Text = dayNames[c],
                FontSize = 12,
                Foreground = Brush.Parse("#888888"),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            Grid.SetColumn(lbl, c);
            SidebarDowHeader.Children.Add(lbl);
        }
    }

    private void BuildSidebarMonth()
    {
        SidebarMonthStack.Children.Clear();
        _dayButtons.Clear();

        SidebarMonthHeading.Text = new DateTime(_sidebarMonth.Year, _sidebarMonth.Month, 1)
            .ToString("MMMM yyyy");

        var today = DateOnly.FromDateTime(DateTime.Today);
        var daysInMonth = DateTime.DaysInMonth(_sidebarMonth.Year, _sidebarMonth.Month);
        var startDow = (int)_sidebarMonth.DayOfWeek;
        var day = 1;

        for (var row = 0; row < 6; row++)
        {
            var weekGrid = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            for (var c = 0; c < 7; c++)
                weekGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            for (var col = 0; col < 7; col++)
            {
                if ((row == 0 && col < startDow) || day > daysInMonth)
                    continue;

                var date = new DateOnly(_sidebarMonth.Year, _sidebarMonth.Month, day);
                var isToday = date == today;
                var isSelected = date == _selectedDate;

                var dayNumber = new TextBlock
                {
                    Text = day.ToString(),
                    FontSize = 13,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = isSelected
                        ? Brushes.White
                        : isToday
                            ? Brush.Parse("#60CDFF")
                            : Brush.Parse("#CCCCCC"),
                };

                var cellContent = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                cellContent.Children.Add(dayNumber);

                if (_sampleEvents.ContainsKey(date))
                {
                    cellContent.Children.Add(new Ellipse
                    {
                        Width = 4, Height = 4,
                        Fill = Brush.Parse("#60CDFF"),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 1, 0, 0),
                    });
                }

                var btn = new Button
                {
                    Width = 30, Height = 30,
                    Padding = new Thickness(0),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    CornerRadius = new CornerRadius(15),
                    Background = isSelected
                        ? Brush.Parse("#C77DBA")
                        : Brushes.Transparent,
                    BorderBrush = Brushes.Transparent,
                    Content = cellContent,
                    Tag = date,
                };
                btn.Click += SidebarDay_Click;
                Grid.SetColumn(btn, col);
                weekGrid.Children.Add(btn);
                _dayButtons[date] = btn;
                day++;
            }
            SidebarMonthStack.Children.Add(weekGrid);
        }
    }

    private void SelectDate(DateOnly date)
    {
        if (_isSearching)
        {
            _isSearching = false;
            SearchBox.Text = "";
        }

        var oldDate = _selectedDate;
        _selectedDate = date;

        // Update sidebar selection
        UpdateDaySelection(oldDate, date);

        // Navigate sidebar month if needed
        var dateMonth = new DateOnly(date.Year, date.Month, 1);
        if (_sidebarMonth != dateMonth)
        {
            _sidebarMonth = dateMonth;
            BuildSidebarMonth();
        }

        // Update headings
        SidebarDateHeading.Text = date.ToString("MMMM d");
        MainDateHeading.Text = date.ToDateTime(TimeOnly.MinValue).ToString("dddd, MMMM d, yyyy");

        // Build main event list
        while (MainEventList.Children.Count > 1)
            MainEventList.Children.RemoveAt(0);

        // Build sidebar mini-agenda (keep heading as first child)
        while (SidebarAgendaPanel.Children.Count > 1)
            SidebarAgendaPanel.Children.RemoveAt(0);

        if (_sampleEvents.TryGetValue(date, out var events) && events.Count > 0)
        {
            MainEmptyState.IsVisible = false;

            var allDay = events.Where(e => e.IsAllDay);
            var timed = events.Where(e => !e.IsAllDay);

            var mainIndex = 0;
            foreach (var evt in allDay)
                MainEventList.Children.Insert(mainIndex++, CreateEventCard(evt, isAllDay: true));
            foreach (var evt in timed)
                MainEventList.Children.Insert(mainIndex++, CreateEventCard(evt, isAllDay: false));

            // Sidebar mini-agenda
            foreach (var evt in allDay.Concat(timed))
                SidebarAgendaPanel.Children.Add(CreateMiniEventCard(evt));
        }
        else
        {
            MainEmptyState.IsVisible = true;
        }
    }

    private void UpdateDaySelection(DateOnly oldDate, DateOnly newDate)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        if (_dayButtons.TryGetValue(oldDate, out var oldBtn))
        {
            oldBtn.Background = Brushes.Transparent;
            if (oldBtn.Content is StackPanel { Children: [TextBlock oldText, ..] })
            {
                oldText.Foreground = oldDate == today
                    ? Brush.Parse("#60CDFF")
                    : Brush.Parse("#CCCCCC");
            }
        }

        if (_dayButtons.TryGetValue(newDate, out var newBtn))
        {
            newBtn.Background = Brush.Parse("#C77DBA");
            if (newBtn.Content is StackPanel { Children: [TextBlock newText, ..] })
            {
                newText.Foreground = Brushes.White;
            }
        }
    }

    private static Control CreateEventCard(EventItem evt, bool isAllDay)
    {
        var colorBar = new Border
        {
            Background = Brush.Parse(evt.CalendarColor),
            CornerRadius = new CornerRadius(2),
            Width = 4,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        var title = new TextBlock
        {
            Text = evt.Title,
            FontSize = 15,
            Foreground = Brushes.White,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var textStack = new StackPanel { Spacing = 2 };
        textStack.Children.Add(title);

        if (isAllDay)
        {
            textStack.Children.Add(new TextBlock
            {
                Text = "All day",
                FontSize = 13,
                Foreground = Brush.Parse("#999999"),
            });
        }
        else
        {
            if (!string.IsNullOrEmpty(evt.TimeRange))
            {
                textStack.Children.Add(new TextBlock
                {
                    Text = evt.TimeRange,
                    FontSize = 13,
                    Foreground = Brush.Parse("#999999"),
                });
            }

            if (!string.IsNullOrEmpty(evt.Subtitle))
            {
                textStack.Children.Add(new TextBlock
                {
                    Text = evt.Subtitle,
                    FontSize = 12,
                    Foreground = Brush.Parse("#666666"),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                });
            }
        }

        var grid = new Grid
        {
            Margin = new Thickness(0, 4, 0, 4),
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition(4, GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new ColumnDefinition(12, GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

        Grid.SetColumn(colorBar, 0);
        Grid.SetColumn(textStack, 2);
        grid.Children.Add(colorBar);
        grid.Children.Add(textStack);

        if (!string.IsNullOrEmpty(evt.MeetingUrl))
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var joinButton = new Button
            {
                Content = new TextBlock
                {
                    Text = "Join",
                    FontSize = 12,
                    Foreground = Brushes.White,
                },
                Background = Brush.Parse("#0E639C"),
                BorderBrush = Brushes.Transparent,
                Padding = new Thickness(12, 5),
                CornerRadius = new CornerRadius(4),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                Tag = evt.MeetingUrl,
            };
            joinButton.Click += JoinMeeting_Click;
            Grid.SetColumn(joinButton, 3);
            grid.Children.Add(joinButton);
        }

        return new Border
        {
            Child = grid,
            Padding = new Thickness(12, 8),
            CornerRadius = new CornerRadius(6),
            Background = Brush.Parse("#2A2A2A"),
        };
    }

    private static Control CreateMiniEventCard(EventItem evt)
    {
        var dot = new Ellipse
        {
            Width = 6, Height = 6,
            Fill = Brush.Parse(evt.CalendarColor),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        };

        var text = new TextBlock
        {
            Text = evt.IsAllDay
                ? evt.Title
                : $"{evt.TimeRange?.Split('–', '—', '-').FirstOrDefault()?.Trim()} {evt.Title}",
            FontSize = 11,
            Foreground = Brush.Parse("#BBBBBB"),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { dot, text },
        };
    }

    private static void JoinMeeting_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string url } && Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == "https" || uri.Scheme == "http"))
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
    }

    private void SidebarDay_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DateOnly date })
            SelectDate(date);
    }

    private void PrevMonth_Click(object? sender, RoutedEventArgs e)
    {
        _sidebarMonth = _sidebarMonth.AddMonths(-1);
        BuildSidebarMonth();
    }

    private void NextMonth_Click(object? sender, RoutedEventArgs e)
    {
        _sidebarMonth = _sidebarMonth.AddMonths(1);
        BuildSidebarMonth();
    }

    private void GoToToday_Click(object? sender, RoutedEventArgs e)
    {
        SelectDate(DateOnly.FromDateTime(DateTime.Today));
    }
}
