using KanbanCord.Core.Models;
using KanbanCord.Core.Repositories;
using MongoDB.Bson;

namespace KanbanCord.Bot.Helpers;

public class BoardResolver
{
    private readonly IBoardRepository _boardRepository;
    private readonly ITaskItemRepository _taskItemRepository;
    private readonly BoardAuthorizationService _authorization;

    public BoardResolver(
        IBoardRepository boardRepository,
        ITaskItemRepository taskItemRepository,
        BoardAuthorizationService authorization)
    {
        _boardRepository = boardRepository;
        _taskItemRepository = taskItemRepository;
        _authorization = authorization;
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

        var board = ObjectId.TryParse(boardId, out var objectId)
            ? await _boardRepository.GetByObjectIdOrDefaultAsync(objectId, guildId)
            : null;
        return board is not null && await _authorization.CanViewAsync(board, createdById)
            ? board
            : null;
    }

    public async Task<Board?> ResolveEditableAsync(ulong guildId, ulong userId, string? boardId)
    {
        var board = await ResolveAsync(guildId, userId, boardId);
        return board is not null && await _authorization.CanEditAsync(board, userId)
            ? board
            : null;
    }

    public async Task<IReadOnlyList<Board>> GetAccessibleBoardsAsync(ulong guildId, ulong userId)
    {
        await GetDefaultAsync(guildId, userId);
        var boards = await _boardRepository.GetAllByGuildIdAsync(guildId);
        var accessible = new List<Board>();
        foreach (var board in boards)
        {
            if (await _authorization.CanViewAsync(board, userId))
                accessible.Add(board);
        }

        return accessible;
    }

    public async Task<IReadOnlyList<TaskItem>> GetAccessibleTaskItemsAsync(ulong guildId, ulong userId)
    {
        var boardIds = (await GetAccessibleBoardsAsync(guildId, userId))
            .Select(board => board.Id)
            .ToHashSet();
        return (await _taskItemRepository.GetAllTaskItemsByGuildIdAsync(guildId))
            .Where(task => task.BoardId.HasValue && boardIds.Contains(task.BoardId.Value))
            .ToList();
    }

    public Task<bool> CanIncludePersonAsync(Board board, ulong userId) =>
        _authorization.CanViewAsync(board, userId);
}
