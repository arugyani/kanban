using DSharpPlus;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KanbanCord.Bot.HealthChecks;

public class DiscordConnectivityHealthCheck : IHealthCheck
{
    private readonly DiscordClient _discordClient;

    public DiscordConnectivityHealthCheck(DiscordClient discordClient)
    {
        _discordClient = discordClient;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = _discordClient.AllShardsConnected
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("The Discord gateway is disconnected.");
        return Task.FromResult(result);
    }
}
