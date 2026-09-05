using KanbanCord.Bot.Helpers;
using KanbanCord.Core.Models;

namespace KanbanCord.Tests.AuthorizationTests;

public class BoardAuthorizationServiceTests
{
    [Theory]
    [InlineData(10UL, "organizer")]
    [InlineData(20UL, "member")]
    [InlineData(30UL, "view_only")]
    [InlineData(40UL, "member")]
    [InlineData(50UL, null)]
    public void RoleForGroup_UsesExplicitRolesAndLegacyMembership(ulong userId, string? expected)
    {
        var group = new Team
        {
            GuildId = 1,
            Name = "Show Crew",
            NormalizedName = "SHOW CREW",
            CreatedById = 10,
            MemberIds = [10, 20, 30, 40],
            MemberRoles = new Dictionary<string, string>
            {
                ["20"] = "member",
                ["30"] = "view_only",
            },
        };

        Assert.Equal(expected, BoardAuthorizationService.RoleForGroup(group, userId));
    }
}
