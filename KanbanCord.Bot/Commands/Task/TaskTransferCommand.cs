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
    [Description("Move a card to another board.")]
    public async ValueTask TaskTransferCommand(
        SlashCommandContext context,
        [Description("Card to move")][SlashAutoCompleteProvider<AllTaskItemsAutoCompleteProvider>] string task,
        [Description("Destination board")][SlashAutoCompleteProvider<BoardAutoCompleteProvider>] string board)
    {
        var taskItem = await GetTaskAsync(context, task);
        var destinationBoard = await _boardResolver.ResolveEditableAsync(context.Guild!.Id, context.User.Id, board);

        if (taskItem is null || destinationBoard is null)
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithDefaultColor()
                .WithDescription("That card or board could not be found."), ephemeral: true);
            return;
        }

        if (taskItem.BoardId == destinationBoard.Id)
        {
            await context.RespondAsync(new DiscordEmbedBuilder()
                .WithDefaultColor()
                .WithDescription($"That card is already on **{destinationBoard.Name}**."), ephemeral: true);
            return;
        }

        var sourceBoard = taskItem.BoardId.HasValue
            ? await _boardRepository.GetByObjectIdOrDefaultAsync(taskItem.BoardId.Value, context.Guild.Id)
            : null;

        taskItem.BoardId = destinationBoard.Id;
        taskItem.LastUpdatedAt = DateTime.UtcNow;
        taskItem.RecordChange(
            context.User.Id,
            "card_transferred",
            $"{taskItem.Title} moved to the {destinationBoard.Name} board.");
        await _taskItemRepository.UpdateTaskItemAsync(taskItem);

        await context.RespondAsync(new DiscordEmbedBuilder()
            .WithDefaultColor()
            .WithDescription(
                $"Moved **{taskItem.Title}** from **{sourceBoard?.Name ?? "Default"}** to **{destinationBoard.Name}**."), ephemeral: true);
    }
}
