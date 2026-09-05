using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using KanbanCord.Bot.Helpers;
using KanbanCord.Core.Models;
using KanbanCord.Core.Options;
using KanbanCord.Core.Repositories;
using Microsoft.Extensions.Options;
using MongoDB.Bson;

namespace KanbanCord.Bot.Commands.Task;

[Command("card")]
partial class TaskCommandGroup
{
    private readonly ITaskItemRepository _taskItemRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly IBoardRepository _boardRepository;
    private readonly BoardResolver _boardResolver;
    private readonly BoardAuthorizationService _authorization;
    private readonly ILogger<TaskCommandGroup> _logger;
    private readonly string? _publicBoardUrl;

    public TaskCommandGroup(
        ITaskItemRepository taskItemRepository,
        ITeamRepository teamRepository,
        IBoardRepository boardRepository,
        BoardResolver boardResolver,
        BoardAuthorizationService authorization,
        ILogger<TaskCommandGroup> logger,
        IOptions<WebApiOptions> webOptions)
    {
        _taskItemRepository = taskItemRepository;
        _teamRepository = teamRepository;
        _boardRepository = boardRepository;
        _boardResolver = boardResolver;
        _authorization = authorization;
        _logger = logger;
        _publicBoardUrl = Uri.TryCreate(webOptions.Value.PublicBoardUrl, UriKind.Absolute, out var boardUri)
                          && boardUri.Scheme == Uri.UriSchemeHttps
            ? boardUri.ToString().TrimEnd('/')
            : null;
    }

    private async System.Threading.Tasks.Task HandleModalFailureAsync(
        DiscordInteraction interaction,
        Exception exception,
        string action)
    {
        _logger.LogError(
            exception,
            "Discord card modal failed. Action: {Action}; UserId: {UserId}",
            action,
            interaction.User.Id);
        var message = exception.GetBaseException() is TaskItemVersionConflictException
            ? "That card changed while the window was open. Please try again."
            : "That change could not be saved. Please try again.";
        await interaction.EditOriginalResponseAsync(
            new DiscordWebhookBuilder().WithContent(message));
    }

    private async Task<TaskItem?> GetTaskAsync(
        SlashCommandContext context,
        string taskId,
        bool requireEdit = true)
    {
        var task = ObjectId.TryParse(taskId, out var objectId)
            ? await _taskItemRepository.GetTaskItemByObjectIdOrDefaultAsync(objectId, context.Guild!.Id)
            : null;
        if (task?.BoardId is null)
            return null;

        var board = requireEdit
            ? await _boardResolver.ResolveEditableAsync(context.Guild!.Id, context.User.Id, task.BoardId.Value.ToString())
            : await _boardResolver.ResolveAsync(context.Guild!.Id, context.User.Id, task.BoardId.Value.ToString());
        return board is null ? null : task;
    }
}
