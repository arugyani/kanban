using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using DSharpPlus.Extensions;
using DSharpPlus.Interactivity.Extensions;
using KanbanCord.Bot.BackgroundServices;
using KanbanCord.Bot.EventHandlers;
using KanbanCord.Bot.Helpers;
using KanbanCord.Core.Options;
using KanbanCord.Core.Repositories;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace KanbanCord.Bot.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHostDependencies(this IServiceCollection services)
    {
        services
            .AddServices()
            .AddOptions()
            ;
        services.AddHttpClient("uptime-monitor", client =>
            client.Timeout = TimeSpan.FromSeconds(10));

        // MongoClient owns the driver's connection pools and is designed to be
        // reused for the lifetime of the process. Creating one per request
        // would churn sockets and make brief traffic spikes less reliable.
        services.AddSingleton<IMongoClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            return new MongoClient(options.ConnectionString);
        });
        services.AddSingleton<IMongoDatabase>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            return sp.GetRequiredService<IMongoClient>().GetDatabase(options.Name);
        });

        return services;
    }

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        services
            .AddHostedService<DatabaseSetupBackgroundService>()
            .AddHostedService<BotBackgroundService>()
            .AddHostedService<UptimeMonitorBackgroundService>()
            ;

        services
            .AddScoped<ITaskItemRepository, TaskItemRepository>()
            .AddScoped<ISettingsRepository, SettingsRepository>()
            .AddScoped<IBoardRepository, BoardRepository>()
            .AddScoped<ITeamRepository, TeamRepository>()
            .AddScoped<BoardAuthorizationService>()
            .AddScoped<BoardResolver>()
            ;

        return services;
    }

    private static IServiceCollection AddOptions(this IServiceCollection services)
    {
        services.AddOptionsWithValidateOnStart<DatabaseOptions>()
            .BindConfiguration(DatabaseOptions.Database)
            .ValidateDataAnnotations();

        services.AddOptionsWithValidateOnStart<DiscordOptions>()
            .BindConfiguration(DiscordOptions.Discord)
            .ValidateDataAnnotations();

        services.AddOptionsWithValidateOnStart<UptimeMonitorOptions>()
            .BindConfiguration(UptimeMonitorOptions.UptimeMonitor)
            .ValidateDataAnnotations()
            .Validate(
                options => !options.Enabled
                           || (Uri.TryCreate(options.PushUrl, UriKind.Absolute, out var uri)
                               && uri.Scheme is "https" or "http"),
                "UptimeMonitor:PushUrl must be an HTTP(S) URL when monitoring is enabled.")
            .Validate(
                options => !options.Enabled
                           || (options.PushInterval >= TimeSpan.FromMinutes(1)
                               && options.PushInterval <= TimeSpan.FromDays(1)),
                "UptimeMonitor:PushInterval must be between one minute and one day.");

        services.AddOptionsWithValidateOnStart<WebApiOptions>()
            .BindConfiguration(WebApiOptions.Web)
            .ValidateDataAnnotations();

        return services;
    }

    public static IServiceCollection AddDiscordConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        const DiscordIntents intents = DiscordIntents.None
                                       | DiscordIntents.Guilds
                                       | DiscordIntents.GuildMembers;

        services
            .AddDiscordClient(configuration.GetRequiredSection("Discord:Token").Value!, intents)
            .Configure<DiscordConfiguration>(discordConfiguration =>
            {
                discordConfiguration.LogUnknownAuditlogs = false;
                discordConfiguration.LogUnknownEvents = false;
                // The guild is intentionally small. Keeping its roster in the
                // gateway cache avoids a Discord REST request on every dashboard refresh.
                discordConfiguration.AlwaysCacheMembers = true;
            })
            .AddInteractivityExtension()
            .UseZstdCompression()
            .AddCommandsExtension((serviceProvider, extension) =>
            {
                extension.AddProcessor(new SlashCommandProcessor(new SlashCommandConfiguration()));
                extension.AddCommands(typeof(Program).Assembly);

                var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("CommandErrorHandler");

                extension.CommandErrored += async (_, eventArgs) =>
                {
                    logger.LogError(eventArgs.Exception, "Command execution failed.");

                    try
                    {
                        var errorMessage = eventArgs.Exception.GetBaseException()
                            is TaskItemVersionConflictException
                                ? "That card changed while you were looking at it. Run the command again."
                                : "Something went wrong while running that command. Please try again.";

                        if (eventArgs.Context is SlashCommandContext slashContext
                            && slashContext.Interaction.ResponseState != DiscordInteractionResponseState.Unacknowledged)
                        {
                            await eventArgs.Context.EditResponseAsync(errorMessage);
                        }
                        else if (eventArgs.Context is SlashCommandContext privateSlashContext)
                        {
                            await privateSlashContext.RespondAsync(errorMessage, ephemeral: true);
                        }
                        else
                        {
                            await eventArgs.Context.RespondAsync(errorMessage);
                        }
                    }
                    catch (Exception responseException)
                    {
                        logger.LogError(responseException, "Failed to send the command error response.");
                    }
                };
            },
            new CommandsConfiguration
            {
                RegisterDefaultCommandProcessors = false,
                UseDefaultCommandErrorHandler = false
            })
            .ConfigureEventHandlers(eventHandlingBuilder =>
            {
                eventHandlingBuilder.AddEventHandlers<GuildDeletedEventHandler>();
                eventHandlingBuilder.AddEventHandlers<GuildCreatedEventHandler>();
                eventHandlingBuilder.AddEventHandlers<ComponentInteractionCreatedEventHandler>();
                eventHandlingBuilder.AddEventHandlers<MessageContextMenuEventHandler>();
                eventHandlingBuilder.AddEventHandlers<GuildDownloadCompletedEventHandler>();
            });

        return services;
    }
}
