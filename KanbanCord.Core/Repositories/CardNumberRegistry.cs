using System.Globalization;
using KanbanCord.Core.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace KanbanCord.Core.Repositories;

public sealed class CardNumberRegistry
{
    private readonly IMongoCollection<CardNumber> _numbers;
    private readonly IMongoCollection<CardNumberCounter> _counters;

    public CardNumberRegistry(IMongoDatabase database)
    {
        _numbers = database.GetCollection<CardNumber>(nameof(RequiredCollections.CardNumbers));
        _counters = database.GetCollection<CardNumberCounter>(nameof(RequiredCollections.CardNumberCounters));
    }

    public async Task AssignAsync(IReadOnlyCollection<TaskItem> cards, CancellationToken cancellationToken = default)
    {
        // One metadata read per batch, not one request per card on every refresh.
        foreach (var batch in cards.OrderBy(card => card.CreatedAt).ThenBy(card => card.Id).Chunk(500))
        {
            var ids = batch.Select(card => card.Id).ToArray();
            var existing = (await _numbers.Find(number => ids.Contains(number.Id))
                .ToListAsync(cancellationToken)).ToDictionary(number => number.Id);

            foreach (var card in batch)
            {
                if (!existing.TryGetValue(card.Id, out var number))
                    number = await CreateAsync(card, cancellationToken);
                if (number.GuildId != card.GuildId)
                    throw new InvalidOperationException("Card reference belongs to a different guild.");
                card.CardNumber = number.Number;
            }
        }
    }

    public async Task<ObjectId?> FindCardIdAsync(ulong guildId, long number)
    {
        var reference = await _numbers.Find(card => card.GuildId == guildId && card.Number == number)
            .FirstOrDefaultAsync();
        return reference?.Id;
    }

    private async Task<CardNumber> CreateAsync(TaskItem card, CancellationToken cancellationToken)
    {
        var reference = new CardNumber
        {
            Id = card.Id,
            GuildId = card.GuildId,
            Number = await NextAsync(card.GuildId, cancellationToken),
        };
        try
        {
            await _numbers.InsertOneAsync(reference, cancellationToken: cancellationToken);
            return reference;
        }
        catch (MongoWriteException exception)
            when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Concurrent readers may number the same legacy card. The _id index
            // makes the first persisted reference win without ever renumbering it.
            return await _numbers.Find(number => number.Id == card.Id).FirstOrDefaultAsync(cancellationToken)
                   ?? throw new InvalidOperationException("Could not reserve a unique card reference.", exception);
        }
    }

    private async Task<long> NextAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var id = guildId.ToString(CultureInfo.InvariantCulture);
        var filter = Builders<CardNumberCounter>.Filter.Eq(counter => counter.Id, id);
        var update = Builders<CardNumberCounter>.Update.Inc(counter => counter.Value, 1);
        var options = new FindOneAndUpdateOptions<CardNumberCounter>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After,
        };
        CardNumberCounter counter;
        try
        {
            counter = await _counters.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
        }
        catch (MongoCommandException exception) when (exception.Code == 11000)
        {
            // A different process inserted this guild's counter first.
            options.IsUpsert = false;
            counter = await _counters.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
        }
        return counter.Value;
    }
}
