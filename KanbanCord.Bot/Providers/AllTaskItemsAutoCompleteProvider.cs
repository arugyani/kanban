using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using KanbanCord.Bot.Helpers;

namespace KanbanCord.Bot.Providers;

public class AllTaskItemsAutoCompleteProvider : IAutoCompleteProvider
{
    private readonly BoardResolver _boardResolver;

    public AllTaskItemsAutoCompleteProvider(BoardResolver boardResolver)
    {
        _boardResolver = boardResolver;
    }


    public async ValueTask<IEnumerable<DiscordAutoCompleteChoice>> AutoCompleteAsync(AutoCompleteContext context)
    {
        var taskItems = await _boardResolver.GetAccessibleTaskItemsAsync(context.Guild!.Id, context.User.Id);
        var boardNames = (await _boardResolver.GetAccessibleBoardsAsync(context.Guild.Id, context.User.Id))
            .ToDictionary(board => board.Id, board => board.Name);

        var response = taskItems.GetAutoCompleteStrings(boardNames: boardNames);

        return response.Where(x => context.UserInput == null || x.Name.Contains(context.UserInput, StringComparison.OrdinalIgnoreCase))
            .Take(20);
    }
}
