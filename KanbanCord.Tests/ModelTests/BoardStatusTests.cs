using KanbanCord.Core.Models;

namespace KanbanCord.Tests.ModelTests;

public class BoardStatusTests
{
    [Theory]
    [InlineData(BoardStatus.Backlog, "Ideas")]
    [InlineData(BoardStatus.UpNext, "Up Next")]
    [InlineData(BoardStatus.InProgress, "Doing")]
    [InlineData(BoardStatus.Waiting, "Waiting")]
    [InlineData(BoardStatus.Completed, "Done")]
    [InlineData(BoardStatus.Archived, "Archived")]
    public void ToFormattedString_UsesFriendlyColumnNames(BoardStatus status, string expected)
    {
        Assert.Equal(expected, status.ToFormattedString());
    }

    [Fact]
    public void ExistingStoredValues_RemainStable()
    {
        Assert.Equal(0, (int)BoardStatus.Backlog);
        Assert.Equal(1, (int)BoardStatus.InProgress);
        Assert.Equal(2, (int)BoardStatus.Completed);
        Assert.Equal(3, (int)BoardStatus.Archived);
    }
}
