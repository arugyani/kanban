using MongoDB.Bson;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Driver;

namespace KanbanCord.Bot.HealthChecks;

public class MongoDbConnectivityHealthCheck : IHealthCheck
{
    private readonly IMongoDatabase _database;
    private readonly ILogger<MongoDbConnectivityHealthCheck> _logger;

    public MongoDbConnectivityHealthCheck(IMongoDatabase database, ILogger<MongoDbConnectivityHealthCheck> logger)
    {
        _database = database;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _database.RunCommandAsync<BsonDocument>(
                new BsonDocument("ping", 1),
                cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MongoDB health check failed.");
            return HealthCheckResult.Unhealthy("MongoDB health check failed.", ex);
        }
    }
}
