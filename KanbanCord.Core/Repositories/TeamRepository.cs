using KanbanCord.Core.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace KanbanCord.Core.Repositories;

public class TeamRepository : ITeamRepository
{
    private readonly IMongoCollection<Team> _collection;

    public TeamRepository(IMongoDatabase mongoDatabase)
    {
        _collection = mongoDatabase.GetCollection<Team>(nameof(RequiredCollections.Teams));
    }

    public async Task<IReadOnlyList<Team>> GetAllByGuildIdAsync(ulong guildId)
    {
        var teams = await _collection.Find(team => team.GuildId == guildId).ToListAsync() ?? [];

        return teams.OrderBy(team => team.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<Team?> GetByObjectIdOrDefaultAsync(ObjectId objectId, ulong guildId)
    {
        return await _collection
            .Find(team => team.Id == objectId && team.GuildId == guildId)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> NameExistsAsync(ulong guildId, string name, ObjectId? excludingTeamId = null)
    {
        var normalizedName = NormalizeName(name);
        var filter = Builders<Team>.Filter.Eq(team => team.GuildId, guildId)
                     & Builders<Team>.Filter.Eq(team => team.NormalizedName, normalizedName);

        if (excludingTeamId.HasValue)
            filter &= Builders<Team>.Filter.Ne(team => team.Id, excludingTeamId.Value);

        return await _collection.CountDocumentsAsync(filter) != 0;
    }

    public async Task AddAsync(Team team)
    {
        Normalize(team);
        await _collection.InsertOneAsync(team);
    }

    public async Task UpdateAsync(Team team)
    {
        Normalize(team);
        await _collection.ReplaceOneAsync(candidate => candidate.Id == team.Id, team);
    }

    public async Task RemoveAsync(Team team)
    {
        await _collection.DeleteOneAsync(candidate => candidate.Id == team.Id);
    }

    public async Task RemoveAllByGuildIdAsync(ulong guildId)
    {
        await _collection.DeleteManyAsync(team => team.GuildId == guildId);
    }

    private static string NormalizeName(string name) => name.Trim().ToUpperInvariant();

    private static void Normalize(Team team)
    {
        team.Name = team.Name.Trim();
        team.NormalizedName = NormalizeName(team.Name);
        team.MemberIds = team.MemberIds.Distinct().ToList();
        team.MemberRoles = team.MemberRoles
            .Where(pair => ulong.TryParse(pair.Key, out var id) && team.MemberIds.Contains(id))
            .Where(pair => pair.Value is "organizer" or "member" or "view_only")
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        team.Icon = team.Icon is "ghost" or "pumpkin" or "bat" ? team.Icon : "ghost";
        team.Accent = team.Accent is "pumpkin" or "purple" or "green" or "berry" ? team.Accent : "purple";
    }
}
