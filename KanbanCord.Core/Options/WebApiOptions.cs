using System.ComponentModel.DataAnnotations;

namespace KanbanCord.Core.Options;

public class WebApiOptions
{
    public const string Web = "Web";

    [Required(AllowEmptyStrings = false)]
    [MinLength(32)]
    public string ApiKey { get; set; } = string.Empty;

    public List<ulong> AdministratorDiscordUserIds { get; set; } = [];

    public string? PublicBoardUrl { get; set; }
}
