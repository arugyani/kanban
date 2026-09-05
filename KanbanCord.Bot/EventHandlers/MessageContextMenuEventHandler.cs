using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using KanbanCord.Bot.Helpers;
using KanbanCord.Core.Constants;
using KanbanCord.Core.Models;
using KanbanCord.Core.Repositories;

namespace KanbanCord.Bot.EventHandlers;

public sealed class MessageContextMenuEventHandler : IEventHandler<ContextMenuInteractionCreatedEventArgs>
{
    public const string CommandName = "Add to The Board";

    private readonly BoardResolver _boardResolver;
    private readonly ITaskItemRepository _taskRepository;

    public MessageContextMenuEventHandler(
        BoardResolver boardResolver,
        ITaskItemRepository taskRepository)
    {
        _boardResolver = boardResolver;
        _taskRepository = taskRepository;
    }

    public async Task HandleEventAsync(
        DiscordClient sender,
        ContextMenuInteractionCreatedEventArgs eventArgs)
    {
        if (eventArgs.Type != DiscordApplicationCommandType.MessageContextMenu
            || eventArgs.Interaction.Data.Name != CommandName
            || eventArgs.TargetMessage is null
            || eventArgs.Interaction.Guild is null)
            return;

        await eventArgs.Interaction.CreateResponseAsync(
            DiscordInteractionResponseType.DeferredChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().AsEphemeral());

        var message = eventArgs.TargetMessage;
        var guild = eventArgs.Interaction.Guild;
        if (await _taskRepository.GetByDiscordMessageIdOrDefaultAsync(guild.Id, message.Id) is not null)
        {
            await RespondAsync(eventArgs, "That message is already on The Board.");
            return;
        }

        var board = await _boardResolver.ResolveEditableAsync(guild.Id, eventArgs.User.Id, null);
        if (board is null)
        {
            await RespondAsync(eventArgs, "You only have view access to The Board.");
            return;
        }

        var cards = await _taskRepository.GetAllTaskItemsByBoardIdAsync(guild.Id, board.Id);
        var card = new TaskItem
        {
            GuildId = guild.Id,
            BoardId = board.Id,
            Title = TitleFor(message.Content, message.Author?.Username),
            Description = NotesFor(message.Content, message.Author?.Username, message.JumpLink.ToString()),
            AuthorId = eventArgs.User.Id,
            Status = BoardStatus.Backlog,
            Rank = cards
                .Where(candidate => candidate.Status == BoardStatus.Backlog)
                .Select(candidate => candidate.Rank)
                .DefaultIfEmpty(0)
                .Max() + 1024,
            Version = 1,
            DiscordMessageId = message.Id,
            DiscordMessageUrl = message.JumpLink.ToString(),
        };
        card.RecordChange(eventArgs.User.Id, "card_added", $"{card.Title} was added from Discord.");
        if (!await _taskRepository.TryAddTaskItemAsync(card))
        {
            await RespondAsync(eventArgs, "That message is already on The Board.");
            return;
        }

        await RespondAsync(
            eventArgs,
            $"Added **{card.Title}** to **{board.Name}**. Use `/card open` to update it.");
    }

    internal static string TitleFor(string? content, string? author)
    {
        var firstLine = (content ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        var title = string.IsNullOrWhiteSpace(firstLine)
            ? $"Message from {author ?? "Discord"}"
            : firstLine;
        return title.Length <= Limits.TaskTitleMaxLength
            ? title
            : $"{title[..(Limits.TaskTitleMaxLength - 1)].TrimEnd()}…";
    }

    internal static string NotesFor(string? content, string? author, string jumpLink)
    {
        var prefix = $"From {author ?? "Discord"}:\n";
        var suffix = $"\n\nOriginal message: {jumpLink}";
        var available = Math.Max(0, Limits.TaskDescriptionMaxLength - prefix.Length - suffix.Length);
        var body = (content ?? string.Empty).Trim();
        if (body.Length > available)
            body = $"{body[..Math.Max(0, available - 1)].TrimEnd()}…";
        return $"{prefix}{body}{suffix}";
    }

    private static async Task RespondAsync(
        ContextMenuInteractionCreatedEventArgs eventArgs,
        string content)
    {
        await eventArgs.Interaction.EditOriginalResponseAsync(
            new DiscordWebhookBuilder().WithContent(content));
    }
}
