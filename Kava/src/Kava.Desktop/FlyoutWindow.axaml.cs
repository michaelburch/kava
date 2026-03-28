using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace Kava.Desktop;

public partial class FlyoutWindow : Window
{
    private const int FlyoutWidth = 380;
    private const int FlyoutHeight = 560;
    private const int FlyoutMargin = 12;

    private readonly Dictionary<DateOnly, List<EventItem>> _sampleEvents;

    private DateOnly _selectedDate = DateOnly.FromDateTime(DateTime.Today);
    private bool _monthExpanded = true;

    public FlyoutWindow()
    {
        InitializeComponent();

        _sampleEvents = SampleData.CreateSampleEvents();
        UpdateMonthYearHeading();
        BuildDateStrip();
        BuildMonthCalendar();
        SelectDate(_selectedDate);

        Deactivated += OnDeactivated;
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
            var hasEvents = _sampleEvents.ContainsKey(date);

            var dayLabel = new TextBlock
            {
                Text = date.ToString("ddd").ToUpperInvariant()[..2],
                FontSize = 11,
                Foreground = Brush.Parse("#999999"),
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            var numLabel = new TextBlock
            {
                Text = date.Day.ToString(),
                FontSize = 16,
                Foreground = isSelected
                    ? Brushes.White
                    : isToday
                        ? Brush.Parse("#60CDFF")
                        : Brush.Parse("#CCCCCC"),
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
                    Fill = Brush.Parse("#60CDFF"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                });
            }

            var button = new Button
            {
                Width = 44, Height = 56,
                Padding = new Thickness(0),
                Background = isSelected
                    ? Brush.Parse("#333333")
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
        _selectedDate = date;
        DateHeading.Text = date.ToString("MMMM d");
        UpdateMonthYearHeading();
        BuildDateStrip();

        // Keep only EmptyState, remove everything else
        while (EventListPanel.Children.Count > 1)
        {
            EventListPanel.Children.RemoveAt(0);
        }

        if (_sampleEvents.TryGetValue(date, out var events) && events.Count > 0)
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

        if (_monthExpanded)
            BuildMonthCalendar();
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
            FontSize = 14,
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
                FontSize = 12,
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
                    FontSize = 12,
                    Foreground = Brush.Parse("#999999"),
                });
            }

            if (!string.IsNullOrEmpty(evt.Subtitle))
            {
                textStack.Children.Add(new TextBlock
                {
                    Text = evt.Subtitle,
                    FontSize = 11,
                    Foreground = Brush.Parse("#666666"),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                });
            }
        }

        var grid = new Grid
        {
            Margin = new Thickness(0, 2, 0, 2),
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition(4, GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new ColumnDefinition(10, GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

        Grid.SetColumn(colorBar, 0);
        Grid.SetColumn(textStack, 2);
        grid.Children.Add(colorBar);
        grid.Children.Add(textStack);

        return new Border
        {
            Child = grid,
            Padding = new Thickness(8),
            CornerRadius = new CornerRadius(4),
        };
    }

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
            BuildMonthCalendar();
    }

    private void UpdateMonthYearHeading()
    {
        var d = new DateTime(_selectedDate.Year, _selectedDate.Month, 1);
        MonthYearHeading.Text = d.ToString("MMMM yyyy");
    }

    private void BuildMonthCalendar()
    {
        MonthCalendarStack.Children.Clear();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var firstOfMonth = new DateOnly(_selectedDate.Year, _selectedDate.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(_selectedDate.Year, _selectedDate.Month);

        // Day-of-week header
        var headerRow = new Grid();
        for (var c = 0; c < 7; c++)
            headerRow.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

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
            headerRow.Children.Add(lbl);
        }
        MonthCalendarStack.Children.Add(headerRow);

        var startDow = (int)firstOfMonth.DayOfWeek;
        var day = 1;

        for (var row = 0; row < 6 && day <= daysInMonth; row++)
        {
            var weekGrid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            for (var c = 0; c < 7; c++)
                weekGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            for (var col = 0; col < 7; col++)
            {
                if ((row == 0 && col < startDow) || day > daysInMonth)
                    continue;

                var date = new DateOnly(_selectedDate.Year, _selectedDate.Month, day);
                var isToday = date == today;
                var isSelected = date == _selectedDate;
                var hasEvents = _sampleEvents.ContainsKey(date);

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

                if (hasEvents)
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
                    Width = 40, Height = 40,
                    Padding = new Thickness(0),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    CornerRadius = new CornerRadius(20),
                    Background = isSelected
                        ? Brush.Parse("#C77DBA")
                        : Brushes.Transparent,
                    BorderBrush = Brushes.Transparent,
                    Content = cellContent,
                    Tag = date,
                };
                btn.Click += MonthDay_Click;
                Grid.SetColumn(btn, col);
                weekGrid.Children.Add(btn);
                day++;
            }
            MonthCalendarStack.Children.Add(weekGrid);
        }
    }

    private void MonthDay_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DateOnly date })
            SelectDate(date);
    }
}
