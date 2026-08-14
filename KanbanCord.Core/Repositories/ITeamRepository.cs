using KanbanCord.Core.Models;
using MongoDB.Bson;

namespace KanbanCord.Core.Repositories;

public interface ITeamRepository
{
    Task<IReadOnlyList<Team>> GetAllByGuildIdAsync(ulong guildId);

    Task<Team?> GetByObjectIdOrDefaultAsync(ObjectId objectId, ulong guildId);

    Task<bool> NameExistsAsync(ulong guildId, string name, ObjectId? excludingTeamId = null);

    Task AddAsync(Team team);

    Task UpdateAsync(Team team);

    Task RemoveAsync(Team team);

    Task RemoveAllByGuildIdAsync(ulong guildId);
}
