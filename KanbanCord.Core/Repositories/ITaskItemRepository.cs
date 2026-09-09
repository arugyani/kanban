using KanbanCord.Core.Models;
using MongoDB.Bson;

namespace KanbanCord.Core.Repositories;

public interface ITaskItemRepository
{
    Task<IReadOnlyList<TaskItem>> GetAllTaskItemsByGuildIdAsync(ulong guildId);

    Task<IReadOnlyList<TaskItem>> GetAllTaskItemsByBoardIdAsync(ulong guildId, ObjectId boardId);

    Task<TaskItem?> GetTaskItemByObjectIdOrDefaultAsync(ObjectId objectId);

    Task<TaskItem?> GetTaskItemByObjectIdOrDefaultAsync(ObjectId objectId, ulong guildId);

    Task<TaskItem?> GetByReferenceOrDefaultAsync(string reference, ulong guildId);

    Task<TaskItem?> GetByDiscordMessageIdOrDefaultAsync(ulong guildId, ulong messageId);

    Task AddTaskItemAsync(TaskItem task);

    Task<bool> TryAddTaskItemAsync(TaskItem task);

    Task UpdateTaskItemAsync(TaskItem task);

    Task<bool> TryUpdateTaskItemAsync(TaskItem task, long expectedVersion);

    Task RemoveTaskItemAsync(TaskItem task);

    Task RemoveAllTaskItemsByIdAsync(ulong guildId);

    Task RemoveAllTaskItemsByBoardIdAsync(ulong guildId, ObjectId boardId);

    Task AssignLegacyTasksToBoardAsync(ulong guildId, ObjectId boardId);
}
