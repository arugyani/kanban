using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;

namespace KanbanCord.Bot.Extensions;

public static class SlashCommandContextExtensions
{
    public static async ValueTask EditDeferredResponseAsync(
        this SlashCommandContext context,
        string content)
    {
        await context.EditResponseAsync(new DiscordWebhookBuilder().WithContent(content));
    }

    public static async ValueTask EditDeferredResponseAsync(
        this SlashCommandContext context,
        DiscordEmbedBuilder embed)
    {
        await context.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(embed));
    }

    public static async ValueTask EditDeferredResponseAsync(
        this SlashCommandContext context,
        DiscordEmbed embed)
    {
        await context.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(embed));
    }
}
