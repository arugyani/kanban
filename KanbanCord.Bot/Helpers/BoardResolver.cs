using KanbanCord.Core.Models;
using KanbanCord.Core.Repositories;
using MongoDB.Bson;

namespace KanbanCord.Bot.Helpers;

public class BoardResolver
{
    private readonly IBoardRepository _boardRepository;
    private readonly ITaskItemRepository _taskItemRepository;

    public BoardResolver(IBoardRepository boardRepository, ITaskItemRepository taskItemRepository)
    {
        _boardRepository = boardRepository;
        _taskItemRepository = taskItemRepository;
    }

    public async Task<Board> GetDefaultAsync(ulong guildId, ulong createdById)
    {
        var defaultBoard = await _boardRepository.GetOrCreateDefaultAsync(guildId, createdById);
        await _taskItemRepository.AssignLegacyTasksToBoardAsync(guildId, defaultBoard.Id);

        return defaultBoard;
    }

    public async Task<Board?> ResolveAsync(ulong guildId, ulong createdById, string? boardId)
    {
        var defaultBoard = await GetDefaultAsync(guildId, createdById);

        if (string.IsNullOrWhiteSpace(boardId))
            return defaultBoard;

        return ObjectId.TryParse(boardId, out var objectId)
            ? await _boardRepository.GetByObjectIdOrDefaultAsync(objectId, guildId)
            : null;
    }
}
