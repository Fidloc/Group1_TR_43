using System.Text.Json;
using EventOrganizer.Api.Data;
using EventOrganizer.Api.Infrastructure;
using EventOrganizer.Api.Models;

namespace EventOrganizer.Api.Endpoints;

public static class ExpenseEndpoints
{
    public static RouteGroupBuilder MapExpenseEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/events/{eventId}/expenses");

        // Реєстрація витрати (UC-05; FR-09, FR-10, FR-15)
        group.MapPost("/", async (string eventId, JsonElement body, IEventOrganizerRepository repo) =>
        {
            if (!int.TryParse(eventId, out var id) || id <= 0)
                return ApiErrors.Result("invalid_request");

            if (!ExpenseRegistration.TryRead(body, out var request, out var code))
                return ApiErrors.Result(code!);

            var allEvents = await repo.GetEventsAsync();
            var allParticipations = await repo.GetParticipationsByEventIdAsync(id);
            var allExpenses = await repo.GetExpensesByEventIdAsync(id);

            var error = ExpenseRegistration.TryAdd(allEvents, allParticipations, allExpenses,
                id, request!, DateTime.UtcNow, out var expense);
            if (error is not null)
                return ApiErrors.Result(error);

            expense = await repo.AddExpenseAsync(expense!);
            return Results.Created($"/api/events/{id}/expenses/{expense.Id}", expense);
        });

        // Список витрат події
        group.MapGet("/", async (string eventId, IEventOrganizerRepository repo) =>
        {
            if (!int.TryParse(eventId, out var id) || id <= 0)
                return ApiErrors.Result("invalid_request");

            var ev = await repo.GetEventByIdAsync(id);
            if (ev is null)
                return ApiErrors.Result("event_not_found");

            var expenses = await repo.GetExpensesByEventIdAsync(id);
            return Results.Ok(expenses.OrderBy(x => x.Id).ToArray());
        });

        return group;
    }
}
