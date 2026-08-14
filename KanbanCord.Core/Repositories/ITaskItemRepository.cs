using KanbanCord.Core.Models;
using MongoDB.Bson;

namespace KanbanCord.Core.Repositories;

public interface ITaskItemRepository
{
    Task<IReadOnlyList<TaskItem>> GetAllTaskItemsByGuildIdAsync(ulong guildId);

    Task<IReadOnlyList<TaskItem>> GetAllTaskItemsByBoardIdAsync(ulong guildId, ObjectId boardId);
    
    Task<TaskItem?> GetTaskItemByObjectIdOrDefaultAsync(ObjectId objectId);

    Task<TaskItem?> GetTaskItemByObjectIdOrDefaultAsync(ObjectId objectId, ulong guildId);
    
    Task AddTaskItemAsync(TaskItem task);
    
    Task UpdateTaskItemAsync(TaskItem task);
    
    Task RemoveTaskItemAsync(TaskItem task);
    
    Task RemoveAllTaskItemsByIdAsync(ulong guildId);

    Task RemoveAllTaskItemsByBoardIdAsync(ulong guildId, ObjectId boardId);

    Task AssignLegacyTasksToBoardAsync(ulong guildId, ObjectId boardId);
}
