using KanbanCord.Core.Models;
using KanbanCord.Core.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace KanbanCord.Bot.BackgroundServices;

public class DatabaseSetupBackgroundService : IHostedService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<DatabaseSetupBackgroundService> _logger;

    public DatabaseSetupBackgroundService(IServiceScopeFactory serviceScopeFactory, ILogger<DatabaseSetupBackgroundService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
        var createdCollections = await EnsureDatabaseAsync(database, stoppingToken);

        if (createdCollections.Count != 0)
            _logger.LogInformation(
                "Created missing MongoDB collection(s): {Collections}",
                string.Join(", ", createdCollections));
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    internal static async Task<IReadOnlyList<string>> EnsureDatabaseAsync(
        IMongoDatabase database,
        CancellationToken cancellationToken)
    {
        var requiredCollections = Enum.GetValues<RequiredCollections>()
            .Select(x => x.ToString())
            .ToArray();

        var cursor = await database.ListCollectionNamesAsync(cancellationToken: cancellationToken);
        var collectionList = await cursor.ToListAsync<string>(cancellationToken: cancellationToken);

        List<string> createdCollections = [];

        foreach (var collectionName in requiredCollections)
        {
            if (collectionList.Contains(collectionName))
                continue;

            await database.CreateCollectionAsync(collectionName, cancellationToken: cancellationToken);
            createdCollections.Add(collectionName);
        }

        await EnsureIndexesAsync(database, cancellationToken);
        await new TaskItemRepository(database).BackfillReferencesAsync(cancellationToken);
        return createdCollections;
    }

    private static async Task EnsureIndexesAsync(
        IMongoDatabase database,
        CancellationToken cancellationToken)
    {
        var tasks = database.GetCollection<TaskItem>(nameof(RequiredCollections.Tasks));
        var taskIndexes = new[]
        {
            new CreateIndexModel<TaskItem>(
                Builders<TaskItem>.IndexKeys
                    .Ascending(task => task.GuildId)
                    .Ascending(task => task.BoardId)
                    .Ascending(task => task.Status)
                    .Ascending(task => task.Rank),
                new CreateIndexOptions { Name = "guild_board_status_rank" }),
            new CreateIndexModel<TaskItem>(
                Builders<TaskItem>.IndexKeys
                    .Ascending(task => task.GuildId)
                    .Ascending(task => task.DiscordMessageId),
                new CreateIndexOptions<TaskItem>
                {
                    Name = "guild_discord_message_unique",
                    Unique = true,
                    PartialFilterExpression = new BsonDocumentFilterDefinition<TaskItem>(
                        new BsonDocument(nameof(TaskItem.DiscordMessageId),
                            new BsonDocument("$type", "long"))),
                }),
        };
        await tasks.Indexes.CreateManyAsync(taskIndexes, cancellationToken);

        var numbers = database.GetCollection<CardNumber>(nameof(RequiredCollections.CardNumbers));
        await numbers.Indexes.CreateOneAsync(
            new CreateIndexModel<CardNumber>(
                Builders<CardNumber>.IndexKeys.Ascending(number => number.GuildId).Ascending(number => number.Number),
                new CreateIndexOptions { Name = "guild_card_number_unique", Unique = true }),
            cancellationToken: cancellationToken);

        var boards = database.GetCollection<Board>(nameof(RequiredCollections.Boards));
        await boards.Indexes.CreateOneAsync(
            new CreateIndexModel<Board>(
                Builders<Board>.IndexKeys
                    .Ascending(board => board.GuildId)
                    .Ascending(board => board.NormalizedName),
                new CreateIndexOptions<Board>
                {
                    Name = "guild_board_name_unique",
                    Unique = true,
                    PartialFilterExpression = new BsonDocumentFilterDefinition<Board>(
                        new BsonDocument(nameof(Board.NormalizedName),
                            new BsonDocument("$type", "string"))),
                }),
            cancellationToken: cancellationToken);

        var teams = database.GetCollection<Team>(nameof(RequiredCollections.Teams));
        await teams.Indexes.CreateOneAsync(
            new CreateIndexModel<Team>(
                Builders<Team>.IndexKeys
                    .Ascending(team => team.GuildId)
                    .Ascending(team => team.NormalizedName),
                new CreateIndexOptions<Team>
                {
                    Name = "guild_group_name_unique",
                    Unique = true,
                    PartialFilterExpression = new BsonDocumentFilterDefinition<Team>(
                        new BsonDocument(nameof(Team.NormalizedName),
                            new BsonDocument("$type", "string"))),
                }),
            cancellationToken: cancellationToken);
    }
}
