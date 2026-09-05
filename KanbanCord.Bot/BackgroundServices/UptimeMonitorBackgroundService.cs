using KanbanCord.Core.Options;
using Microsoft.Extensions.Options;

namespace KanbanCord.Bot.BackgroundServices;

public class UptimeMonitorBackgroundService : BackgroundService
{
    private readonly UptimeMonitorOptions _uptimeMonitorOptions;
    private readonly HttpClient _httpClient;
    private readonly ILogger<UptimeMonitorBackgroundService> _logger;

    public UptimeMonitorBackgroundService(IHttpClientFactory httpClientFactory, IOptions<UptimeMonitorOptions> uptimeMonitorOptions, ILogger<UptimeMonitorBackgroundService> logger)
    {
        _logger = logger;
        _uptimeMonitorOptions = uptimeMonitorOptions.Value;
        _httpClient = httpClientFactory.CreateClient("uptime-monitor");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_uptimeMonitorOptions.Enabled)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var response = await _httpClient.GetAsync(
                    _uptimeMonitorOptions.PushUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    stoppingToken);
                response.EnsureSuccessStatusCode();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Uptime heartbeat failed in {Service}.",
                    nameof(UptimeMonitorBackgroundService));
            }

            await Task.Delay(_uptimeMonitorOptions.PushInterval, stoppingToken);
        }
    }
}
