using System.Text.Json;
using Microsoft.AspNetCore.RateLimiting;

namespace KanbanCord.Bot.Web;

public static class BoardApiEndpoints
{
    public static IEndpointRouteBuilder MapBoardApi(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api").RequireRateLimiting("board-api");

        api.MapGet("/dashboard", async (BoardApiService service, HttpContext context) =>
            Results.Json(await service.GetDashboardAsync(context.Request)));

        api.MapPost("/cards", async (CreateCardRequest request, BoardApiService service, HttpContext context) =>
            Results.Json(new { card = await service.CreateCardAsync(context.Request, request) }, statusCode: 201));

        api.MapPatch("/cards/{id}", async (string id, JsonElement request, BoardApiService service, HttpContext context) =>
            Results.Json(new { card = await service.UpdateCardAsync(context.Request, id, request) }));

        api.MapPost("/cards/{id}/move", async (string id, MoveCardRequest request, BoardApiService service, HttpContext context) =>
            Results.Json(new { card = await service.MoveCardAsync(context.Request, id, request) }));

        api.MapPost("/cards/{id}/comments", async (string id, AddCommentRequest request, BoardApiService service, HttpContext context) =>
            Results.Json(new { comment = await service.AddCommentAsync(context.Request, id, request) }, statusCode: 201));

        api.MapPost("/cards/{id}/checklist", async (string id, AddChecklistItemRequest request, BoardApiService service, HttpContext context) =>
            Results.Json(new { item = await service.AddChecklistItemAsync(context.Request, id, request) }, statusCode: 201));

        api.MapPatch("/cards/{id}/checklist/{itemId}", async (string id, string itemId, UpdateChecklistItemRequest request, BoardApiService service, HttpContext context) =>
            Results.Json(new { item = await service.UpdateChecklistItemAsync(context.Request, id, itemId, request) }));

        api.MapPost("/cards/{id}/links", async (string id, AddGitHubLinkRequest request, BoardApiService service, HttpContext context) =>
            Results.Json(new { link = await service.AddGitHubLinkAsync(context.Request, id, request) }, statusCode: 201));

        api.MapDelete("/cards/{id}/links/{linkId}", async (string id, string linkId, BoardApiService service, HttpContext context) =>
        {
            await service.RemoveGitHubLinkAsync(context.Request, id, linkId);
            return Results.NoContent();
        });

        api.MapPost("/groups", async (CreateGroupRequest request, BoardApiService service, HttpContext context) =>
            Results.Json(new { group = await service.CreateGroupAsync(context.Request, request) }, statusCode: 201));

        api.MapPatch("/groups/{id}", async (string id, UpdateGroupRequest request, BoardApiService service, HttpContext context) =>
            Results.Json(new { group = await service.UpdateGroupAsync(context.Request, id, request) }));

        api.MapPut("/groups/{id}/members/{personId}", async (string id, string personId, SetGroupMemberRequest request, BoardApiService service, HttpContext context) =>
            Results.Json(new { group = await service.SetGroupMemberAsync(context.Request, id, personId, request) }));

        api.MapDelete("/groups/{id}/members/{personId}", async (string id, string personId, BoardApiService service, HttpContext context) =>
        {
            await service.RemoveGroupMemberAsync(context.Request, id, personId);
            return Results.NoContent();
        });

        api.MapDelete("/groups/{id}", async (string id, BoardApiService service, HttpContext context) =>
        {
            await service.DeleteGroupAsync(context.Request, id);
            return Results.NoContent();
        });

        api.MapPost("/boards", async (CreateBoardRequest request, BoardApiService service, HttpContext context) =>
            Results.Json(new { board = await service.CreateBoardAsync(context.Request, request) }, statusCode: 201));

        api.MapPatch("/boards/{id}", async (string id, UpdateBoardRequest request, BoardApiService service, HttpContext context) =>
            Results.Json(new { board = await service.UpdateBoardAsync(context.Request, id, request) }));

        api.MapDelete("/boards/{id}", async (string id, BoardApiService service, HttpContext context) =>
        {
            await service.DeleteBoardAsync(context.Request, id);
            return Results.NoContent();
        });

        return endpoints;
    }
}
