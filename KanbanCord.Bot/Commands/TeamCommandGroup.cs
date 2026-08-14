using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using KanbanCord.Bot.Extensions;
using KanbanCord.Bot.Providers;
using KanbanCord.Core.Constants;
using KanbanCord.Core.Models;
using KanbanCord.Core.Repositories;
using MongoDB.Bson;

namespace KanbanCord.Bot.Commands;

[Command("team")]
[RequirePermissions(userPermissions: [DiscordPermission.ManageMessages], botPermissions: [])]
public class TeamCommandGroup
{
    private readonly ITeamRepository _teamRepository;
    private readonly IBoardRepository _boardRepository;
    private readonly ITaskItemRepository _taskItemRepository;

    public TeamCommandGroup(
        ITeamRepository teamRepository,
        IBoardRepository boardRepository,
        ITaskItemRepository taskItemRepository)
    {
        _teamRepository = teamRepository;
        _boardRepository = boardRepository;
        _taskItemRepository = taskItemRepository;
    }

    [Command("list")]
    [Description("List teams and their people.")]
    [RequirePermissions(userPermissions: [], botPermissions: [])]
    public async ValueTask ListAsync(SlashCommandContext context)
    {
        var teams = await _teamRepository.GetAllByGuildIdAsync(context.Guild!.Id);
        var embed = new DiscordEmbedBuilder()
            .WithDefaultColor()
            .WithAuthor("KanbanCord Teams");

        if (teams.Count == 0)
        {
            embed.WithDescription("No teams have been created yet.");
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

        await context.RespondAsync(embed);
    }

    [Command("create")]
    [Description("Create a team.")]
    public async ValueTask CreateAsync(SlashCommandContext context, [Description("Team name")] string name)
    {
        name = name.Trim();

        if (name.Length is 0 or > Limits.TeamNameMaxLength)
        {
            await RespondWithErrorAsync(context, $"Team names must be between 1 and {Limits.TeamNameMaxLength} characters.");
            return;
        }

        if (await _teamRepository.NameExistsAsync(context.Guild!.Id, name))
        {
            await RespondWithErrorAsync(context, $"A team named \"{name}\" already exists.");
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
            .WithDescription($"Created the **{team.Name}** team."));
    }

    [Command("add-person")]
    [Description("Add a person to a team.")]
    public async ValueTask AddPersonAsync(
        SlashCommandContext context,
        [Description("Team to update")] [SlashAutoCompleteProvider<TeamAutoCompleteProvider>] string team,
        [Description("Person to add")] DiscordUser person)
    {
        var selectedTeam = await ResolveTeamAsync(context.Guild!.Id, team);

        if (selectedTeam is null)
        {
            await RespondWithErrorAsync(context, "The selected team was not found.");
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
    [Description("Remove a person from a team.")]
    public async ValueTask RemovePersonAsync(
        SlashCommandContext context,
        [Description("Team to update")] [SlashAutoCompleteProvider<TeamAutoCompleteProvider>] string team,
        [Description("Person to remove")] DiscordUser person)
    {
        var selectedTeam = await ResolveTeamAsync(context.Guild!.Id, team);

        if (selectedTeam is null)
        {
            await RespondWithErrorAsync(context, "The selected team was not found.");
            return;
        }

        if (!selectedTeam.MemberIds.Remove(person.Id))
        {
            await RespondWithErrorAsync(context, $"{person.Mention} is not on **{selectedTeam.Name}**.");
            return;
        }

        await _teamRepository.UpdateAsync(selectedTeam);
        await context.RespondAsync(new DiscordEmbedBuilder()
            .WithDefaultColor()
            .WithDescription($"Removed {person.Mention} from **{selectedTeam.Name}**."));
    }

    [Command("delete")]
    [Description("Delete a team that has no boards or assigned tasks.")]
    public async ValueTask DeleteAsync(
        SlashCommandContext context,
        [Description("Team to delete")] [SlashAutoCompleteProvider<TeamAutoCompleteProvider>] string team)
    {
        var selectedTeam = await ResolveTeamAsync(context.Guild!.Id, team);

        if (selectedTeam is null)
        {
            await RespondWithErrorAsync(context, "The selected team was not found.");
            return;
        }

        var boards = await _boardRepository.GetAllByGuildIdAsync(context.Guild.Id);
        var tasks = await _taskItemRepository.GetAllTaskItemsByGuildIdAsync(context.Guild.Id);

        if (boards.Any(board => board.TeamId == selectedTeam.Id)
            || tasks.Any(task => task.AssigneeTeamId == selectedTeam.Id))
        {
            await RespondWithErrorAsync(context, "Remove this team from its boards and tasks before deleting it.");
            return;
        }

        await _teamRepository.RemoveAsync(selectedTeam);
        await context.RespondAsync(new DiscordEmbedBuilder()
            .WithDefaultColor()
            .WithDescription($"Deleted the **{selectedTeam.Name}** team."));
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
