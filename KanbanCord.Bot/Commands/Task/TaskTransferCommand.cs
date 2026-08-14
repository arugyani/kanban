using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using KanbanCord.Bot.Extensions;
using KanbanCord.Bot.Providers;

namespace KanbanCord.Bot.Commands.Task;

partial class TaskCommandGroup
{
    [Command("transfer")]
    [Description("Transfer a task to another board.")]
    public async ValueTask TaskTransferCommand(
        SlashCommandContext context,
        [Description("Task to transfer")] [SlashAutoCompleteProvider<AllTaskItemsAutoCompleteProvider>] string task,
        [Description("Destination board")] [SlashAutoCompleteProvider<BoardAutoCompleteProvider>] string board)
    {
        var taskItem = await GetTaskAsync(context, task);
        var destinationBoard = await _boardResolver.ResolveAsync(context.Guild!.Id, context.User.Id, board);

        if (taskItem is null || destinationBoard is null)
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithDefaultColor()
                .WithDescription("The selected task or board was not found."));
            return;
        }

        if (taskItem.BoardId == destinationBoard.Id)
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithDefaultColor()
                .WithDescription($"The task is already on **{destinationBoard.Name}**."));
            return;
        }

        var sourceBoard = taskItem.BoardId.HasValue
            ? await _boardRepository.GetByObjectIdOrDefaultAsync(taskItem.BoardId.Value, context.Guild.Id)
            : null;

        taskItem.BoardId = destinationBoard.Id;
        taskItem.LastUpdatedAt = DateTime.UtcNow;
        await _taskItemRepository.UpdateTaskItemAsync(taskItem);

        await context.RespondAsync(new DiscordEmbedBuilder()
            .WithDefaultColor()
            .WithDescription(
                $"Transferred \"{taskItem.Title}\" from **{sourceBoard?.Name ?? "Default"}** to **{destinationBoard.Name}**."));
    }
}
