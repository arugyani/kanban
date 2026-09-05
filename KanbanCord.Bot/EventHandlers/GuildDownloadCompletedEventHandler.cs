using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace KanbanCord.Bot.EventHandlers;

public class GuildDownloadCompletedEventHandler : IEventHandler<GuildDownloadCompletedEventArgs>
{
    private readonly ILogger<GuildDownloadCompletedEventHandler> _logger;

    public GuildDownloadCompletedEventHandler(ILogger<GuildDownloadCompletedEventHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleEventAsync(DiscordClient sender, GuildDownloadCompletedEventArgs eventArgs)
    {
        _logger.LogInformation("Guild Download Completed: Downloaded {count} guilds.", eventArgs.Guilds.Count);
        try
        {
            var commands = await sender.GetGlobalApplicationCommandsAsync();
            if (!commands.Any(command =>
                    command.Type == DiscordApplicationCommandType.MessageContextMenu
                    && command.Name == MessageContextMenuEventHandler.CommandName))
            {
                await sender.CreateGlobalApplicationCommandAsync(new DiscordApplicationCommand(
                    MessageContextMenuEventHandler.CommandName,
                    string.Empty,
                    type: DiscordApplicationCommandType.MessageContextMenu,
                    allowDMUsage: false));
                _logger.LogInformation("Registered the Add to The Board message action.");
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not register the Add to The Board message action.");
        }
    }
}
