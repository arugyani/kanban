using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using KanbanCord.Bot.Extensions;
using KanbanCord.Bot.Helpers;
using KanbanCord.Bot.Providers;
using KanbanCord.Core.Models;
using KanbanCord.Core.Repositories;

namespace KanbanCord.Bot.Commands;

[Command("board")]
public class BoardCommand
{
    private readonly ITaskItemRepository _repository;
    private readonly ITeamRepository _teamRepository;
    private readonly BoardResolver _boardResolver;

    public BoardCommand(
        ITaskItemRepository repository,
        ITeamRepository teamRepository,
        BoardResolver boardResolver)
    {
        _repository = repository;
        _teamRepository = teamRepository;
        _boardResolver = boardResolver;
    }


    [Command("recap")]
    [Description("Summarize a board or one of its columns.")]
    public async ValueTask ExecuteAsync(
        SlashCommandContext context,
        [Description("Board to show; defaults to Default")][SlashAutoCompleteProvider<BoardAutoCompleteProvider>] string? board = null,
        [Description("Column to show")][SlashChoiceProvider<ColumnChoiceProvider>] int? column = null)
    {
        await context.DeferResponseAsync();

        var selectedBoard = await _boardResolver.ResolveAsync(context.Guild!.Id, context.User.Id, board);

        if (selectedBoard is null)
        {
            await context.EditDeferredResponseAsync("The selected board was not found.");
            return;
        }

        var boardItems = await _repository.GetAllTaskItemsByBoardIdAsync(context.Guild.Id, selectedBoard.Id);
        var team = selectedBoard.TeamId.HasValue
            ? await _teamRepository.GetByObjectIdOrDefaultAsync(selectedBoard.TeamId.Value, context.Guild.Id)
            : null;

        var boardStatus = (BoardStatus?)column;

        var embed = await BoardHelper.GetBoardEmbed(context.Client, boardItems, boardStatus, selectedBoard, team);

        await context.EditDeferredResponseAsync(embed);
    }
}
