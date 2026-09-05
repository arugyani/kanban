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
    [Command("checklist")]
    [Description("View or change a card's checklist.")]
    public async ValueTask TaskChecklistCommand(
        SlashCommandContext context,
        [Description("What to do")][SlashChoiceProvider<ChecklistActionChoiceProvider>] int action,
        [Description("Card to update")][SlashAutoCompleteProvider<AllTaskItemsAutoCompleteProvider>] string task,
        [Description("New checklist text for Add")] string? text = null,
        [Description("Item number for Complete or Reopen")] int? item = null)
    {
        await context.DeferResponseAsync(ephemeral: true);

        if (action is < ChecklistActionChoiceProvider.View or > ChecklistActionChoiceProvider.Reopen)
        {
            await context.EditDeferredResponseAsync("Choose a valid checklist action.");
            return;
        }
        var card = await GetTaskAsync(
            context,
            task,
            requireEdit: action != ChecklistActionChoiceProvider.View);
        if (card is null)
        {
            await context.EditDeferredResponseAsync("That card could not be found.");
            return;
        }

        if (action == ChecklistActionChoiceProvider.View)
        {
            var description = card.Checklist.Count == 0
                ? "No checklist items yet."
                : string.Join('\n', card.Checklist
                    .OrderBy(entry => entry.Rank)
                    .Select((entry, index) => $"{(entry.Complete ? "☑" : "☐")} **{index + 1}.** {entry.Text}"));
            await context.EditDeferredResponseAsync(
                new DiscordEmbedBuilder()
                    .WithDefaultColor()
                    .WithTitle($"{card.Title} · Checklist")
                    .WithDescription(description));
            return;
        }

        if (action == ChecklistActionChoiceProvider.Add)
        {
            var trimmed = text?.Trim() ?? string.Empty;
            if (trimmed.Length is 0 or > 240)
            {
                await context.EditDeferredResponseAsync("Checklist text must be between 1 and 240 characters.");
                return;
            }
            card.Checklist.Add(new ChecklistItem
            {
                Text = trimmed,
                Rank = card.Checklist.Count == 0 ? 1024 : card.Checklist.Max(entry => entry.Rank) + 1024,
            });
            card.LastUpdatedAt = DateTime.UtcNow;
            card.RecordChange(context.User.Id, "checklist_added", $"A checklist item was added to {card.Title}.");
            await _taskItemRepository.UpdateTaskItemAsync(card);
            await context.EditDeferredResponseAsync($"Added a checklist item to **{card.Title}**.");
            return;
        }

        var ordered = card.Checklist.OrderBy(entry => entry.Rank).ToList();
        if (!item.HasValue || item < 1 || item > ordered.Count)
        {
            await context.EditDeferredResponseAsync("Choose an item number from the checklist.");
            return;
        }
        var selected = ordered[item.Value - 1];
        selected.Complete = action == ChecklistActionChoiceProvider.Complete;
        card.LastUpdatedAt = DateTime.UtcNow;
        card.RecordChange(
            context.User.Id,
            selected.Complete ? "checklist_completed" : "checklist_reopened",
            $"A checklist item on {card.Title} was {(selected.Complete ? "completed" : "reopened")}.");
        await _taskItemRepository.UpdateTaskItemAsync(card);
        await context.EditDeferredResponseAsync(
            $"**{selected.Text}** is now {(selected.Complete ? "complete" : "open")}.");
    }
}
