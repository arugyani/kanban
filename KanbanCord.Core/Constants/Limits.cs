namespace KanbanCord.Core.Constants;

public static class Limits
{
    public const int BoardNameMaxLength = 50;
    public const int TeamNameMaxLength = 50;

    public const int TaskTitleMaxLength = 40;
    public const int TaskDescriptionMaxLength = 600;

    public const int TaskCommentMinLength = 10;
    public const int TaskCommentMaxLength = 600;

    public const int TaskTagMaxLength = 30;
    public const int TaskTagsMaxCount = 10;
    public const int TaskBlockedReasonMaxLength = 300;
}
