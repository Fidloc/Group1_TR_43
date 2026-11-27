using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Models;

namespace EventOrganizer.Api.Pages.ApiFacade;

// Результат операції фасаду без даних (успіх або текст помилки для користувача)
public sealed record ApiCallResult(bool IsSuccess, string? ErrorMessage = null);

// Результат операції фасаду з даними та необов'язковим попередженням (напр., щодо бюджету)
public sealed record ApiCallResult<T>(
    bool IsSuccess,
    T? Data = default,
    string? ErrorMessage = null,
    string? WarningMessage = null);

public sealed record LoginResult(int UserId, string UserText);

public sealed record EventParticipantItem(int Id, int UserId, string UserName, string Role);

public sealed record ExpenseWithSharesItem(Expense Expense, string PayerName, ExpenseShare[] Shares);

public sealed record EventDetailsViewData(
    EventDetailsDto Event,
    EventParticipantItem[] Participants,
    BalanceDto[] Balances,
    ExpenseWithSharesItem[] Expenses);
