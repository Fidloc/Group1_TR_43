using EventOrganizer.Api.Models;

namespace EventOrganizer.Api.Data.Interfaces;

public interface IExpenseRepository
{
    Task<List<Expense>> GetExpensesByEventIdAsync(int eventId);
    Task<Expense?> GetExpenseByIdAsync(int eventId, int expenseId);
    Task<decimal> GetTotalSpentByEventIdAsync(int eventId, CancellationToken cancellationToken = default);
    Task<Expense> AddExpenseAsync(Expense expense);
    Task<(Expense Expense, List<ExpenseShare> Shares)> AddExpenseWithSharesAsync(
        Expense expense,
        IEnumerable<ExpenseShare> shares,
        CancellationToken cancellationToken = default);

    Task<List<ExpenseShare>> GetSharesByExpenseIdAsync(int expenseId);
    Task<List<ExpenseShare>> GetSharesByEventIdAsync(int eventId);
    Task ReplaceSharesAsync(int expenseId, IEnumerable<ExpenseShare> newShares);
}
