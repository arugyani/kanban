using DSharpPlus;
using DSharpPlus.Entities;
using KanbanCord.Bot.Extensions;
using KanbanCord.Core.Models;

namespace KanbanCord.Bot.Helpers;

public static class BoardHelper
{
    public static async Task<DiscordEmbed> GetBoardEmbed(
        DiscordClient client,
        IReadOnlyList<TaskItem> boardItems,
        BoardStatus? boardStatus = null,
        Board? board = null,
        Team? team = null)
    {
        var embed = new DiscordEmbedBuilder()
            .WithDefaultColor()
            .WithAuthor("KanbanCord Board")
            .WithTitle(board?.Name ?? "Board");

        if (team is not null)
            embed.WithFooter($"Team: {team.Name}");

        if (boardStatus is not null)
        {
            var boardString = await boardItems.GetBoardTaskString(client, (BoardStatus)boardStatus);
            embed.WithTitle($"{board?.Name ?? "Board"} · {((BoardStatus)boardStatus).ToFormattedString()}");
            embed.WithDescription(boardString);
        }
        else
        {
            var backlogString = await boardItems.GetBoardTaskString(client, BoardStatus.Backlog);
            embed.AddField("Backlog", backlogString);
        
            var inProgressString = await boardItems.GetBoardTaskString(client, BoardStatus.InProgress);
            embed.AddField("In Progress", inProgressString);
        
            var completedString = await boardItems.GetBoardTaskString(client, BoardStatus.Completed);
            embed.AddField("Completed", completedString);
        }
        
        return embed;
    }
}
