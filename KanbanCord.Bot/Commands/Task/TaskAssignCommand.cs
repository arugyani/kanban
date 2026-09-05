using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using KanbanCord.Bot.Extensions;
using KanbanCord.Bot.Providers;
using KanbanCord.Core.Models;
using MongoDB.Bson;

namespace KanbanCord.Bot.Commands.Task;

partial class TaskCommandGroup
{
    [Command("people")]
    [Description("Add or remove a person from a card.")]
    public async ValueTask TaskAssignCommand(
        SlashCommandContext context,
        [Description("Card to update")][SlashAutoCompleteProvider<AllTaskItemsAutoCompleteProvider>] string task,
        [Description("Person to add or remove")] DiscordUser? person = null,
        [Description("Remove this person")] bool remove = false,
        [Description("Assign a whole group instead")][SlashAutoCompleteProvider<TeamAutoCompleteProvider>] string? group = null)
    {
        await context.DeferResponseAsync(ephemeral: true);

        var card = await GetTaskAsync(context, task);
        if (card is null)
        {
            await context.EditDeferredResponseAsync("That card could not be found.");
            return;
        }
        if (person is not null && group is not null)
        {
            await context.EditDeferredResponseAsync("Choose a person or a group, not both.");
            return;
        }

        if (group is not null)
        {
            var selectedGroup = ObjectId.TryParse(group, out var groupId)
                ? await _teamRepository.GetByObjectIdOrDefaultAsync(groupId, context.Guild!.Id)
                : null;
            if (selectedGroup is null)
            {
                await context.EditDeferredResponseAsync("That group could not be found.");
                return;
            }
            if (!_authorization.CanViewGroup(selectedGroup, context.User.Id))
            {
                await context.EditDeferredResponseAsync("That group could not be found.");
                return;
            }
            var board = card.BoardId.HasValue
                ? await _boardRepository.GetByObjectIdOrDefaultAsync(card.BoardId.Value, context.Guild!.Id)
                : null;
            if (board is null)
            {
                await context.EditDeferredResponseAsync("That card's board could not be found.");
                return;
            }
            foreach (var memberId in selectedGroup.MemberIds)
            {
                if (!await _boardResolver.CanIncludePersonAsync(board, memberId))
                {
                    await context.EditDeferredResponseAsync(
                        "Choose a group whose people can open this card's board.");
                    return;
                }
            }
            card.AssigneeId = null;
            card.AssigneeIds = [];
            card.AssigneeTeamId = selectedGroup.Id;
            card.LastUpdatedAt = DateTime.UtcNow;
            card.RecordChange(context.User.Id, "people_changed", $"{selectedGroup.Name} joined {card.Title}.");
            await _taskItemRepository.UpdateTaskItemAsync(card);
            await context.EditDeferredResponseAsync(
                $"**{card.Title}** now involves **{selectedGroup.Name}**.");
            return;
        }

        if (person is null)
        {
            card.AssigneeId = null;
            card.AssigneeIds = [];
            card.AssigneeTeamId = null;
            card.LastUpdatedAt = DateTime.UtcNow;
            card.RecordChange(context.User.Id, "people_changed", $"Everyone left {card.Title}.");
            await _taskItemRepository.UpdateTaskItemAsync(card);
            await context.EditDeferredResponseAsync(
                $"Nobody is on **{card.Title}** now.");
            return;
        }

        var people = card.AssigneeIds.ToHashSet();
        if (card.AssigneeId.HasValue)
            people.Add(card.AssigneeId.Value);
        string message;
        if (remove)
        {
            if (!people.Remove(person.Id))
            {
                await context.EditDeferredResponseAsync(
                    $"{person.Mention} is not on **{card.Title}**.");
                return;
            }
            message = $"Removed {person.Mention} from **{card.Title}**.";
        }
        else
        {
            var board = card.BoardId.HasValue
                ? await _boardRepository.GetByObjectIdOrDefaultAsync(card.BoardId.Value, context.Guild!.Id)
                : null;
            if (board is null || !await _boardResolver.CanIncludePersonAsync(board, person.Id))
            {
                await context.EditDeferredResponseAsync(
                    "Choose someone who has access to this card's group.");
                return;
            }
            if (!people.Add(person.Id))
            {
                await context.EditDeferredResponseAsync(
                    $"{person.Mention} is already on **{card.Title}**.");
                return;
            }
            message = $"Added {person.Mention} to **{card.Title}**.";
            await TryNotifyAsync(context, person, card);
        }

        card.AssigneeIds = people.ToList();
        card.AssigneeId = card.AssigneeIds.Count == 0 ? null : card.AssigneeIds[0];
        card.AssigneeTeamId = null;
        card.LastUpdatedAt = DateTime.UtcNow;
        card.RecordChange(context.User.Id, "people_changed", $"People on {card.Title} changed.");
        await _taskItemRepository.UpdateTaskItemAsync(card);
        await context.EditDeferredResponseAsync(message);
    }

    private static async System.Threading.Tasks.Task TryNotifyAsync(
        SlashCommandContext context,
        DiscordUser person,
        TaskItem card)
    {
        try
        {
            await person.SendMessageAsync(new DiscordEmbedBuilder()
                .WithDefaultColor()
                .WithAuthor(context.Guild!.Name, iconUrl: context.Guild.IconUrl)
                .WithTitle("You were added to a card")
                .AddField("Card", card.Title)
                .AddField("Notes", string.IsNullOrWhiteSpace(card.Description) ? "No notes yet." : card.Description));
        }
        catch
        {
            // Direct messages are optional. The card update still succeeds if
            // this person has DMs disabled for the server.
        }
    }
}
