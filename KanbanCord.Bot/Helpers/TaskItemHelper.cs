using DSharpPlus;
using DSharpPlus.Entities;
using KanbanCord.Core.Models;
using MongoDB.Bson;

namespace KanbanCord.Bot.Helpers;

public static class TaskItemHelper
{
    public static async Task<string> GetBoardTaskString(this IReadOnlyList<TaskItem> boardItems, DiscordClient client, BoardStatus boardStatus, ulong? assigneeId = null)
    {
        List<string> taskStrings = [];

        foreach (var boardItem in boardItems.Where(x => x.Status == boardStatus))
        {
            if ((assigneeId.HasValue
                 && (boardItem.AssigneeId == assigneeId.Value || boardItem.AssigneeIds.Contains(assigneeId.Value)))
                || !assigneeId.HasValue)
            {
                var user = await client.GetUserAsync(boardItem.AuthorId);

                taskStrings.Add($"{boardItem.Key} - \"{boardItem.Title}\" added by: {user.Username}");
            }
        }

        return $"```bash\n{(taskStrings.Any() ? string.Join('\n', taskStrings) : " ")}```";
    }

    public static IReadOnlyList<DiscordAutoCompleteChoice> GetAutoCompleteStrings(
        this IReadOnlyList<TaskItem> tasks,
        BoardStatus? boardStatus = null,
        IReadOnlyDictionary<ObjectId, string>? boardNames = null)
    {
        List<DiscordAutoCompleteChoice> taskItems = [];

        if (boardStatus.HasValue)
        {
            foreach (var task in tasks.Where(x => x.Status == boardStatus))
            {
                taskItems.Add(new DiscordAutoCompleteChoice(
                    GetChoiceName(task, boardStatus.Value, boardNames),
                    task.Id.ToString()));
            }
        }
        else
        {
            var boardStatusList = Enum.GetValues(typeof(BoardStatus)).Cast<BoardStatus>().ToList();
            foreach (var newBoardStatus in boardStatusList)
            {
                foreach (var task in tasks.Where(x => x.Status == newBoardStatus))
                {
                    taskItems.Add(new DiscordAutoCompleteChoice(
                        GetChoiceName(task, newBoardStatus, boardNames),
                        task.Id.ToString()));
                }
            }
        }

        return taskItems;
    }

    private static string GetChoiceName(
        TaskItem task,
        BoardStatus boardStatus,
        IReadOnlyDictionary<ObjectId, string>? boardNames)
    {
        var boardName = task.BoardId.HasValue
                        && boardNames is not null
                        && boardNames.TryGetValue(task.BoardId.Value, out var name)
            ? $"{name} · "
            : string.Empty;

        var choiceName = $"{task.Key} · {task.Title} [{boardName}{boardStatus.ToFormattedString()}]";

        return choiceName.Length <= 100 ? choiceName : choiceName[..100];
    }
}
