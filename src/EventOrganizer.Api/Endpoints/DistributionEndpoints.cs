using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Services.Interfaces;

namespace EventOrganizer.Api.Endpoints;

public static class DistributionEndpoints
{
    public static RouteGroupBuilder MapDistributionEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/events/{eventId:int}");

        // Розподілити витрату (UC-09)
        group.MapPost("/expenses/{expenseId:int}/distribute", async (
            int eventId,
            int expenseId,
            DistributeExpenseRequest request,
            IExpenseService expenseService) =>
        {
            var (shares, error) = await expenseService.DistributeExpenseAsync(eventId, expenseId, request);
            if (error is not null)
                return ApiErrors.Result(error);

            return Results.Ok(shares);
        });

        // Отримати частки витрати
        group.MapGet("/expenses/{expenseId:int}/shares", async (
            int eventId,
            int expenseId,
            IExpenseService expenseService) =>
        {
            var (shares, error) = await expenseService.GetSharesAsync(eventId, expenseId);
            if (error is not null)
                return ApiErrors.Result(error);

            return Results.Ok(shares);
        });

        // Переглянути баланс події (UC-06)
        group.MapGet("/balances", async (int eventId, IExpenseService expenseService) =>
        {
            var (balances, error) = await expenseService.GetBalancesAsync(eventId);
            if (error is not null)
                return ApiErrors.Result(error);

            return Results.Ok(balances);
        });

        // План взаєморозрахунків (UC-12 / US-04)
        group.MapGet("/settlements", async (int eventId, IExpenseService expenseService) =>
        {
            var (settlements, error) = await expenseService.GetSettlementsAsync(eventId);
            if (error is not null)
                return ApiErrors.Result(error);

            return Results.Ok(settlements);
        });

        return group;
    }
}
