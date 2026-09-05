namespace KanbanCord.Core.Models;

public sealed class BoardColumnDefinition
{
    public BoardStatus Status { get; set; }

    public required string Name { get; set; }

    public int Rank { get; set; }

    public required string Color { get; set; }
}

public static class BoardColumns
{
    public static IReadOnlyList<BoardColumnDefinition> For(Board board)
    {
        if (board.Columns.Count == 5
            && board.Columns.Select(column => column.Status).Distinct().Count() == 5)
            return board.Columns.OrderBy(column => column.Rank).ToList();

        return
        [
            new() { Status = BoardStatus.Backlog, Name = "Ideas", Rank = 1024, Color = "purple" },
            new() { Status = BoardStatus.UpNext, Name = "Up Next", Rank = 2048, Color = "pumpkin" },
            new() { Status = BoardStatus.InProgress, Name = "Doing", Rank = 3072, Color = "green" },
            new() { Status = BoardStatus.Waiting, Name = "Waiting", Rank = 4096, Color = "berry" },
            new() { Status = BoardStatus.Completed, Name = "Done", Rank = 5120, Color = "neutral" },
        ];
    }
}
