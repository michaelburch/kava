namespace Kava.Core.Models;

public class Calendar
{
    public string CalendarId { get; set; } = Guid.NewGuid().ToString();
    public string AccountId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string CalDavUrl { get; set; } = string.Empty;
    public string Color { get; set; } = "#0078D4";
    public bool IsReadOnly { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? CTag { get; set; }
}
