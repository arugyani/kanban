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
        
        var id = 1;
        
        foreach (var boardItem in boardItems.Where(x => x.Status == boardStatus))
        {
            if ((assigneeId.HasValue && boardItem.AssigneeId == assigneeId.Value) || !assigneeId.HasValue)
            {
                var user = await client.GetUserAsync(boardItem.AuthorId);
            
                taskStrings.Add($"{id} - \"{boardItem.Title}\" added by: {user.Username}");
            }
            
            id++;
        }
        
        return $"```bash\n{(taskStrings.Any() ? string.Join('\n', taskStrings) : " ")}```";
    }

    public static IReadOnlyList<DiscordAutoCompleteChoice> GetAutoCompleteStrings(
        this IReadOnlyList<TaskItem> tasks,
        BoardStatus? boardStatus = null,
        IReadOnlyDictionary<ObjectId, string>? boardNames = null)
    {
        List<DiscordAutoCompleteChoice> taskItems = [];
        
        var id = 1;

        if (boardStatus.HasValue)
        {
            foreach (var task in tasks.Where(x => x.Status == boardStatus))
            {
                taskItems.Add(new DiscordAutoCompleteChoice(
                    GetChoiceName(task, boardStatus.Value, id, boardNames),
                    task.Id.ToString()));
            
                id++;
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
                        GetChoiceName(task, newBoardStatus, id, boardNames),
                        task.Id.ToString()));
            
                    id++;
                }

                id = 1;
            }
        }
        
        return taskItems;
    }

    private static string GetChoiceName(
        TaskItem task,
        BoardStatus boardStatus,
        int id,
        IReadOnlyDictionary<ObjectId, string>? boardNames)
    {
        var boardName = task.BoardId.HasValue
                        && boardNames is not null
                        && boardNames.TryGetValue(task.BoardId.Value, out var name)
            ? $"{name} · "
            : string.Empty;

        var choiceName = $"[{boardName}{boardStatus.ToFormattedString()}] {id} - {task.Title}";

        return choiceName.Length <= 100 ? choiceName : choiceName[..100];
    }
}
