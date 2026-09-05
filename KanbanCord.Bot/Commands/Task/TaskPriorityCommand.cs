using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using KanbanCord.Bot.Extensions;
using KanbanCord.Bot.Providers;
using KanbanCord.Core.Models;

namespace KanbanCord.Bot.Commands.Task;

partial class TaskCommandGroup
{
    [Command("importance")]
    [Description("Set a card's importance.")]
    public async ValueTask TaskPriorityCommand(
        SlashCommandContext context,
        [Description("Card to update")][SlashAutoCompleteProvider<AllTaskItemsAutoCompleteProvider>] string task,
        [Description("New importance")][SlashChoiceProvider<PriorityChoiceProvider>] int importance)
    {
        await context.DeferResponseAsync(ephemeral: true);

        Priority? nextPriority;
        if (importance == PriorityChoiceProvider.None)
            nextPriority = null;
        else if (Enum.IsDefined((Priority)importance))
            nextPriority = (Priority)importance;
        else
        {
            await context.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent("Choose a valid importance."));
            return;
        }

        var taskItem = await GetTaskAsync(context, task);
        if (taskItem is null)
        {
            await context.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent("That card could not be found."));
            return;
        }

        var nextName = nextPriority?.ToString() ?? "No flag";
        if (taskItem.Priority == nextPriority)
        {
            await context.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent($"That card already has **{nextName}** importance."));
            return;
        }

        var previousName = taskItem.Priority?.ToString() ?? "No flag";
        taskItem.Priority = nextPriority;
        taskItem.RecordChange(
            context.User.Id,
            "importance_changed",
            $"{taskItem.Title} changed to {nextName} importance.");

        await _taskItemRepository.UpdateTaskItemAsync(taskItem);

        await context.EditResponseAsync(new DiscordWebhookBuilder()
            .AddEmbed(new DiscordEmbedBuilder()
                .WithDefaultColor()
                .WithDescription(
                    $"**{taskItem.Title}** changed from **{previousName}** to **{nextName}** importance.")));
    }
}
