using KanbanCord.Core.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace KanbanCord.Core.Repositories;

public class TaskItemRepository : ITaskItemRepository
{
    private readonly IMongoCollection<TaskItem> _collection;
    private readonly CardNumberRegistry _numbers;

    public TaskItemRepository(IMongoDatabase mongoDatabase)
    {
        _collection = mongoDatabase.GetCollection<TaskItem>(nameof(RequiredCollections.Tasks));
        _numbers = new CardNumberRegistry(mongoDatabase);
    }

    public async Task<IReadOnlyList<TaskItem>> GetAllTaskItemsByGuildIdAsync(ulong guildId)
    {
        var tasks = await _collection.Find(task => task.GuildId == guildId).ToListAsync() ?? [];
        await _numbers.AssignAsync(tasks);

        return tasks;
    }

    public async Task<IReadOnlyList<TaskItem>> GetAllTaskItemsByBoardIdAsync(ulong guildId, ObjectId boardId)
    {
        var tasks = await _collection
            .Find(task => task.GuildId == guildId && task.BoardId == boardId)
            .ToListAsync() ?? [];

        await _numbers.AssignAsync(tasks);

        return tasks;
    }

    public async Task<TaskItem?> GetTaskItemByObjectIdOrDefaultAsync(ObjectId objectId)
    {
        var task = await _collection.Find(task => task.Id == objectId).FirstOrDefaultAsync();
        if (task is not null)
            await _numbers.AssignAsync([task]);

        return task;
    }

    public async Task<TaskItem?> GetTaskItemByObjectIdOrDefaultAsync(ObjectId objectId, ulong guildId)
    {
        var task = await _collection
            .Find(task => task.Id == objectId && task.GuildId == guildId)
            .FirstOrDefaultAsync();
        if (task is not null)
            await _numbers.AssignAsync([task]);
        return task;
    }

    public async Task<TaskItem?> GetByReferenceOrDefaultAsync(string reference, ulong guildId)
    {
        if (ObjectId.TryParse(reference, out var objectId))
            return await GetTaskItemByObjectIdOrDefaultAsync(objectId, guildId);
        if (!CardReference.TryParse(reference, out var number))
            return null;
        var id = await _numbers.FindCardIdAsync(guildId, number);
        return id.HasValue ? await GetTaskItemByObjectIdOrDefaultAsync(id.Value, guildId) : null;
    }

    public async Task<TaskItem?> GetByDiscordMessageIdOrDefaultAsync(ulong guildId, ulong messageId)
    {
        var task = await _collection
            .Find(task => task.GuildId == guildId && task.DiscordMessageId == messageId)
            .FirstOrDefaultAsync();
        if (task is not null)
            await _numbers.AssignAsync([task]);
        return task;
    }

    public async Task AddTaskItemAsync(TaskItem task)
    {
        await PrepareReferenceAsync(task);
        task.CommitChange("card_added", $"{task.Title} was added.");
        await _collection.InsertOneAsync(task);
    }

    public async Task<bool> TryAddTaskItemAsync(TaskItem task)
    {
        await PrepareReferenceAsync(task);
        task.CommitChange("card_added", $"{task.Title} was added.");
        try
        {
            await _collection.InsertOneAsync(task);
            return true;
        }
        catch (MongoWriteException exception)
            when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task UpdateTaskItemAsync(TaskItem task)
    {
        var expectedVersion = Math.Max(0, task.Version);
        if (!await TryUpdateTaskItemAsync(task, expectedVersion))
            throw new TaskItemVersionConflictException(task.Id);
    }

    public async Task<bool> TryUpdateTaskItemAsync(TaskItem task, long expectedVersion)
    {
        var versionFilter = expectedVersion == 0
            ? Builders<TaskItem>.Filter.Or(
                Builders<TaskItem>.Filter.Eq(candidate => candidate.Version, 0),
                Builders<TaskItem>.Filter.Exists(candidate => candidate.Version, false))
            : Builders<TaskItem>.Filter.Eq(candidate => candidate.Version, expectedVersion);
        var filter = Builders<TaskItem>.Filter.Eq(candidate => candidate.Id, task.Id)
                     & Builders<TaskItem>.Filter.Eq(candidate => candidate.GuildId, task.GuildId)
                     & versionFilter;

        task.CommitChange("card_updated", $"{task.Title} was updated.");
        task.Version = expectedVersion + 1;
        task.LastUpdatedAt = DateTime.UtcNow;
        var result = await _collection.ReplaceOneAsync(filter, task);

        return result.ModifiedCount == 1;
    }

    public async Task BackfillReferencesAsync(CancellationToken cancellationToken)
    {
        using var cursor = await _collection.Find(Builders<TaskItem>.Filter.Empty)
            .SortBy(task => task.CreatedAt).ThenBy(task => task.Id)
            .ToCursorAsync(cancellationToken);
        while (await cursor.MoveNextAsync(cancellationToken))
            await _numbers.AssignAsync(cursor.Current.ToArray(), cancellationToken);
    }

    private async Task PrepareReferenceAsync(TaskItem task)
    {
        if (task.Id == ObjectId.Empty)
            task.Id = ObjectId.GenerateNewId();
        await _numbers.AssignAsync([task]);
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
