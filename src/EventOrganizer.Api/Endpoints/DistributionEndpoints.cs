using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Data;
using EventOrganizer.Api.Infrastructure;
using EventOrganizer.Api.Models;

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
            IEventOrganizerRepository repo) =>
        {
            if (eventId <= 0 || expenseId <= 0 || request.OrganizerUserId <= 0)
                return ApiErrors.Result("invalid_request");

            var selectedIds = request.ParticipantIds?.Distinct().ToArray() ?? Array.Empty<int>();
            if (selectedIds.Length == 0)
                return ApiErrors.Result("no_participants_selected");

            var ev = await repo.GetEventByIdAsync(eventId);
            if (ev is null)
                return ApiErrors.Result("event_not_found");

            if (ev.Status != EventStatus.Created)
                return ApiErrors.Result("event_not_active"); // FR-15

            var participations = await repo.GetParticipationsByEventIdAsync(eventId);
            var isOrganizer = participations.Any(p =>
                p.UserId == request.OrganizerUserId &&
                p.Role == ParticipantRole.Organizer);
            if (!isOrganizer)
                return ApiErrors.Result("forbidden_not_organizer");

            var expense = await repo.GetExpenseByIdAsync(eventId, expenseId);
            if (expense is null)
                return ApiErrors.Result("expense_not_found");

            var validMembers = participations.Select(p => p.Id).ToHashSet();
            if (selectedIds.Any(id => !validMembers.Contains(id)))
                return ApiErrors.Result("participant_not_found");

            var calculated = ExpenseCalculator.SplitEqually(expense.Id, expense.Amount, selectedIds);

            if (calculated.Sum(s => s.Amount) != expense.Amount)
                return ApiErrors.Result("sum_mismatch");

            await repo.ReplaceSharesAsync(expense.Id, calculated);

            return Results.Ok(calculated.ToArray());
        });

        // Отримати частки витрати
        group.MapGet("/expenses/{expenseId:int}/shares", async (
            int eventId,
            int expenseId,
            IEventOrganizerRepository repo) =>
        {
            var ev = await repo.GetEventByIdAsync(eventId);
            if (ev is null)
                return ApiErrors.Result("event_not_found");

            var expense = await repo.GetExpenseByIdAsync(eventId, expenseId);
            if (expense is null)
                return ApiErrors.Result("expense_not_found");

            var shares = await repo.GetSharesByExpenseIdAsync(expenseId);
            return Results.Ok(shares.ToArray());
        });

        // Переглянути баланс події (UC-06)
        group.MapGet("/balances", async (int eventId, IEventOrganizerRepository repo) =>
        {
            var ev = await repo.GetEventByIdAsync(eventId);
            if (ev is null)
                return ApiErrors.Result("event_not_found");

            var users = await repo.GetAllUsersAsync();
            var participations = await repo.GetParticipationsByEventIdAsync(eventId);
            var expenses = await repo.GetExpensesByEventIdAsync(eventId);
            var shares = await repo.GetSharesByEventIdAsync(eventId);

            var result = ExpenseCalculator.GetBalances(eventId, users, participations, expenses, shares);
            return Results.Ok(result);
        });

        return group;
    }
}
