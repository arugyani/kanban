namespace KanbanCord.Core.Models;

public class ChecklistItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public required string Text { get; set; }

    public bool Complete { get; set; }

    public long Rank { get; set; }
}
