using KanbanCord.Core.Models;
using KanbanCord.Core.Repositories;
using Mongo2Go;
using MongoDB.Driver;

namespace KanbanCord.Tests.RepositoryTests;

[Collection(MongoDatabaseCollection.Name)]
public class TeamRepositoryTests : IDisposable
{
    private readonly MongoDbRunner _runner;
    private readonly TeamRepository _repository;

    public TeamRepositoryTests()
    {
        _runner = MongoDbRunner.Start();
        var database = new MongoClient(_runner.ConnectionString).GetDatabase("KanbanCord");
        _repository = new TeamRepository(database);
    }

    [Fact]
    public async Task AddAsync_ShouldNormalizeAndDeduplicateMembers()
    {
        var team = NewTeam(123, " Product ");
        team.MemberIds = [10, 10, 20];

        await _repository.AddAsync(team);
        var result = await _repository.GetByObjectIdOrDefaultAsync(team.Id, 123);

        Assert.NotNull(result);
        Assert.Equal("Product", result.Name);
        Assert.Equal("PRODUCT", result.NormalizedName);
        Assert.Equal([10UL, 20UL], result.MemberIds);
    }

    [Fact]
    public async Task NameExistsAsync_ShouldBeCaseInsensitiveAndSupportExclusion()
    {
        var team = NewTeam(123, "Design");
        await _repository.AddAsync(team);

        Assert.True(await _repository.NameExistsAsync(123, "design"));
        Assert.False(await _repository.NameExistsAsync(123, "design", team.Id));
        Assert.False(await _repository.NameExistsAsync(999, "design"));
    }

    [Fact]
    public async Task UpdateAsync_ShouldPersistRosterChanges()
    {
        var team = NewTeam(123, "Design");
        await _repository.AddAsync(team);
        team.MemberIds.Add(42);

        await _repository.UpdateAsync(team);
        var result = await _repository.GetByObjectIdOrDefaultAsync(team.Id, 123);

        Assert.NotNull(result);
        Assert.Contains((ulong)42, result.MemberIds);
    }

    [Fact]
    public async Task UpdateAsync_ShouldKeepOnlyValidRolesForCurrentMembers()
    {
        var team = NewTeam(123, "Design");
        team.MemberIds = [42, 84];
        team.MemberRoles = new Dictionary<string, string>
        {
            ["42"] = "organizer",
            ["84"] = "view_only",
            ["999"] = "member",
            ["not-a-user"] = "member",
        };

        await _repository.AddAsync(team);
        var result = await _repository.GetByObjectIdOrDefaultAsync(team.Id, 123);

        Assert.NotNull(result);
        Assert.Equal("organizer", result.MemberRoles["42"]);
        Assert.Equal("view_only", result.MemberRoles["84"]);
        Assert.DoesNotContain("999", result.MemberRoles.Keys);
        Assert.DoesNotContain("not-a-user", result.MemberRoles.Keys);
    }

    [Fact]
    public async Task GetByObjectIdOrDefaultAsync_ShouldEnforceGuildScope()
    {
        var team = NewTeam(123, "Design");
        await _repository.AddAsync(team);

        Assert.Null(await _repository.GetByObjectIdOrDefaultAsync(team.Id, 999));
    }

    private static Team NewTeam(ulong guildId, string name) => new()
    {
        GuildId = guildId,
        Name = name,
        NormalizedName = name.Trim().ToUpperInvariant(),
        CreatedById = 456
    };

    public void Dispose()
    {
        _runner.Dispose();
    }
}
