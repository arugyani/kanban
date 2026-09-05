using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using KanbanCord.Bot.Providers;
using KanbanCord.Bot.Extensions;
using KanbanCord.Bot.Helpers;
using KanbanCord.Core.Models;
using MongoDB.Bson;

namespace KanbanCord.Bot.Commands.Task;

partial class TaskCommandGroup
{
    [Command("complete")]
    [Description("Move a card from Doing to Done.")]
    public async ValueTask TaskCompleteCommand(SlashCommandContext context, [Description("Card to finish")][SlashAutoCompleteProvider<InProgressTaskItemsAutoCompleteProvider>] string task)
    {
        var taskItem = await GetTaskAsync(context, task);

        var embed = new DiscordEmbedBuilder()
            .WithDefaultColor();

        if (taskItem is null)
        {
            embed.WithDescription("That card could not be found.");

            await context.RespondAsync(embed, ephemeral: true);
            return;
        }

        taskItem.Status = BoardStatus.Completed;
        taskItem.LastUpdatedAt = DateTime.UtcNow;
        taskItem.RecordChange(context.User.Id, "card_moved", $"{taskItem.Title} moved to Done.");

        await _taskItemRepository.UpdateTaskItemAsync(taskItem);

        var commands = await context.Client.GetGlobalApplicationCommandsAsync();

        embed.WithDescription(
                $"**{taskItem.Title}** moved to **Done**. View it using {commands.GetMention(["board", "recap"])}.");

        await context.RespondAsync(embed, ephemeral: true);
    }
}
