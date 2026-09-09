using KanbanCord.Core.Models;

namespace KanbanCord.Tests.ModelTests;

public sealed class CardReferenceTests
{
    [Theory]
    [InlineData(1, "BOO-001")]
    [InlineData(9, "BOO-009")]
    [InlineData(10, "BOO-010")]
    [InlineData(999, "BOO-999")]
    [InlineData(1000, "BOO-1000")]
    public void Format_PadsToThreeDigitsWithoutWrapping(long number, string expected)
    {
        Assert.Equal(expected, CardReference.Format(number));
    }

    [Theory]
    [InlineData("BOO-001", 1)]
    [InlineData("boo-42", 42)]
    [InlineData(" BOO-1000 ", 1000)]
    public void Parse_AcceptsCaseAndOptionalPadding(string reference, long expected)
    {
        Assert.True(CardReference.TryParse(reference, out var number));
        Assert.Equal(expected, number);
    }

    [Theory]
    [InlineData("")]
    [InlineData("BOO-000")]
    [InlineData("BOO--1")]
    [InlineData("BOO-+1")]
    [InlineData("BOO-1.0")]
    [InlineData("BOO- 1")]
    [InlineData("WEB-001")]
    [InlineData("BOO-9223372036854775808")]
    public void Parse_RejectsInvalidReferences(string reference)
    {
        Assert.False(CardReference.TryParse(reference, out _));
    }
}
