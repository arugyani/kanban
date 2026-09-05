using KanbanCord.Bot.Web;

namespace KanbanCord.Tests.WebTests;

public sealed class CardInputTests
{
    [Fact]
    public void ParseTagNames_TrimsAndDeduplicatesTags()
    {
        var tags = BoardApiService.ParseTagNames([" frontend ", "Frontend", "discord"]);

        Assert.Equal(["frontend", "discord"], tags);
    }

    [Fact]
    public void ParseTagNames_RejectsTooManyOrOverlongTags()
    {
        var tooMany = Enumerable.Range(1, 11).Select(index => $"tag-{index}");

        Assert.Equal(
            "invalid_input",
            Assert.Throws<BoardApiException>(() => BoardApiService.ParseTagNames(tooMany)).Code);
        Assert.Equal(
            "invalid_input",
            Assert.Throws<BoardApiException>(() => BoardApiService.ParseTagNames([new string('x', 31)])).Code);
    }

    [Theory]
    [InlineData("09/05/2026")]
    [InlineData("2026-9-5")]
    [InlineData("not-a-date")]
    public void ParseDate_RequiresTheStableIsoFormat(string value)
    {
        Assert.Equal(
            "invalid_input",
            Assert.Throws<BoardApiException>(() => BoardApiService.ParseDate(value)).Code);
    }

    [Fact]
    public void ParseDate_AcceptsIsoDateAndClear()
    {
        Assert.Equal(
            new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc),
            BoardApiService.ParseDate("2026-10-31"));
        Assert.Null(BoardApiService.ParseDate(null));
        Assert.Null(BoardApiService.ParseDate(string.Empty));
    }
}
