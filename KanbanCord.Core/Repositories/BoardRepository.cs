using KanbanCord.Core.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace KanbanCord.Core.Repositories;

public class BoardRepository : IBoardRepository
{
    private const string DefaultBoardName = "Default";
    private readonly IMongoCollection<Board> _collection;

    public BoardRepository(IMongoDatabase mongoDatabase)
    {
        _collection = mongoDatabase.GetCollection<Board>(nameof(RequiredCollections.Boards));
    }

    public async Task<IReadOnlyList<Board>> GetAllByGuildIdAsync(ulong guildId)
    {
        var boards = await _collection.Find(board => board.GuildId == guildId).ToListAsync() ?? [];

        return boards
            .OrderByDescending(board => board.IsDefault)
            .ThenBy(board => board.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<Board?> GetByObjectIdOrDefaultAsync(ObjectId objectId, ulong guildId)
    {
        return await _collection
            .Find(board => board.Id == objectId && board.GuildId == guildId)
            .FirstOrDefaultAsync();
    }

    public async Task<Board> GetOrCreateDefaultAsync(ulong guildId, ulong createdById)
    {
        var existing = await _collection
            .Find(board => board.GuildId == guildId && board.IsDefault)
            .FirstOrDefaultAsync();

        if (existing is not null)
            return existing;

        var board = new Board
        {
            GuildId = guildId,
            Name = DefaultBoardName,
            NormalizedName = NormalizeName(DefaultBoardName),
            IsDefault = true,
            CreatedById = createdById
        };

        await _collection.InsertOneAsync(board);

        return board;
    }

    public async Task<bool> NameExistsAsync(ulong guildId, string name, ObjectId? excludingBoardId = null)
    {
        var normalizedName = NormalizeName(name);
        var filter = Builders<Board>.Filter.Eq(board => board.GuildId, guildId)
                     & Builders<Board>.Filter.Eq(board => board.NormalizedName, normalizedName);

        if (excludingBoardId.HasValue)
            filter &= Builders<Board>.Filter.Ne(board => board.Id, excludingBoardId.Value);

        return await _collection.CountDocumentsAsync(filter) != 0;
    }

    public async Task AddAsync(Board board)
    {
        board.Name = board.Name.Trim();
        board.NormalizedName = NormalizeName(board.Name);
        await _collection.InsertOneAsync(board);
    }

    public async Task UpdateAsync(Board board)
    {
        board.Name = board.Name.Trim();
        board.NormalizedName = NormalizeName(board.Name);
        await _collection.ReplaceOneAsync(candidate => candidate.Id == board.Id, board);
    }

    public async Task RemoveAsync(Board board)
    {
        await _collection.DeleteOneAsync(candidate => candidate.Id == board.Id);
    }

    public async Task RemoveAllByGuildIdAsync(ulong guildId)
    {
        await _collection.DeleteManyAsync(board => board.GuildId == guildId);
    }

    private static string NormalizeName(string name) => name.Trim().ToUpperInvariant();
}
