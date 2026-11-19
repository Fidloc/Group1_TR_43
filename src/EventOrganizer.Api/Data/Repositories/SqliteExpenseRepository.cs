using EventOrganizer.Api.Data.Interfaces;
using EventOrganizer.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EventOrganizer.Api.Data.Repositories;

/// <summary>
/// Реалізація сховища витрат та часток розподілу у SQLite через EF Core.
/// </summary>
public sealed class SqliteExpenseRepository : IExpenseRepository
{
    private readonly AppDbContext _db;

    public SqliteExpenseRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<Expense>> GetExpensesByEventIdAsync(int eventId)
    {
        return await _db.Expenses.AsNoTracking().Where(x => x.EventId == eventId).OrderBy(x => x.Id).ToListAsync();
    }

    public async Task<Expense?> GetExpenseByIdAsync(int eventId, int expenseId)
    {
        return await _db.Expenses.AsNoTracking().FirstOrDefaultAsync(x => x.EventId == eventId && x.Id == expenseId);
    }

    public async Task<Expense> AddExpenseAsync(Expense expense)
    {
        expense.Id = 0; // Дозволяємо SQLite призначити автоінкрементний первинний ключ
        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync();
        return expense;
    }

    public async Task<List<ExpenseShare>> GetSharesByExpenseIdAsync(int expenseId)
    {
        return await _db.ExpenseShares.AsNoTracking().Where(s => s.ExpenseId == expenseId).ToListAsync();
    }

    public async Task<List<ExpenseShare>> GetSharesByEventIdAsync(int eventId)
    {
        var expenseIds = await _db.Expenses
            .AsNoTracking()
            .Where(x => x.EventId == eventId)
            .Select(x => x.Id)
            .ToListAsync();

        return await _db.ExpenseShares
            .AsNoTracking()
            .Where(s => expenseIds.Contains(s.ExpenseId))
            .ToListAsync();
    }

    public async Task ReplaceSharesAsync(int expenseId, IEnumerable<ExpenseShare> newShares)
    {
        var existing = await _db.ExpenseShares.Where(s => s.ExpenseId == expenseId).ToListAsync();
        if (existing.Count > 0)
        {
            _db.ExpenseShares.RemoveRange(existing);
        }

        foreach (var share in newShares)
        {
            share.Id = 0; // Let SQLite autoincrement primary key
            _db.ExpenseShares.Add(share);
        }

        await _db.SaveChangesAsync();
    }
}
