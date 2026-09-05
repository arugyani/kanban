using System.ComponentModel;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using KanbanCord.Bot.Extensions;
using KanbanCord.Bot.Providers;
using KanbanCord.Core.Models;

namespace KanbanCord.Bot.Commands.Task;

partial class TaskCommandGroup
{
    [Command("open")]
    [Description("Open a card and its notes, people, and comments.")]
    public async ValueTask TaskViewCommand(
        SlashCommandContext context,
        [Description("Card to open")][SlashAutoCompleteProvider<AllTaskItemsAutoCompleteProvider>] string task)
    {
        await context.DeferResponseAsync(ephemeral: true);

        var card = await GetTaskAsync(context, task, requireEdit: false);
        if (card is null)
        {
            await context.EditDeferredResponseAsync("That card could not be found.");
            return;
        }

        var author = await context.Client.GetUserAsync(card.AuthorId);
        var assigneeIds = card.AssigneeIds.ToHashSet();
        if (card.AssigneeId.HasValue)
            assigneeIds.Add(card.AssigneeId.Value);
        var assigneeGroup = card.AssigneeTeamId.HasValue
            ? await _teamRepository.GetByObjectIdOrDefaultAsync(card.AssigneeTeamId.Value, context.Guild!.Id)
            : null;
        var importance = card.Priority switch
        {
            Priority.Low => ":yellow_circle: Low",
            Priority.High => ":red_circle: High",
            Priority.Urgent => ":purple_circle: Urgent",
            _ => ":orange_circle: Medium",
        };
        var people = assigneeIds.Count == 0
            ? assigneeGroup?.Name ?? "Nobody yet"
            : string.Join(", ", assigneeIds.Select(id => $"<@{id}>"));
        var shortId = card.Id.ToString()[^6..].ToUpperInvariant();

        var embed = new DiscordEmbedBuilder()
            .WithDefaultColor()
            .WithTitle(card.Title)
            .WithDescription(string.IsNullOrWhiteSpace(card.Description) ? "No notes yet." : card.Description)
            .AddField("Card", shortId, true)
            .AddField("Column", card.Status.ToFormattedString(), true)
            .AddField("Importance", importance, true)
            .AddField("People on this", people)
            .AddField("Added by", author.Mention, true)
            .AddField("Last changed", Formatter.Timestamp(card.LastUpdatedAt, TimestampFormat.RelativeTime), true);

        if (card.DueAt.HasValue)
            embed.AddField("When", Formatter.Timestamp(card.DueAt.Value, TimestampFormat.ShortDate), true);
        if (!string.IsNullOrWhiteSpace(card.BlockedReason))
            embed.AddField("Waiting on", card.BlockedReason);
        if (card.Checklist.Count != 0)
            embed.AddField(
                "Checklist",
                $"{card.Checklist.Count(item => item.Complete)} of {card.Checklist.Count} complete");
        if (card.Links.Count != 0)
            embed.AddField(
                "GitHub issues",
                string.Join('\n', card.Links.Take(5).Select(link => $"[{link.Owner}/{link.Repository}#{link.IssueNumber}]({link.Url})")));
        if (Uri.TryCreate(card.DiscordMessageUrl, UriKind.Absolute, out var discordMessageUrl)
            && discordMessageUrl.Scheme == Uri.UriSchemeHttps)
            embed.AddField("Discord message", $"[Open the original message]({discordMessageUrl})");

        foreach (var note in card.Comments.OrderByDescending(comment => comment.CreatedAt).Take(5))
        {
            var commenter = await context.Client.GetUserAsync(note.AuthorId);
            embed.AddField(
                $"{commenter.Username} · {Formatter.Timestamp(note.CreatedAt, TimestampFormat.RelativeTime)}",
                note.Text);
        }

        var response = new DiscordWebhookBuilder().AddEmbed(embed);
        var canEdit = card.BoardId.HasValue
                      && await _boardResolver.ResolveEditableAsync(
                          context.Guild!.Id,
                          context.User.Id,
                          card.BoardId.Value.ToString()) is not null;
        if (canEdit)
        {
            response.AddActionRowComponent(
            [
                new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"{card.Id}.join", "Join this"),
                new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"{card.Id}.move-next", "Move"),
                new DiscordButtonComponent(DiscordButtonStyle.Success, $"{card.Id}.done", "Mark done"),
                new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"{card.Id}.waiting", "Waiting"),
                new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"{card.Id}.add-note", "Add note"),
            ]);
        }
        if (_publicBoardUrl is not null)
            response.AddActionRowComponent(new DiscordLinkButtonComponent(_publicBoardUrl, "Open The Board"));

        await context.EditResponseAsync(response);
    }
}
