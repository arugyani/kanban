using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using KanbanCord.Bot.Extensions;
using KanbanCord.Bot.Helpers;
using KanbanCord.Bot.Providers;
using KanbanCord.Core.Models;
using KanbanCord.Core.Repositories;

namespace KanbanCord.Bot.Commands;

public class ArchiveCommand
{
    private readonly ITaskItemRepository _repository;
    private readonly BoardResolver _boardResolver;

    public ArchiveCommand(ITaskItemRepository repository, BoardResolver boardResolver)
    {
        _repository = repository;
        _boardResolver = boardResolver;
    }
    

    [Command("archive")]
    [Description("Displays all the archived tasks.")]
    public async ValueTask ExecuteAsync(
        SlashCommandContext context,
        [Description("Board archive to show; defaults to Default")] [SlashAutoCompleteProvider<BoardAutoCompleteProvider>] string? board = null)
    {
        var selectedBoard = await _boardResolver.ResolveAsync(context.Guild!.Id, context.User.Id, board);

        if (selectedBoard is null)
        {
            await context.RespondAsync("The selected board was not found.");
            return;
        }

        var boardItems = await _repository.GetAllTaskItemsByBoardIdAsync(context.Guild.Id, selectedBoard.Id);
        
        var embed = new DiscordEmbedBuilder()
            .WithDefaultColor()
            .WithAuthor("KanbanCord Archive")
            .WithTitle(selectedBoard.Name);
        
        var archiveString = await boardItems.GetBoardTaskString(context.Client, BoardStatus.Archived);
        
        embed.WithDescription(archiveString);

        await context.RespondAsync(embed);
    }
}
