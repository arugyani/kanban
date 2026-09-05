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
    [Command("archive")]
    [Description("Archive a card from Done.")]
    public async ValueTask TaskArchiveCommand(SlashCommandContext context, [Description("Card to archive")][SlashAutoCompleteProvider<CompletedTaskItemsAutoCompleteProvider>] string task)
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

        var fromColumn = taskItem.Status;

        taskItem.Status = BoardStatus.Archived;
        taskItem.LastUpdatedAt = DateTime.UtcNow;
        taskItem.RecordChange(context.User.Id, "card_archived", $"{taskItem.Title} was archived.");

        await _taskItemRepository.UpdateTaskItemAsync(taskItem);

        var commands = await context.Client.GetGlobalApplicationCommandsAsync();

        embed.WithDescription(
                $"**{taskItem.Title}** moved from **{fromColumn.ToFormattedString()}** to the archive. View it using {commands.GetMention(["archive"])}.");

        await context.RespondAsync(embed, ephemeral: true);
    }
}
