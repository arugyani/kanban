using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using DSharpPlus;
using DSharpPlus.Entities;
using KanbanCord.Core.Constants;
using KanbanCord.Core.Models;
using KanbanCord.Core.Options;
using KanbanCord.Core.Repositories;
using Microsoft.Extensions.Options;
using MongoDB.Bson;

namespace KanbanCord.Bot.Web;

public sealed class BoardApiService
{
    private const string EveryoneGroupPrefix = "discord-guild-";
    private readonly DiscordClient _discordClient;
    private readonly IBoardRepository _boardRepository;
    private readonly ITaskItemRepository _taskRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly HashSet<ulong> _administratorIds;

    public BoardApiService(
        DiscordClient discordClient,
        IBoardRepository boardRepository,
        ITaskItemRepository taskRepository,
        ITeamRepository teamRepository,
        IOptions<WebApiOptions> webApiOptions)
    {
        _discordClient = discordClient;
        _boardRepository = boardRepository;
        _taskRepository = taskRepository;
        _teamRepository = teamRepository;
        _administratorIds = webApiOptions.Value.AdministratorDiscordUserIds.ToHashSet();
    }

    public async Task<object> GetDashboardAsync(HttpRequest request)
    {
        var identity = await ResolveIdentityAsync(request);
        var defaultBoard = await _boardRepository.GetOrCreateDefaultAsync(identity.Guild.Id, identity.UserId);
        await _taskRepository.AssignLegacyTasksToBoardAsync(identity.Guild.Id, defaultBoard.Id);

        var allBoards = await _boardRepository.GetAllByGuildIdAsync(identity.Guild.Id);
        var allTeams = await _teamRepository.GetAllByGuildIdAsync(identity.Guild.Id);
        var teams = allTeams
            .Where(team => identity.IsAdministrator || TeamRole(team, identity.UserId) is not null)
            .ToList();
        var accessibleTeamIds = teams.Select(team => team.Id).ToHashSet();
        var boards = allBoards
            .Where(board => !board.TeamId.HasValue || accessibleTeamIds.Contains(board.TeamId.Value))
            .ToList();
        var accessibleBoardIds = boards.Select(board => board.Id).ToHashSet();
        var tasks = (await _taskRepository.GetAllTaskItemsByGuildIdAsync(identity.Guild.Id))
            .Where(task => task.BoardId.HasValue && accessibleBoardIds.Contains(task.BoardId.Value))
            .ToList();
        var activeTasks = tasks.Where(task => task.Status != BoardStatus.Archived).ToList();
        var people = await BuildPeopleAsync(identity, boards, teams, tasks);
        var everyoneGroupId = EveryoneGroupId(identity.Guild.Id);
        var boardGroups = boards.ToDictionary(
            board => board.Id,
            board => board.TeamId?.ToString() ?? everyoneGroupId);
        var teamById = teams.ToDictionary(team => team.Id);
        var boardById = boards.ToDictionary(board => board.Id);
        var tags = BuildTags(activeTasks, boardGroups);

        return new
        {
            viewer = people.Single(person => person.Id == identity.UserId.ToString()),
            people,
            groups = new object[]
                {
                    MapEveryoneGroup(identity.Guild),
                }
                .Concat(teams.Select(MapGroup)),
            boards = boards.Select(board => new
            {
                id = board.Id.ToString(),
                groupId = boardGroups[board.Id],
                name = board.Name,
                slug = Slug(board.Name),
                note = string.Empty,
            }),
            columns = boards.SelectMany(ColumnsFor),
            cards = activeTasks
                .Where(task => task.BoardId.HasValue && boardById.ContainsKey(task.BoardId.Value))
                .Select(task => MapCard(task, boardById[task.BoardId!.Value], teamById, boardGroups)),
            tags = tags.Select(pair => new
            {
                id = pair.Key,
                groupId = pair.Value.GroupId,
                name = pair.Value.Name,
                color = "purple",
            }),
            activity = activeTasks
                .SelectMany(task => ActivityFor(task, boardGroups, everyoneGroupId))
                .OrderByDescending(activity => activity.CreatedAt)
                .Take(50),
        };
    }

    public async Task<object> CreateCardAsync(HttpRequest httpRequest, CreateCardRequest request)
    {
        var identity = await ResolveIdentityAsync(httpRequest);
        var board = await GetBoardAsync(request.BoardId, identity.Guild.Id);
        await EnsureCanEditAsync(identity, board);
        var status = ParseColumn(request.ColumnId, board.Id);
        var title = RequiredText(request.Title, "title", 1, Limits.TaskTitleMaxLength);
        var notes = OptionalText(request.Notes, Limits.TaskDescriptionMaxLength);
        var existing = await _taskRepository.GetAllTaskItemsByBoardIdAsync(identity.Guild.Id, board.Id);
        var people = ParsePeople(request.PersonIds);
        await ValidatePeopleAsync(identity, board, people);
        var task = new TaskItem
        {
            GuildId = identity.Guild.Id,
            BoardId = board.Id,
            Title = title,
            Description = notes,
            AuthorId = identity.UserId,
            AssigneeId = people.Count == 0 ? null : people[0],
            AssigneeIds = people,
            Priority = ParsePriority(request.Importance),
            DueAt = ParseDate(request.When),
            Status = status,
            Rank = existing.Count == 0 ? 1024 : existing.Max(item => EffectiveRank(item)) + 1024,
            Version = 1,
        };
        task.RecordChange(identity.UserId, "card_added", $"{title} was added.");

        await _taskRepository.AddTaskItemAsync(task);
        var teams = await _teamRepository.GetAllByGuildIdAsync(identity.Guild.Id);
        return MapCard(task, board, teams.ToDictionary(team => team.Id), await BuildBoardGroupsAsync(identity.Guild.Id));
    }

    public async Task<object> UpdateCardAsync(HttpRequest httpRequest, string id, JsonElement request)
    {
        var identity = await ResolveIdentityAsync(httpRequest);
        var task = await GetTaskAsync(id, identity.Guild.Id);
        var board = await GetTaskBoardAsync(task, identity.Guild.Id);
        await EnsureCanEditAsync(identity, board);
        var expectedVersion = RequiredLong(request, "expectedVersion");

        if (request.ValueKind != JsonValueKind.Object)
            throw InvalidInput("Send card changes as a JSON object.");
        if (request.TryGetProperty("title", out var title))
            task.Title = RequiredText(JsonString(title, "title") ?? string.Empty, "title", 1, Limits.TaskTitleMaxLength);
        if (request.TryGetProperty("notes", out var notes))
            task.Description = OptionalText(JsonString(notes, "notes"), Limits.TaskDescriptionMaxLength);
        if (request.TryGetProperty("importance", out var importance))
            task.Priority = ParsePriority(JsonString(importance, "importance"));
        if (request.TryGetProperty("when", out var when))
            task.DueAt = ParseDate(JsonString(when, "when"));
        if (request.TryGetProperty("blockedReason", out var blockedReason))
        {
            var reason = JsonString(blockedReason, "blockedReason");
            task.BlockedReason = string.IsNullOrWhiteSpace(reason)
                ? null
                : OptionalText(reason, Limits.TaskBlockedReasonMaxLength);
        }
        if (request.TryGetProperty("blocked", out var blocked))
        {
            if (blocked.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw InvalidInput("blocked must be true or false.");
            if (!blocked.GetBoolean())
                task.BlockedReason = null;
            else if (string.IsNullOrWhiteSpace(task.BlockedReason))
                task.BlockedReason = "Waiting on an update";
        }
        if (request.TryGetProperty("personIds", out var personIds))
        {
            task.AssigneeIds = ParsePeople(JsonStringArray(personIds, "personIds"));
            await ValidatePeopleAsync(identity, board, task.AssigneeIds);
            task.AssigneeId = task.AssigneeIds.Count == 0 ? null : task.AssigneeIds[0];
            task.AssigneeTeamId = null;
        }
        if (request.TryGetProperty("tagIds", out var tagIds))
        {
            var knownTags = await BuildTagLookupAsync(identity.Guild.Id, board);
            var requestedTagIds = JsonStringArray(tagIds, "tagIds");
            if (requestedTagIds.Any(value => !knownTags.ContainsKey(value)))
                throw InvalidInput("Choose tags that belong to this group.");
            task.Tags = requestedTagIds
                .Select(value => knownTags[value])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        if (request.TryGetProperty("tagNames", out var tagNames))
            task.Tags = ParseTagNames(JsonStringArray(tagNames, "tagNames"));

        task.RecordChange(identity.UserId, "card_updated", $"{task.Title} was updated.");
        await SaveWithVersionAsync(task, expectedVersion);
        var teams = await _teamRepository.GetAllByGuildIdAsync(identity.Guild.Id);
        return MapCard(task, board, teams.ToDictionary(team => team.Id), await BuildBoardGroupsAsync(identity.Guild.Id));
    }

    public async Task<object> MoveCardAsync(HttpRequest httpRequest, string id, MoveCardRequest request)
    {
        var identity = await ResolveIdentityAsync(httpRequest);
        var task = await GetTaskAsync(id, identity.Guild.Id);
        var board = await GetTaskBoardAsync(task, identity.Guild.Id);
        await EnsureCanEditAsync(identity, board);
        var status = ParseColumn(request.ColumnId, board.Id);
        var cards = await _taskRepository.GetAllTaskItemsByBoardIdAsync(identity.Guild.Id, board.Id);
        var target = ResolveMoveTarget(request, task, cards, board, status);
        task.Status = status;
        task.Rank = RankForMove(task, cards, status, target, request.Edge);
        if (task.Status == BoardStatus.Waiting && string.IsNullOrWhiteSpace(task.BlockedReason))
            task.BlockedReason = "Waiting on an update";
        else if (task.Status != BoardStatus.Waiting)
            task.BlockedReason = null;
        var columnName = BoardColumns.For(board).Single(column => column.Status == status).Name;
        task.RecordChange(identity.UserId, "card_moved", $"{task.Title} moved to {columnName}.");
        await SaveWithVersionAsync(task, request.ExpectedVersion);
        var teams = await _teamRepository.GetAllByGuildIdAsync(identity.Guild.Id);
        return MapCard(task, board, teams.ToDictionary(team => team.Id), await BuildBoardGroupsAsync(identity.Guild.Id));
    }

    private static TaskItem? ResolveMoveTarget(
        MoveCardRequest request,
        TaskItem moving,
        IReadOnlyList<TaskItem> cards,
        Board board,
        BoardStatus status)
    {
        if (request.TargetCardId is null)
        {
            if (request.Edge is not null)
                throw new BoardApiException(400, "invalid_position", "Choose a card before choosing where to place it.");
            return null;
        }
        if (request.Edge is not ("before" or "after"))
            throw new BoardApiException(400, "invalid_position", "Choose before or after the nearby card.");
        if (!ObjectId.TryParse(request.TargetCardId, out var targetId))
            throw new BoardApiException(400, "invalid_position", "Choose a valid nearby card.");
        var target = cards.SingleOrDefault(candidate => candidate.Id == targetId)
                     ?? throw new BoardApiException(404, "not_found", "That nearby card could not be found.");
        if (target.Id == moving.Id
            || target.BoardId != board.Id
            || target.Status != status
            || target.Status == BoardStatus.Archived)
            throw new BoardApiException(400, "invalid_position", "Choose another card in the destination column.");
        return target;
    }

    internal static double RankForMove(
        TaskItem moving,
        IEnumerable<TaskItem> cards,
        BoardStatus status,
        TaskItem? target,
        string? edge)
    {
        var ordered = cards
            .Where(candidate => candidate.Id != moving.Id
                                && candidate.Status == status
                                && candidate.Status != BoardStatus.Archived)
            .OrderBy(EffectiveRank)
            .ThenBy(candidate => candidate.CreatedAt)
            .ThenBy(candidate => candidate.Id)
            .ToList();

        if (target is null)
            return ordered.Count == 0 ? 1024 : EffectiveRank(ordered[^1]) + 1024;

        var targetIndex = ordered.FindIndex(candidate => candidate.Id == target.Id);
        var insertionIndex = edge == "after" ? targetIndex + 1 : targetIndex;
        var previous = insertionIndex > 0 ? EffectiveRank(ordered[insertionIndex - 1]) : (double?)null;
        var next = insertionIndex < ordered.Count ? EffectiveRank(ordered[insertionIndex]) : (double?)null;
        if (previous is null)
            return next!.Value - 1024;
        if (next is null)
            return previous.Value + 1024;

        var rank = previous.Value + (next.Value - previous.Value) / 2;
        if (rank > previous.Value && rank < next.Value)
            return rank;

        throw new BoardApiException(
            409,
            "position_too_crowded",
            "Those cards are packed too closely together. Refresh the board and try again.");
    }

    public async Task<object> AddCommentAsync(HttpRequest httpRequest, string id, AddCommentRequest request)
    {
        var identity = await ResolveIdentityAsync(httpRequest);
        var task = await GetTaskAsync(id, identity.Guild.Id);
        await EnsureCanEditAsync(identity, await GetTaskBoardAsync(task, identity.Guild.Id));
        var comment = new Comment
        {
            AuthorId = identity.UserId,
            Text = RequiredText(request.Body, "comment", 1, Limits.TaskCommentMaxLength),
        };
        task.Comments.Add(comment);
        task.RecordChange(identity.UserId, "comment_added", $"A note was added to {task.Title}.");
        await SaveWithVersionAsync(task, task.Version);
        return new { id = comment.Id, authorId = comment.AuthorId.ToString(), body = comment.Text, createdAt = comment.CreatedAt };
    }

    public async Task<object> AddChecklistItemAsync(HttpRequest httpRequest, string id, AddChecklistItemRequest request)
    {
        var identity = await ResolveIdentityAsync(httpRequest);
        var task = await GetTaskAsync(id, identity.Guild.Id);
        await EnsureCanEditAsync(identity, await GetTaskBoardAsync(task, identity.Guild.Id));
        var item = new ChecklistItem
        {
            Text = RequiredText(request.Text, "checklist item", 1, 240),
            Rank = task.Checklist.Count == 0 ? 1024 : task.Checklist.Max(entry => entry.Rank) + 1024,
        };
        task.Checklist.Add(item);
        task.RecordChange(identity.UserId, "checklist_added", $"A checklist item was added to {task.Title}.");
        await SaveWithVersionAsync(task, task.Version);
        return MapChecklistItem(item);
    }

    public async Task<object> UpdateChecklistItemAsync(
        HttpRequest httpRequest,
        string id,
        string itemId,
        UpdateChecklistItemRequest request)
    {
        var identity = await ResolveIdentityAsync(httpRequest);
        var task = await GetTaskAsync(id, identity.Guild.Id);
        await EnsureCanEditAsync(identity, await GetTaskBoardAsync(task, identity.Guild.Id));
        var item = task.Checklist.SingleOrDefault(entry => entry.Id == itemId)
                   ?? throw new BoardApiException(404, "not_found", "That checklist item could not be found.");
        item.Complete = request.Complete;
        task.RecordChange(
            identity.UserId,
            request.Complete ? "checklist_completed" : "checklist_reopened",
            $"A checklist item on {task.Title} was {(request.Complete ? "completed" : "reopened")}.");
        await SaveWithVersionAsync(task, task.Version);
        return MapChecklistItem(item);
    }

    public async Task<object> AddGitHubLinkAsync(HttpRequest httpRequest, string id, AddGitHubLinkRequest request)
    {
        var identity = await ResolveIdentityAsync(httpRequest);
        var task = await GetTaskAsync(id, identity.Guild.Id);
        await EnsureCanEditAsync(identity, await GetTaskBoardAsync(task, identity.Guild.Id));
        var link = ParseGitHubIssue(request.Url);
        if (task.Links.Any(existing => string.Equals(existing.Url, link.Url, StringComparison.OrdinalIgnoreCase)))
            throw new BoardApiException(409, "link_exists", "That GitHub issue is already linked.");
        task.Links.Add(link);
        task.RecordChange(identity.UserId, "github_link_added", $"A GitHub issue was linked to {task.Title}.");
        await SaveWithVersionAsync(task, task.Version);
        return MapGitHubLink(link);
    }

    public async Task RemoveGitHubLinkAsync(HttpRequest httpRequest, string id, string linkId)
    {
        var identity = await ResolveIdentityAsync(httpRequest);
        var task = await GetTaskAsync(id, identity.Guild.Id);
        await EnsureCanEditAsync(identity, await GetTaskBoardAsync(task, identity.Guild.Id));
        var removed = task.Links.RemoveAll(link => link.Id == linkId);
        if (removed == 0)
            throw new BoardApiException(404, "not_found", "That GitHub issue link could not be found.");
        task.RecordChange(identity.UserId, "github_link_removed", $"A GitHub issue was unlinked from {task.Title}.");
        await SaveWithVersionAsync(task, task.Version);
    }

    public async Task<object> CreateGroupAsync(HttpRequest httpRequest, CreateGroupRequest request)
    {
        var identity = await ResolveIdentityAsync(httpRequest);
        EnsureCanCreateGroups(identity);
        var name = RequiredText(request.Name, "group name", 1, Limits.TeamNameMaxLength);
        if (await _teamRepository.NameExistsAsync(identity.Guild.Id, name))
            throw new BoardApiException(409, "name_exists", "A group with that name already exists.");

        var group = new Team
        {
            GuildId = identity.Guild.Id,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            CreatedById = identity.UserId,
            MemberIds = [identity.UserId],
            MemberRoles = new Dictionary<string, string>
            {
                [identity.UserId.ToString()] = "organizer",
            },
            Icon = ParseIcon(request.Icon),
            Accent = ParseAccent(request.Accent),
        };
        await _teamRepository.AddAsync(group);
        return MapGroup(group);
    }

    public async Task<object> UpdateGroupAsync(HttpRequest httpRequest, string id, UpdateGroupRequest request)
    {
        var identity = await ResolveIdentityAsync(httpRequest);
        var group = await GetGroupAsync(id, identity.Guild.Id);
        EnsureCanManageGroup(identity, group);
        if (request.Name is not null)
        {
            var name = RequiredText(request.Name, "group name", 1, Limits.TeamNameMaxLength);
            if (await _teamRepository.NameExistsAsync(identity.Guild.Id, name, group.Id))
                throw new BoardApiException(409, "name_exists", "A group with that name already exists.");
            group.Name = name;
        }
        if (request.Icon is not null)
            group.Icon = ParseIcon(request.Icon);
        if (request.Accent is not null)
            group.Accent = ParseAccent(request.Accent);
        await _teamRepository.UpdateAsync(group);
        return MapGroup(group);
    }

    public async Task<object> SetGroupMemberAsync(
        HttpRequest httpRequest,
        string id,
        string personId,
        SetGroupMemberRequest request)
    {
        var identity = await ResolveIdentityAsync(httpRequest);
        var group = await GetGroupAsync(id, identity.Guild.Id);
        EnsureCanManageGroup(identity, group);
        if (!string.Equals(personId, request.PersonId, StringComparison.Ordinal))
            throw new BoardApiException(400, "invalid_person", "The selected person does not match this request.");
        var memberId = ParsePersonId(personId);
        await EnsureGuildMemberAsync(identity.Guild, memberId);
        var role = ParseGroupRole(request.Role);
        if (memberId == group.CreatedById && role != "organizer")
            throw new BoardApiException(409, "creator_role", "The person who created this group must remain an organizer.");
        if (!group.MemberIds.Contains(memberId))
            group.MemberIds.Add(memberId);
        group.MemberRoles[memberId.ToString()] = role;
        await _teamRepository.UpdateAsync(group);
        return MapGroup(group);
    }

    public async Task RemoveGroupMemberAsync(HttpRequest httpRequest, string id, string personId)
    {
        var identity = await ResolveIdentityAsync(httpRequest);
        var group = await GetGroupAsync(id, identity.Guild.Id);
        EnsureCanManageGroup(identity, group);
        var memberId = ParsePersonId(personId);
        if (memberId == group.CreatedById)
            throw new BoardApiException(409, "creator_required", "The person who created this group cannot be removed.");
        if (!group.MemberIds.Remove(memberId))
            throw new BoardApiException(404, "not_found", "That person is not in this group.");
        group.MemberRoles.Remove(memberId.ToString());
        await _teamRepository.UpdateAsync(group);
    }

    public async Task DeleteGroupAsync(HttpRequest httpRequest, string id)
    {
        var identity = await ResolveIdentityAsync(httpRequest);
        var group = await GetGroupAsync(id, identity.Guild.Id);
        EnsureCanManageGroup(identity, group);
        var boards = await _boardRepository.GetAllByGuildIdAsync(identity.Guild.Id);
        if (boards.Any(board => board.TeamId == group.Id))
            throw new BoardApiException(409, "group_not_empty", "Move or delete this group's boards first.");
        var tasks = await _taskRepository.GetAllTaskItemsByGuildIdAsync(identity.Guild.Id);
        if (tasks.Any(task => task.AssigneeTeamId == group.Id))
            throw new BoardApiException(409, "group_not_empty", "Remove this group from its cards first.");
        await _teamRepository.RemoveAsync(group);
    }

    public async Task<object> CreateBoardAsync(HttpRequest httpRequest, CreateBoardRequest request)
    {
        var identity = await ResolveIdentityAsync(httpRequest);
        var name = RequiredText(request.Name, "board name", 1, Limits.BoardNameMaxLength);
        if (await _boardRepository.NameExistsAsync(identity.Guild.Id, name))
            throw new BoardApiException(409, "name_exists", "A board with that name already exists.");
        var group = await ResolveBoardGroupAsync(identity, request.GroupId);
        EnsureCanManageBoardStructure(identity, group);
        var board = new Board
        {
            GuildId = identity.Guild.Id,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            TeamId = group?.Id,
            CreatedById = identity.UserId,
        };
        await _boardRepository.AddAsync(board);
        return MapBoard(board, request.GroupId);
    }

    public async Task<object> UpdateBoardAsync(HttpRequest httpRequest, string id, UpdateBoardRequest request)
    {
        var identity = await ResolveIdentityAsync(httpRequest);
        var board = await GetBoardAsync(id, identity.Guild.Id);
        var currentGroup = board.TeamId.HasValue
            ? await _teamRepository.GetByObjectIdOrDefaultAsync(board.TeamId.Value, identity.Guild.Id)
            : null;
        EnsureCanManageBoardStructure(identity, currentGroup);
        if (request.Name is not null)
        {
            var name = RequiredText(request.Name, "board name", 1, Limits.BoardNameMaxLength);
            if (await _boardRepository.NameExistsAsync(identity.Guild.Id, name, board.Id))
                throw new BoardApiException(409, "name_exists", "A board with that name already exists.");
            board.Name = name;
        }
        if (request.GroupId is not null)
        {
            var nextGroup = await ResolveBoardGroupAsync(identity, request.GroupId);
            EnsureCanManageBoardStructure(identity, nextGroup);
            board.TeamId = nextGroup?.Id;
        }
        if (request.Columns is not null)
            board.Columns = ParseColumns(board, request.Columns);
        await _boardRepository.UpdateAsync(board);
        return MapBoard(board, board.TeamId?.ToString() ?? EveryoneGroupId(identity.Guild.Id));
    }

    public async Task DeleteBoardAsync(HttpRequest httpRequest, string id)
    {
        var identity = await ResolveIdentityAsync(httpRequest);
        var board = await GetBoardAsync(id, identity.Guild.Id);
        var group = board.TeamId.HasValue
            ? await _teamRepository.GetByObjectIdOrDefaultAsync(board.TeamId.Value, identity.Guild.Id)
            : null;
        EnsureCanManageBoardStructure(identity, group);
        if (board.IsDefault)
            throw new BoardApiException(409, "default_board", "The default board cannot be deleted.");
        var tasks = await _taskRepository.GetAllTaskItemsByBoardIdAsync(identity.Guild.Id, board.Id);
        if (tasks.Count != 0)
            throw new BoardApiException(409, "board_not_empty", "Move or archive this board's cards first.");
        await _boardRepository.RemoveAsync(board);
    }

    private async Task<RequestIdentity> ResolveIdentityAsync(HttpRequest request)
    {
        if (_discordClient.Guilds.Count == 0)
            throw new BoardApiException(503, "discord_not_ready", "The Discord bot is still connecting.");

        DiscordGuild? guild = null;
        var guildHeader = request.Headers["X-Discord-Guild-Id"].ToString();
        if (ulong.TryParse(guildHeader, out var requestedGuildId))
            _discordClient.Guilds.TryGetValue(requestedGuildId, out guild);
        else if (_discordClient.Guilds.Count == 1)
            guild = _discordClient.Guilds.Values.Single();

        if (guild is null)
            throw new BoardApiException(400, "guild_required", "Choose which Discord server to open.");

        var userHeader = request.Headers["X-Discord-User-Id"].ToString();
        if (string.IsNullOrWhiteSpace(userHeader))
            throw new BoardApiException(401, "not_authenticated", "Sign in with Discord to open The Board.");
        var userId = ulong.TryParse(userHeader, out var parsedUserId)
            ? parsedUserId
            : throw new BoardApiException(400, "invalid_user", "The Discord user ID is invalid.");

        DiscordMember member;
        try
        {
            member = await guild.GetMemberAsync(userId);
        }
        catch
        {
            throw new BoardApiException(403, "not_in_guild", "Join the Discord server before opening The Board.");
        }

        var isAdministrator = userId == guild.OwnerId || _administratorIds.Contains(userId);
        var canOrganize = isAdministrator || member.Permissions.HasPermission(DiscordPermission.ManageMessages);
        return new RequestIdentity(guild, member, isAdministrator, canOrganize);
    }

    private async Task EnsureCanEditAsync(RequestIdentity identity, Board board)
    {
        var role = await BoardRoleAsync(identity, board);
        if (role is "admin" or "organizer" or "member")
            return;
        if (role == "view_only")
            throw new BoardApiException(403, "view_only", "Your role in this group is view only.");
        throw new BoardApiException(404, "not_found", "That board could not be found.");
    }

    private static void EnsureCanCreateGroups(RequestIdentity identity)
    {
        if (!identity.IsAdministrator && !identity.CanOrganize)
            throw new BoardApiException(403, "not_allowed", "Only an organizer can add a group.");
    }

    private static void EnsureCanManageGroup(RequestIdentity identity, Team group)
    {
        if (!identity.IsAdministrator && TeamRole(group, identity.UserId) != "organizer")
            throw new BoardApiException(403, "not_allowed", "Only a group organizer can make that change.");
    }

    private static void EnsureCanManageBoardStructure(RequestIdentity identity, Team? group)
    {
        var canManage = identity.IsAdministrator
                        || (group is null && identity.CanOrganize)
                        || (group is not null && TeamRole(group, identity.UserId) == "organizer");
        if (!canManage)
            throw new BoardApiException(403, "not_allowed", "Only an organizer can change this board.");
    }

    private async Task<string?> BoardRoleAsync(RequestIdentity identity, Board board)
    {
        if (identity.IsAdministrator)
            return "admin";
        if (!board.TeamId.HasValue)
            return identity.CanOrganize ? "organizer" : "member";
        var team = await _teamRepository.GetByObjectIdOrDefaultAsync(board.TeamId.Value, identity.Guild.Id);
        return team is null ? null : TeamRole(team, identity.UserId);
    }

    private static string? TeamRole(Team team, ulong userId)
    {
        if (team.CreatedById == userId)
            return "organizer";
        if (team.MemberRoles.TryGetValue(userId.ToString(), out var role)
            && role is "organizer" or "member" or "view_only")
            return role;
        return team.MemberIds.Contains(userId) ? "member" : null;
    }

    private async Task<Team> GetGroupAsync(string id, ulong guildId)
    {
        if (!ObjectId.TryParse(id, out var objectId))
            throw new BoardApiException(404, "not_found", "That group could not be found.");
        return await _teamRepository.GetByObjectIdOrDefaultAsync(objectId, guildId)
               ?? throw new BoardApiException(404, "not_found", "That group could not be found.");
    }

    private async Task<Team?> ResolveBoardGroupAsync(RequestIdentity identity, string groupId)
    {
        if (groupId == EveryoneGroupId(identity.Guild.Id))
            return null;
        return await GetGroupAsync(groupId, identity.Guild.Id);
    }

    private static ulong ParsePersonId(string value)
    {
        if (!ulong.TryParse(value, out var personId))
            throw new BoardApiException(400, "invalid_person", "Choose a valid Discord member.");
        return personId;
    }

    private static async Task EnsureGuildMemberAsync(DiscordGuild guild, ulong personId)
    {
        try
        {
            await guild.GetMemberAsync(personId);
        }
        catch
        {
            throw new BoardApiException(400, "invalid_person", "Choose someone who is still in the Discord server.");
        }
    }

    private async Task<List<PersonResponse>> BuildPeopleAsync(
        RequestIdentity identity,
        IReadOnlyList<Board> boards,
        IReadOnlyList<Team> teams,
        IReadOnlyList<TaskItem> tasks)
    {
        var ids = new HashSet<ulong> { identity.UserId, identity.Guild.OwnerId };
        ids.UnionWith(identity.Guild.Members.Values
            .Where(member => !member.IsBot)
            .Select(member => member.Id));
        foreach (var board in boards)
            ids.Add(board.CreatedById);
        foreach (var team in teams)
        {
            ids.Add(team.CreatedById);
            ids.UnionWith(team.MemberIds);
        }
        foreach (var task in tasks)
        {
            ids.Add(task.AuthorId);
            if (task.AssigneeId.HasValue)
                ids.Add(task.AssigneeId.Value);
            ids.UnionWith(task.AssigneeIds);
            ids.UnionWith(task.Comments.Select(comment => comment.AuthorId));
        }

        var people = new List<PersonResponse>();
        foreach (var id in ids)
        {
            DiscordMember? member = null;
            try
            {
                member = await identity.Guild.GetMemberAsync(id);
            }
            catch
            {
                // Keep historical authors visible even if their Discord account
                // is no longer available to the bot.
            }

            var isAdministrator = id == identity.Guild.OwnerId || _administratorIds.Contains(id);
            var roles = new Dictionary<string, string>();
            if (member is not null)
            {
                roles[EveryoneGroupId(identity.Guild.Id)] = isAdministrator
                    || member.Permissions.HasPermission(DiscordPermission.ManageMessages)
                        ? "organizer"
                        : "member";
                foreach (var team in teams)
                {
                    var role = isAdministrator ? "organizer" : TeamRole(team, id);
                    if (role is not null)
                        roles[team.Id.ToString()] = role;
                }
            }

            people.Add(new PersonResponse(
                id.ToString(),
                member?.DisplayName ?? member?.Username ?? $"Discord user {id}",
                string.Empty,
                member?.AvatarUrl,
                id.ToString(),
                isAdministrator ? "admin" : "member",
                member is not null,
                roles));
        }

        return people.OrderBy(person => person.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static Dictionary<string, TagValue> BuildTags(
        IEnumerable<TaskItem> tasks,
        IReadOnlyDictionary<ObjectId, string> boardGroups)
    {
        var result = new Dictionary<string, TagValue>();
        foreach (var task in tasks.Where(task => task.BoardId.HasValue))
        {
            if (!boardGroups.TryGetValue(task.BoardId!.Value, out var groupId))
                continue;
            foreach (var tag in task.Tags.Where(tag => !string.IsNullOrWhiteSpace(tag)))
                result[TagId(groupId, tag)] = new TagValue(groupId, tag.Trim());
        }

        return result;
    }

    private async Task<Dictionary<string, string>> BuildTagLookupAsync(ulong guildId, Board board)
    {
        var boards = await _boardRepository.GetAllByGuildIdAsync(guildId);
        var tasks = await _taskRepository.GetAllTaskItemsByGuildIdAsync(guildId);
        var groups = boards.ToDictionary(
            candidate => candidate.Id,
            candidate => candidate.TeamId?.ToString() ?? EveryoneGroupId(guildId));
        var groupId = board.TeamId?.ToString() ?? EveryoneGroupId(guildId);
        return BuildTags(tasks, groups)
            .Where(pair => pair.Value.GroupId == groupId)
            .ToDictionary(pair => pair.Key, pair => pair.Value.Name);
    }

    private async Task ValidatePeopleAsync(RequestIdentity identity, Board board, IEnumerable<ulong> people)
    {
        Team? team = null;
        if (board.TeamId.HasValue)
            team = await _teamRepository.GetByObjectIdOrDefaultAsync(board.TeamId.Value, identity.Guild.Id);

        foreach (var personId in people)
        {
            try
            {
                await identity.Guild.GetMemberAsync(personId);
            }
            catch
            {
                throw new BoardApiException(400, "invalid_person", "Choose someone who is still in the Discord server.");
            }

            if (team is not null
                && personId != identity.Guild.OwnerId
                && !_administratorIds.Contains(personId)
                && TeamRole(team, personId) is null)
                throw new BoardApiException(400, "invalid_person", "Choose someone who belongs to this group.");
        }
    }

    private async Task<Dictionary<ObjectId, string>> BuildBoardGroupsAsync(ulong guildId)
    {
        var boards = await _boardRepository.GetAllByGuildIdAsync(guildId);
        return boards.ToDictionary(board => board.Id, board => board.TeamId?.ToString() ?? EveryoneGroupId(guildId));
    }

    private static object MapCard(
        TaskItem task,
        Board board,
        IReadOnlyDictionary<ObjectId, Team> teams,
        IReadOnlyDictionary<ObjectId, string> boardGroups)
    {
        var people = new HashSet<ulong>(task.AssigneeIds);
        if (task.AssigneeId.HasValue)
            people.Add(task.AssigneeId.Value);
        if (task.AssigneeTeamId.HasValue && teams.TryGetValue(task.AssigneeTeamId.Value, out var assignedTeam))
            people.UnionWith(assignedTeam.MemberIds);
        var groupId = boardGroups[board.Id];

        return new
        {
            id = task.Id.ToString(),
            key = task.Key,
            boardId = board.Id.ToString(),
            columnId = ColumnId(board.Id, task.Status),
            title = task.Title,
            notes = task.Description,
            importance = FormatPriority(task.Priority),
            when = task.DueAt?.ToString("yyyy-MM-dd"),
            blocked = !string.IsNullOrWhiteSpace(task.BlockedReason),
            blockedReason = task.BlockedReason,
            rank = EffectiveRank(task),
            version = task.Version,
            personIds = people.Select(id => id.ToString()),
            tagIds = task.Tags.Select(tag => TagId(groupId, tag)),
            checklist = task.Checklist.Select(MapChecklistItem),
            comments = task.Comments.Select(comment => new
            {
                id = comment.Id,
                authorId = comment.AuthorId.ToString(),
                body = comment.Text,
                createdAt = comment.CreatedAt,
            }),
            links = task.Links.Select(MapGitHubLink),
            discordMessageUrl = task.DiscordMessageUrl,
            changes = ChangesFor(task).Select(change => new
            {
                id = change.Id,
                actorId = change.ActorId?.ToString(),
                kind = change.Kind,
                summary = change.Summary,
                createdAt = change.CreatedAt,
            }),
            createdAt = task.CreatedAt,
            updatedAt = task.LastUpdatedAt,
        };
    }

    private static object MapChecklistItem(ChecklistItem item) => new
    {
        id = item.Id,
        text = item.Text,
        complete = item.Complete,
        rank = item.Rank,
    };

    private static object MapGitHubLink(GitHubIssueLink link) => new
    {
        id = link.Id,
        kind = "github_issue",
        url = link.Url,
        owner = link.Owner,
        repo = link.Repository,
        issueNumber = link.IssueNumber,
        title = link.Title,
        state = link.State,
    };

    private static IEnumerable<CardChange> ChangesFor(TaskItem task)
    {
        if (task.Changes.Count != 0)
            return task.Changes.OrderByDescending(change => change.CreatedAt);

        // Old Mongo documents have no audit array. Keep one honest fallback
        // entry until their next edit starts a proper history.
        return
        [
            new CardChange
            {
                Id = $"legacy-{task.Id}-{task.LastUpdatedAt.Ticks}",
                Kind = "card_updated",
                Summary = $"{task.Title} is in {task.Status.ToFormattedString()}.",
                CreatedAt = task.LastUpdatedAt,
            },
        ];
    }

    private static IEnumerable<ActivityResponse> ActivityFor(
        TaskItem task,
        IReadOnlyDictionary<ObjectId, string> boardGroups,
        string everyoneGroupId)
    {
        var groupId = task.BoardId.HasValue
                      && boardGroups.TryGetValue(task.BoardId.Value, out var mappedGroupId)
            ? mappedGroupId
            : everyoneGroupId;
        return ChangesFor(task).Select(change => new ActivityResponse(
            change.Id,
            change.ActorId?.ToString(),
            groupId,
            task.BoardId?.ToString(),
            task.Id.ToString(),
            change.Kind,
            change.Summary,
            change.CreatedAt));
    }

    private static object MapEveryoneGroup(DiscordGuild guild) => new
    {
        id = EveryoneGroupId(guild.Id),
        name = guild.Name,
        slug = Slug(guild.Name),
        icon = "pumpkin",
        accent = "pumpkin",
        isPrivate = false,
        discordChannelId = (string?)null,
        recapEnabled = false,
        recapHourUtc = 16,
    };

    private static object MapGroup(Team group) => new
    {
        id = group.Id.ToString(),
        name = group.Name,
        slug = Slug(group.Name),
        icon = ParseIcon(group.Icon),
        accent = ParseAccent(group.Accent),
        isPrivate = true,
        discordChannelId = (string?)null,
        recapEnabled = false,
        recapHourUtc = 16,
    };

    private static object MapBoard(Board board, string groupId) => new
    {
        id = board.Id.ToString(),
        groupId,
        name = board.Name,
        slug = Slug(board.Name),
        note = string.Empty,
    };

    private static IEnumerable<object> ColumnsFor(Board board)
    {
        foreach (var column in BoardColumns.For(board))
            yield return new
            {
                id = ColumnId(board.Id, column.Status),
                boardId = board.Id.ToString(),
                name = column.Name,
                rank = column.Rank,
                color = column.Color,
            };
    }

    private async Task<Board> GetBoardAsync(string id, ulong guildId)
    {
        if (!ObjectId.TryParse(id, out var objectId))
            throw new BoardApiException(404, "not_found", "That board could not be found.");
        return await _boardRepository.GetByObjectIdOrDefaultAsync(objectId, guildId)
               ?? throw new BoardApiException(404, "not_found", "That board could not be found.");
    }

    private async Task<TaskItem> GetTaskAsync(string id, ulong guildId)
    {
        if (!ObjectId.TryParse(id, out var objectId))
            throw new BoardApiException(404, "not_found", "That card could not be found.");
        return await _taskRepository.GetTaskItemByObjectIdOrDefaultAsync(objectId, guildId)
               ?? throw new BoardApiException(404, "not_found", "That card could not be found.");
    }

    private async Task<Board> GetTaskBoardAsync(TaskItem task, ulong guildId)
    {
        if (!task.BoardId.HasValue)
            throw new BoardApiException(409, "card_has_no_board", "This older card needs to be assigned to a board first.");
        return await _boardRepository.GetByObjectIdOrDefaultAsync(task.BoardId.Value, guildId)
               ?? throw new BoardApiException(404, "not_found", "That card's board could not be found.");
    }

    private async Task SaveWithVersionAsync(TaskItem task, long expectedVersion)
    {
        if (task.Version != expectedVersion)
            throw VersionConflict();
        if (!await _taskRepository.TryUpdateTaskItemAsync(task, expectedVersion))
            throw VersionConflict();
    }

    private static BoardApiException VersionConflict() => new(
        409,
        "version_conflict",
        "This card changed while you were looking at it. Refresh and try again.");

    private static string RequiredText(string value, string field, int minLength, int maxLength)
    {
        var result = value.Trim();
        if (result.Length < minLength || result.Length > maxLength)
            throw new BoardApiException(400, "invalid_input", $"The {field} must be between {minLength} and {maxLength} characters.");
        return result;
    }

    private static string OptionalText(string? value, int maxLength)
    {
        var result = value?.Trim() ?? string.Empty;
        if (result.Length > maxLength)
            throw new BoardApiException(400, "invalid_input", $"Keep that text under {maxLength} characters.");
        return result;
    }

    private static long RequiredLong(JsonElement request, string property)
    {
        if (!request.TryGetProperty(property, out var value) || !value.TryGetInt64(out var result) || result < 0)
            throw new BoardApiException(400, "invalid_input", $"{property} is required.");
        return result;
    }

    private static string? JsonString(JsonElement value, string property)
    {
        if (value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind != JsonValueKind.String)
            throw InvalidInput($"{property} must be text.");
        return value.GetString();
    }

    private static IReadOnlyList<string> JsonStringArray(JsonElement value, string property)
    {
        if (value.ValueKind != JsonValueKind.Array)
            throw InvalidInput($"{property} must be a list.");
        var result = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                throw InvalidInput($"Every value in {property} must be text.");
            result.Add(item.GetString() ?? string.Empty);
        }
        return result;
    }

    internal static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (!DateOnly.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
            throw new BoardApiException(400, "invalid_input", "Use a valid date.");
        return date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    }

    internal static List<string> ParseTagNames(IEnumerable<string> values)
    {
        var tags = values
            .Select(value => value.Trim())
            .Where(value => value.Length != 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (tags.Count > Limits.TaskTagsMaxCount)
            throw InvalidInput($"Keep each card to {Limits.TaskTagsMaxCount} tags or fewer.");
        if (tags.Any(tag => tag.Length > Limits.TaskTagMaxLength))
            throw InvalidInput($"Keep every tag under {Limits.TaskTagMaxLength} characters.");
        return tags;
    }

    private static BoardApiException InvalidInput(string message) =>
        new(400, "invalid_input", message);

    private static Priority? ParsePriority(string? value) => value?.ToLowerInvariant() switch
    {
        null or "" or "none" => null,
        "low" => Priority.Low,
        "medium" => Priority.Medium,
        "high" => Priority.High,
        "urgent" => Priority.Urgent,
        _ => throw new BoardApiException(400, "invalid_input", "Choose a valid importance."),
    };

    private static string ParseGroupRole(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "organizer" => "organizer",
        "member" => "member",
        "view_only" => "view_only",
        _ => throw new BoardApiException(400, "invalid_role", "Choose organizer, member, or view only."),
    };

    private static string ParseIcon(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        null or "" or "ghost" => "ghost",
        "pumpkin" => "pumpkin",
        "bat" => "bat",
        _ => throw new BoardApiException(400, "invalid_icon", "Choose ghost, pumpkin, or bat."),
    };

    private static string ParseAccent(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        null or "" or "purple" => "purple",
        "pumpkin" => "pumpkin",
        "green" => "green",
        "berry" => "berry",
        _ => throw new BoardApiException(400, "invalid_accent", "Choose pumpkin, purple, green, or berry."),
    };

    private static string FormatPriority(Priority? value) => value switch
    {
        Priority.Low => "low",
        Priority.Medium => "medium",
        Priority.High => "high",
        Priority.Urgent => "urgent",
        _ => "none",
    };

    private static BoardStatus ParseColumn(string columnId, ObjectId boardId)
    {
        var prefix = $"{boardId}:";
        if (!columnId.StartsWith(prefix, StringComparison.Ordinal))
            throw new BoardApiException(400, "invalid_column", "Choose a column on this board.");
        return columnId[prefix.Length..] switch
        {
            "backlog" => BoardStatus.Backlog,
            "up-next" => BoardStatus.UpNext,
            "doing" => BoardStatus.InProgress,
            "waiting" => BoardStatus.Waiting,
            "done" => BoardStatus.Completed,
            _ => throw new BoardApiException(400, "invalid_column", "Choose a valid board column."),
        };
    }

    private static List<BoardColumnDefinition> ParseColumns(
        Board board,
        IReadOnlyList<UpdateBoardColumnRequest> columns)
    {
        if (columns.Count != 5)
            throw new BoardApiException(400, "invalid_columns", "Keep all five board columns.");
        var parsed = new List<BoardColumnDefinition>();
        foreach (var column in columns.OrderBy(column => column.Rank))
        {
            var status = ParseColumn(column.Id, board.Id);
            var name = RequiredText(column.Name, "column name", 1, 30);
            var color = column.Color.Trim().ToLowerInvariant();
            if (color is not ("purple" or "pumpkin" or "green" or "berry" or "neutral"))
                throw new BoardApiException(400, "invalid_columns", "Choose a valid column color.");
            parsed.Add(new BoardColumnDefinition
            {
                Status = status,
                Name = name,
                Rank = parsed.Count * 1024 + 1024,
                Color = color,
            });
        }
        if (parsed.Select(column => column.Status).Distinct().Count() != 5
            || parsed.Select(column => column.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 5)
            throw new BoardApiException(400, "invalid_columns", "Each column needs a different name and position.");
        return parsed;
    }

    private static List<ulong> ParsePeople(IReadOnlyList<string>? values)
    {
        if (values is null)
            return [];
        var people = new List<ulong>();
        foreach (var value in values)
        {
            if (!ulong.TryParse(value, out var id))
                throw new BoardApiException(400, "invalid_person", "Choose a valid Discord member.");
            people.Add(id);
        }

        return people.Distinct().ToList();
    }

    private static GitHubIssueLink ParseGitHubIssue(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            throw InvalidGitHubIssue();
        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || parts[2] != "issues" || !int.TryParse(parts[3], out var issueNumber) || issueNumber < 1)
            throw InvalidGitHubIssue();

        return new GitHubIssueLink
        {
            Url = $"https://github.com/{parts[0]}/{parts[1]}/issues/{issueNumber}",
            Owner = parts[0],
            Repository = parts[1],
            IssueNumber = issueNumber,
        };
    }

    private static BoardApiException InvalidGitHubIssue() => new(
        400,
        "invalid_link",
        "Use a link like github.com/owner/repo/issues/123.");

    private static double EffectiveRank(TaskItem task) => task.Rank > 0
        ? task.Rank
        : new DateTimeOffset(DateTime.SpecifyKind(task.CreatedAt, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    private static string EveryoneGroupId(ulong guildId) => $"{EveryoneGroupPrefix}{guildId}";

    private static string ColumnId(ObjectId boardId, BoardStatus status) => status switch
    {
        BoardStatus.Backlog => $"{boardId}:backlog",
        BoardStatus.UpNext => $"{boardId}:up-next",
        BoardStatus.InProgress => $"{boardId}:doing",
        BoardStatus.Waiting => $"{boardId}:waiting",
        BoardStatus.Completed => $"{boardId}:done",
        _ => $"{boardId}:archived",
    };

    private static string Slug(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
                builder.Append(character);
            else if (builder.Length > 0 && builder[^1] != '-')
                builder.Append('-');
        }
        return builder.ToString().Trim('-');
    }

    private static string TagId(string groupId, string tag)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{groupId}:{tag.Trim().ToLowerInvariant()}"));
        return $"tag-{Convert.ToHexString(bytes)[..12].ToLowerInvariant()}";
    }

    private sealed record RequestIdentity(
        DiscordGuild Guild,
        DiscordMember Member,
        bool IsAdministrator,
        bool CanOrganize)
    {
        public ulong UserId => Member.Id;
    }
    private sealed record TagValue(string GroupId, string Name);
    private sealed record ActivityResponse(
        string Id,
        string? ActorId,
        string GroupId,
        string? BoardId,
        string CardId,
        string Kind,
        string Summary,
        DateTime CreatedAt);
    private sealed record PersonResponse(
        string Id,
        string Name,
        string Email,
        string? Image,
        string DiscordId,
        string SystemRole,
        bool Active,
        IReadOnlyDictionary<string, string> GroupRoles);
}
