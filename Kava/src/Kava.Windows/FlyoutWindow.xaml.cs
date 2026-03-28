using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;
using Windows.UI;

namespace Kava.Windows;

public sealed partial class FlyoutWindow : Window
{
    private const int FlyoutWidth = 360;
    private const int FlyoutHeight = 560;

    private readonly AppWindow _appWindow;
    private DateOnly _selectedDate = DateOnly.FromDateTime(DateTime.Today);
    private readonly Dictionary<DateOnly, List<EventItem>> _sampleEvents;
    private bool _isClosing;

    public FlyoutWindow()
    {
        this.InitializeComponent();

        var hWnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        // Remove title bar for a flyout feel
        _appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        _appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;

        // Use overlapped presenter to remove standard chrome
        var presenter = OverlappedPresenter.CreateForContextMenu();
        _appWindow.SetPresenter(presenter);

        _appWindow.Resize(new global::Windows.Graphics.SizeInt32(FlyoutWidth, FlyoutHeight));

        // Dismiss on losing focus
        this.Activated += OnActivated;

        // Load sample data and populate UI
        _sampleEvents = SampleData.CreateSampleEvents();
        BuildDateStrip();
        SelectDate(_selectedDate);
    }

    public void PositionNearTaskbar()
    {
        var displayArea = DisplayArea.GetFromWindowId(
            _appWindow.Id, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;

        var x = workArea.X + workArea.Width - FlyoutWidth - 12;
        var y = workArea.Y + workArea.Height - FlyoutHeight - 12;

        _appWindow.Move(new global::Windows.Graphics.PointInt32(x, y));
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
                Foreground = new SolidColorBrush(ColorFromHex("#999999")),
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            var numForeground = isSelected
                ? new SolidColorBrush(Microsoft.UI.Colors.White)
                : isToday
                    ? new SolidColorBrush(ColorFromHex("#60CDFF"))
                    : new SolidColorBrush(ColorFromHex("#CCCCCC"));

            var numLabel = new TextBlock
            {
                Text = date.Day.ToString(),
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = numForeground,
                HorizontalAlignment = HorizontalAlignment.Center,
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
                var dot = new Microsoft.UI.Xaml.Shapes.Ellipse
                {
                    Width = 4,
                    Height = 4,
                    Fill = new SolidColorBrush(ColorFromHex("#60CDFF")),
                    HorizontalAlignment = HorizontalAlignment.Center,
                };
                stack.Children.Add(dot);
            }

            var btn = new Button
            {
                Width = 44,
                Height = 56,
                Padding = new Thickness(0),
                Background = isSelected
                    ? new SolidColorBrush(ColorFromHex("#333333"))
                    : new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                CornerRadius = new CornerRadius(6),
                Content = stack,
                Tag = date,
            };
            btn.Click += DateButton_Click;
            DateStripPanel.Children.Add(btn);
        }
    }

    private void SelectDate(DateOnly date)
    {
        _selectedDate = date;
        DateHeading.Text = date.ToString("MMMM d");
        BuildDateStrip(); // Rebuild to update selection visual

        // Clear event list
        // Remove all children except EmptyState
        while (EventListPanel.Children.Count > 1)
        {
            EventListPanel.Children.RemoveAt(0);
        }

        if (_sampleEvents.TryGetValue(date, out var events) && events.Count > 0)
        {
            EmptyState.Visibility = Visibility.Collapsed;

            // Add all-day events first, then timed
            var allDay = events.Where(e => e.IsAllDay).ToList();
            var timed = events.Where(e => !e.IsAllDay).ToList();

            int insertIndex = 0;
            foreach (var evt in allDay)
            {
                EventListPanel.Children.Insert(insertIndex++, CreateEventCard(evt, isAllDay: true));
            }
            foreach (var evt in timed)
            {
                EventListPanel.Children.Insert(insertIndex++, CreateEventCard(evt, isAllDay: false));
            }
        }
        else
        {
            EmptyState.Visibility = Visibility.Visible;
        }
    }

    private static UIElement CreateEventCard(EventItem evt, bool isAllDay)
    {
        var colorBar = new Border
        {
            Background = evt.CalendarColorBrush,
            CornerRadius = new CornerRadius(2),
            Width = 4,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        var title = new TextBlock
        {
            Text = evt.Title,
            FontSize = 14,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
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
                Foreground = new SolidColorBrush(ColorFromHex("#999999")),
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
                    Foreground = new SolidColorBrush(ColorFromHex("#999999")),
                });
            }
            if (!string.IsNullOrEmpty(evt.Subtitle))
            {
                textStack.Children.Add(new TextBlock
                {
                    Text = evt.Subtitle,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(ColorFromHex("#666666")),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                });
            }
        }

        var grid = new Grid
        {
            Padding = new Thickness(8, 8, 8, 8),
            Margin = new Thickness(0, 2, 0, 2),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(4) },
                new ColumnDefinition { Width = new GridLength(10) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
        };

        Grid.SetColumn(colorBar, 0);
        Grid.SetColumn(textStack, 2);
        grid.Children.Add(colorBar);
        grid.Children.Add(textStack);

        if (!string.IsNullOrEmpty(evt.MeetingUrl))
        {
            var icon = new FontIcon
            {
                Glyph = "\uE714",
                FontSize = 14,
                Foreground = new SolidColorBrush(ColorFromHex("#999999")),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(icon, 3);
            grid.Children.Add(icon);
        }

        return grid;
    }

    private void DateButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DateOnly date)
        {
            SelectDate(date);
        }
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated && !_isClosing)
        {
            _isClosing = true;
            try { this.Close(); } catch { }
        }
    }

    private static global::Windows.UI.Color ColorFromHex(string hex)
    {
        hex = hex.TrimStart('#');
        var r = Convert.ToByte(hex[..2], 16);
        var g = Convert.ToByte(hex[2..4], 16);
        var b = Convert.ToByte(hex[4..6], 16);
        return global::Windows.UI.Color.FromArgb(255, r, g, b);
    }
}
