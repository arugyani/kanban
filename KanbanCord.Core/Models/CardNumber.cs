using MongoDB.Bson;

namespace KanbanCord.Core.Models;

// Kept separately so an older bot release can still read and replace TaskItem
// documents without rejecting or erasing the new public card references.
public sealed class CardNumber
{
    public ObjectId Id { get; set; }
    public ulong GuildId { get; set; }
    public long Number { get; set; }
}

public sealed class CardNumberCounter
{
    public required string Id { get; set; }
    public long Value { get; set; }
}
