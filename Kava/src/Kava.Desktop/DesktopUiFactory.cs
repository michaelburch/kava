using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;

namespace Kava.Desktop;

internal readonly record struct EventCardStyle(
    double TitleFontSize,
    double MetadataFontSize,
    double SubtitleFontSize,
    double JoinButtonFontSize,
    Thickness GridMargin,
    double SpacerColumnWidth,
    Thickness JoinButtonPadding,
    Thickness JoinButtonMargin,
    Thickness CardPadding,
    CornerRadius CardCornerRadius,
    string? CardBackgroundBrushKey = null);

internal static class DesktopUiFactory
{
    private const string AccentBrushKey = "KavaAccent";
    private const string ActionBrushKey = "KavaAction";
    private const string PrimaryTextBrushKey = "KavaTextPrimary";
    private const string SecondaryTextBrushKey = "KavaTextSecondary";
    private const string TertiaryTextBrushKey = "KavaTextTertiary";
    private const string QuaternaryTextBrushKey = "KavaTextQuaternary";

    internal static readonly EventCardStyle MainEventCardStyle = new(
        TitleFontSize: 15,
        MetadataFontSize: 13,
        SubtitleFontSize: 12,
        JoinButtonFontSize: 12,
        GridMargin: new Thickness(0, 4, 0, 4),
        SpacerColumnWidth: 12,
        JoinButtonPadding: new Thickness(12, 5),
        JoinButtonMargin: new Thickness(8, 0, 0, 0),
        CardPadding: new Thickness(12, 8),
        CardCornerRadius: new CornerRadius(6),
        CardBackgroundBrushKey: "KavaCardBg");

    internal static readonly EventCardStyle FlyoutEventCardStyle = new(
        TitleFontSize: 14,
        MetadataFontSize: 12,
        SubtitleFontSize: 11,
        JoinButtonFontSize: 11,
        GridMargin: new Thickness(0, 2, 0, 2),
        SpacerColumnWidth: 10,
        JoinButtonPadding: new Thickness(10, 4),
        JoinButtonMargin: new Thickness(6, 0, 0, 0),
        CardPadding: new Thickness(8),
        CardCornerRadius: new CornerRadius(4));

    internal static IBrush GetDayNumberForeground(bool isSelected, bool isToday)
    {
        if (isSelected)
            return Brushes.White;

        return isToday
            ? ThemeHelper.Brush(AccentBrushKey)
            : ThemeHelper.Brush(SecondaryTextBrushKey);
    }

    internal static Button CreateDayButton(
        DateOnly date,
        bool isSelected,
        Control content,
        double size,
        double cornerRadius,
        IBrush selectedBackground)
    {
        return new Button
        {
            Width = size,
            Height = size,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            CornerRadius = new CornerRadius(cornerRadius),
            Background = isSelected ? selectedBackground : Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            Content = content,
            Tag = date,
        };
    }

    internal static StackPanel CreateDayCellContent(int day, bool isSelected, bool isToday, bool hasEvents, double fontSize)
    {
        var cellContent = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        cellContent.Children.Add(new TextBlock
        {
            Text = day.ToString(),
            FontSize = fontSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = GetDayNumberForeground(isSelected, isToday),
        });

        if (hasEvents)
        {
            cellContent.Children.Add(new Ellipse
            {
                Width = 4,
                Height = 4,
                Fill = ThemeHelper.Brush(AccentBrushKey),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 1, 0, 0),
            });
        }

        return cellContent;
    }

    internal static Border CreateEventCard(EventItem evt, bool isAllDay, EventCardStyle style)
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
            FontSize = style.TitleFontSize,
            Foreground = ThemeHelper.Brush(PrimaryTextBrushKey),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var textStack = new StackPanel { Spacing = 2 };
        textStack.Children.Add(title);

        if (isAllDay)
        {
            textStack.Children.Add(new TextBlock
            {
                Text = "All day",
                FontSize = style.MetadataFontSize,
                Foreground = ThemeHelper.Brush(TertiaryTextBrushKey),
            });
        }
        else
        {
            AddTimedEventDetails(textStack, evt, style);
        }

        var grid = new Grid
        {
            Margin = style.GridMargin,
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition(4, GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new ColumnDefinition(style.SpacerColumnWidth, GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

        Grid.SetColumn(colorBar, 0);
        Grid.SetColumn(textStack, 2);
        grid.Children.Add(colorBar);
        grid.Children.Add(textStack);

        AddJoinButton(grid, evt.MeetingUrl, style);

        return new Border
        {
            Child = grid,
            Padding = style.CardPadding,
            CornerRadius = style.CardCornerRadius,
            Background = style.CardBackgroundBrushKey is null ? null : ThemeHelper.Brush(style.CardBackgroundBrushKey),
        };
    }

    private static void AddTimedEventDetails(StackPanel textStack, EventItem evt, EventCardStyle style)
    {
        if (!string.IsNullOrEmpty(evt.TimeRange))
        {
            textStack.Children.Add(new TextBlock
            {
                Text = evt.TimeRange,
                FontSize = style.MetadataFontSize,
                Foreground = ThemeHelper.Brush(TertiaryTextBrushKey),
            });
        }

        if (!string.IsNullOrEmpty(evt.Subtitle))
        {
            textStack.Children.Add(new TextBlock
            {
                Text = evt.Subtitle,
                FontSize = style.SubtitleFontSize,
                Foreground = ThemeHelper.Brush(QuaternaryTextBrushKey),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        }
    }

    private static void AddJoinButton(Grid grid, string? meetingUrl, EventCardStyle style)
    {
        if (string.IsNullOrEmpty(meetingUrl))
            return;

        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        var joinButton = new Button
        {
            Content = new TextBlock
            {
                Text = "Join",
                FontSize = style.JoinButtonFontSize,
                Foreground = Brushes.White,
            },
            Background = ThemeHelper.Brush(ActionBrushKey),
            BorderBrush = Brushes.Transparent,
            Padding = style.JoinButtonPadding,
            CornerRadius = new CornerRadius(4),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = style.JoinButtonMargin,
        };
        joinButton.Click += (_, _) => OpenMeetingUrl(meetingUrl);

        Grid.SetColumn(joinButton, 3);
        grid.Children.Add(joinButton);
    }

    private static void OpenMeetingUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == "https" || uri.Scheme == "http"))
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
    }
}