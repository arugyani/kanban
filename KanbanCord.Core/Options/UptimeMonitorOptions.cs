namespace KanbanCord.Core.Options;

public class UptimeMonitorOptions
{
    public static readonly string UptimeMonitor = nameof(UptimeMonitor);

    public bool Enabled { get; set; }

    public string PushUrl { get; set; } = string.Empty;

    public TimeSpan PushInterval { get; set; } = TimeSpan.FromMinutes(5);
}
