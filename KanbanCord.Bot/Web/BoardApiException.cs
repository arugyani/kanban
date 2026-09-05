namespace KanbanCord.Bot.Web;

public sealed class BoardApiException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public string Code { get; } = code;
}
