namespace Kava.Core.Models;

public class Calendar
{
    public string CalendarId { get; set; } = Guid.NewGuid().ToString();
    public string AccountId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string CalDavUrl { get; set; } = string.Empty;
    public string Color { get; set; } = "#0078D4";
    /// <summary>User-chosen color override. When set, takes precedence over server Color.</summary>
    public string? LocalColor { get; set; }
    /// <summary>Returns LocalColor if the user overrode it, otherwise the server Color.</summary>
    public string EffectiveColor => LocalColor ?? Color;
    public bool IsReadOnly { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? CTag { get; set; }
    public string? SyncToken { get; set; }
    /// <summary>When non-null, this calendar is an ICS subscription (read-only HTTP feed).</summary>
    public string? IcsUrl { get; set; }
}
