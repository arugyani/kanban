namespace KanbanCord.Core.Models;

public class Comment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public required ulong AuthorId { get; set; }

    public required string Text { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
