using KanbanCord.Bot.Helpers;
using KanbanCord.Core.Models;
using MongoDB.Bson;

namespace KanbanCord.Tests.CommandTests;

public sealed class CardChoiceTests
{
    [Fact]
    public void Autocomplete_ShowsStableReferenceAndRetainsInternalButtonIdentity()
    {
        var card = new TaskItem
        {
            Id = ObjectId.GenerateNewId(),
            BoardId = ObjectId.GenerateNewId(),
            GuildId = 1,
            AuthorId = 1,
            Title = new string('x', 150),
            Description = string.Empty,
            CardNumber = 42,
        };
        TaskItem[] cards = [card];
        var choice = Assert.Single(cards.GetAutoCompleteStrings());
        Assert.StartsWith("BOO-042 · ", choice.Name);
        Assert.Equal(100, choice.Name.Length);
        Assert.Equal(card.Id.ToString(), choice.Value);

        card.Status = BoardStatus.Completed;
        Assert.StartsWith("BOO-042 · ", Assert.Single(cards.GetAutoCompleteStrings(BoardStatus.Completed)).Name);
    }
}
