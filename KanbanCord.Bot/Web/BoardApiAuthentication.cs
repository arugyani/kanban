using System.Security.Cryptography;
using System.Text;
using KanbanCord.Core.Options;
using Microsoft.Extensions.Options;

namespace KanbanCord.Bot.Web;

public static class BoardApiAuthentication
{
    public static IApplicationBuilder UseBoardApiAuthentication(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/api"))
            {
                await next();
                return;
            }

            var correlationId = context.TraceIdentifier;
            context.Response.Headers.Append("X-Correlation-Id", correlationId);

            try
            {
                var options = context.RequestServices.GetRequiredService<IOptions<WebApiOptions>>().Value;
                var authorization = context.Request.Headers.Authorization.ToString();
                var supplied = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    ? authorization["Bearer ".Length..]
                    : string.Empty;

                if (string.IsNullOrWhiteSpace(options.ApiKey))
                    throw new BoardApiException(503, "api_not_configured", "The bot API has not been configured yet.");

                if (!FixedTimeEquals(supplied, options.ApiKey))
                    throw new BoardApiException(401, "not_authenticated", "The Board could not verify this connection.");

                await next();
            }
            catch (BoardApiException exception)
            {
                context.Response.StatusCode = exception.StatusCode;
                await context.Response.WriteAsJsonAsync(new
                {
                    code = exception.Code,
                    message = exception.Message,
                    correlationId,
                });
            }
            catch (BadHttpRequestException)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new
                {
                    code = "invalid_request",
                    message = "That request could not be read.",
                    correlationId,
                });
            }
            catch (Exception exception)
            {
                var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("BoardApi");
                logger.LogError(exception, "Board API request failed. CorrelationId: {CorrelationId}", correlationId);
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new
                {
                    code = "unexpected_error",
                    message = "The shared board hit a snag. Please try again.",
                    correlationId,
                });
            }
        });
    }

    private static bool FixedTimeEquals(string supplied, string expected)
    {
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash);
    }
}
