using DSharpPlus;
using KanbanCord.Core.Models;
using KanbanCord.Core.Options;
using KanbanCord.Core.Repositories;
using Microsoft.Extensions.Options;

namespace KanbanCord.Bot.Helpers;

public sealed class BoardAuthorizationService
{
    private readonly ITeamRepository _teamRepository;
    private readonly DiscordClient _discordClient;
    private readonly HashSet<ulong> _administratorIds;

    public BoardAuthorizationService(
        ITeamRepository teamRepository,
        DiscordClient discordClient,
        IOptions<WebApiOptions> options)
    {
        _teamRepository = teamRepository;
        _discordClient = discordClient;
        _administratorIds = options.Value.AdministratorDiscordUserIds.ToHashSet();
    }

    public async Task<bool> CanViewAsync(Board board, ulong userId)
    {
        return await RoleAsync(board, userId) is not null;
    }

    public async Task<bool> CanEditAsync(Board board, ulong userId)
    {
        return await RoleAsync(board, userId) is "admin" or "organizer" or "member";
    }

    public bool CanViewGroup(Team group, ulong userId)
    {
        return IsAdministrator(group.GuildId, userId) || RoleForGroup(group, userId) is not null;
    }

    private async Task<string?> RoleAsync(Board board, ulong userId)
    {
        if (IsAdministrator(board.GuildId, userId))
            return "admin";
        if (!board.TeamId.HasValue)
            return "member";

        var group = await _teamRepository.GetByObjectIdOrDefaultAsync(board.TeamId.Value, board.GuildId);
        return group is null ? null : RoleForGroup(group, userId);
    }

    public static string? RoleForGroup(Team group, ulong userId)
    {
        if (group.CreatedById == userId)
            return "organizer";
        if (group.MemberRoles.TryGetValue(userId.ToString(), out var role)
            && role is "organizer" or "member" or "view_only")
            return role;
        return group.MemberIds.Contains(userId) ? "member" : null;
    }

    private bool IsAdministrator(ulong guildId, ulong userId)
    {
        return _administratorIds.Contains(userId)
               || (_discordClient.Guilds.TryGetValue(guildId, out var guild) && guild.OwnerId == userId);
    }
}
