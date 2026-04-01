using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Kava.Desktop;

public partial class FlyoutWindow : Window
{
    private const int FlyoutWidth = 380;
    private const int FlyoutHeight = 560;
    private const int FlyoutMargin = 12;
    private const string AccentBrushKey = "KavaAccent";
    private const string SecondaryTextBrushKey = "KavaTextSecondary";

    private Dictionary<DateOnly, List<EventItem>> _events;

    private DateOnly _selectedDate = DateOnly.FromDateTime(DateTime.Today);
    private bool _monthExpanded = true;
    private readonly List<(DateOnly month, Control panel)> _monthPanels = new();
    private readonly Dictionary<DateOnly, Button> _dayButtons = new();
    private DateOnly _viewMonth;

    public FlyoutWindow()
    {
        InitializeComponent();

        _events = new Dictionary<DateOnly, List<EventItem>>();
        _viewMonth = new DateOnly(_selectedDate.Year, _selectedDate.Month, 1);
        UpdateMonthYearHeading();
        BuildDayOfWeekHeader();

#if SAMPLE_DATA
        _events = SampleData.CreateSampleEvents();
#else
        // Use cached events for instant display
        var today = DateOnly.FromDateTime(DateTime.Today);
        var cached = App.AccountService?.GetCachedEvents(today.AddMonths(-6), today.AddMonths(6));
        if (cached != null)
            _events = cached;
#endif

        BuildDateStrip();
        BuildMultiMonthCalendar();
        SelectDate(_selectedDate);

#if !SAMPLE_DATA
        // Refresh from DB in background for any updates
        Loaded += async (_, _) => await LoadDataAsync();
#endif

        MonthScroller.ScrollChanged += OnMonthScrollChanged;
        Deactivated += OnDeactivated;
        ActualThemeVariantChanged += OnThemeChanged;
    }

#if !SAMPLE_DATA
    private async Task LoadDataAsync()
    {
        var service = App.AccountService;
        if (service == null) return;

        var today = DateOnly.FromDateTime(DateTime.Today);
        var fresh = await service.GetEventsAsync(today.AddMonths(-6), today.AddMonths(6));

        // Skip rebuild if data hasn't changed (cache was already showing it)
        if (ReferenceEquals(fresh, _events)) return;

        _events = fresh;
        BuildDateStrip();
        BuildMultiMonthCalendar();
        SelectDate(_selectedDate);
    }
#endif

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        BuildDateStrip();
        BuildDayOfWeekHeader();
        BuildMultiMonthCalendar();
        SelectDate(_selectedDate);
    }

    public void PositionNearTaskbar()
    {
        var screen = Screens.Primary;
        if (screen == null) return;

        var workArea = screen.WorkingArea;
        var scaling = screen.Scaling;

        var x = (workArea.X + workArea.Width) / scaling - FlyoutWidth - FlyoutMargin;
        var y = (workArea.Y + workArea.Height) / scaling - FlyoutHeight - FlyoutMargin;

        Position = new PixelPoint((int)(x * scaling), (int)(y * scaling));
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        Close();
    }

    private void BuildDateStrip()
    {
        DateStripPanel.Children.Clear();
        var today = DateOnly.FromDateTime(DateTime.Today);

        for (int i = -3; i <= 10; i++)
        {
            var date = today.AddDays(i);
            var isToday = date == today;
            var isSelected = date == _selectedDate;
            var hasEvents = _events.ContainsKey(date);

            var dayLabel = new TextBlock
            {
                Text = date.ToString("ddd").ToUpperInvariant()[..2],
                FontSize = 11,
                Foreground = ThemeHelper.Brush("KavaTextTertiary"),
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            var numLabel = new TextBlock
            {
                Text = date.Day.ToString(),
                FontSize = 16,
                Foreground = DesktopUiFactory.GetDayNumberForeground(isSelected, isToday),
                HorizontalAlignment = HorizontalAlignment.Center,
                FontWeight = isSelected ? FontWeight.SemiBold : FontWeight.Normal,
            };

            var stack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 2,
            };
            stack.Children.Add(dayLabel);
            stack.Children.Add(numLabel);

            if (hasEvents)
            {
                stack.Children.Add(new Ellipse
                {
                    Width = 4, Height = 4,
                    Fill = ThemeHelper.Brush(AccentBrushKey),
                    HorizontalAlignment = HorizontalAlignment.Center,
                });
            }

            var button = new Button
            {
                Width = 44, Height = 56,
                Padding = new Thickness(0),
                Background = isSelected
                    ? ThemeHelper.Brush("KavaSelectedBg")
                    : Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                CornerRadius = new CornerRadius(6),
                Content = stack,
                Tag = date,
            };
            button.Click += DateButton_Click;
            DateStripPanel.Children.Add(button);
        }
    }

    private void SelectDate(DateOnly date)
    {
        var oldDate = _selectedDate;
        _selectedDate = date;
        DateHeading.Text = date.ToString("MMMM d");
        BuildDateStrip();

        if (_monthExpanded)
            UpdateDaySelection(oldDate, date);
        else
            UpdateMonthYearHeading();

        // Keep only EmptyState, remove everything else
        while (EventListPanel.Children.Count > 1)
        {
            EventListPanel.Children.RemoveAt(0);
        }

        if (_events.TryGetValue(date, out var events) && events.Count > 0)
        {
            EmptyState.IsVisible = false;

            var allDayEvents = events.Where(e => e.IsAllDay);
            var timedEvents = events.Where(e => !e.IsAllDay);

            var insertIndex = 0;
            foreach (var evt in allDayEvents)
                EventListPanel.Children.Insert(insertIndex++, CreateEventCard(evt, isAllDay: true));
            foreach (var evt in timedEvents)
                EventListPanel.Children.Insert(insertIndex++, CreateEventCard(evt, isAllDay: false));
        }
        else
        {
            EmptyState.IsVisible = true;
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
                    ? ThemeHelper.Brush(AccentBrushKey)
                    : ThemeHelper.Brush(SecondaryTextBrushKey);
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

    private static Border CreateEventCard(EventItem evt, bool isAllDay) =>
        DesktopUiFactory.CreateEventCard(evt, isAllDay, DesktopUiFactory.FlyoutEventCardStyle);

    private void DateButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DateOnly date })
            SelectDate(date);
    }

    private void ExpandCollapse_Click(object? sender, RoutedEventArgs e)
    {
        _monthExpanded = !_monthExpanded;
        ExpandChevron.Data = _monthExpanded
            ? Geometry.Parse("M 0,5 L 5,0 L 10,5")   // chevron up
            : Geometry.Parse("M 0,0 L 5,5 L 10,0");   // chevron down
        MonthCalendarPanel.IsVisible = _monthExpanded;
        DateStripScroller.IsVisible = !_monthExpanded;
        if (_monthExpanded)
        {
            _viewMonth = new DateOnly(_selectedDate.Year, _selectedDate.Month, 1);
            BuildMultiMonthCalendar();
        }
        UpdateMonthYearHeading();
    }

    private void UpdateMonthYearHeading()
    {
        var month = _monthExpanded ? _viewMonth : new DateOnly(_selectedDate.Year, _selectedDate.Month, 1);
        MonthYearHeading.Text = CreateLocalMonthStart(month).ToString("MMMM yyyy");
    }

    private void BuildDayOfWeekHeader()
    {
        DayOfWeekHeader.ColumnDefinitions.Clear();
        DayOfWeekHeader.Children.Clear();
        for (var c = 0; c < 7; c++)
            DayOfWeekHeader.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

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
            DayOfWeekHeader.Children.Add(lbl);
        }
    }

    private DateOnly? _pendingScrollMonth;

    private void BuildMultiMonthCalendar()
    {
        MonthCalendarStack.Children.Clear();
        _monthPanels.Clear();
        _dayButtons.Clear();

        // Build only 3 months initially for fast display
        for (int offset = -1; offset <= 1; offset++)
        {
            var monthStart = _viewMonth.AddMonths(offset);
            var panel = BuildSingleMonth(monthStart);
            _monthPanels.Add((monthStart, panel));
            MonthCalendarStack.Children.Add(panel);
        }

        _pendingScrollMonth = _viewMonth;
        MonthCalendarStack.LayoutUpdated += OnCalendarLayoutUpdated;

        // Lazily add remaining months after the window is shown
        Dispatcher.UIThread.Post(ExpandMonthRange, DispatcherPriority.Background);
    }

    private void ExpandMonthRange()
    {
        // Add months -12 to -2 at the beginning
        for (int offset = -12; offset <= -2; offset++)
        {
            var monthStart = _viewMonth.AddMonths(offset);
            var panel = BuildSingleMonth(monthStart);
            var insertIdx = offset + 12; // 0..10
            _monthPanels.Insert(insertIdx, (monthStart, panel));
            MonthCalendarStack.Children.Insert(insertIdx, panel);
        }

        // Add months +2 to +12 at the end
        for (int offset = 2; offset <= 12; offset++)
        {
            var monthStart = _viewMonth.AddMonths(offset);
            var panel = BuildSingleMonth(monthStart);
            _monthPanels.Add((monthStart, panel));
            MonthCalendarStack.Children.Add(panel);
        }

        // Re-scroll to current month (positions shifted after insert)
        _pendingScrollMonth = _viewMonth;
        MonthCalendarStack.LayoutUpdated += OnCalendarLayoutUpdated;
    }

    private void OnCalendarLayoutUpdated(object? sender, EventArgs e)
    {
        if (_pendingScrollMonth is not { } target)
            return;

        for (int i = 0; i < _monthPanels.Count; i++)
        {
            if (_monthPanels[i].month == target && _monthPanels[i].panel.Bounds.Height > 0)
            {
                MonthScroller.Offset = new Vector(0, _monthPanels[i].panel.Bounds.Y);
                _pendingScrollMonth = null;
                MonthCalendarStack.LayoutUpdated -= OnCalendarLayoutUpdated;
                return;
            }
        }
    }

    private StackPanel BuildSingleMonth(DateOnly firstOfMonth)
    {
        var panel = new StackPanel();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var daysInMonth = DateTime.DaysInMonth(firstOfMonth.Year, firstOfMonth.Month);
        var startDow = (int)firstOfMonth.DayOfWeek;
        var day = 1;

        for (var row = 0; row < 6; row++)
        {
            var weekGrid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            for (var c = 0; c < 7; c++)
                weekGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            for (var col = 0; col < 7; col++)
            {
                if ((row == 0 && col < startDow) || day > daysInMonth)
                    continue;

                var date = new DateOnly(firstOfMonth.Year, firstOfMonth.Month, day);
                var btn = CreateMonthDayButton(date, day, today);
                btn.Click += MonthDay_Click;
                Grid.SetColumn(btn, col);
                weekGrid.Children.Add(btn);
                _dayButtons[date] = btn;
                day++;
            }
            panel.Children.Add(weekGrid);
        }

        return panel;
    }

    private Button CreateMonthDayButton(DateOnly date, int day, DateOnly today)
    {
        var isToday = date == today;
        var isSelected = date == _selectedDate;

        return DesktopUiFactory.CreateDayButton(
            date,
            isSelected,
            CreateMonthDayContent(day, isSelected, isToday, _events.ContainsKey(date)),
            size: 40,
            cornerRadius: 20,
            selectedBackground: ThemeHelper.Brush("KavaSelection"));
    }

    private static StackPanel CreateMonthDayContent(int day, bool isSelected, bool isToday, bool hasEvents) =>
        DesktopUiFactory.CreateDayCellContent(day, isSelected, isToday, hasEvents, fontSize: 13);

    private void OnMonthScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        var viewportTop = MonthScroller.Offset.Y;
        var viewportHeight = MonthScroller.Viewport.Height;
        var viewportMid = viewportTop + viewportHeight / 2;

        foreach (var (month, panel) in _monthPanels)
        {
            var panelTop = panel.Bounds.Y;
            var panelBottom = panelTop + panel.Bounds.Height;

            if (panelTop <= viewportMid && panelBottom > viewportMid)
            {
                if (_viewMonth != month)
                {
                    _viewMonth = month;
                    MonthYearHeading.Text = CreateLocalMonthStart(month).ToString("MMMM yyyy");
                }
                break;
            }
        }
    }

    private void MonthDay_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DateOnly date })
            SelectDate(date);
    }

    private static void OpenMainWindow_Click(object? sender, RoutedEventArgs e)
    {
        TrayIconManager.Instance?.ShowMainWindow();
    }

    private static DateTime CreateLocalMonthStart(DateOnly month) =>
        new(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Local);

    private void GoToToday_Click(object? sender, RoutedEventArgs e)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        _viewMonth = new DateOnly(today.Year, today.Month, 1);
        BuildMultiMonthCalendar();
        SelectDate(today);
    }
}
