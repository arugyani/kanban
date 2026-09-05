namespace KanbanCord.Bot.Web;

public sealed record CreateCardRequest(
    string BoardId,
    string ColumnId,
    string Title,
    string? Notes,
    string? Importance,
    string? When,
    IReadOnlyList<string>? PersonIds);

public sealed record MoveCardRequest(
    string ColumnId,
    long ExpectedVersion,
    string? TargetCardId = null,
    string? Edge = null);

public sealed record AddCommentRequest(string Body);

public sealed record AddChecklistItemRequest(string Text);

public sealed record UpdateChecklistItemRequest(bool Complete);

public sealed record AddGitHubLinkRequest(string Url);

public sealed record CreateGroupRequest(string Name, string? Icon, string? Accent);

public sealed record UpdateGroupRequest(string? Name, string? Icon, string? Accent);

public sealed record SetGroupMemberRequest(string PersonId, string Role);

public sealed record CreateBoardRequest(string Name, string GroupId);

public sealed record UpdateBoardColumnRequest(string Id, string Name, int Rank, string Color);

public sealed record UpdateBoardRequest(
    string? Name,
    string? GroupId,
    IReadOnlyList<UpdateBoardColumnRequest>? Columns);
