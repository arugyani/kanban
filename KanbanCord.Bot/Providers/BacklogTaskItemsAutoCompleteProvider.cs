using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using KanbanCord.Bot.Helpers;
using KanbanCord.Core.Models;
using KanbanCord.Core.Repositories;

namespace KanbanCord.Bot.Providers;

public class BacklogTaskItemsAutoCompleteProvider : IAutoCompleteProvider
{
    private readonly ITaskItemRepository _repository;
    private readonly IBoardRepository _boardRepository;
    private readonly BoardResolver _boardResolver;

    public BacklogTaskItemsAutoCompleteProvider(
        ITaskItemRepository repository,
        IBoardRepository boardRepository,
        BoardResolver boardResolver)
    {
        _repository = repository;
        _boardRepository = boardRepository;
        _boardResolver = boardResolver;
    }
    
    
    public async ValueTask<IEnumerable<DiscordAutoCompleteChoice>> AutoCompleteAsync(AutoCompleteContext context)
    {
        await _boardResolver.GetDefaultAsync(context.Guild!.Id, context.User.Id);
        var taskItems = await _repository.GetAllTaskItemsByGuildIdAsync(context.Guild.Id);
        var boardNames = (await _boardRepository.GetAllByGuildIdAsync(context.Guild.Id))
            .ToDictionary(board => board.Id, board => board.Name);

        var response = taskItems.GetAutoCompleteStrings(BoardStatus.Backlog, boardNames);
        
        return response.Where(x => context.UserInput == null || x.Name.Contains(context.UserInput, StringComparison.OrdinalIgnoreCase))
            .Take(20);
    }
}
