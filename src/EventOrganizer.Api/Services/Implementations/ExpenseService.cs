using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Data.Interfaces;
using EventOrganizer.Api.Models;
using EventOrganizer.Api.Services.Interfaces;

namespace EventOrganizer.Api.Services.Implementations;

/// <summary>
/// Координація прикладних сценаріїв витрат, їх розподілу та балансів (Application Layer: UC-05, UC-06, UC-09).
/// </summary>
public sealed class ExpenseService : IExpenseService
{
    private readonly IExpenseRepository _expenseRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IUserRepository _userRepository;

    public ExpenseService(
        IExpenseRepository expenseRepository,
        IEventRepository eventRepository,
        IUserRepository userRepository)
    {
        _expenseRepository = expenseRepository;
        _eventRepository = eventRepository;
        _userRepository = userRepository;
    }

    public async Task<(Expense? Expense, string? Error)> AddExpenseAsync(int eventId, CreateExpenseRequest request)
    {
        if (eventId <= 0)
            return (null, "invalid_request");

        var allEvents = await _eventRepository.GetEventsAsync();
        var allParticipations = await _eventRepository.GetParticipationsByEventIdAsync(eventId);
        var allExpenses = await _expenseRepository.GetExpensesByEventIdAsync(eventId);

        var error = ExpenseRegistration.TryAdd(allEvents, allParticipations, allExpenses,
            eventId, request, DateTime.UtcNow, out var expense);
        if (error is not null)
            return (null, error);

        expense = await _expenseRepository.AddExpenseAsync(expense!);
        return (expense, null);
    }

    public async Task<(Expense[]? Expenses, string? Error)> GetExpensesByEventIdAsync(int eventId)
    {
        if (eventId <= 0)
            return (null, "invalid_request");

        var ev = await _eventRepository.GetEventByIdAsync(eventId);
        if (ev is null)
            return (null, "event_not_found");

        var expenses = await _expenseRepository.GetExpensesByEventIdAsync(eventId);
        return (expenses.OrderBy(x => x.Id).ToArray(), null);
    }

    public async Task<(ExpenseShare[]? Shares, string? Error)> DistributeExpenseAsync(
        int eventId,
        int expenseId,
        DistributeExpenseRequest request)
    {
        if (eventId <= 0 || expenseId <= 0 || request.OrganizerUserId <= 0)
            return (null, "invalid_request");

        var selectedIds = request.ParticipantIds?.Distinct().ToArray() ?? Array.Empty<int>();
        if (selectedIds.Length == 0)
            return (null, "no_participants_selected");

        var ev = await _eventRepository.GetEventByIdAsync(eventId);
        if (ev is null)
            return (null, "event_not_found");

        if (ev.Status != EventStatus.Created)
            return (null, "event_not_active"); // FR-15

        var participations = await _eventRepository.GetParticipationsByEventIdAsync(eventId);
        if (!EventManagement.IsOrganizer(participations, eventId, request.OrganizerUserId))
            return (null, "forbidden_not_organizer");

        var expense = await _expenseRepository.GetExpenseByIdAsync(eventId, expenseId);
        if (expense is null)
            return (null, "expense_not_found");

        var validMembers = participations.Select(p => p.Id).ToHashSet();
        if (selectedIds.Any(id => !validMembers.Contains(id)))
            return (null, "participant_not_found");

        var calculated = ExpenseCalculator.SplitEqually(expense.Id, expense.Amount, selectedIds);

        if (calculated.Sum(s => s.Amount) != expense.Amount)
            return (null, "sum_mismatch");

        await _expenseRepository.ReplaceSharesAsync(expense.Id, calculated);

        return (calculated.ToArray(), null);
    }

    public async Task<(ExpenseShare[]? Shares, string? Error)> GetSharesAsync(int eventId, int expenseId)
    {
        var ev = await _eventRepository.GetEventByIdAsync(eventId);
        if (ev is null)
            return (null, "event_not_found");

        var expense = await _expenseRepository.GetExpenseByIdAsync(eventId, expenseId);
        if (expense is null)
            return (null, "expense_not_found");

        var shares = await _expenseRepository.GetSharesByExpenseIdAsync(expenseId);
        return (shares.ToArray(), null);
    }

    public async Task<(BalanceDto[]? Balances, string? Error)> GetBalancesAsync(int eventId)
    {
        var ev = await _eventRepository.GetEventByIdAsync(eventId);
        if (ev is null)
            return (null, "event_not_found");

        var users = await _userRepository.GetAllUsersAsync();
        var participations = await _eventRepository.GetParticipationsByEventIdAsync(eventId);
        var expenses = await _expenseRepository.GetExpensesByEventIdAsync(eventId);
        var shares = await _expenseRepository.GetSharesByEventIdAsync(eventId);

        var result = ExpenseCalculator.GetBalances(eventId, users, participations, expenses, shares);
        return (result, null);
    }
}
