using System.Collections.Generic;

namespace BatIDE.Models;

public class AppSettings
{
    public string? LastPath { get; set; }
    public string? LastName { get; set; }
    public List<string> RecentProjects { get; set; } = [];

    /// <summary>
    /// Hard cap on recent-project entries (both in the settings file and the
    /// editor's ComboBox). Prevents the lists from growing indefinitely over
    /// a long session.
    /// </summary>
    public int MaxRecentProjects { get; set; } = 20;
}
