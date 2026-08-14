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
using KanbanCord.Core.Repositories;

namespace KanbanCord.Bot.Commands;

public class ResetCommand
{
    private readonly ITaskItemRepository _repository;
    private readonly BoardResolver _boardResolver;

    public ResetCommand(ITaskItemRepository repository, BoardResolver boardResolver)
    {
        _repository = repository;
        _boardResolver = boardResolver;
    }
    
    
    [Command("reset")]
    [Description("Resets the kanban board completely, this will delete all current and archived tasks.")]
    [RequirePermissions(userPermissions: [DiscordPermission.ManageMessages], botPermissions: [])]
    public async ValueTask ExecuteAsync(
        SlashCommandContext context,
        [Description("Board to reset; defaults to Default")] [SlashAutoCompleteProvider<BoardAutoCompleteProvider>] string? board = null)
    {
        var selectedBoard = await _boardResolver.ResolveAsync(context.Guild!.Id, context.User.Id, board);

        if (selectedBoard is null)
        {
            await context.RespondAsync("The selected board was not found.");
            return;
        }

        var clearButton = new DiscordButtonComponent(DiscordButtonStyle.Danger, Guid.NewGuid().ToString(), "Reset");
        
        var embed = new DiscordEmbedBuilder()
            .WithDefaultColor()
            .WithAuthor($"Reset {selectedBoard.Name}")
            .WithDescription("Are you sure you want to delete every current and archived task on this board? This cannot be undone.");
        
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
                await _repository.RemoveAllTaskItemsByBoardIdAsync(context.Guild!.Id, selectedBoard.Id);
            
                var deletedEmbed = new DiscordEmbedBuilder()
                    .WithDefaultColor()
                    .WithDescription($"The **{selectedBoard.Name}** board has been reset.");
            
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
