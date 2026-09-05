using System.ComponentModel;
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
    [Command("github")]
    [Description("View or change a card's GitHub issue links.")]
    public async ValueTask TaskLinkCommand(
        SlashCommandContext context,
        [Description("What to do")][SlashChoiceProvider<LinkActionChoiceProvider>] int action,
        [Description("Card to update")][SlashAutoCompleteProvider<AllTaskItemsAutoCompleteProvider>] string task,
        [Description("GitHub issue URL for Add")] string? url = null,
        [Description("Link number for Remove")] int? link = null)
    {
        await context.DeferResponseAsync(ephemeral: true);

        if (action is < LinkActionChoiceProvider.View or > LinkActionChoiceProvider.Remove)
        {
            await context.EditDeferredResponseAsync("Choose a valid GitHub action.");
            return;
        }
        var card = await GetTaskAsync(context, task, requireEdit: action != LinkActionChoiceProvider.View);
        if (card is null)
        {
            await context.EditDeferredResponseAsync("That card could not be found.");
            return;
        }

        if (action == LinkActionChoiceProvider.View)
        {
            var description = card.Links.Count == 0
                ? "No GitHub issues are linked yet."
                : string.Join('\n', card.Links.Select((entry, index) =>
                    $"**{index + 1}.** [{entry.Owner}/{entry.Repository}#{entry.IssueNumber}]({entry.Url})"));
            await context.EditDeferredResponseAsync(
                new DiscordEmbedBuilder()
                    .WithDefaultColor()
                    .WithTitle($"{card.Title} · GitHub issues")
                    .WithDescription(description));
            return;
        }

        if (action == LinkActionChoiceProvider.Add)
        {
            if (!TryParseGitHubIssue(url, out var issue))
            {
                await context.EditDeferredResponseAsync(
                    "Use a link like https://github.com/owner/repo/issues/123.");
                return;
            }
            if (card.Links.Any(existing => string.Equals(existing.Url, issue!.Url, StringComparison.OrdinalIgnoreCase)))
            {
                await context.EditDeferredResponseAsync("That issue is already linked.");
                return;
            }
            card.Links.Add(issue!);
            card.LastUpdatedAt = DateTime.UtcNow;
            card.RecordChange(context.User.Id, "github_link_added", $"A GitHub issue was linked to {card.Title}.");
            await _taskItemRepository.UpdateTaskItemAsync(card);
            await context.EditDeferredResponseAsync("GitHub issue linked.");
            return;
        }

        if (!link.HasValue || link < 1 || link > card.Links.Count)
        {
            await context.EditDeferredResponseAsync("Choose a link number from this card.");
            return;
        }
        card.Links.RemoveAt(link.Value - 1);
        card.LastUpdatedAt = DateTime.UtcNow;
        card.RecordChange(context.User.Id, "github_link_removed", $"A GitHub issue was unlinked from {card.Title}.");
        await _taskItemRepository.UpdateTaskItemAsync(card);
        await context.EditDeferredResponseAsync("GitHub issue link removed.");
    }

    internal static bool TryParseGitHubIssue(string? value, out GitHubIssueLink? issue)
    {
        issue = null;
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            return false;
        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4
            || !string.Equals(parts[2], "issues", StringComparison.Ordinal)
            || !int.TryParse(parts[3], out var issueNumber)
            || issueNumber < 1)
            return false;
        issue = new GitHubIssueLink
        {
            Url = $"https://github.com/{parts[0]}/{parts[1]}/issues/{issueNumber}",
            Owner = parts[0],
            Repository = parts[1],
            IssueNumber = issueNumber,
        };
        return true;
    }
}
