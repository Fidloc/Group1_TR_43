using EventOrganizer.Api.Models;

namespace EventOrganizer.Api.Data.Interfaces;

/// <summary>
/// Контракт сховища для роботи з витратами та частками розподілу (ISP).
/// </summary>
public interface IExpenseRepository
{
    Task<List<Expense>> GetExpensesByEventIdAsync(int eventId);
    Task<Expense?> GetExpenseByIdAsync(int eventId, int expenseId);
    Task<Expense> AddExpenseAsync(Expense expense);

    Task<List<ExpenseShare>> GetSharesByExpenseIdAsync(int expenseId);
    Task<List<ExpenseShare>> GetSharesByEventIdAsync(int eventId);
    Task ReplaceSharesAsync(int expenseId, IEnumerable<ExpenseShare> newShares);
}
