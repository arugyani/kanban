using System.ComponentModel;
using System.Globalization;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using KanbanCord.Bot.Extensions;
using KanbanCord.Bot.Providers;
using KanbanCord.Core.Constants;
using KanbanCord.Core.Models;

namespace KanbanCord.Bot.Commands.Task;

partial class TaskCommandGroup
{
    [Command("update")]
    [Description("Update a card's details.")]
    public async ValueTask TaskEditCommand(
        SlashCommandContext context,
        [Description("Card to update")][SlashAutoCompleteProvider<AllTaskItemsAutoCompleteProvider>] string task,
        [Description("New title")] string? title = null,
        [Description("New notes")] string? notes = null,
        [Description("Remove the current notes")] bool clearNotes = false,
        [Description("Comma-separated tags, or clear")] string? tags = null,
        [Description("Date as YYYY-MM-DD, or clear")] string? when = null,
        [Description("New importance")][SlashChoiceProvider<PriorityChoiceProvider>] int? importance = null,
        [Description("Mark or unmark as waiting")] bool? waiting = null,
        [Description("What the card is waiting on")] string? waitingReason = null)
    {
        await context.DeferResponseAsync(ephemeral: true);

        if (title is null
            && notes is null
            && !clearNotes
            && tags is null
            && when is null
            && importance is null
            && waiting is null
            && waitingReason is null)
        {
            await context.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent("Choose at least one card detail to update."));
            return;
        }
        if (notes is not null && clearNotes)
        {
            await context.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent("Choose new notes or remove them, but not both."));
            return;
        }
        if (waiting is false && waitingReason is not null)
        {
            await context.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent("Remove the waiting state or add a waiting reason, but not both."));
            return;
        }

        var taskItem = await GetTaskAsync(context, task);
        if (taskItem is null)
        {
            await context.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent("That card could not be found."));
            return;
        }

        var nextTitle = title?.Trim() ?? taskItem.Title;
        if (nextTitle.Length is 0 or > Limits.TaskTitleMaxLength)
        {
            await context.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent($"Keep the title between 1 and {Limits.TaskTitleMaxLength} characters."));
            return;
        }

        var nextNotes = clearNotes ? string.Empty : notes?.Trim() ?? taskItem.Description;
        if (nextNotes.Length > Limits.TaskDescriptionMaxLength)
        {
            await context.EditResponseAsync(new DiscordWebhookBuilder()
                .WithContent($"Keep the notes under {Limits.TaskDescriptionMaxLength} characters."));
            return;
        }

        var nextDueAt = taskItem.DueAt;
        if (when is not null)
        {
            if (string.Equals(when.Trim(), "clear", StringComparison.OrdinalIgnoreCase))
                nextDueAt = null;
            else if (DateTime.TryParseExact(
                         when.Trim(),
                         "yyyy-MM-dd",
                         CultureInfo.InvariantCulture,
                         DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                         out var parsedDate))
                nextDueAt = parsedDate;
            else
            {
                await context.EditResponseAsync(new DiscordWebhookBuilder()
                    .WithContent("Use a date like 2026-10-31, or type clear."));
                return;
            }
        }

        var nextTags = taskItem.Tags;
        if (tags is not null)
        {
            nextTags = string.Equals(tags.Trim(), "clear", StringComparison.OrdinalIgnoreCase)
                ? []
                : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            if (nextTags.Count > Limits.TaskTagsMaxCount
                || nextTags.Any(tag => tag.Length is 0 or > Limits.TaskTagMaxLength))
            {
                await context.EditResponseAsync(new DiscordWebhookBuilder()
                    .WithContent(
                        $"Use no more than {Limits.TaskTagsMaxCount} tags, each under {Limits.TaskTagMaxLength} characters."));
                return;
            }
        }

        Priority? nextPriority = taskItem.Priority;
        if (importance.HasValue)
        {
            if (importance == PriorityChoiceProvider.None)
                nextPriority = null;
            else if (Enum.IsDefined((Priority)importance.Value))
                nextPriority = (Priority)importance.Value;
            else
            {
                await context.EditResponseAsync(new DiscordWebhookBuilder()
                    .WithContent("Choose a valid importance."));
                return;
            }
        }

        var nextStatus = taskItem.Status;
        var nextBlockedReason = taskItem.BlockedReason;
        if (waiting is false)
        {
            if (nextStatus == BoardStatus.Waiting)
                nextStatus = BoardStatus.UpNext;
            nextBlockedReason = null;
        }
        else if (waiting is true || waitingReason is not null)
        {
            var reason = waitingReason?.Trim();
            if (reason?.Length > Limits.TaskBlockedReasonMaxLength)
            {
                await context.EditResponseAsync(new DiscordWebhookBuilder()
                    .WithContent($"Keep the waiting reason under {Limits.TaskBlockedReasonMaxLength} characters."));
                return;
            }
            nextStatus = BoardStatus.Waiting;
            nextBlockedReason = string.IsNullOrWhiteSpace(reason)
                ? taskItem.BlockedReason ?? "Waiting on an update"
                : reason;
        }

        taskItem.Title = nextTitle;
        taskItem.Description = nextNotes;
        taskItem.DueAt = nextDueAt;
        taskItem.Tags = nextTags;
        taskItem.Priority = nextPriority;
        taskItem.Status = nextStatus;
        taskItem.BlockedReason = nextBlockedReason;
        taskItem.RecordChange(context.User.Id, "card_updated", $"{taskItem.Title} was updated.");

        await _taskItemRepository.UpdateTaskItemAsync(taskItem);

        await context.EditResponseAsync(new DiscordWebhookBuilder()
            .AddEmbed(new DiscordEmbedBuilder()
                .WithDefaultColor()
                .WithDescription($"**{taskItem.Title}** was updated. Use `/card open` to view it.")));
    }
}
