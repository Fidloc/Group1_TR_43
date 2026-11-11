using EventOrganizer.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EventOrganizer.Api.Data;

public sealed class SqliteEventOrganizerRepository : IEventOrganizerRepository
{
    private readonly AppDbContext _db;

    public SqliteEventOrganizerRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<User?> GetUserByIdAsync(int id)
    {
        return await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        return await _db.Users.AsNoTracking().OrderBy(u => u.Id).ToListAsync();
    }

    public async Task<User> AddUserAsync(User user)
    {
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    public async Task<Event?> GetEventByIdAsync(int id)
    {
        return await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<List<Event>> GetEventsAsync(int? userId = null)
    {
        if (userId is null)
        {
            return await _db.Events.AsNoTracking().OrderBy(e => e.Id).ToListAsync();
        }

        var eventIds = await _db.Participations
            .AsNoTracking()
            .Where(p => p.UserId == userId.Value)
            .Select(p => p.EventId)
            .Distinct()
            .ToListAsync();

        return await _db.Events
            .AsNoTracking()
            .Where(e => eventIds.Contains(e.Id))
            .OrderBy(e => e.Id)
            .ToListAsync();
    }

    public async Task<Event> AddEventAsync(Event ev)
    {
        _db.Events.Add(ev);
        await _db.SaveChangesAsync();
        return ev;
    }

    public async Task UpdateEventAsync(Event ev)
    {
        var existing = await _db.Events.FirstOrDefaultAsync(e => e.Id == ev.Id);
        if (existing is not null)
        {
            existing.Title = ev.Title;
            existing.Date = ev.Date;
            existing.Place = ev.Place;
            existing.Description = ev.Description;
            existing.Status = ev.Status;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<List<Participation>> GetParticipationsByEventIdAsync(int eventId)
    {
        return await _db.Participations.AsNoTracking().Where(p => p.EventId == eventId).ToListAsync();
    }

    public async Task<Participation?> GetParticipationAsync(int eventId, int userId)
    {
        return await _db.Participations.AsNoTracking().FirstOrDefaultAsync(p => p.EventId == eventId && p.UserId == userId);
    }

    public async Task<Participation> AddParticipationAsync(Participation participation)
    {
        _db.Participations.Add(participation);
        await _db.SaveChangesAsync();
        return participation;
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
