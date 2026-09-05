using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using KanbanCord.Bot.Providers;

namespace KanbanCord.Bot.Commands.Task;

partial class TaskCommandGroup
{
    [Command("me")]
    [Description("Show cards involving you on one board.")]
    [RequirePermissions(userPermissions: [], botPermissions: [])]
    public async ValueTask TaskMeCommand(
        SlashCommandContext context,
        [Description("Board to inspect; defaults to Default")][SlashAutoCompleteProvider<BoardAutoCompleteProvider>] string? board = null) =>
        await TaskUserCommand(context, context.User, board);
}
