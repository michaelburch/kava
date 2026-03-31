using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Kava.Desktop;

public partial class MainWindow : Window
{
    private Dictionary<DateOnly, List<EventItem>> _events;
    private readonly Dictionary<DateOnly, Button> _dayButtons = new();
    private List<AccountItem> _accounts;

    private DateOnly _selectedDate = DateOnly.FromDateTime(DateTime.Today);
    private DateOnly _sidebarMonth;
    private string _subscribeColor = "#0877cc";

    // Synology EOS palette (--eos-color-{name}-500 values from Synology CSS)
    private static readonly string[] CalendarColors =
    [
        "#cc4e4e", // rose
        "#d67240", // papaya
        "#db8f2c", // tangerine
        "#e0a416", // mango
        "#e0bb00", // lemon
        "#adb204", // olive
        "#92a822", // lime
        "#75a828", // grass
        "#49a33b", // forest
        "#1c9963", // jade
        "#008e9e", // aqua
        "#0877cc", // azure
        "#3267d1", // denim
        "#5555e0", // mauve
        "#8153db", // lavender
        "#9e4cc7", // lilac
        "#b240af", // mallow
        "#c24279", // camellia
        "#6e6e6e", // smoke
        "#666f80", // iron
    ];

    public MainWindow()
    {
        InitializeComponent();
        Icon = TrayIconManager.CreateIcon();

        _events = new Dictionary<DateOnly, List<EventItem>>();
        _accounts = [];
        _sidebarMonth = new DateOnly(_selectedDate.Year, _selectedDate.Month, 1);

        BuildSidebarDowHeader();
        BuildSidebarMonth();
        SetupSubscribeColorSwatch();

#if SAMPLE_DATA
        _events = SampleData.CreateSampleEvents();
        _accounts = CreateSampleAccounts();
        BuildAccountList();
        SelectDate(_selectedDate);
#else
        Loaded += async (_, _) => await LoadDataAsync();

        if (App.Sync != null)
            App.Sync.SyncCompleted += OnBackgroundSyncCompleted;
#endif

        SearchBox.TextChanged += OnSearchTextChanged;
        ActualThemeVariantChanged += OnThemeChanged;
    }

#if !SAMPLE_DATA
    private async Task LoadDataAsync()
    {
        var service = App.AccountService;
        if (service == null) return;

        var accounts = await service.GetAccountsAsync();
        _accounts = [];
        foreach (var account in accounts)
        {
            var calendars = await service.GetCalendarsAsync(account.AccountId);
            _accounts.Add(new AccountItem
            {
                AccountId = account.AccountId,
                Name = account.DisplayName,
                ServerUrl = account.ServerBaseUrl,
                Username = account.Username,
                Status = account.LastSyncUtc.HasValue ? "Synced" : "Not synced",
                ProviderType = account.ProviderType,
                Calendars = calendars.Select(c => new CalendarInfo
                {
                    CalendarId = c.CalendarId,
                    Name = c.DisplayName,
                    Color = c.EffectiveColor,
                    Enabled = c.IsEnabled,
                }).ToList(),
            });
        }

        // Load events for a wide range (±6 months)
        var today = DateOnly.FromDateTime(DateTime.Today);
        _events = await service.GetEventsAsync(
            today.AddMonths(-6),
            today.AddMonths(6));

        BuildSidebarMonth();
        BuildAccountList();
        SelectDate(_selectedDate);
    }

    private void OnBackgroundSyncCompleted()
    {
        Dispatcher.UIThread.Post(async () => await LoadDataAsync());
    }
#endif

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        BuildSidebarDowHeader();
        BuildSidebarMonth();

        if (AccountsScroller.IsVisible)
            BuildAccountList();
        else if (AccountDetailScroller.IsVisible)
        {
            // Re-show the last account detail if visible
        }
        else
            SelectDate(_selectedDate);
    }

#if SAMPLE_DATA
    private static List<AccountItem> CreateSampleAccounts() =>
    [
        new AccountItem
        {
            Name = "Work Calendar",
            ServerUrl = "https://cal.example.com/work",
            Username = "michael@example.com",
            Status = "Synced",
            Calendars =
            [
                new CalendarInfo { Name = "Work", Color = "#4A90D9", Enabled = true },
                new CalendarInfo { Name = "Team Events", Color = "#7B68EE", Enabled = true },
                new CalendarInfo { Name = "Holidays", Color = "#50C878", Enabled = false },
            ],
        },
        new AccountItem
        {
            Name = "Personal",
            ServerUrl = "https://cloud.example.com/dav",
            Username = "michael@personal.com",
            Status = "Synced",
            Calendars =
            [
                new CalendarInfo { Name = "Personal", Color = "#E85D75", Enabled = true },
                new CalendarInfo { Name = "Family", Color = "#F5A623", Enabled = true },
                new CalendarInfo { Name = "Birthdays", Color = "#BD93F9", Enabled = true },
            ],
        },
    ];
#endif

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

        var results = _events
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
                Foreground = ThemeHelper.Brush("KavaTextSecondary"),
                Margin = new Thickness(0, 12, 0, 4),
            });

            foreach (var (date, evt) in monthGroup)
            {
                // Date + event card
                var dateLabel = new TextBlock
                {
                    Text = date.ToString("ddd, MMM d"),
                    FontSize = 12,
                    Foreground = ThemeHelper.Brush("KavaTextMuted"),
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
                Foreground = ThemeHelper.Brush("KavaTextQuaternary"),
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
                Foreground = ThemeHelper.Brush("KavaTextMuted"),
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
                            ? ThemeHelper.Brush("KavaAccent")
                            : ThemeHelper.Brush("KavaTextSecondary"),
                };

                var cellContent = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                cellContent.Children.Add(dayNumber);

                if (_events.ContainsKey(date))
                {
                    cellContent.Children.Add(new Ellipse
                    {
                        Width = 4, Height = 4,
                        Fill = ThemeHelper.Brush("KavaAccent"),
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
                        ? ThemeHelper.Brush("KavaSelection")
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

        // Build main event list with surrounding context
        MainEventList.Children.Clear();
        MainEmptyState.IsVisible = false;
        BuildAgendaView(date);
        ScrollToSelectedDay(date);

        RefreshSidebarAgenda();
    }

    private void RefreshSidebarAgenda()
    {
        // Rebuild sidebar mini-agenda (keep heading as first child)
        while (SidebarAgendaPanel.Children.Count > 1)
            SidebarAgendaPanel.Children.RemoveAt(SidebarAgendaPanel.Children.Count - 1);

        if (_events.TryGetValue(_selectedDate, out var events) && events.Count > 0)
        {
            var allDay = events.Where(e => e.IsAllDay);
            var timed = events.Where(e => !e.IsAllDay);
            foreach (var evt in allDay.Concat(timed))
                SidebarAgendaPanel.Children.Add(CreateMiniEventCard(evt));
        }
    }

    private void BuildAgendaView(DateOnly selectedDate)
    {
        // Collect days to show: try the week first, fall back to the month
        var weekStart = selectedDate.AddDays(-(int)selectedDate.DayOfWeek);
        var weekDays = Enumerable.Range(0, 7).Select(i => weekStart.AddDays(i)).ToList();

        var daysWithEvents = weekDays.Where(d => _events.ContainsKey(d)).ToList();

        List<DateOnly> daysToShow;
        if (daysWithEvents.Count > 0 || _events.ContainsKey(selectedDate))
        {
            // Show the full week
            daysToShow = weekDays;
        }
        else
        {
            // No events this week — show all days in the month that have events, plus the selected day
            var monthStart = new DateOnly(selectedDate.Year, selectedDate.Month, 1);
            var daysInMonth = DateTime.DaysInMonth(selectedDate.Year, selectedDate.Month);
            daysToShow = Enumerable.Range(0, daysInMonth)
                .Select(i => monthStart.AddDays(i))
                .Where(d => _events.ContainsKey(d) || d == selectedDate)
                .ToList();

            if (!daysToShow.Contains(selectedDate))
                daysToShow.Add(selectedDate);
            daysToShow.Sort();
        }

        var today = DateOnly.FromDateTime(DateTime.Today);

        foreach (var day in daysToShow)
        {
            var isSelected = day == selectedDate;
            var hasEvents = _events.TryGetValue(day, out var dayEvents) && dayEvents.Count > 0;

            // Day header
            var headerText = day == today
                ? $"{day:dddd, MMMM d} — Today"
                : $"{day:dddd, MMMM d}";

            var dayHeader = new TextBlock
            {
                Text = headerText,
                FontSize = isSelected ? 15 : 13,
                FontWeight = isSelected ? FontWeight.SemiBold : FontWeight.Normal,
                Foreground = isSelected
                    ? ThemeHelper.Brush("KavaTextPrimary")
                    : ThemeHelper.Brush("KavaTextSubtle"),
                Margin = new Thickness(0, 10, 0, 2),
                Tag = day,
            };
            MainEventList.Children.Add(dayHeader);

            if (hasEvents)
            {
                var allDay = dayEvents!.Where(e => e.IsAllDay);
                var timed = dayEvents!.Where(e => !e.IsAllDay);
                foreach (var evt in allDay)
                    MainEventList.Children.Add(CreateEventCard(evt, isAllDay: true));
                foreach (var evt in timed)
                    MainEventList.Children.Add(CreateEventCard(evt, isAllDay: false));
            }
            else
            {
                MainEventList.Children.Add(new TextBlock
                {
                    Text = "No events",
                    FontSize = 12,
                    Foreground = ThemeHelper.Brush("KavaTextDisabled"),
                    FontStyle = FontStyle.Italic,
                    Margin = new Thickness(4, 2, 0, 0),
                });
            }
        }
    }

    private void ScrollToSelectedDay(DateOnly date)
    {
        // Find the tagged header for the selected date
        var header = MainEventList.Children
            .OfType<TextBlock>()
            .FirstOrDefault(tb => tb.Tag is DateOnly d && d == date);

        if (header == null) return;

        // Defer until layout is complete so positions are computed
        Dispatcher.UIThread.Post(() =>
        {
            var transform = header.TranslatePoint(new Point(0, 0), MainEventList);
            if (transform is { } pos)
            {
                var viewportHeight = AgendaScroller.Viewport.Height;
                var target = pos.Y - (viewportHeight / 2) + (header.Bounds.Height / 2);
                AgendaScroller.Offset = new Vector(0, Math.Max(0, target));
            }
        }, DispatcherPriority.Loaded);
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
                    ? ThemeHelper.Brush("KavaAccent")
                    : ThemeHelper.Brush("KavaTextSecondary");
            }
        }

        if (_dayButtons.TryGetValue(newDate, out var newBtn))
        {
            newBtn.Background = ThemeHelper.Brush("KavaSelection");
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
            Foreground = ThemeHelper.Brush("KavaTextPrimary"),
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
                Foreground = ThemeHelper.Brush("KavaTextTertiary"),
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
                    Foreground = ThemeHelper.Brush("KavaTextTertiary"),
                });
            }

            if (!string.IsNullOrEmpty(evt.Subtitle))
            {
                textStack.Children.Add(new TextBlock
                {
                    Text = evt.Subtitle,
                    FontSize = 12,
                    Foreground = ThemeHelper.Brush("KavaTextQuaternary"),
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
                Background = ThemeHelper.Brush("KavaAction"),
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
            Background = ThemeHelper.Brush("KavaCardBg"),
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
            Foreground = ThemeHelper.Brush("KavaTextSubtle"),
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
        {
            ShowAgendaView();
            SelectDate(date);
        }
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
        ShowAgendaView();
        SelectDate(DateOnly.FromDateTime(DateTime.Today));
    }

    private void Accounts_Click(object? sender, RoutedEventArgs e)
    {
        if (AccountsScroller.IsVisible || AccountDetailScroller.IsVisible)
        {
            ShowAgendaView();
            SelectDate(_selectedDate);
        }
        else
        {
            ShowAccountsView();
        }
    }

    private void ShowAccountsView()
    {
        AgendaScroller.IsVisible = false;
        AccountDetailScroller.IsVisible = false;
        AccountsScroller.IsVisible = true;
        MainDateHeading.Text = "Accounts";
        AccountsButtonText.Text = "Today";
        BuildAccountList();
    }

    private void ShowAgendaView()
    {
        AgendaScroller.IsVisible = true;
        AccountsScroller.IsVisible = false;
        AccountDetailScroller.IsVisible = false;
        AccountsButtonText.Text = "Accounts";
    }

    private void BuildAccountList()
    {
        AccountListPanel.Children.Clear();
        foreach (var account in _accounts)
        {
            var statusColor = account.Status == "Synced"
                ? ThemeHelper.Brush("KavaSuccess")
                : ThemeHelper.Brush("KavaWarning");

            var dot = new Ellipse
            {
                Width = 10, Height = 10,
                Fill = statusColor,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
            };

            var nameBlock = new TextBlock
            {
                Text = account.Name,
                FontSize = 14,
                Foreground = ThemeHelper.Brush("KavaTextPrimary"),
            };

            var urlBlock = new TextBlock
            {
                Text = account.ServerUrl,
                FontSize = 11,
                Foreground = ThemeHelper.Brush("KavaTextMuted"),
            };

            var calCount = new TextBlock
            {
                Text = account.ProviderType == Kava.Core.Models.ProviderType.IcsSubscription
                    ? "ICS Subscription"
                    : $"{account.Calendars.Count(c => c.Enabled)} of {account.Calendars.Count} calendars",
                FontSize = 11,
                Foreground = ThemeHelper.Brush("KavaTextQuaternary"),
            };

            var infoStack = new StackPanel { Spacing = 2 };
            infoStack.Children.Add(nameBlock);
            infoStack.Children.Add(urlBlock);
            infoStack.Children.Add(calCount);

            var statusBlock = new TextBlock
            {
                Text = account.Status,
                FontSize = 12,
                Foreground = statusColor,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var chevron = new TextBlock
            {
                Text = ">",
                FontSize = 16,
                Foreground = ThemeHelper.Brush("KavaTextQuaternary"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            Grid.SetColumn(dot, 0);
            Grid.SetColumn(infoStack, 1);
            Grid.SetColumn(statusBlock, 2);
            Grid.SetColumn(chevron, 3);
            grid.Children.Add(dot);
            grid.Children.Add(infoStack);
            grid.Children.Add(statusBlock);
            grid.Children.Add(chevron);

            var card = new Button
            {
                Content = grid,
                Background = ThemeHelper.Brush("KavaCardBg"),
                BorderBrush = Brushes.Transparent,
                Padding = new Thickness(16),
                CornerRadius = new CornerRadius(8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Tag = account,
            };
            card.Click += AccountCard_Click;

            AccountListPanel.Children.Add(card);
        }
    }

    private void AccountCard_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AccountItem account })
            ShowAccountDetail(account);
    }

    private void ShowAccountDetail(AccountItem account)
    {
        AgendaScroller.IsVisible = false;
        AccountsScroller.IsVisible = false;
        AccountDetailScroller.IsVisible = true;
        MainDateHeading.Text = account.Name;
        AccountsButtonText.Text = "Today";

        AccountDetailPanel.Children.Clear();

        // Back button
        var backButton = new Button
        {
            Content = new TextBlock
            {
                Text = "\u2190 Back to Accounts",
                FontSize = 13,
                Foreground = ThemeHelper.Brush("KavaAccent"),
            },
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            Padding = new Thickness(0, 0, 0, 4),
        };
        backButton.Click += (_, _) => ShowAccountsView();
        AccountDetailPanel.Children.Add(backButton);

        // Account settings section
        var settingsCard = new Border
        {
            Background = ThemeHelper.Brush("KavaCardBg"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20),
        };

        var settingsStack = new StackPanel { Spacing = 12 };
        var isSubscription = account.ProviderType == Kava.Core.Models.ProviderType.IcsSubscription;

        settingsStack.Children.Add(new TextBlock
        {
            Text = isSubscription ? "Subscription Settings" : "Account Settings",
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = ThemeHelper.Brush("KavaTextPrimary"),
        });

        var saveButton = new Button
        {
            Content = "Save Changes",
            Background = ThemeHelper.Brush("KavaAction"),
            Foreground = Brushes.White,
            BorderBrush = Brushes.Transparent,
            Padding = new Thickness(16, 8),
            CornerRadius = new CornerRadius(4),
            FontSize = 13,
            IsEnabled = false,
            Opacity = 0.5,
        };

        void CheckForChanges(object? s, TextChangedEventArgs e2)
        {
            var nameField = settingsStack.Children.OfType<Grid>().ElementAtOrDefault(0)
                ?.Children.OfType<TextBox>().FirstOrDefault();
            var urlField = settingsStack.Children.OfType<Grid>().ElementAtOrDefault(1)
                ?.Children.OfType<TextBox>().FirstOrDefault();

            bool changed;
            if (isSubscription)
            {
                changed = nameField?.Text != account.Name
                       || urlField?.Text != account.ServerUrl;
            }
            else
            {
                var userField = settingsStack.Children.OfType<Grid>().ElementAtOrDefault(2)
                    ?.Children.OfType<TextBox>().FirstOrDefault();
                changed = nameField?.Text != account.Name
                       || urlField?.Text != account.ServerUrl
                       || userField?.Text != account.Username;
            }

            saveButton.IsEnabled = changed;
            saveButton.Opacity = changed ? 1.0 : 0.5;
        }

        settingsStack.Children.Add(CreateFormField("Display name", account.Name, CheckForChanges));
        settingsStack.Children.Add(CreateFormField(isSubscription ? "Calendar URL" : "Server URL", account.ServerUrl, CheckForChanges));
        if (!isSubscription)
            settingsStack.Children.Add(CreateFormField("Username", account.Username, CheckForChanges));

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };
        buttonRow.Children.Add(saveButton);
        var removeButton = new Button
        {
            Content = isSubscription ? "Unsubscribe" : "Remove Account",
            Background = Brushes.Transparent,
            Foreground = ThemeHelper.Brush("KavaDestructive"),
            BorderBrush = ThemeHelper.Brush("KavaDestructive"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16, 8),
            CornerRadius = new CornerRadius(4),
            FontSize = 13,
        };
        removeButton.Click += (_, _) => RemoveAccount_Click(account);
        buttonRow.Children.Add(removeButton);
        settingsStack.Children.Add(buttonRow);

        settingsCard.Child = settingsStack;
        AccountDetailPanel.Children.Add(settingsCard);

        // Calendars section
        AccountDetailPanel.Children.Add(new TextBlock
        {
            Text = "Calendars",
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = ThemeHelper.Brush("KavaTextPrimary"),
            Margin = new Thickness(0, 8, 0, 4),
        });

        foreach (var cal in account.Calendars)
        {
            var calCard = new Border
            {
                Background = ThemeHelper.Brush("KavaCardBg"),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 4),
            };

            var calGrid = new Grid();
            calGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            calGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            calGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            calGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            // Color swatch button
            var colorSwatch = new Border
            {
                Width = 18, Height = 18,
                Background = Brush.Parse(cal.Color),
                CornerRadius = new CornerRadius(3),
            };

            var colorButton = new Button
            {
                Width = 28, Height = 28,
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderBrush = ThemeHelper.Brush("KavaBorderSubtle"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 12, 0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Content = colorSwatch,
                Tag = cal,
            };
            ToolTip.SetTip(colorButton, "Change color");
            colorButton.Click += (s, _) =>
            {
                if (s is not Button { Tag: CalendarInfo ci } btn) return;
                var flyout = new Flyout
                {
                    Placement = PlacementMode.BottomEdgeAlignedLeft,
                    ShowMode = FlyoutShowMode.Standard,
                };

                var wrap = new WrapPanel { MaxWidth = 210 };

                // Build color list: preset palette + current color if not already included
                var colors = CalendarColors.ToList();
                if (!colors.Contains(ci.Color, StringComparer.OrdinalIgnoreCase))
                    colors.Insert(0, ci.Color);

                foreach (var c in colors)
                {
                    var swatchBorder = new Border
                    {
                        Width = 24, Height = 24,
                        Background = Brush.Parse(c),
                        CornerRadius = new CornerRadius(4),
                        BorderThickness = new Thickness(c.Equals(ci.Color, StringComparison.OrdinalIgnoreCase) ? 2 : 0),
                        BorderBrush = ThemeHelper.Brush("KavaTextPrimary"),
                    };

                    var swatchButton = new Button
                    {
                        Width = 30, Height = 30,
                        Padding = new Thickness(0),
                        Margin = new Thickness(2),
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        HorizontalContentAlignment = HorizontalAlignment.Center,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Content = swatchBorder,
                        Tag = c,
                        Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                    };

                    swatchButton.Click += async (sender, _) =>
                    {
                        if (sender is not Button { Tag: string picked }) return;
                        ci.Color = picked;
                        colorSwatch.Background = Brush.Parse(picked);
                        Dispatcher.UIThread.Post(() => flyout.Hide());
#if !SAMPLE_DATA
                        if (!string.IsNullOrEmpty(ci.CalendarId))
                        {
                            Debug.WriteLine($"[Color] Setting calendar color to {picked}");
                            await App.AccountService!.UpdateCalendarColorAsync(ci.CalendarId, picked);
                            // Update cached events so the agenda view reflects the new color immediately
                            foreach (var evtList in _events.Values)
                                foreach (var evt in evtList)
                                    if (evt.CalendarId == ci.CalendarId)
                                        evt.CalendarColor = picked;
                            RefreshSidebarAgenda();
                        }
#endif
                    };

                    wrap.Children.Add(swatchButton);
                }

                flyout.Content = wrap;
                flyout.ShowAt(btn);
            };

            var calName = new TextBlock
            {
                Text = cal.Name,
                FontSize = 14,
                Foreground = cal.Enabled ? ThemeHelper.Brush("KavaTextPrimary") : ThemeHelper.Brush("KavaTextQuaternary"),
                VerticalAlignment = VerticalAlignment.Center,
            };

            var enabledToggle = new CheckBox
            {
                IsChecked = cal.Enabled,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                Tag = cal,
            };

            var capturedCalName = calName;
            var capturedColorButton = colorButton;
            enabledToggle.IsCheckedChanged += async (s, _) =>
            {
                if (s is CheckBox { Tag: CalendarInfo ci, IsChecked: { } isChecked })
                {
                    ci.Enabled = isChecked;
                    capturedCalName.Foreground = isChecked ? ThemeHelper.Brush("KavaTextPrimary") : ThemeHelper.Brush("KavaTextQuaternary");
                    capturedColorButton.Opacity = isChecked ? 1.0 : 0.4;
#if !SAMPLE_DATA
                    if (!string.IsNullOrEmpty(ci.CalendarId))
                        await App.AccountService!.UpdateCalendarEnabledAsync(ci.CalendarId, isChecked);
#endif
                }
            };
            colorButton.Opacity = cal.Enabled ? 1.0 : 0.4;

            Grid.SetColumn(colorButton, 0);
            Grid.SetColumn(calName, 1);
            Grid.SetColumn(enabledToggle, 3);
            calGrid.Children.Add(colorButton);
            calGrid.Children.Add(calName);
            calGrid.Children.Add(enabledToggle);

            calCard.Child = calGrid;
            AccountDetailPanel.Children.Add(calCard);
        }
    }

    private static Grid CreateFormField(string label, string value, EventHandler<TextChangedEventArgs>? onChanged = null)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(100, GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));

        var lbl = new TextBlock
        {
            Text = label,
            FontSize = 13,
            Foreground = ThemeHelper.Brush("KavaTextSecondary"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
        };

        var box = new TextBox
        {
            Text = value,
            FontSize = 13,
            Background = ThemeHelper.Brush("KavaInputBg"),
            Foreground = ThemeHelper.Brush("KavaTextSecondary"),
            BorderBrush = ThemeHelper.Brush("KavaBorder"),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 6),
        };

        if (onChanged != null)
            box.TextChanged += onChanged;

        Grid.SetColumn(lbl, 0);
        Grid.SetColumn(box, 1);
        grid.Children.Add(lbl);
        grid.Children.Add(box);

        return grid;
    }

    private async void AddAccount_Click(object? sender, RoutedEventArgs e)
    {
#if SAMPLE_DATA
        // No-op in sample data mode
        return;
#else
        var name = AccountNameBox.Text?.Trim() ?? "";
        var url = AccountUrlBox.Text?.Trim() ?? "";
        var user = AccountUserBox.Text?.Trim() ?? "";
        var pass = AccountPassBox.Text ?? "";

        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        {
            ShowStatus("Please fill in server URL, username, and password.", isError: true);
            return;
        }

        if (string.IsNullOrEmpty(name))
            name = user;

        AddAccountButton.IsEnabled = false;
        ShowStatus("Connecting...", isError: false);

        var service = App.AccountService;
        if (service == null) return;

        var error = await service.AddAccountAsync(name, url, user, pass);

        if (error != null)
        {
            ShowStatus(error, isError: true);
            AddAccountButton.IsEnabled = true;
            return;
        }

        // Success — clear form and reload
        AccountNameBox.Text = "";
        AccountUrlBox.Text = "";
        AccountUserBox.Text = "";
        AccountPassBox.Text = "";
        ShowStatus("Account added successfully!", isError: false);
        AddAccountButton.IsEnabled = true;

        await LoadDataAsync();
        ShowAccountsView();
#endif
    }

    private async void SyncAll_Click(object? sender, RoutedEventArgs e)
    {
#if SAMPLE_DATA
        return;
#else
        var service = App.AccountService;
        if (service == null) return;

        MainDateHeading.Text = "Syncing...";

        if (App.Sync != null)
            await App.Sync.SyncNowAsync();
        else
            await service.SyncAllAsync();

        // Reload from DB
        await LoadDataAsync();

        if (AccountsScroller.IsVisible)
        {
            MainDateHeading.Text = "Accounts";
            BuildAccountList();
        }
        else
        {
            SelectDate(_selectedDate);
        }
#endif
    }

    private async void RemoveAccount_Click(AccountItem account)
    {
#if SAMPLE_DATA
        return;
#else
        var service = App.AccountService;
        if (service == null) return;

        await service.RemoveAccountAsync(account.AccountId);
        await LoadDataAsync();
        ShowAccountsView();
#endif
    }

    private void ShowStatus(string message, bool isError)
    {
        AddAccountStatus.Text = message;
        AddAccountStatus.Foreground = isError
            ? ThemeHelper.Brush("KavaDestructive")
            : ThemeHelper.Brush("KavaSuccess");
        AddAccountStatus.IsVisible = true;
    }

    private void SetupSubscribeColorSwatch()
    {
        SubscribeColorSwatch.Click += (_, _) =>
        {
            var flyout = new Flyout
            {
                Placement = PlacementMode.BottomEdgeAlignedLeft,
                ShowMode = FlyoutShowMode.Standard,
            };

            var wrap = new WrapPanel { MaxWidth = 210 };
            foreach (var c in CalendarColors)
            {
                var border = new Border
                {
                    Width = 24, Height = 24,
                    Background = Brush.Parse(c),
                    CornerRadius = new CornerRadius(4),
                    BorderThickness = new Thickness(c.Equals(_subscribeColor, StringComparison.OrdinalIgnoreCase) ? 2 : 0),
                    BorderBrush = ThemeHelper.Brush("KavaTextPrimary"),
                };
                var btn = new Button
                {
                    Width = 30, Height = 30,
                    Padding = new Thickness(0),
                    Margin = new Thickness(2),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Content = border,
                    Tag = c,
                    Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                };
                btn.Click += (sender, _) =>
                {
                    if (sender is not Button { Tag: string picked }) return;
                    _subscribeColor = picked;
                    SubscribeColorSwatchInner.Background = Brush.Parse(picked);
                    Dispatcher.UIThread.Post(() => flyout.Hide());
                };
                wrap.Children.Add(btn);
            }

            flyout.Content = wrap;
            flyout.ShowAt(SubscribeColorSwatch);
        };
    }

    private async void Subscribe_Click(object? sender, RoutedEventArgs e)
    {
#if SAMPLE_DATA
        return;
#else
        var name = SubscribeNameBox.Text?.Trim() ?? "";
        var url = SubscribeUrlBox.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(url))
        {
            ShowSubscribeStatus("Please enter a calendar URL.", true);
            return;
        }

        if (string.IsNullOrEmpty(name))
            name = "Subscription";

        SubscribeButton.IsEnabled = false;
        ShowSubscribeStatus("Subscribing...", false);

        var service = App.AccountService;
        if (service == null) return;

        var error = await service.AddSubscriptionAsync(name, url, _subscribeColor);
        if (error != null)
        {
            ShowSubscribeStatus(error, true);
            SubscribeButton.IsEnabled = true;
            return;
        }

        ShowSubscribeStatus($"Subscribed to {name}!", false);
        SubscribeNameBox.Text = "";
        SubscribeUrlBox.Text = "";
        SubscribeButton.IsEnabled = true;

        await LoadDataAsync();
        ShowAccountsView();
#endif
    }

    private void ShowSubscribeStatus(string message, bool isError)
    {
        SubscribeStatus.Text = message;
        SubscribeStatus.Foreground = isError
            ? ThemeHelper.Brush("KavaDestructive")
            : ThemeHelper.Brush("KavaSuccess");
        SubscribeStatus.IsVisible = true;
    }
}
