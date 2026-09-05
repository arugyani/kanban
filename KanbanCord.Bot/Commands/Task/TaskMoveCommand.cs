using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using KanbanCord.Bot.Providers;
using KanbanCord.Bot.Extensions;
using KanbanCord.Core.Models;
using MongoDB.Bson;

namespace KanbanCord.Bot.Commands.Task;

partial class TaskCommandGroup
{
    [Command("move")]
    [Description("Move a card to another column.")]
    public async ValueTask TaskMoveCommand(
        SlashCommandContext context,
        [Description("Card to move")][SlashAutoCompleteProvider<AllTaskItemsAutoCompleteProvider>] string task,
        [SlashChoiceProvider<ColumnChoiceProvider>] int to)
    {
        await context.DeferResponseAsync(ephemeral: true);

        var taskItem = await GetTaskAsync(context, task);

        var embed = new DiscordEmbedBuilder()
            .WithDefaultColor();

        if (taskItem is null)
        {
            embed.WithDescription("That card could not be found.");

            await context.EditDeferredResponseAsync(embed);
            return;
        }

        if (taskItem.Status == (BoardStatus)to)
        {
            embed.WithDescription($"That card is already in **{((BoardStatus)to).ToFormattedString()}**.");

            await context.EditDeferredResponseAsync(embed);
            return;
        }

        var fromColumn = taskItem.Status;

        taskItem.Status = (BoardStatus)to;
        if (taskItem.Status == BoardStatus.Waiting && string.IsNullOrWhiteSpace(taskItem.BlockedReason))
            taskItem.BlockedReason = "Waiting on an update";
        else if (taskItem.Status != BoardStatus.Waiting)
            taskItem.BlockedReason = null;
        taskItem.LastUpdatedAt = DateTime.UtcNow;
        taskItem.RecordChange(
            context.User.Id,
            "card_moved",
            $"{taskItem.Title} moved to {((BoardStatus)to).ToFormattedString()}.");

        await _taskItemRepository.UpdateTaskItemAsync(taskItem);

        embed.WithDescription(
                $"**{taskItem.Title}** moved from **{fromColumn.ToFormattedString()}** to **{((BoardStatus)to).ToFormattedString()}**.");

        await context.EditDeferredResponseAsync(embed);
    }
}
