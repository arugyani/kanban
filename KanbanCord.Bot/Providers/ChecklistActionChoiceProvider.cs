using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Entities;

namespace KanbanCord.Bot.Providers;

public sealed class ChecklistActionChoiceProvider : IChoiceProvider
{
    public const int View = 0;
    public const int Add = 1;
    public const int Complete = 2;
    public const int Reopen = 3;

    private static readonly IReadOnlyList<DiscordApplicationCommandOptionChoice> Choices =
    [
        new("View", View),
        new("Add", Add),
        new("Complete", Complete),
        new("Reopen", Reopen),
    ];

    public ValueTask<IEnumerable<DiscordApplicationCommandOptionChoice>> ProvideAsync(CommandParameter parameter) =>
        ValueTask.FromResult(Choices.AsEnumerable());
}
