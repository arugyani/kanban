namespace KanbanCord.Core.Models;

public sealed class CardChange
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public ulong? ActorId { get; set; }

    public required string Kind { get; set; }

    public required string Summary { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
