using KanbanCord.Bot.EventHandlers;
using KanbanCord.Core.Models;

namespace KanbanCord.Tests.InteractionTests;

public class CardButtonActionTests
{
    [Fact]
    public void Join_AddsThePersonOnce()
    {
        var card = NewCard();

        ComponentInteractionCreatedEventHandler.Join(card, 42);
        var message = ComponentInteractionCreatedEventHandler.Join(card, 42);

        Assert.Equal([42UL], card.AssigneeIds);
        Assert.Equal((ulong)42, card.AssigneeId);
        Assert.Contains("already", message);
    }

    [Theory]
    [InlineData(BoardStatus.Backlog, BoardStatus.UpNext)]
    [InlineData(BoardStatus.UpNext, BoardStatus.InProgress)]
    [InlineData(BoardStatus.InProgress, BoardStatus.Completed)]
    [InlineData(BoardStatus.Waiting, BoardStatus.InProgress)]
    public void MoveNext_FollowsTheFriendlyWorkflow(BoardStatus start, BoardStatus expected)
    {
        var card = NewCard();
        card.Status = start;
        card.BlockedReason = "Waiting";

        ComponentInteractionCreatedEventHandler.MoveNext(card);

        Assert.Equal(expected, card.Status);
        Assert.Null(card.BlockedReason);
    }

    [Fact]
    public void WaitingAndDone_KeepBlockedStateConsistent()
    {
        var card = NewCard();

        ComponentInteractionCreatedEventHandler.MarkWaiting(card);
        Assert.Equal(BoardStatus.Waiting, card.Status);
        Assert.NotNull(card.BlockedReason);

        ComponentInteractionCreatedEventHandler.MarkDone(card);
        Assert.Equal(BoardStatus.Completed, card.Status);
        Assert.Null(card.BlockedReason);
    }

    private static TaskItem NewCard() => new()
    {
        GuildId = 1,
        Title = "Check the lanterns",
        Description = string.Empty,
        AuthorId = 10,
    };
}
