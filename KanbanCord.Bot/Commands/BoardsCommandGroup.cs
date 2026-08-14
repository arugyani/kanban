using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using KanbanCord.Bot.Extensions;
using KanbanCord.Bot.Helpers;
using KanbanCord.Bot.Providers;
using KanbanCord.Core.Constants;
using KanbanCord.Core.Models;
using KanbanCord.Core.Repositories;
using MongoDB.Bson;

namespace KanbanCord.Bot.Commands;

[Command("boards")]
[RequirePermissions(userPermissions: [DiscordPermission.ManageMessages], botPermissions: [])]
public class BoardsCommandGroup
{
    private readonly IBoardRepository _boardRepository;
    private readonly ITaskItemRepository _taskItemRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly BoardResolver _boardResolver;

    public BoardsCommandGroup(
        IBoardRepository boardRepository,
        ITaskItemRepository taskItemRepository,
        ITeamRepository teamRepository,
        BoardResolver boardResolver)
    {
        _boardRepository = boardRepository;
        _taskItemRepository = taskItemRepository;
        _teamRepository = teamRepository;
        _boardResolver = boardResolver;
    }

    [Command("list")]
    [Description("List the boards in this server.")]
    [RequirePermissions(userPermissions: [], botPermissions: [])]
    public async ValueTask ListAsync(SlashCommandContext context)
    {
        await _boardResolver.GetDefaultAsync(context.Guild!.Id, context.User.Id);
        var boards = await _boardRepository.GetAllByGuildIdAsync(context.Guild.Id);
        var teams = await _teamRepository.GetAllByGuildIdAsync(context.Guild.Id);
        var tasks = await _taskItemRepository.GetAllTaskItemsByGuildIdAsync(context.Guild.Id);

        var description = string.Join('\n', boards.Select(board =>
        {
            var team = board.TeamId.HasValue ? teams.FirstOrDefault(candidate => candidate.Id == board.TeamId) : null;
            var suffix = team is null ? string.Empty : $" · {team.Name}";
            var defaultMarker = board.IsDefault ? " (default)" : string.Empty;
            var taskCount = tasks.Count(task => task.BoardId == board.Id);
            return $"**{board.Name}**{defaultMarker}{suffix} — {taskCount} task{(taskCount == 1 ? string.Empty : "s")}";
        }));

        var embed = new DiscordEmbedBuilder()
            .WithDefaultColor()
            .WithAuthor("KanbanCord Boards")
            .WithDescription(description);

        await context.RespondAsync(embed);
    }

    [Command("create")]
    [Description("Create a board, optionally owned by a team.")]
    public async ValueTask CreateAsync(
        SlashCommandContext context,
        [Description("Board name")] string name,
        [Description("Team that owns this board")] [SlashAutoCompleteProvider<TeamAutoCompleteProvider>] string? team = null)
    {
        name = name.Trim();

        await _boardResolver.GetDefaultAsync(context.Guild!.Id, context.User.Id);

        if (name.Length is 0 or > Limits.BoardNameMaxLength)
        {
            await RespondWithErrorAsync(context, $"Board names must be between 1 and {Limits.BoardNameMaxLength} characters.");
            return;
        }

        if (await _boardRepository.NameExistsAsync(context.Guild.Id, name))
        {
            await RespondWithErrorAsync(context, $"A board named \"{name}\" already exists.");
            return;
        }

        Team? selectedTeam = null;

        if (team is not null)
        {
            selectedTeam = await ResolveTeamAsync(context.Guild.Id, team);

            if (selectedTeam is null)
            {
                await RespondWithErrorAsync(context, "The selected team was not found.");
                return;
            }
        }

        var board = new Board
        {
            GuildId = context.Guild.Id,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            TeamId = selectedTeam?.Id,
            CreatedById = context.User.Id
        };

        await _boardRepository.AddAsync(board);

        var description = selectedTeam is null
            ? $"Created the **{board.Name}** board."
            : $"Created the **{board.Name}** board for **{selectedTeam.Name}**.";
        await context.RespondAsync(new DiscordEmbedBuilder().WithDefaultColor().WithDescription(description));
    }

    [Command("rename")]
    [Description("Rename a board.")]
    public async ValueTask RenameAsync(
        SlashCommandContext context,
        [Description("Board to rename")] [SlashAutoCompleteProvider<BoardAutoCompleteProvider>] string board,
        [Description("New board name")] string name)
    {
        var selectedBoard = await _boardResolver.ResolveAsync(context.Guild!.Id, context.User.Id, board);
        name = name.Trim();

        if (selectedBoard is null)
        {
            await RespondWithErrorAsync(context, "The selected board was not found.");
            return;
        }

        if (name.Length is 0 or > Limits.BoardNameMaxLength)
        {
            await RespondWithErrorAsync(context, $"Board names must be between 1 and {Limits.BoardNameMaxLength} characters.");
            return;
        }

        if (await _boardRepository.NameExistsAsync(context.Guild.Id, name, selectedBoard.Id))
        {
            await RespondWithErrorAsync(context, $"A board named \"{name}\" already exists.");
            return;
        }

        var oldName = selectedBoard.Name;
        selectedBoard.Name = name;
        await _boardRepository.UpdateAsync(selectedBoard);

        await context.RespondAsync(new DiscordEmbedBuilder()
            .WithDefaultColor()
            .WithDescription($"Renamed **{oldName}** to **{selectedBoard.Name}**."));
    }

    [Command("set-team")]
    [Description("Set or clear the team that owns a board.")]
    public async ValueTask SetTeamAsync(
        SlashCommandContext context,
        [Description("Board to update")] [SlashAutoCompleteProvider<BoardAutoCompleteProvider>] string board,
        [Description("Team to assign; omit to clear")] [SlashAutoCompleteProvider<TeamAutoCompleteProvider>] string? team = null)
    {
        var selectedBoard = await _boardResolver.ResolveAsync(context.Guild!.Id, context.User.Id, board);

        if (selectedBoard is null)
        {
            await RespondWithErrorAsync(context, "The selected board was not found.");
            return;
        }

        Team? selectedTeam = null;

        if (team is not null)
        {
            selectedTeam = await ResolveTeamAsync(context.Guild.Id, team);

            if (selectedTeam is null)
            {
                await RespondWithErrorAsync(context, "The selected team was not found.");
                return;
            }
        }

        selectedBoard.TeamId = selectedTeam?.Id;
        await _boardRepository.UpdateAsync(selectedBoard);

        var description = selectedTeam is null
            ? $"The **{selectedBoard.Name}** board is no longer owned by a team."
            : $"The **{selectedBoard.Name}** board now belongs to **{selectedTeam.Name}**.";
        await context.RespondAsync(new DiscordEmbedBuilder().WithDefaultColor().WithDescription(description));
    }

    [Command("delete")]
    [Description("Delete an empty, non-default board.")]
    public async ValueTask DeleteAsync(
        SlashCommandContext context,
        [Description("Board to delete")] [SlashAutoCompleteProvider<BoardAutoCompleteProvider>] string board)
    {
        var selectedBoard = await _boardResolver.ResolveAsync(context.Guild!.Id, context.User.Id, board);

        if (selectedBoard is null)
        {
            await RespondWithErrorAsync(context, "The selected board was not found.");
            return;
        }

        if (selectedBoard.IsDefault)
        {
            await RespondWithErrorAsync(context, "The default board cannot be deleted.");
            return;
        }

        var tasks = await _taskItemRepository.GetAllTaskItemsByBoardIdAsync(context.Guild.Id, selectedBoard.Id);

        if (tasks.Count != 0)
        {
            await RespondWithErrorAsync(context, "Move or delete this board's tasks before deleting it.");
            return;
        }

        await _boardRepository.RemoveAsync(selectedBoard);
        await context.RespondAsync(new DiscordEmbedBuilder()
            .WithDefaultColor()
            .WithDescription($"Deleted the **{selectedBoard.Name}** board."));
    }

    private async Task<Team?> ResolveTeamAsync(ulong guildId, string teamId)
    {
        return ObjectId.TryParse(teamId, out var objectId)
            ? await _teamRepository.GetByObjectIdOrDefaultAsync(objectId, guildId)
            : null;
    }

    private static async System.Threading.Tasks.Task RespondWithErrorAsync(SlashCommandContext context, string description)
    {
        await context.RespondAsync(new DiscordEmbedBuilder().WithDefaultColor().WithDescription(description));
    }
}
