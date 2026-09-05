using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace KanbanCord.Core.Models;

public class TaskItem
{
    public ObjectId Id { get; set; } = new();

    public required ulong GuildId { get; set; }

    // Null on documents created before multiple-board support. Those tasks are
    // moved to the guild's default board the first time its boards are loaded.
    public ObjectId? BoardId { get; set; }

    public required string Title { get; set; }

    public required string Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    public required ulong AuthorId { get; set; }

    public ulong? AssigneeId { get; set; }

    // AssigneeId remains for backwards compatibility with the existing Discord
    // commands. The web board can attach more than one person to the same card.
    public List<ulong> AssigneeIds { get; set; } = [];

    public ObjectId? AssigneeTeamId { get; set; }

    public List<Comment> Comments { get; set; } = [];

    public BoardStatus Status { get; set; } = BoardStatus.Backlog;

    public Priority? Priority { get; set; } = Models.Priority.Medium;

    public DateTime? DueAt { get; set; }

    public string? BlockedReason { get; set; }

    public List<string> Tags { get; set; } = [];

    public List<ChecklistItem> Checklist { get; set; } = [];

    public List<GitHubIssueLink> Links { get; set; } = [];

    public ulong? DiscordMessageId { get; set; }

    public string? DiscordMessageUrl { get; set; }

    // Kept on the card so Discord and the website always share one audit trail.
    // The repository caps this list to keep old cards small.
    public List<CardChange> Changes { get; set; } = [];

    [BsonIgnore]
    public CardChange? PendingChange { get; private set; }

    // A floating-point rank lets cards be inserted between two neighbours
    // without rewriting the rest of the column. Existing integer Mongo values
    // deserialize safely, so this remains backwards compatible.
    public double Rank { get; set; }

    // Old Mongo documents deserialize as version 0. The first update upgrades
    // them to version 1, allowing the website to reject stale edits safely.
    public long Version { get; set; }

    public void RecordChange(ulong? actorId, string kind, string summary)
    {
        PendingChange = new CardChange
        {
            ActorId = actorId,
            Kind = kind,
            Summary = summary,
        };
    }

    internal void CommitChange(string fallbackKind, string fallbackSummary)
    {
        Changes.Add(PendingChange ?? new CardChange
        {
            Kind = fallbackKind,
            Summary = fallbackSummary,
        });
        PendingChange = null;
        if (Changes.Count > 100)
            Changes = Changes.OrderByDescending(change => change.CreatedAt).Take(100).ToList();
    }
}
