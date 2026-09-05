namespace KanbanCord.Core.Models;

public class GitHubIssueLink
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public required string Url { get; set; }

    public required string Owner { get; set; }

    public required string Repository { get; set; }

    public required int IssueNumber { get; set; }

    public string? Title { get; set; }

    public string State { get; set; } = "unknown";
}
