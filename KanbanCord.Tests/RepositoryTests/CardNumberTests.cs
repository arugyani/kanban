using KanbanCord.Bot.BackgroundServices;
using KanbanCord.Core.Models;
using KanbanCord.Core.Repositories;
using Mongo2Go;
using MongoDB.Bson;
using MongoDB.Driver;

namespace KanbanCord.Tests.RepositoryTests;

[Collection(MongoDatabaseCollection.Name)]
public sealed class CardNumberTests : IDisposable
{
    private readonly MongoDbRunner _runner = MongoDbRunner.Start();
    private readonly IMongoDatabase _database;
    private readonly TaskItemRepository _repository;

    public CardNumberTests()
    {
        _database = new MongoClient(_runner.ConnectionString).GetDatabase("CardNumbers");
        _repository = new TaskItemRepository(_database);
    }

    [Fact]
    public async Task NewCards_ShareOneSequenceAcrossBoardsButNotGuilds()
    {
        var first = Card();
        var second = Card();
        var elsewhere = Card(456);
        await _repository.AddTaskItemAsync(first);
        await _repository.AddTaskItemAsync(second);
        await _repository.AddTaskItemAsync(elsewhere);

        Assert.Equal("BOO-001", first.Key);
        Assert.Equal("BOO-002", second.Key);
        Assert.Equal("BOO-001", elsewhere.Key);
        Assert.NotEqual(first.BoardId, second.BoardId);
    }

    [Fact]
    public async Task SimultaneousCreates_AcrossRepositoryInstancesReserveUniqueNumbers()
    {
        await DatabaseSetupBackgroundService.EnsureDatabaseAsync(_database, CancellationToken.None);
        var cards = Enumerable.Range(0, 32).Select(_ => Card()).ToArray();
        await Task.WhenAll(cards.Select(card => new TaskItemRepository(_database).AddTaskItemAsync(card)));

        Assert.Equal(Enumerable.Range(1, 32).Select(number => (long)number), cards.Select(card => card.CardNumber).Order());
        Assert.Equal(32, (await _repository.GetAllTaskItemsByGuildIdAsync(123)).Count);
    }

    [Fact]
    public async Task References_SurviveEditsTransfersAndRepositoryRestarts()
    {
        var card = Card();
        await _repository.AddTaskItemAsync(card);
        var originalId = card.Id;
        card.Title = "Renamed";
        card.BoardId = ObjectId.GenerateNewId();
        card.Status = BoardStatus.Completed;
        await _repository.UpdateTaskItemAsync(card);

        var restarted = new TaskItemRepository(_database);
        var saved = await restarted.GetByReferenceOrDefaultAsync("boo-1", 123);
        Assert.NotNull(saved);
        Assert.Equal(originalId, saved.Id);
        Assert.Equal("BOO-001", saved.Key);
        Assert.Equal("Renamed", saved.Title);
        Assert.Equal(card.BoardId, saved.BoardId);
        Assert.Equal(1, saved.Version);
        Assert.Null(await restarted.GetByReferenceOrDefaultAsync("BOO-001", 456));
        Assert.Null(await restarted.GetByReferenceOrDefaultAsync("BOO-999", 123));
        Assert.Null(await restarted.GetByReferenceOrDefaultAsync("invalid", 123));
        Assert.Equal(originalId, (await restarted.GetByReferenceOrDefaultAsync(originalId.ToString(), 123))?.Id);
        Assert.Null(await restarted.GetByReferenceOrDefaultAsync(originalId.ToString(), 456));
    }

    [Theory]
    [InlineData("card")]
    [InlineData("board")]
    [InlineData("guild")]
    public async Task Deletion_DoesNotReuseNumbers(string scope)
    {
        var card = Card();
        await _repository.AddTaskItemAsync(card);
        if (scope == "card")
            await _repository.RemoveTaskItemAsync(card);
        else if (scope == "board")
            await _repository.RemoveAllTaskItemsByBoardIdAsync(card.GuildId, card.BoardId!.Value);
        else
            await _repository.RemoveAllTaskItemsByIdAsync(card.GuildId);

        Assert.Null(await _repository.GetByReferenceOrDefaultAsync("BOO-001", 123));
        var next = Card();
        await new TaskItemRepository(_database).AddTaskItemAsync(next);
        Assert.Equal("BOO-002", next.Key);
    }

    [Fact]
    public async Task Upgrade_NumbersLegacyAndArchivedCardsWithoutRewritingDocuments()
    {
        var tasks = _database.GetCollection<TaskItem>(nameof(RequiredCollections.Tasks));
        var oldest = Card();
        oldest.CreatedAt = DateTime.UtcNow.AddYears(-1);
        oldest.Status = BoardStatus.Archived;
        var newest = Card();
        newest.CreatedAt = DateTime.UtcNow;
        await tasks.InsertManyAsync([newest, oldest]);
        var raw = _database.GetCollection<BsonDocument>(nameof(RequiredCollections.Tasks));
        var before = await raw.Find(Builders<BsonDocument>.Filter.Empty).Sort("{_id: 1}").ToListAsync();

        await DatabaseSetupBackgroundService.EnsureDatabaseAsync(_database, CancellationToken.None);
        await DatabaseSetupBackgroundService.EnsureDatabaseAsync(_database, CancellationToken.None);

        Assert.Equal(oldest.Id, (await _repository.GetByReferenceOrDefaultAsync("BOO-001", 123))?.Id);
        Assert.Equal(newest.Id, (await _repository.GetByReferenceOrDefaultAsync("BOO-002", 123))?.Id);
        var after = await raw.Find(Builders<BsonDocument>.Filter.Empty).Sort("{_id: 1}").ToListAsync();
        Assert.Equal(before, after);
        var next = Card();
        await _repository.AddTaskItemAsync(next);
        Assert.Equal("BOO-003", next.Key);
    }

    [Fact]
    public async Task ConcurrentLegacyReads_KeepTheSameReferenceForEachCard()
    {
        var card = Card();
        await _database.GetCollection<TaskItem>(nameof(RequiredCollections.Tasks)).InsertOneAsync(card);
        var reads = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ =>
            new TaskItemRepository(_database).GetTaskItemByObjectIdOrDefaultAsync(card.Id, card.GuildId)));

        Assert.Single(reads.Select(saved => saved!.Key).Distinct());
        Assert.StartsWith("BOO-", reads[0]!.Key);
        Assert.Equal(1, await _database.GetCollection<CardNumber>(nameof(RequiredCollections.CardNumbers))
            .CountDocumentsAsync(Builders<CardNumber>.Filter.Empty));
    }

    [Fact]
    public async Task RetryOfSameCard_KeepsItsOriginalReference()
    {
        var card = Card();
        Assert.True(await _repository.TryAddTaskItemAsync(card));
        Assert.False(await new TaskItemRepository(_database).TryAddTaskItemAsync(card));
        Assert.Equal("BOO-001", card.Key);
        var next = Card();
        await _repository.AddTaskItemAsync(next);
        Assert.Equal("BOO-002", next.Key);
    }

    [Fact]
    public async Task UniqueIndex_RejectsDuplicateNumbersWithinAGuild()
    {
        await DatabaseSetupBackgroundService.EnsureDatabaseAsync(_database, CancellationToken.None);
        var card = Card();
        await _repository.AddTaskItemAsync(card);
        var error = await Assert.ThrowsAsync<MongoWriteException>(() =>
            _database.GetCollection<CardNumber>(nameof(RequiredCollections.CardNumbers)).InsertOneAsync(new CardNumber
            {
                Id = ObjectId.GenerateNewId(),
                GuildId = card.GuildId,
                Number = card.CardNumber,
            }));
        Assert.Equal(ServerErrorCategory.DuplicateKey, error.WriteError.Category);
    }

    private static TaskItem Card(ulong guildId = 123) => new()
    {
        GuildId = guildId,
        BoardId = ObjectId.GenerateNewId(),
        Title = "Card",
        Description = "Notes",
        AuthorId = 42,
    };

    public void Dispose() => _runner.Dispose();
}
