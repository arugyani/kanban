using KanbanCord.Bot.Commands.Task;

namespace KanbanCord.Tests.CommandTests;

public class GitHubIssueLinkTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://github.com/rgboo/site/issues/12")]
    [InlineData("https://example.com/rgboo/site/issues/12")]
    [InlineData("https://github.com/rgboo/site/pull/12")]
    [InlineData("https://github.com/rgboo/site/issues/nope")]
    public void TryParseGitHubIssue_RejectsAnythingButHttpsIssueLinks(string? value)
    {
        Assert.False(TaskCommandGroup.TryParseGitHubIssue(value, out _));
    }

    [Fact]
    public void TryParseGitHubIssue_NormalizesTheCanonicalIssueUrl()
    {
        var result = TaskCommandGroup.TryParseGitHubIssue(
            "https://github.com/rgboo/site/issues/12?notification_referrer_id=1",
            out var issue);

        Assert.True(result);
        Assert.NotNull(issue);
        Assert.Equal("https://github.com/rgboo/site/issues/12", issue.Url);
        Assert.Equal("rgboo", issue.Owner);
        Assert.Equal("site", issue.Repository);
        Assert.Equal(12, issue.IssueNumber);
    }
}
