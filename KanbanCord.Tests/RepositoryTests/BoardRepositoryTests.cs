using KanbanCord.Core.Models;
using KanbanCord.Core.Repositories;
using Mongo2Go;
using MongoDB.Driver;

namespace KanbanCord.Tests.RepositoryTests;

public class BoardRepositoryTests : IDisposable
{
    private readonly MongoDbRunner _runner;
    private readonly BoardRepository _repository;

    public BoardRepositoryTests()
    {
        _runner = MongoDbRunner.Start();
        var database = new MongoClient(_runner.ConnectionString).GetDatabase("KanbanCord");
        _repository = new BoardRepository(database);
    }

    [Fact]
    public async Task GetOrCreateDefaultAsync_ShouldReuseOneDefaultBoard()
    {
        var first = await _repository.GetOrCreateDefaultAsync(123, 456);
        var second = await _repository.GetOrCreateDefaultAsync(123, 999);

        Assert.Equal(first.Id, second.Id);
        Assert.True(first.IsDefault);
        Assert.Equal("Default", first.Name);
        Assert.Equal((ulong)456, second.CreatedById);
    }

    [Fact]
    public async Task NameExistsAsync_ShouldBeCaseInsensitiveAndGuildScoped()
    {
        await _repository.AddAsync(NewBoard(123, "Product"));

        Assert.True(await _repository.NameExistsAsync(123, " product "));
        Assert.False(await _repository.NameExistsAsync(999, "Product"));
    }

    [Fact]
    public async Task GetAllByGuildIdAsync_ShouldPutDefaultFirst()
    {
        await _repository.AddAsync(NewBoard(123, "Zebra"));
        var defaultBoard = await _repository.GetOrCreateDefaultAsync(123, 456);
        await _repository.AddAsync(NewBoard(123, "Alpha"));
        await _repository.AddAsync(NewBoard(999, "Other Guild"));

        var result = await _repository.GetAllByGuildIdAsync(123);

        Assert.Equal(3, result.Count);
        Assert.Equal(defaultBoard.Id, result[0].Id);
        Assert.Equal("Alpha", result[1].Name);
        Assert.Equal("Zebra", result[2].Name);
    }

    [Fact]
    public async Task RemoveAllByGuildIdAsync_ShouldNotRemoveAnotherGuildsBoards()
    {
        await _repository.AddAsync(NewBoard(123, "Product"));
        await _repository.AddAsync(NewBoard(999, "Product"));

        await _repository.RemoveAllByGuildIdAsync(123);

        Assert.Empty(await _repository.GetAllByGuildIdAsync(123));
        Assert.Single(await _repository.GetAllByGuildIdAsync(999));
    }

    private static Board NewBoard(ulong guildId, string name) => new()
    {
        GuildId = guildId,
        Name = name,
        NormalizedName = name.ToUpperInvariant(),
        CreatedById = 456
    };

    public void Dispose()
    {
        _runner.Dispose();
    }
}
