using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using KanbanCord.Bot.Helpers;
using KanbanCord.Core.Constants;
using KanbanCord.Core.Models;
using KanbanCord.Core.Repositories;
using MongoDB.Bson;

namespace KanbanCord.Bot.EventHandlers;

public sealed class ComponentInteractionCreatedEventHandler : IEventHandler<ComponentInteractionCreatedEventArgs>
{
    private readonly ITaskItemRepository _repository;
    private readonly BoardResolver _boardResolver;
    private readonly ILogger<ComponentInteractionCreatedEventHandler> _logger;

    public ComponentInteractionCreatedEventHandler(
        ITaskItemRepository repository,
        BoardResolver boardResolver,
        ILogger<ComponentInteractionCreatedEventHandler> logger)
    {
        _repository = repository;
        _boardResolver = boardResolver;
        _logger = logger;
    }

    public async Task HandleEventAsync(
        DiscordClient sender,
        ComponentInteractionCreatedEventArgs eventArgs)
    {
        if (eventArgs.Guild is null)
            return;

        var separator = eventArgs.Id.IndexOf('.');
        if (separator <= 0 || !ObjectId.TryParse(eventArgs.Id[..separator], out var cardId))
            return;
        var action = eventArgs.Id[(separator + 1)..];
        if (action is not ("join" or "move-next" or "done" or "waiting" or "add-note" or "add-comment"))
            return;

        if (action is "add-note" or "add-comment")
        {
            await OpenNoteModalAsync(sender, eventArgs, cardId);
            return;
        }

        await DeferPrivateAsync(eventArgs.Interaction);
        try
        {
            var card = await _repository.GetTaskItemByObjectIdOrDefaultAsync(cardId, eventArgs.Guild.Id);
            var board = card?.BoardId is null
                ? null
                : await _boardResolver.ResolveEditableAsync(
                    eventArgs.Guild.Id,
                    eventArgs.User.Id,
                    card.BoardId.Value.ToString());
            if (card is null || board is null)
            {
                await EditPrivateAsync(eventArgs.Interaction, "That card could not be found, or you only have view access.");
                return;
            }

            var message = action switch
            {
                "join" => Join(card, eventArgs.User.Id),
                "move-next" => MoveNext(card),
                "done" => MarkDone(card),
                "waiting" => MarkWaiting(card),
                _ => throw new InvalidOperationException("Unsupported card action."),
            };
            var expectedVersion = card.Version;
            card.LastUpdatedAt = DateTime.UtcNow;
            card.RecordChange(
                eventArgs.User.Id,
                action == "join" ? "people_changed" : "card_moved",
                message.Replace("**", string.Empty, StringComparison.Ordinal));
            if (!await _repository.TryUpdateTaskItemAsync(card, expectedVersion))
            {
                await EditPrivateAsync(eventArgs.Interaction, "This card changed a moment ago. Open it again and retry.");
                return;
            }

            await EditPrivateAsync(eventArgs.Interaction, message);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Card component interaction failed. GuildId: {GuildId}; UserId: {UserId}; Action: {Action}",
                eventArgs.Guild.Id,
                eventArgs.User.Id,
                action);
            await EditPrivateAsync(eventArgs.Interaction, "That change could not be saved. Please try again.");
        }
    }

    private async Task OpenNoteModalAsync(
        DiscordClient sender,
        ComponentInteractionCreatedEventArgs eventArgs,
        ObjectId cardId)
    {
        var modal = new DiscordModalBuilder()
            .WithCustomId(Guid.NewGuid().ToString())
            .WithTitle("Add a note")
            .AddTextInput(new DiscordTextInputComponent(
                "noteField",
                "Share an update with the group",
                required: true,
                max_length: Limits.TaskCommentMaxLength,
                min_length: Limits.TaskCommentMinLength,
                style: DiscordTextInputStyle.Paragraph), "Note", "This will be visible to everyone on the card.");
        await eventArgs.Interaction.CreateResponseAsync(DiscordInteractionResponseType.Modal, modal);

        var interactivity = sender.ServiceProvider.GetRequiredService<InteractivityExtension>();
        var response = await interactivity.WaitForModalAsync(modal.CustomId, TimeSpan.FromMinutes(5));
        if (response.TimedOut)
            return;

        var modalInteraction = response.Result.Interaction;
        await DeferPrivateAsync(modalInteraction);
        try
        {
            var card = await _repository.GetTaskItemByObjectIdOrDefaultAsync(cardId, eventArgs.Guild!.Id);
            var board = card?.BoardId is null
                ? null
                : await _boardResolver.ResolveEditableAsync(
                    eventArgs.Guild.Id,
                    eventArgs.User.Id,
                    card.BoardId.Value.ToString());
            if (card is null || board is null)
            {
                await EditPrivateAsync(modalInteraction, "That card could not be found, or you only have view access.");
                return;
            }

            var expectedVersion = card.Version;
            card.Comments.Add(new Comment
            {
                AuthorId = eventArgs.User.Id,
                Text = ((TextInputModalSubmission)response.Result.Values["noteField"]).Value,
            });
            card.LastUpdatedAt = DateTime.UtcNow;
            card.RecordChange(eventArgs.User.Id, "comment_added", $"A note was added to {card.Title}.");
            var saved = await _repository.TryUpdateTaskItemAsync(card, expectedVersion);
            var confirmation = saved
                ? $"Your note was added to **{card.Title}**."
                : "This card changed while the note window was open. Please add the note again.";
            await EditPrivateAsync(modalInteraction, confirmation);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Card note modal failed. GuildId: {GuildId}; UserId: {UserId}",
                eventArgs.Guild.Id,
                eventArgs.User.Id);
            await EditPrivateAsync(modalInteraction, "That note could not be saved. Please try again.");
        }
    }

    internal static string Join(TaskItem card, ulong userId)
    {
        if (card.AssigneeId == userId || card.AssigneeIds.Contains(userId))
            return $"You’re already on **{card.Title}**.";
        card.AssigneeIds.Add(userId);
        card.AssigneeId ??= userId;
        card.AssigneeTeamId = null;
        return $"You joined **{card.Title}**.";
    }

    internal static string MoveNext(TaskItem card)
    {
        card.Status = card.Status switch
        {
            BoardStatus.Backlog => BoardStatus.UpNext,
            BoardStatus.UpNext => BoardStatus.InProgress,
            BoardStatus.InProgress => BoardStatus.Completed,
            BoardStatus.Waiting => BoardStatus.InProgress,
            _ => card.Status,
        };
        card.BlockedReason = null;
        return $"**{card.Title}** moved to **{card.Status.ToFormattedString()}**.";
    }

    internal static string MarkDone(TaskItem card)
    {
        card.Status = BoardStatus.Completed;
        card.BlockedReason = null;
        return $"**{card.Title}** is done.";
    }

    internal static string MarkWaiting(TaskItem card)
    {
        card.Status = BoardStatus.Waiting;
        card.BlockedReason ??= "Waiting on an update";
        return $"**{card.Title}** moved to **Waiting**.";
    }

    private static Task DeferPrivateAsync(DiscordInteraction interaction)
    {
        return interaction.CreateResponseAsync(
            DiscordInteractionResponseType.DeferredChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().AsEphemeral());
    }

    private static Task<DiscordMessage> EditPrivateAsync(
        DiscordInteraction interaction,
        string message)
    {
        return interaction.EditOriginalResponseAsync(
            new DiscordWebhookBuilder().WithContent(message));
    }
}
