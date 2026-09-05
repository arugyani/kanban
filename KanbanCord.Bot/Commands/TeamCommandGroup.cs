using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using KanbanCord.Bot.Extensions;
using KanbanCord.Bot.Helpers;
using KanbanCord.Bot.Providers;
using KanbanCord.Core.Constants;
using KanbanCord.Core.Models;
using KanbanCord.Core.Repositories;
using MongoDB.Bson;

namespace KanbanCord.Bot.Commands;

[Command("group")]
[RequirePermissions(userPermissions: [DiscordPermission.ManageMessages], botPermissions: [])]
public class TeamCommandGroup
{
    private readonly ITeamRepository _teamRepository;
    private readonly IBoardRepository _boardRepository;
    private readonly ITaskItemRepository _taskItemRepository;
    private readonly BoardAuthorizationService _authorization;

    public TeamCommandGroup(
        ITeamRepository teamRepository,
        IBoardRepository boardRepository,
        ITaskItemRepository taskItemRepository,
        BoardAuthorizationService authorization)
    {
        _teamRepository = teamRepository;
        _boardRepository = boardRepository;
        _taskItemRepository = taskItemRepository;
        _authorization = authorization;
    }

    [Command("list")]
    [Description("List groups and their people.")]
    [RequirePermissions(userPermissions: [], botPermissions: [])]
    public async ValueTask ListAsync(SlashCommandContext context)
    {
        var teams = (await _teamRepository.GetAllByGuildIdAsync(context.Guild!.Id))
            .Where(team => _authorization.CanViewGroup(team, context.User.Id))
            .ToList();
        var embed = new DiscordEmbedBuilder()
            .WithDefaultColor()
            .WithAuthor("RGBOO Groups");

        if (teams.Count == 0)
        {
            embed.WithDescription("No groups are available to you yet.");
        }
        else
        {
            foreach (var team in teams.Take(25))
            {
                var people = team.MemberIds.Count == 0
                    ? "No people yet"
                    : string.Join(", ", team.MemberIds.Take(30).Select(memberId => $"<@{memberId}>"));
                embed.AddField(team.Name, people);
            }
        }

        await context.RespondAsync(embed, ephemeral: true);
    }

    [Command("create")]
    [Description("Create a group.")]
    public async ValueTask CreateAsync(SlashCommandContext context, [Description("Group name")] string name)
    {
        name = name.Trim();

        if (name.Length is 0 or > Limits.TeamNameMaxLength)
        {
            await RespondWithErrorAsync(context, $"Group names must be between 1 and {Limits.TeamNameMaxLength} characters.");
            return;
        }

        if (await _teamRepository.NameExistsAsync(context.Guild!.Id, name))
        {
            await RespondWithErrorAsync(context, $"A group named \"{name}\" already exists.");
            return;
        }

        var team = new Team
        {
            GuildId = context.Guild.Id,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            CreatedById = context.User.Id
        };

        await _teamRepository.AddAsync(team);
        await context.RespondAsync(new DiscordEmbedBuilder()
            .WithDefaultColor()
            .WithDescription($"Created the **{team.Name}** group."));
    }

    [Command("add-person")]
    [Description("Add a person to a group.")]
    public async ValueTask AddPersonAsync(
        SlashCommandContext context,
        [Description("Group to update")][SlashAutoCompleteProvider<TeamAutoCompleteProvider>] string team,
        [Description("Person to add")] DiscordUser person)
    {
        var selectedTeam = await ResolveTeamAsync(context.Guild!.Id, team);

        if (selectedTeam is null)
        {
            await RespondWithErrorAsync(context, "The selected group was not found.");
            return;
        }

        if (selectedTeam.MemberIds.Contains(person.Id))
        {
            await RespondWithErrorAsync(context, $"{person.Mention} is already on **{selectedTeam.Name}**.");
            return;
        }

        selectedTeam.MemberIds.Add(person.Id);
        await _teamRepository.UpdateAsync(selectedTeam);

        await context.RespondAsync(new DiscordEmbedBuilder()
            .WithDefaultColor()
            .WithDescription($"Added {person.Mention} to **{selectedTeam.Name}**."));
    }

    [Command("remove-person")]
    [Description("Remove a person from a group.")]
    public async ValueTask RemovePersonAsync(
        SlashCommandContext context,
        [Description("Group to update")][SlashAutoCompleteProvider<TeamAutoCompleteProvider>] string team,
        [Description("Person to remove")] DiscordUser person)
    {
        var selectedTeam = await ResolveTeamAsync(context.Guild!.Id, team);

        if (selectedTeam is null)
        {
            await RespondWithErrorAsync(context, "The selected group was not found.");
            return;
        }

        if (!selectedTeam.MemberIds.Remove(person.Id))
        {
            await RespondWithErrorAsync(context, $"{person.Mention} is not on **{selectedTeam.Name}**.");
            return;
        }

        selectedTeam.MemberRoles.Remove(person.Id.ToString());

        await _teamRepository.UpdateAsync(selectedTeam);
        await context.RespondAsync(new DiscordEmbedBuilder()
            .WithDefaultColor()
            .WithDescription($"Removed {person.Mention} from **{selectedTeam.Name}**."));
    }

    [Command("delete")]
    [Description("Delete a group that has no boards or cards.")]
    public async ValueTask DeleteAsync(
        SlashCommandContext context,
        [Description("Group to delete")][SlashAutoCompleteProvider<TeamAutoCompleteProvider>] string team)
    {
        var selectedTeam = await ResolveTeamAsync(context.Guild!.Id, team);

        if (selectedTeam is null)
        {
            await RespondWithErrorAsync(context, "The selected group was not found.");
            return;
        }

        var boards = await _boardRepository.GetAllByGuildIdAsync(context.Guild.Id);
        var tasks = await _taskItemRepository.GetAllTaskItemsByGuildIdAsync(context.Guild.Id);

        if (boards.Any(board => board.TeamId == selectedTeam.Id)
            || tasks.Any(task => task.AssigneeTeamId == selectedTeam.Id))
        {
            await RespondWithErrorAsync(context, "Remove this group from its boards and cards before deleting it.");
            return;
        }

        await _teamRepository.RemoveAsync(selectedTeam);
        await context.RespondAsync(new DiscordEmbedBuilder()
            .WithDefaultColor()
            .WithDescription($"Deleted the **{selectedTeam.Name}** group."));
    }

    private async Task<Team?> ResolveTeamAsync(ulong guildId, string teamId)
    {
        return ObjectId.TryParse(teamId, out var objectId)
            ? await _teamRepository.GetByObjectIdOrDefaultAsync(objectId, guildId)
            : null;
    }

    private static async System.Threading.Tasks.Task RespondWithErrorAsync(SlashCommandContext context, string description)
    {
        await context.RespondAsync(new DiscordEmbedBuilder().WithDefaultColor().WithDescription(description));
    }
}
