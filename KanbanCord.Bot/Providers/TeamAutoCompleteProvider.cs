using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using KanbanCord.Core.Repositories;

namespace KanbanCord.Bot.Providers;

public class TeamAutoCompleteProvider : IAutoCompleteProvider
{
    private readonly ITeamRepository _teamRepository;

    public TeamAutoCompleteProvider(ITeamRepository teamRepository)
    {
        _teamRepository = teamRepository;
    }

    public async ValueTask<IEnumerable<DiscordAutoCompleteChoice>> AutoCompleteAsync(AutoCompleteContext context)
    {
        var teams = await _teamRepository.GetAllByGuildIdAsync(context.Guild!.Id);

        return teams
            .Where(team => context.UserInput is null
                           || team.Name.Contains(context.UserInput, StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .Select(team => new DiscordAutoCompleteChoice(team.Name, team.Id.ToString()));
    }
}
