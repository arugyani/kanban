using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using KanbanCord.Bot.Extensions;
using KanbanCord.Bot.Helpers;
using KanbanCord.Core.Options;
using Microsoft.Extensions.Options;

namespace KanbanCord.Bot.Commands;

public class HelpCommand
{
    private const string BaseInviteUrl = "https://discord.com/oauth2/authorize?client_id=";
    private readonly string? supportInvite;

    public HelpCommand(IOptions<DiscordOptions> options)
    {
        supportInvite = options.Value.SupportInvite ?? null;
    }
    
    
    [Command("help")]
    [Description("Displays all the commands available.")]
    public async ValueTask ExecuteAsync(SlashCommandContext context)
    {
        await context.DeferResponseAsync();

        var commands = await context.Client.GetGlobalApplicationCommandsAsync();
        
        var commandDescriptions = new List<(string CommandMention, string Description)>
        {
            (commands.GetMention(["board"]), commands.GetDescription(["board"])),
            (commands.GetMention(["boards", "list"]), commands.GetDescription(["boards", "list"])),
            (commands.GetMention(["boards", "create"]), commands.GetDescription(["boards", "create"])),
            (commands.GetMention(["boards", "rename"]), commands.GetDescription(["boards", "rename"])),
            (commands.GetMention(["boards", "set-team"]), commands.GetDescription(["boards", "set-team"])),
            (commands.GetMention(["boards", "delete"]), commands.GetDescription(["boards", "delete"])),
            (commands.GetMention(["team", "list"]), commands.GetDescription(["team", "list"])),
            (commands.GetMention(["team", "create"]), commands.GetDescription(["team", "create"])),
            (commands.GetMention(["team", "add-person"]), commands.GetDescription(["team", "add-person"])),
            (commands.GetMention(["team", "remove-person"]), commands.GetDescription(["team", "remove-person"])),
            (commands.GetMention(["team", "delete"]), commands.GetDescription(["team", "delete"])),
            (commands.GetMention(["people"]), commands.GetDescription(["people"])),
            (commands.GetMention(["archive"]), commands.GetDescription(["archive"])),
            (commands.GetMention(["clear"]), commands.GetDescription(["clear"])),
            (commands.GetMention(["reset"]), commands.GetDescription(["reset"])),
            (commands.GetMention(["repository"]), commands.GetDescription(["repository"])),
            (commands.GetMention(["stats"]), commands.GetDescription(["stats"])),
            (commands.GetMention(["task", "add"]), commands.GetDescription(["task", "add"])),
            (commands.GetMention(["task", "edit"]), commands.GetDescription(["task", "edit"])),
            (commands.GetMention(["task", "delete"]), commands.GetDescription(["task", "delete"])),
            (commands.GetMention(["task", "view"]), commands.GetDescription(["task", "view"])),
            (commands.GetMention(["task", "start"]), commands.GetDescription(["task", "start"])),
            (commands.GetMention(["task", "complete"]), commands.GetDescription(["task", "complete"])),
            (commands.GetMention(["task", "archive"]), commands.GetDescription(["task", "archive"])),
            (commands.GetMention(["task", "move"]), commands.GetDescription(["task", "move"])),
            (commands.GetMention(["task", "transfer"]), commands.GetDescription(["task", "transfer"])),
            (commands.GetMention(["task", "assign"]), commands.GetDescription(["task", "assign"])),
            (commands.GetMention(["task", "me"]), commands.GetDescription(["task", "me"])),
            (commands.GetMention(["task", "user"]), commands.GetDescription(["task", "user"])),
            (commands.GetMention(["task", "priority"]), commands.GetDescription(["task", "priority"])),
            (commands.GetMention(["task", "comment"]), commands.GetDescription(["task", "comment"]))
        };

        var response = new DiscordWebhookBuilder();

        foreach (var commandDescriptionChunk in commandDescriptions.Chunk(20))
        {
            var embed = new DiscordEmbedBuilder()
                .WithDefaultColor()
                .WithAuthor("KanbanCord Commands");

            foreach (var commandDescription in commandDescriptionChunk)
                embed.AddField(commandDescription.CommandMention, commandDescription.Description);

            response.AddEmbed(embed.Build());
        }

        List<DiscordButtonComponent> additionalComponents = [new DiscordLinkButtonComponent(BaseInviteUrl + context.Client.CurrentUser.Id, "Invite")];
        
        if (supportInvite is not null)
            additionalComponents.Add(new DiscordLinkButtonComponent(supportInvite, "Support"));

        response.AddActionRowComponent(additionalComponents);

        await context.EditResponseAsync(response);
    }
}
