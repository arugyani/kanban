using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using KanbanCord.Bot.Extensions;
using KanbanCord.Bot.Helpers;
using KanbanCord.Bot.Providers;
using KanbanCord.Core.Repositories;

namespace KanbanCord.Bot.Commands;

public class PeopleCommand
{
    private readonly BoardResolver _boardResolver;
    private readonly ITaskItemRepository _taskItemRepository;
    private readonly ITeamRepository _teamRepository;

    public PeopleCommand(
        BoardResolver boardResolver,
        ITaskItemRepository taskItemRepository,
        ITeamRepository teamRepository)
    {
        _boardResolver = boardResolver;
        _taskItemRepository = taskItemRepository;
        _teamRepository = teamRepository;
    }

    [Command("people")]
    [Description("Show the people contributing to a board.")]
    public async ValueTask ExecuteAsync(
        SlashCommandContext context,
        [Description("Board to inspect; defaults to Default")][SlashAutoCompleteProvider<BoardAutoCompleteProvider>] string? board = null)
    {
        var selectedBoard = await _boardResolver.ResolveAsync(context.Guild!.Id, context.User.Id, board);

        if (selectedBoard is null)
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithDefaultColor()
                .WithDescription("The selected board was not found."));
            return;
        }

        var tasks = await _taskItemRepository.GetAllTaskItemsByBoardIdAsync(context.Guild.Id, selectedBoard.Id);
        var contributorIds = tasks
            .SelectMany(task => new ulong?[] { task.AuthorId, task.AssigneeId })
            .Where(personId => personId.HasValue)
            .Select(personId => personId!.Value)
            .Distinct()
            .ToList();

        var embed = new DiscordEmbedBuilder()
            .WithDefaultColor()
            .WithAuthor("KanbanCord People")
            .WithTitle(selectedBoard.Name)
            .AddField(
                "Contributors",
                contributorIds.Count == 0
                    ? "No contributors yet"
                    : string.Join(", ", contributorIds.Take(30).Select(personId => $"<@{personId}>")));

        if (selectedBoard.TeamId.HasValue)
        {
            var team = await _teamRepository.GetByObjectIdOrDefaultAsync(selectedBoard.TeamId.Value, context.Guild.Id);

            if (team is not null)
            {
                var people = team.MemberIds.Count == 0
                    ? "No people yet"
                    : string.Join(", ", team.MemberIds.Take(30).Select(personId => $"<@{personId}>"));
                embed.AddField($"{team.Name} team", people);
            }
        }

        await context.RespondAsync(embed);
    }
}
