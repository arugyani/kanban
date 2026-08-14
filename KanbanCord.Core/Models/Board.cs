using MongoDB.Bson;

namespace KanbanCord.Core.Models;

public class Board
{
    public ObjectId Id { get; set; } = new();

    public required ulong GuildId { get; set; }

    public required string Name { get; set; }

    public required string NormalizedName { get; set; }

    public ObjectId? TeamId { get; set; }

    public bool IsDefault { get; set; }

    public required ulong CreatedById { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
