using KanbanCord.Bot.Web;
using KanbanCord.Core.Models;
using MongoDB.Bson;

namespace KanbanCord.Tests.WebTests;

public sealed class CardOrderingTests
{
    [Fact]
    public void RankForMove_AppendsToAnEmptyColumn()
    {
        var moving = Card(BoardStatus.UpNext, 50);

        var rank = BoardApiService.RankForMove(moving, [moving], BoardStatus.Backlog, null, null);

        Assert.Equal(1024, rank);
    }

    [Fact]
    public void RankForMove_InsertsBeforeAndAfterTheTarget()
    {
        var first = Card(BoardStatus.Backlog, 1024);
        var target = Card(BoardStatus.Backlog, 2048);
        var last = Card(BoardStatus.Backlog, 3072);
        var moving = Card(BoardStatus.Backlog, 4096);
        TaskItem[] cards = [first, target, last, moving];

        var before = BoardApiService.RankForMove(moving, cards, BoardStatus.Backlog, target, "before");
        var after = BoardApiService.RankForMove(moving, cards, BoardStatus.Backlog, target, "after");

        Assert.Equal(1536, before);
        Assert.Equal(2560, after);
    }

    [Fact]
    public void RankForMove_AppendsAfterTheLastCardAndIgnoresOtherColumns()
    {
        var target = Card(BoardStatus.Backlog, 2048);
        var otherColumn = Card(BoardStatus.UpNext, 99999);
        var moving = Card(BoardStatus.UpNext, 1024);

        var rank = BoardApiService.RankForMove(
            moving,
            [target, otherColumn, moving],
            BoardStatus.Backlog,
            target,
            "after");

        Assert.Equal(3072, rank);
    }

    [Fact]
    public void RankForMove_RejectsAPositionWithNoRepresentableGap()
    {
        var first = Card(BoardStatus.Backlog, 1);
        var target = Card(BoardStatus.Backlog, Math.BitIncrement(1));
        var moving = Card(BoardStatus.Backlog, 4);

        var error = Assert.Throws<BoardApiException>(() => BoardApiService.RankForMove(
            moving,
            [first, target, moving],
            BoardStatus.Backlog,
            target,
            "before"));

        Assert.Equal("position_too_crowded", error.Code);
    }

    private static TaskItem Card(BoardStatus status, double rank) => new()
    {
        Id = ObjectId.GenerateNewId(),
        GuildId = 1,
        BoardId = ObjectId.GenerateNewId(),
        Title = "Card",
        Description = string.Empty,
        AuthorId = 1,
        Status = status,
        Rank = rank,
    };
}
