using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using KanbanCord.Bot.Helpers;
using KanbanCord.Core.Repositories;

namespace KanbanCord.Bot.Providers;

public class BoardAutoCompleteProvider : IAutoCompleteProvider
{
    private readonly IBoardRepository _boardRepository;
    private readonly BoardResolver _boardResolver;

    public BoardAutoCompleteProvider(IBoardRepository boardRepository, BoardResolver boardResolver)
    {
        _boardRepository = boardRepository;
        _boardResolver = boardResolver;
    }

    public async ValueTask<IEnumerable<DiscordAutoCompleteChoice>> AutoCompleteAsync(AutoCompleteContext context)
    {
        await _boardResolver.GetDefaultAsync(context.Guild!.Id, context.User.Id);
        var boards = await _boardRepository.GetAllByGuildIdAsync(context.Guild.Id);

        return boards
            .Where(board => context.UserInput is null
                            || board.Name.Contains(context.UserInput, StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .Select(board => new DiscordAutoCompleteChoice(board.Name, board.Id.ToString()));
    }
}
