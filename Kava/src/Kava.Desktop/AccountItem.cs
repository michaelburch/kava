using System.Collections.Generic;

namespace Kava.Desktop;

public class AccountItem
{
    public string Name { get; set; } = string.Empty;
    public string ServerUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Status { get; set; } = "Synced";
    public List<CalendarInfo> Calendars { get; set; } = [];
}

public class CalendarInfo
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#0078D4";
    public bool Enabled { get; set; } = true;
}
