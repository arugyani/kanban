using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using KanbanCord.Bot.Providers;
using KanbanCord.Bot.Extensions;
using KanbanCord.Bot.Helpers;
using KanbanCord.Core.Constants;
using KanbanCord.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;

namespace KanbanCord.Bot.Commands.Task;

partial class TaskCommandGroup
{
    [Command("note")]
    [Description("Add a note to a card.")]
    public async ValueTask TaskCommentCommand(SlashCommandContext context, [Description("Card to add a note to")][SlashAutoCompleteProvider<AllTaskItemsAutoCompleteProvider>] string task)
    {
        var modal = new DiscordModalBuilder()
            .WithCustomId(Guid.NewGuid().ToString())
            .WithTitle("Add a note")
            .AddTextInput(new DiscordTextInputComponent(
                "commentField",
                "Share an update with the group",
                required: true,
                max_length: Limits.TaskCommentMaxLength,
                min_length: Limits.TaskCommentMinLength,
                style: DiscordTextInputStyle.Paragraph), "Note", "This will be visible to everyone on the card.");

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
                var taskItem = await GetTaskAsync(context, task);
                if (taskItem is null)
                {
                    await modalInteraction.EditOriginalResponseAsync(
                        new DiscordWebhookBuilder().WithContent("That card could not be found."));
                    return;
                }

                var values = response.Result.Values;

                taskItem.Comments.Add(new Comment
                {
                    AuthorId = context.User.Id,
                    Text = ((TextInputModalSubmission)values["commentField"]).Value
                });
                taskItem.RecordChange(context.User.Id, "comment_added", $"A note was added to {taskItem.Title}.");

                await _taskItemRepository.UpdateTaskItemAsync(taskItem);

                await modalInteraction.EditOriginalResponseAsync(
                    new DiscordWebhookBuilder().AddEmbed(new DiscordEmbedBuilder()
                        .WithDefaultColor()
                        .WithDescription(
                            $"Your note was added to **{taskItem.Title}**. Use `/card open` to view it.")));
            }
            catch (Exception exception)
            {
                await HandleModalFailureAsync(modalInteraction, exception, "note");
            }
        }
    }
}
