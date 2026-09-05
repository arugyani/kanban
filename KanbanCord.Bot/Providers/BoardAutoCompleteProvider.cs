using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using KanbanCord.Bot.Helpers;

namespace KanbanCord.Bot.Providers;

public class BoardAutoCompleteProvider : IAutoCompleteProvider
{
    private readonly BoardResolver _boardResolver;

    public BoardAutoCompleteProvider(BoardResolver boardResolver)
    {
        _boardResolver = boardResolver;
    }

    public async ValueTask<IEnumerable<DiscordAutoCompleteChoice>> AutoCompleteAsync(AutoCompleteContext context)
    {
        var boards = await _boardResolver.GetAccessibleBoardsAsync(context.Guild!.Id, context.User.Id);

        return boards
            .Where(board => context.UserInput is null
                            || board.Name.Contains(context.UserInput, StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .Select(board => new DiscordAutoCompleteChoice(board.Name, board.Id.ToString()));
    }
}
