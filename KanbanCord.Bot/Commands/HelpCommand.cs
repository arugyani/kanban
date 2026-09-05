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
        await context.DeferResponseAsync(ephemeral: true);

        var commands = await context.Client.GetGlobalApplicationCommandsAsync();

        var commandDescriptions = new List<(string CommandMention, string Description)>
        {
            (commands.GetMention(["my-list"]), commands.GetDescription(["my-list"])),
            (commands.GetMention(["board", "recap"]), commands.GetDescription(["board", "recap"])),
            (commands.GetMention(["boards", "list"]), commands.GetDescription(["boards", "list"])),
            (commands.GetMention(["boards", "create"]), commands.GetDescription(["boards", "create"])),
            (commands.GetMention(["boards", "rename"]), commands.GetDescription(["boards", "rename"])),
            (commands.GetMention(["boards", "set-group"]), commands.GetDescription(["boards", "set-group"])),
            (commands.GetMention(["boards", "delete"]), commands.GetDescription(["boards", "delete"])),
            (commands.GetMention(["group", "list"]), commands.GetDescription(["group", "list"])),
            (commands.GetMention(["group", "create"]), commands.GetDescription(["group", "create"])),
            (commands.GetMention(["group", "add-person"]), commands.GetDescription(["group", "add-person"])),
            (commands.GetMention(["group", "remove-person"]), commands.GetDescription(["group", "remove-person"])),
            (commands.GetMention(["group", "delete"]), commands.GetDescription(["group", "delete"])),
            (commands.GetMention(["people"]), commands.GetDescription(["people"])),
            (commands.GetMention(["archive"]), commands.GetDescription(["archive"])),
            (commands.GetMention(["clear"]), commands.GetDescription(["clear"])),
            (commands.GetMention(["reset"]), commands.GetDescription(["reset"])),
            (commands.GetMention(["repository"]), commands.GetDescription(["repository"])),
            (commands.GetMention(["stats"]), commands.GetDescription(["stats"])),
            (commands.GetMention(["card", "add"]), commands.GetDescription(["card", "add"])),
            (commands.GetMention(["card", "update"]), commands.GetDescription(["card", "update"])),
            (commands.GetMention(["card", "delete"]), commands.GetDescription(["card", "delete"])),
            (commands.GetMention(["card", "open"]), commands.GetDescription(["card", "open"])),
            (commands.GetMention(["card", "start"]), commands.GetDescription(["card", "start"])),
            (commands.GetMention(["card", "complete"]), commands.GetDescription(["card", "complete"])),
            (commands.GetMention(["card", "archive"]), commands.GetDescription(["card", "archive"])),
            (commands.GetMention(["card", "move"]), commands.GetDescription(["card", "move"])),
            (commands.GetMention(["card", "transfer"]), commands.GetDescription(["card", "transfer"])),
            (commands.GetMention(["card", "people"]), commands.GetDescription(["card", "people"])),
            (commands.GetMention(["card", "mine"]), commands.GetDescription(["card", "mine"])),
            (commands.GetMention(["card", "user"]), commands.GetDescription(["card", "user"])),
            (commands.GetMention(["card", "importance"]), commands.GetDescription(["card", "importance"])),
            (commands.GetMention(["card", "note"]), commands.GetDescription(["card", "note"])),
            (commands.GetMention(["card", "checklist"]), commands.GetDescription(["card", "checklist"])),
            (commands.GetMention(["card", "github"]), commands.GetDescription(["card", "github"]))
        };

        var response = new DiscordWebhookBuilder();

        foreach (var commandDescriptionChunk in commandDescriptions.Chunk(20))
        {
            var embed = new DiscordEmbedBuilder()
                .WithDefaultColor()
                .WithAuthor("RGBOO Commands");

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
