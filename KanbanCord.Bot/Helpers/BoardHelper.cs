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
            .WithAuthor("RGBOO Board")
            .WithTitle(board?.Name ?? "Board");

        if (team is not null)
            embed.WithFooter($"Group: {team.Name}");

        if (boardStatus is not null)
        {
            var boardString = await boardItems.GetBoardTaskString(client, (BoardStatus)boardStatus);
            embed.WithTitle($"{board?.Name ?? "Board"} · {((BoardStatus)boardStatus).ToFormattedString()}");
            embed.WithDescription(boardString);
        }
        else
        {
            var backlogString = await boardItems.GetBoardTaskString(client, BoardStatus.Backlog);
            embed.AddField(BoardStatus.Backlog.ToFormattedString(), backlogString);

            var upNextString = await boardItems.GetBoardTaskString(client, BoardStatus.UpNext);
            embed.AddField(BoardStatus.UpNext.ToFormattedString(), upNextString);

            var inProgressString = await boardItems.GetBoardTaskString(client, BoardStatus.InProgress);
            embed.AddField(BoardStatus.InProgress.ToFormattedString(), inProgressString);

            var waitingString = await boardItems.GetBoardTaskString(client, BoardStatus.Waiting);
            embed.AddField(BoardStatus.Waiting.ToFormattedString(), waitingString);

            var completedString = await boardItems.GetBoardTaskString(client, BoardStatus.Completed);
            embed.AddField(BoardStatus.Completed.ToFormattedString(), completedString);
        }

        return embed;
    }
}
