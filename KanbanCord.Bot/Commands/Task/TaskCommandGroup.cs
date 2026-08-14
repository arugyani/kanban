using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using KanbanCord.Bot.Helpers;
using KanbanCord.Core.Models;
using KanbanCord.Core.Repositories;
using MongoDB.Bson;

namespace KanbanCord.Bot.Commands.Task;

[Command("task")]
[RequirePermissions(userPermissions: [DiscordPermission.ManageMessages], botPermissions: [])]
partial class TaskCommandGroup
{
    private readonly ITaskItemRepository _taskItemRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly IBoardRepository _boardRepository;
    private readonly BoardResolver _boardResolver;

    public TaskCommandGroup(
        ITaskItemRepository taskItemRepository,
        ITeamRepository teamRepository,
        IBoardRepository boardRepository,
        BoardResolver boardResolver)
    {
        _taskItemRepository = taskItemRepository;
        _teamRepository = teamRepository;
        _boardRepository = boardRepository;
        _boardResolver = boardResolver;
    }

    private async Task<TaskItem?> GetTaskAsync(SlashCommandContext context, string taskId)
    {
        return ObjectId.TryParse(taskId, out var objectId)
            ? await _taskItemRepository.GetTaskItemByObjectIdOrDefaultAsync(objectId, context.Guild!.Id)
            : null;
    }
}
