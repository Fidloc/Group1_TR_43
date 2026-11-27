using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Data.Interfaces;
using EventOrganizer.Api.Models;
using EventOrganizer.Api.Services.Interfaces;

namespace EventOrganizer.Api.Services.Implementations;

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

    // UC-05 + US-03: перевірка бюджету і атомарне збереження витрати разом із частками
    public async Task<(CreateExpenseResultDto? Result, string? Error)> AddExpenseAsync(int eventId, CreateExpenseRequest request)
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

        // TryAdd уже переконався, що подія існує
        var ev = allEvents.Single(e => e.Id == eventId);

        var alreadySpent = await _expenseRepository.GetTotalSpentByEventIdAsync(eventId);
        var budgetCheck = BudgetRule.Evaluate(ev.BudgetLimit, alreadySpent, expense!.Amount);
        if (budgetCheck.Status == BudgetCheckStatus.Rejected)
            return (null, "budget_exceeded");

        var shares = new List<ExpenseShare>();
        var selectedIds = request.ShareParticipantIds?.Distinct().ToArray() ?? Array.Empty<int>();
        if (selectedIds.Length > 0)
        {
            var validMembers = allParticipations.Select(p => p.Id).ToHashSet();
            if (selectedIds.Any(id => !validMembers.Contains(id)))
                return (null, "participant_not_found");

            shares = ExpenseCalculator.SplitEqually(expense.Id, expense.Amount, selectedIds);
            if (shares.Sum(s => s.Amount) != expense.Amount)
                return (null, "sum_mismatch");
        }

        var (savedExpense, savedShares) = await _expenseRepository.AddExpenseWithSharesAsync(expense, shares);
        return (new CreateExpenseResultDto(savedExpense, budgetCheck.WarningMessage, savedShares.ToArray()), null);
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

    // UC-12 / US-04: план взаєморозрахунків (хто кому скільки переказує)
    public async Task<(SettlementTransferDto[]? Settlements, string? Error)> GetSettlementsAsync(int eventId)
    {
        if (eventId <= 0)
            return (null, "invalid_request");

        var (balances, error) = await GetBalancesAsync(eventId);
        if (error is not null)
            return (null, error);

        return (ExpenseCalculator.CalculateSettlements(balances!), null);
    }
}
