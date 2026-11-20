using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Models;

namespace EventOrganizer.Api.Services.Interfaces;

public interface IExpenseService
{
    Task<(Expense? Expense, string? Error)> AddExpenseAsync(int eventId, CreateExpenseRequest request);
    Task<(Expense[]? Expenses, string? Error)> GetExpensesByEventIdAsync(int eventId);
    Task<(ExpenseShare[]? Shares, string? Error)> DistributeExpenseAsync(int eventId, int expenseId, DistributeExpenseRequest request);
    Task<(ExpenseShare[]? Shares, string? Error)> GetSharesAsync(int eventId, int expenseId);
    Task<(BalanceDto[]? Balances, string? Error)> GetBalancesAsync(int eventId);
}
