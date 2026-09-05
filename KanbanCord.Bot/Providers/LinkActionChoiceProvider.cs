using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Entities;

namespace KanbanCord.Bot.Providers;

public sealed class LinkActionChoiceProvider : IChoiceProvider
{
    public const int View = 0;
    public const int Add = 1;
    public const int Remove = 2;

    private static readonly IReadOnlyList<DiscordApplicationCommandOptionChoice> Choices =
    [
        new("View", View),
        new("Add", Add),
        new("Remove", Remove),
    ];

    public ValueTask<IEnumerable<DiscordApplicationCommandOptionChoice>> ProvideAsync(CommandParameter parameter) =>
        ValueTask.FromResult(Choices.AsEnumerable());
}
