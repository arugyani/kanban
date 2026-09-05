using MongoDB.Bson;

namespace KanbanCord.Core.Repositories;

public sealed class TaskItemVersionConflictException : InvalidOperationException
{
    public TaskItemVersionConflictException(ObjectId taskId)
        : base($"Card {taskId} changed before this update could be saved.")
    {
    }
}
