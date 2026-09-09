using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using KanbanCord.Bot.Extensions;
using KanbanCord.Bot.Helpers;
using KanbanCord.Core.Models;
using KanbanCord.Core.Repositories;

namespace KanbanCord.Bot.Commands;

public sealed class MyListCommand
{
    private readonly BoardResolver _boardResolver;
    private readonly ITeamRepository _teamRepository;

    public MyListCommand(BoardResolver boardResolver, ITeamRepository teamRepository)
    {
        _boardResolver = boardResolver;
        _teamRepository = teamRepository;
    }

    [Command("my-list")]
    [Description("Show the cards that involve you.")]
    public async ValueTask ExecuteAsync(SlashCommandContext context)
    {
        await context.DeferResponseAsync(ephemeral: true);

        var guildId = context.Guild!.Id;
        var userId = context.User.Id;
        var boards = (await _boardResolver.GetAccessibleBoardsAsync(guildId, userId))
            .ToDictionary(board => board.Id);
        var groups = (await _teamRepository.GetAllByGuildIdAsync(guildId))
            .ToDictionary(group => group.Id);
        var cards = (await _boardResolver.GetAccessibleTaskItemsAsync(guildId, userId))
            .Where(card => card.Status != BoardStatus.Archived)
            .Where(card => IsInvolved(card, userId, groups))
            .OrderBy(card => card.DueAt ?? DateTime.MaxValue)
            .ThenByDescending(card => card.Priority)
            .Take(40)
            .ToList();

        var lines = cards.Select(card =>
        {
            var boardName = card.BoardId.HasValue && boards.TryGetValue(card.BoardId.Value, out var board)
                ? board.Name
                : "Board";
            var when = card.DueAt.HasValue ? $" · <t:{new DateTimeOffset(card.DueAt.Value).ToUnixTimeSeconds()}:d>" : string.Empty;
            return $"**{boardName} · {card.Status.ToFormattedString()}** — {card.Title} (`{card.Key}`){when}";
        });
        var description = cards.Count == 0
            ? "Nothing is on your list right now."
            : string.Join('\n', lines);
        if (description.Length > 4000)
            description = $"{description[..3997]}…";

        var embed = new DiscordEmbedBuilder()
            .WithDefaultColor()
            .WithTitle("My List")
            .WithDescription(description);
        await context.EditDeferredResponseAsync(embed);
    }

    private static bool IsInvolved(
        TaskItem card,
        ulong userId,
        IReadOnlyDictionary<MongoDB.Bson.ObjectId, Team> groups)
    {
        if (card.AssigneeId == userId || card.AssigneeIds.Contains(userId))
            return true;
        return card.AssigneeTeamId.HasValue
               && groups.TryGetValue(card.AssigneeTeamId.Value, out var group)
               && group.MemberIds.Contains(userId);
    }
}
