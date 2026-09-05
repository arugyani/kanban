using System.Text.RegularExpressions;

namespace KanbanCord.Core.Models;

public enum BoardStatus
{
    // Keep the original numeric values stable so existing MongoDB documents
    // retain their meaning after the two new workflow columns are introduced.
    Backlog = 0,
    InProgress = 1,
    Completed = 2,
    Archived = 3,
    UpNext = 4,
    Waiting = 5,
}

public static class EnumExtensions
{
    public static string ToFormattedString(this BoardStatus status)
    {
        return status switch
        {
            BoardStatus.Backlog => "Ideas",
            BoardStatus.InProgress => "Doing",
            BoardStatus.Completed => "Done",
            BoardStatus.UpNext => "Up Next",
            BoardStatus.Waiting => "Waiting",
            _ => Regex.Replace(status.ToString(), "([A-Z])", " $1").Trim(),
        };
    }
}
