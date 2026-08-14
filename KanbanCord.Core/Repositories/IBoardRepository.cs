using KanbanCord.Core.Models;
using MongoDB.Bson;

namespace KanbanCord.Core.Repositories;

public interface IBoardRepository
{
    Task<IReadOnlyList<Board>> GetAllByGuildIdAsync(ulong guildId);

    Task<Board?> GetByObjectIdOrDefaultAsync(ObjectId objectId, ulong guildId);

    Task<Board> GetOrCreateDefaultAsync(ulong guildId, ulong createdById);

    Task<bool> NameExistsAsync(ulong guildId, string name, ObjectId? excludingBoardId = null);

    Task AddAsync(Board board);

    Task UpdateAsync(Board board);

    Task RemoveAsync(Board board);

    Task RemoveAllByGuildIdAsync(ulong guildId);
}
