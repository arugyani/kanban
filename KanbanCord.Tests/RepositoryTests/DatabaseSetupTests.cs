using KanbanCord.Bot.BackgroundServices;
using KanbanCord.Core.Models;
using KanbanCord.Core.Repositories;
using Mongo2Go;
using MongoDB.Driver;

namespace KanbanCord.Tests.RepositoryTests;

[Collection(MongoDatabaseCollection.Name)]
public sealed class DatabaseSetupTests : IDisposable
{
    private readonly MongoDbRunner _runner = MongoDbRunner.Start();

    [Fact]
    public async Task EnsureDatabaseAsync_CreatesQueryAndDeduplicationIndexes()
    {
        var database = new MongoClient(_runner.ConnectionString).GetDatabase("KanbanCord");

        await DatabaseSetupBackgroundService.EnsureDatabaseAsync(database, CancellationToken.None);

        var tasks = database.GetCollection<TaskItem>(nameof(RequiredCollections.Tasks));
        var indexes = await (await tasks.Indexes.ListAsync()).ToListAsync();
        Assert.Contains(indexes, index => index["name"] == "guild_board_status_rank");
        Assert.Contains(indexes, index => index["name"] == "guild_discord_message_unique");

        var repository = new TaskItemRepository(database);
        var first = Card(123, 456, "First");
        var retry = Card(123, 456, "Retry");

        Assert.True(await repository.TryAddTaskItemAsync(first));
        Assert.False(await repository.TryAddTaskItemAsync(retry));
        Assert.Equal(
            first.Id,
            (await repository.GetByDiscordMessageIdOrDefaultAsync(123, 456))?.Id);
    }

    private static TaskItem Card(ulong guildId, ulong messageId, string title) => new()
    {
        GuildId = guildId,
        Title = title,
        Description = string.Empty,
        AuthorId = 42,
        DiscordMessageId = messageId,
    };

    public void Dispose()
    {
        _runner.Dispose();
    }
}
