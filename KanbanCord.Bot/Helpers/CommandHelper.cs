using DSharpPlus.Entities;

namespace KanbanCord.Bot.Helpers;

public static class CommandHelper
{
    public static string GetMention(this IReadOnlyList<DiscordApplicationCommand> applicationCommands, string[] commands)
    {
        var fallback = $"/{string.Join(' ', commands)}";
        var command = applicationCommands.FirstOrDefault(x => x.Name == commands.First());

        if (command is null)
            return fallback;

        if (commands.Length == 1)
            return command.Mention;

        return command.Options.Any(x => x.Name == commands[1])
            ? command.GetSubcommandMention(commands.Skip(1).ToArray())
            : fallback;
    }
    
    public static string GetDescription(this IReadOnlyList<DiscordApplicationCommand> applicationCommands, string[] commands)
    {
        var command = applicationCommands.FirstOrDefault(x => x.Name == commands.First());

        if (command is null)
            return "Command details are temporarily unavailable.";

        if (commands.Length == 1)
            return command.Description;

        return command.Options.FirstOrDefault(x => x.Name == commands[1])?.Description
               ?? "Command details are temporarily unavailable.";
    }
}
