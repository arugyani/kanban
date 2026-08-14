using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Extensions;
using KanbanCord.Bot.Extensions;
using KanbanCord.Bot.Helpers;
using KanbanCord.Bot.Providers;
using KanbanCord.Core.Models;
using KanbanCord.Core.Repositories;

namespace KanbanCord.Bot.Commands;

public class ClearCommand
{
    private readonly ITaskItemRepository _repository;
    private readonly BoardResolver _boardResolver;

    public ClearCommand(ITaskItemRepository repository, BoardResolver boardResolver)
    {
        _repository = repository;
        _boardResolver = boardResolver;
    }
    
    
    [Command("clear")]
    [Description("Clear the kanban board completely, this will archive all current tasks.")]
    [RequirePermissions(userPermissions: [DiscordPermission.ManageMessages], botPermissions: [])]
    public async ValueTask ExecuteAsync(
        SlashCommandContext context,
        [Description("Board to clear; defaults to Default")] [SlashAutoCompleteProvider<BoardAutoCompleteProvider>] string? board = null)
    {
        var selectedBoard = await _boardResolver.ResolveAsync(context.Guild!.Id, context.User.Id, board);

        if (selectedBoard is null)
        {
            await context.RespondAsync("The selected board was not found.");
            return;
        }

        var clearButton = new DiscordButtonComponent(DiscordButtonStyle.Danger, Guid.NewGuid().ToString(), "Clear");
        
        var embed = new DiscordEmbedBuilder()
            .WithDefaultColor()
            .WithAuthor($"Clear {selectedBoard.Name}")
            .WithDescription("Are you sure you want to archive every active task on this board?");
        
        var responseMessage = new DiscordMessageBuilder()
            .AddEmbed(embed)
            .AddActionRowComponent(clearButton);
        
        await context.RespondAsync(responseMessage);
        
        var message = await context.Interaction.GetOriginalResponseAsync();

        var response = await message.WaitForButtonAsync();

        switch (response.TimedOut)
        {
            case false when response.Result.Id == clearButton.CustomId && response.Result.User.Id == context.User.Id:
            {
                var tasks = await _repository.GetAllTaskItemsByBoardIdAsync(context.Guild!.Id, selectedBoard.Id);

                foreach (var task in tasks.Where(task => task.Status != BoardStatus.Archived))
                {
                    task.Status = BoardStatus.Archived;

                    await _repository.UpdateTaskItemAsync(task);
                }
                
                var deletedEmbed = new DiscordEmbedBuilder()
                    .WithDefaultColor()
                    .WithDescription($"The **{selectedBoard.Name}** board has been cleared.");
            
                await response.Result.Interaction.CreateResponseAsync(
                    DiscordInteractionResponseType.UpdateMessage,
                    new DiscordInteractionResponseBuilder()
                        .AddEmbed(deletedEmbed));

                return;
            }
            case true:
            {
                clearButton.Disable();
            
                var timedOutMessage = new DiscordMessageBuilder()
                    .AddEmbed(embed)
                    .AddActionRowComponent(clearButton);
            
                await message.ModifyAsync(timedOutMessage);
                break;
            }
        }
    }
}
