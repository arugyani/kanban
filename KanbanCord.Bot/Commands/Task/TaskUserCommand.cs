using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using KanbanCord.Bot.Extensions;
using KanbanCord.Bot.Helpers;
using KanbanCord.Bot.Providers;
using KanbanCord.Core.Models;

namespace KanbanCord.Bot.Commands.Task;

partial class TaskCommandGroup
{
    [Command("user")]
    [Description("Show cards involving a person on one board.")]
    [RequirePermissions(userPermissions: [], botPermissions: [])]
    public async ValueTask TaskUserCommand(
        SlashCommandContext context,
        DiscordUser user,
        [Description("Board to inspect; defaults to Default")][SlashAutoCompleteProvider<BoardAutoCompleteProvider>] string? board = null)
    {
        var selectedBoard = await _boardResolver.ResolveAsync(context.Guild!.Id, context.User.Id, board);

        if (selectedBoard is null)
        {
            await context.RespondAsync("The selected board was not found.", ephemeral: true);
            return;
        }

        var boardItems = await _taskItemRepository.GetAllTaskItemsByBoardIdAsync(context.Guild.Id, selectedBoard.Id);

        var embed = new DiscordEmbedBuilder()
            .WithDefaultColor()
            .WithAuthor($"RGBOO Board · {selectedBoard.Name}")
            .WithDescription($"Cards involving {user.Mention}");

        var backlogString = await boardItems.GetBoardTaskString(context.Client, BoardStatus.Backlog, user.Id);
        embed.AddField(BoardStatus.Backlog.ToFormattedString(), backlogString);

        var upNextString = await boardItems.GetBoardTaskString(context.Client, BoardStatus.UpNext, user.Id);
        embed.AddField(BoardStatus.UpNext.ToFormattedString(), upNextString);

        var inProgressString = await boardItems.GetBoardTaskString(context.Client, BoardStatus.InProgress, user.Id);
        embed.AddField(BoardStatus.InProgress.ToFormattedString(), inProgressString);

        var waitingString = await boardItems.GetBoardTaskString(context.Client, BoardStatus.Waiting, user.Id);
        embed.AddField(BoardStatus.Waiting.ToFormattedString(), waitingString);

        var completedString = await boardItems.GetBoardTaskString(context.Client, BoardStatus.Completed, user.Id);
        embed.AddField(BoardStatus.Completed.ToFormattedString(), completedString);

        await context.RespondAsync(embed, ephemeral: true);
    }
}
