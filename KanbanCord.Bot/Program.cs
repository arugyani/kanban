using System.Threading.RateLimiting;
using KanbanCord.Bot.Extensions;
using KanbanCord.Bot.Web;
using KanbanCord.Core.Options;
using Microsoft.AspNetCore.RateLimiting;

namespace KanbanCord.Bot;

internal class Program
{
    private static async Task Main()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options =>
            options.Limits.MaxRequestBodySize = 64 * 1024);

        builder.Host
            .UseDefaultServiceProvider()
            .UseSerilog();

        builder.Services
            .AddHostDependencies()
            .AddDiscordConfiguration(builder.Configuration);
        builder.Services.AddScoped<BoardApiService>();
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    code = "rate_limited",
                    message = "The Board is receiving too many changes. Try again in a moment.",
                    correlationId = context.HttpContext.TraceIdentifier,
                }, cancellationToken);
            };
            options.AddPolicy("board-api", context =>
            {
                var userId = context.Request.Headers["X-Discord-User-Id"].ToString();
                var partition = string.IsNullOrWhiteSpace(userId)
                    ? context.Connection.RemoteIpAddress?.ToString() ?? "unknown"
                    : userId;
                return RateLimitPartition.GetFixedWindowLimiter(partition, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 120,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                });
            });
        });

        builder.Services.AddHealthChecks()
            .AddKanbanCordHealthChecks();

        var app = builder.Build();

        app.UseBoardApiAuthentication();
        app.UseRateLimiter();
        app.UseHealthChecks("/health");
        app.MapBoardApi();

        await app.RunAsync();
    }
}
