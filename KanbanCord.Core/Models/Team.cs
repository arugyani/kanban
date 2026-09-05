using MongoDB.Bson;

namespace KanbanCord.Core.Models;

public class Team
{
    public ObjectId Id { get; set; } = new();

    public required ulong GuildId { get; set; }

    public required string Name { get; set; }

    public required string NormalizedName { get; set; }

    public List<ulong> MemberIds { get; set; } = [];

    // Existing teams only have MemberIds and continue to treat those people as
    // members. Newer installations can override a person's role per group.
    public Dictionary<string, string> MemberRoles { get; set; } = [];

    public string Icon { get; set; } = "ghost";

    public string Accent { get; set; } = "purple";

    public required ulong CreatedById { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
