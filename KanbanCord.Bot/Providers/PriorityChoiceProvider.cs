using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Entities;
using KanbanCord.Core.Models;

namespace KanbanCord.Bot.Providers;

public class PriorityChoiceProvider : IChoiceProvider
{
    public const int None = -1;

    private static readonly IReadOnlyList<DiscordApplicationCommandOptionChoice> Priorities =
    [
        new("No flag", None),
        new(Priority.Low.ToString(), (int)Priority.Low),
        new(Priority.Medium.ToString(), (int)Priority.Medium),
        new(Priority.High.ToString(), (int)Priority.High),
        new(Priority.Urgent.ToString(), (int)Priority.Urgent)
    ];

    public ValueTask<IEnumerable<DiscordApplicationCommandOptionChoice>> ProvideAsync(CommandParameter parameter) =>
        ValueTask.FromResult(Priorities.AsEnumerable());
}
