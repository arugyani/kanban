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
    [Command("assign")]
    [Description("Assign a task to a person or team.")]
    public async ValueTask TaskAssignCommand(
        SlashCommandContext context,
        [Description("Search for the task to select")] [SlashAutoCompleteProvider<AllTaskItemsAutoCompleteProvider>] string task,
        [Description("Person to assign; omit to remove")] DiscordUser? assignee = null,
        [Description("Team to assign; omit to remove")] [SlashAutoCompleteProvider<TeamAutoCompleteProvider>] string? team = null)
    {
        var taskItem = await GetTaskAsync(context, task);

        var embed = new DiscordEmbedBuilder()
            .WithDefaultColor();
        
        if (taskItem is null)
        {
            embed.WithDescription("The selected task was not found, please try again.");
            
            await context.RespondAsync(embed);
            return;
        }

        if (assignee is not null && team is not null)
        {
            embed.WithDescription("Assign a task to either a person or a team, not both.");
            await context.RespondAsync(embed);
            return;
        }

        Team? selectedTeam = null;

        if (team is not null)
        {
            selectedTeam = ObjectId.TryParse(team, out var teamId)
                ? await _teamRepository.GetByObjectIdOrDefaultAsync(teamId, context.Guild!.Id)
                : null;

            if (selectedTeam is null)
            {
                embed.WithDescription("The selected team was not found.");
                await context.RespondAsync(embed);
                return;
            }
        }

        if (assignee is null && selectedTeam is null)
        {
            if (taskItem.AssigneeId is null && taskItem.AssigneeTeamId is null)
            {
                embed.WithDescription("The selected task has no person or team assigned.");
            
                await context.RespondAsync(embed);
                return;
            }
            
            var previousAssignee = taskItem.AssigneeId.HasValue
                ? $"<@{taskItem.AssigneeId.Value}>"
                : "the assigned team";
            
            taskItem.AssigneeId = null;
            taskItem.AssigneeTeamId = null;
            taskItem.LastUpdatedAt = DateTime.UtcNow;
            
            await _taskItemRepository.UpdateTaskItemAsync(taskItem);
            
            embed.WithDescription($"The task \"{taskItem.Title}\" is no longer assigned to {previousAssignee}.");
            
            await context.RespondAsync(embed);
        }
        else if (assignee is not null)
        {
            taskItem.AssigneeId = assignee.Id;
            taskItem.AssigneeTeamId = null;
            taskItem.LastUpdatedAt = DateTime.UtcNow;
        
            await _taskItemRepository.UpdateTaskItemAsync(taskItem);

            bool directMessageSentToAssignee;

            try
            {
                var assigneeEmbed = new DiscordEmbedBuilder()
                    .WithDefaultColor()
                    .WithAuthor(context.Guild!.Name, iconUrl: context.Guild.IconUrl)
                    .WithTitle("You have been assigned to a Task!")
                    .AddField("Task Name", taskItem.Title)
                    .AddField("Task Description", taskItem.Description);
            
                await assignee.SendMessageAsync(assigneeEmbed);

                directMessageSentToAssignee = true;
            }
            catch (Exception)
            {
                directMessageSentToAssignee = false;
            }

            embed.WithDescription($"The task \"{taskItem.Title}\" has been assigned to {assignee.Mention}.");
        
            var response = new DiscordInteractionResponseBuilder()
                .AddMention(new UserMention(assignee))
                .AddEmbed(embed);
        
            if (!directMessageSentToAssignee)
                response.WithContent(assignee.Mention);
        
            await context.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, response);
        }
        else
        {
            taskItem.AssigneeId = null;
            taskItem.AssigneeTeamId = selectedTeam!.Id;
            taskItem.LastUpdatedAt = DateTime.UtcNow;

            await _taskItemRepository.UpdateTaskItemAsync(taskItem);

            embed.WithDescription($"The task \"{taskItem.Title}\" has been assigned to **{selectedTeam.Name}**.");
            await context.RespondAsync(embed);
        }
    }
}
