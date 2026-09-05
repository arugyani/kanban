using System.ComponentModel;
using System.Globalization;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using KanbanCord.Bot.Extensions;
using KanbanCord.Bot.Helpers;
using KanbanCord.Bot.Providers;
using KanbanCord.Core.Constants;
using KanbanCord.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace KanbanCord.Bot.Commands.Task;

partial class TaskCommandGroup
{
    [Command("add")]
    [Description("Add a card to a board.")]
    public async ValueTask TaskAddCommand(
        SlashCommandContext context,
        [Description("Board to add to; defaults to Default")][SlashAutoCompleteProvider<BoardAutoCompleteProvider>] string? board = null,
        [Description("First person on this card")] DiscordUser? person = null,
        [Description("Another person on this card")] DiscordUser? anotherPerson = null,
        [Description("Card importance")][SlashChoiceProvider<PriorityChoiceProvider>] int? importance = null,
        [Description("Date as YYYY-MM-DD")] string? when = null)
    {
        var modal = new DiscordModalBuilder()
            .WithCustomId(Guid.NewGuid().ToString())
            .WithTitle("Add a card")
            .AddTextInput(new DiscordTextInputComponent(
                "titleField",
                "A short card title",
                required: true,
                max_length: Limits.TaskTitleMaxLength), "Title", "Keep it short and clear.")
            .AddTextInput(new DiscordTextInputComponent(
                "descriptionField",
                "Useful context for the group",
                required: false,
                max_length: Limits.TaskDescriptionMaxLength,
                style: DiscordTextInputStyle.Paragraph), "Notes", "Optional context for the group.");

        await context.Interaction.CreateResponseAsync(DiscordInteractionResponseType.Modal, modal);

        var interaction = context.Client.ServiceProvider.GetRequiredService<InteractivityExtension>();

        var response = await interaction.WaitForModalAsync(modal.CustomId, TimeSpan.FromMinutes(5));

        if (!response.TimedOut)
        {
            var modalInteraction = response.Result.Interaction;
            await modalInteraction.CreateResponseAsync(
                DiscordInteractionResponseType.DeferredChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().AsEphemeral());
            try
            {
                var selectedBoard = await _boardResolver.ResolveEditableAsync(
                    context.Guild!.Id,
                    context.User.Id,
                    board);
                if (selectedBoard is null)
                {
                    await modalInteraction.EditOriginalResponseAsync(
                        new DiscordWebhookBuilder().WithContent("The selected board was not found."));
                    return;
                }

                var people = new[] { person, anotherPerson }
                    .Where(candidate => candidate is not null)
                    .Select(candidate => candidate!.Id)
                    .Distinct()
                    .ToList();
                foreach (var personId in people)
                {
                    if (!await _boardResolver.CanIncludePersonAsync(selectedBoard, personId))
                    {
                        await modalInteraction.EditOriginalResponseAsync(
                            new DiscordWebhookBuilder().WithContent(
                                "Choose people who have access to this board's group."));
                        return;
                    }
                }

                DateTime? dueAt = null;
                if (!string.IsNullOrWhiteSpace(when))
                {
                    if (!DateTime.TryParseExact(
                            when,
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                            out var parsedDate))
                    {
                        await modalInteraction.EditOriginalResponseAsync(
                            new DiscordWebhookBuilder().WithContent("Use a date like 2026-10-31."));
                        return;
                    }
                    dueAt = parsedDate;
                }

                Priority? selectedImportance;
                if (!importance.HasValue)
                    selectedImportance = Priority.Medium;
                else if (importance == PriorityChoiceProvider.None)
                    selectedImportance = null;
                else if (Enum.IsDefined((Priority)importance.Value))
                    selectedImportance = (Priority)importance.Value;
                else
                {
                    await modalInteraction.EditOriginalResponseAsync(
                        new DiscordWebhookBuilder().WithContent("Choose a valid importance."));
                    return;
                }

                var values = response.Result.Values;
                var newTask = new TaskItem
                {
                    GuildId = context.Guild!.Id,
                    BoardId = selectedBoard.Id,
                    Title = ((TextInputModalSubmission)values["titleField"]).Value,
                    Description = ((TextInputModalSubmission)values["descriptionField"]).Value,
                    AuthorId = context.User.Id,
                    AssigneeId = people.Count == 0 ? null : people[0],
                    AssigneeIds = people,
                    Priority = selectedImportance,
                    DueAt = dueAt,
                    Version = 1,
                };
                newTask.RecordChange(context.User.Id, "card_added", $"{newTask.Title} was added.");

                await _taskItemRepository.AddTaskItemAsync(newTask);

                var embed = new DiscordEmbedBuilder()
                    .WithDefaultColor()
                    .WithDescription(
                        $"**{newTask.Title}** was added to **{selectedBoard.Name}**. Use `/board recap` to see it.");

                await modalInteraction.EditOriginalResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(embed));
            }
            catch (Exception exception)
            {
                await HandleModalFailureAsync(modalInteraction, exception, "add");
            }
        }
    }
}
