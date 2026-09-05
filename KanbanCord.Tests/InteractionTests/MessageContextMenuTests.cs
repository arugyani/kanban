using KanbanCord.Bot.EventHandlers;

namespace KanbanCord.Tests.InteractionTests;

public sealed class MessageContextMenuTests
{
    [Fact]
    public void TitleFor_UsesTheFirstLineAndStaysWithinDiscordLimits()
    {
        var title = MessageContextMenuEventHandler.TitleFor(
            "A useful message that is deliberately longer than the card title limit\nMore context",
            "Aru");

        Assert.EndsWith("…", title);
        Assert.Equal(40, title.Length);
    }

    [Fact]
    public void TitleFor_HandlesMessagesWithoutText()
    {
        Assert.Equal("Message from Aru", MessageContextMenuEventHandler.TitleFor(null, "Aru"));
    }

    [Fact]
    public void NotesFor_PreservesTheOriginalLinkWithinTheStorageLimit()
    {
        const string link = "https://discord.com/channels/1/2/3";

        var notes = MessageContextMenuEventHandler.NotesFor(new string('x', 1000), "Aru", link);

        Assert.True(notes.Length <= 600);
        Assert.EndsWith(link, notes);
    }
}
