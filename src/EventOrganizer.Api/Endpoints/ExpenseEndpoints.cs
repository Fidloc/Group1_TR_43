using System.Text.Json;
using EventOrganizer.Api.Services;
using EventOrganizer.Api.Services.Interfaces;

namespace EventOrganizer.Api.Endpoints;

public static class ExpenseEndpoints
{
    public static RouteGroupBuilder MapExpenseEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/events/{eventId}/expenses");

        // Реєстрація витрати (UC-05; FR-09, FR-10, FR-15)
        group.MapPost("/", async (string eventId, JsonElement body, IExpenseService expenseService) =>
        {
            if (!int.TryParse(eventId, out var id) || id <= 0)
                return ApiErrors.Result("invalid_request");

            if (!ExpenseRegistration.TryRead(body, out var request, out var code))
                return ApiErrors.Result(code!);

            var (expense, error) = await expenseService.AddExpenseAsync(id, request!);
            if (error is not null)
                return ApiErrors.Result(error);

            return Results.Created($"/api/events/{id}/expenses/{expense!.Id}", expense);
        });

        // Список витрат події
        group.MapGet("/", async (string eventId, IExpenseService expenseService) =>
        {
            if (!int.TryParse(eventId, out var id) || id <= 0)
                return ApiErrors.Result("invalid_request");

            var (expenses, error) = await expenseService.GetExpensesByEventIdAsync(id);
            if (error is not null)
                return ApiErrors.Result(error);

            return Results.Ok(expenses);
        });

        return group;
    }
}
