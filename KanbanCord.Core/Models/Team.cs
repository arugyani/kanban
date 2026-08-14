using MongoDB.Bson;

namespace KanbanCord.Core.Models;

public class Team
{
    public ObjectId Id { get; set; } = new();

    public required ulong GuildId { get; set; }

    public required string Name { get; set; }

    public required string NormalizedName { get; set; }

    public List<ulong> MemberIds { get; set; } = [];

    public required ulong CreatedById { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
