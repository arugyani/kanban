using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
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
    [Description("Add a task to the backlog")]
    public async ValueTask TaskAddCommand(
        SlashCommandContext context,
        [Description("Board to add to; defaults to Default")] [SlashAutoCompleteProvider<BoardAutoCompleteProvider>] string? board = null)
    {
        var selectedBoard = await _boardResolver.ResolveAsync(context.Guild!.Id, context.User.Id, board);

        if (selectedBoard is null)
        {
            await context.RespondAsync("The selected board was not found.");
            return;
        }

        var modal = new DiscordInteractionResponseBuilder()
            .WithCustomId(Guid.NewGuid().ToString())
            .WithTitle("Add a new Task")
            .AddTextInputComponent(new DiscordTextInputComponent(
                "Title:",
                "titleField",
                "Title of the task",
                max_length: Limits.TaskTitleMaxLength))
            .AddTextInputComponent(new DiscordTextInputComponent(
                "Description:",
                "descriptionField",
                "Description of the task",
                max_length: Limits.TaskDescriptionMaxLength,
                style: DiscordTextInputStyle.Paragraph));
        
        await context.Interaction.CreateResponseAsync(DiscordInteractionResponseType.Modal, modal);
        
        var interaction = context.Client.ServiceProvider.GetRequiredService<InteractivityExtension>();

        var response = await interaction.WaitForModalAsync(modal.CustomId, TimeSpan.FromMinutes(5));
        
        if (!response.TimedOut)
        {
            var modalInteraction = response.Result.Values;
            
            var newTask = new TaskItem
            {
                GuildId = context.Guild!.Id,
                BoardId = selectedBoard.Id,
                Title = modalInteraction["titleField"],
                Description = modalInteraction["descriptionField"],
                AuthorId = context.User.Id,
            };
            
            await _taskItemRepository.AddTaskItemAsync(newTask);

            var commands = await context.Client.GetGlobalApplicationCommandsAsync();
            
            var embed = new DiscordEmbedBuilder()
                .WithDefaultColor()
                .WithDescription(
                    $"The task \"{newTask.Title}\" has been added to **{selectedBoard.Name}**. View it using {commands.GetMention(["board"])}.");
            
            await response.Result.Interaction.CreateResponseAsync(
                DiscordInteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .AddEmbed(embed));
        }
    }
}
