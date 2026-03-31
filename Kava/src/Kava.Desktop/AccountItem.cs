using System.Collections.Generic;
using Kava.Core.Models;

namespace Kava.Desktop;

public class AccountItem
{
    public string AccountId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ServerUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Status { get; set; } = "Synced";
    public ProviderType ProviderType { get; set; }
    public List<CalendarInfo> Calendars { get; set; } = [];
}

public class CalendarInfo
{
    public string CalendarId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#0078D4";
    public bool Enabled { get; set; } = true;
}
