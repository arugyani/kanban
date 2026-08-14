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
    [Description("Displays all the tasks assigned to a specified user.")]
    [RequirePermissions(userPermissions: [], botPermissions: [])]
    public async ValueTask TaskUserCommand(
        SlashCommandContext context,
        DiscordUser user,
        [Description("Board to inspect; defaults to Default")] [SlashAutoCompleteProvider<BoardAutoCompleteProvider>] string? board = null)
    {
        var selectedBoard = await _boardResolver.ResolveAsync(context.Guild!.Id, context.User.Id, board);

        if (selectedBoard is null)
        {
            await context.RespondAsync("The selected board was not found.");
            return;
        }

        var boardItems = await _taskItemRepository.GetAllTaskItemsByBoardIdAsync(context.Guild.Id, selectedBoard.Id);
        
        var embed = new DiscordEmbedBuilder()
            .WithDefaultColor()
            .WithAuthor($"KanbanCord Board · {selectedBoard.Name}")
            .WithDescription($"Tasks assigned to {user.Mention}");
        
        var backlogString = await boardItems.GetBoardTaskString(context.Client, BoardStatus.Backlog, user.Id);
        embed.AddField("Backlog", backlogString);
        
        var inProgressString = await boardItems.GetBoardTaskString(context.Client, BoardStatus.InProgress, user.Id);
        embed.AddField("In Progress", inProgressString);
        
        var compltedString = await boardItems.GetBoardTaskString(context.Client, BoardStatus.Completed, user.Id);
        embed.AddField("Completed", compltedString);

        await context.RespondAsync(embed);
    }
}
