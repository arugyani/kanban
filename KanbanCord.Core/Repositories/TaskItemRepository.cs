using KanbanCord.Core.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace KanbanCord.Core.Repositories;

public class TaskItemRepository : ITaskItemRepository
{
    private readonly IMongoCollection<TaskItem> _collection;

    public TaskItemRepository(IMongoDatabase mongoDatabase)
    {
        _collection = mongoDatabase.GetCollection<TaskItem>(nameof(RequiredCollections.Tasks));
    }

    public async Task<IReadOnlyList<TaskItem>> GetAllTaskItemsByGuildIdAsync(ulong guildId)
    {
        var tasks = await _collection.Find(task => task.GuildId == guildId).ToListAsync() ?? [];

        return tasks;
    }

    public async Task<IReadOnlyList<TaskItem>> GetAllTaskItemsByBoardIdAsync(ulong guildId, ObjectId boardId)
    {
        var tasks = await _collection
            .Find(task => task.GuildId == guildId && task.BoardId == boardId)
            .ToListAsync() ?? [];

        return tasks;
    }

    public async Task<TaskItem?> GetTaskItemByObjectIdOrDefaultAsync(ObjectId objectId)
    {
        var task = await _collection.Find(task => task.Id == objectId).FirstOrDefaultAsync();

        return task;
    }

    public async Task<TaskItem?> GetTaskItemByObjectIdOrDefaultAsync(ObjectId objectId, ulong guildId)
    {
        return await _collection
            .Find(task => task.Id == objectId && task.GuildId == guildId)
            .FirstOrDefaultAsync();
    }

    public async Task AddTaskItemAsync(TaskItem task)
    {
        await _collection.InsertOneAsync(task);
    }

    public async Task UpdateTaskItemAsync(TaskItem task)
    {
        await _collection.ReplaceOneAsync(x => x.Id == task.Id, task);
    }

    public async Task RemoveTaskItemAsync(TaskItem task)
    {
        await _collection.DeleteOneAsync(x => x.Id == task.Id);
    }

    public async Task RemoveAllTaskItemsByIdAsync(ulong guildId)
    {
        await _collection.DeleteManyAsync(x => x.GuildId == guildId);
    }

    public async Task RemoveAllTaskItemsByBoardIdAsync(ulong guildId, ObjectId boardId)
    {
        await _collection.DeleteManyAsync(task => task.GuildId == guildId && task.BoardId == boardId);
    }

    public async Task AssignLegacyTasksToBoardAsync(ulong guildId, ObjectId boardId)
    {
        var filter = Builders<TaskItem>.Filter.Eq(task => task.GuildId, guildId)
                     & Builders<TaskItem>.Filter.Eq(task => task.BoardId, null);
        var update = Builders<TaskItem>.Update.Set(task => task.BoardId, boardId);

        await _collection.UpdateManyAsync(filter, update);
    }
}
