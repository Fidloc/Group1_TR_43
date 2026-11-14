using EventOrganizer.Api.Models;

namespace EventOrganizer.Api.Data;

public interface IEventOrganizerRepository
{
    // Users
    Task<User?> GetUserByIdAsync(int id);
    Task<User?> GetUserByEmailAsync(string email);
    Task<List<User>> GetAllUsersAsync();
    Task<User> AddUserAsync(User user);

    // Events
    Task<Event?> GetEventByIdAsync(int id);
    Task<List<Event>> GetEventsAsync(int? userId = null);
    Task<Event> AddEventAsync(Event ev);
    Task UpdateEventAsync(Event ev);

    // Participations
    Task<List<Participation>> GetParticipationsByEventIdAsync(int eventId);
    Task<Participation?> GetParticipationAsync(int eventId, int userId);
    Task<Participation> AddParticipationAsync(Participation participation);

    // Expenses
    Task<List<Expense>> GetExpensesByEventIdAsync(int eventId);
    Task<Expense?> GetExpenseByIdAsync(int eventId, int expenseId);
    Task<Expense> AddExpenseAsync(Expense expense);

    // ExpenseShares
    Task<List<ExpenseShare>> GetSharesByExpenseIdAsync(int expenseId);
    Task<List<ExpenseShare>> GetSharesByEventIdAsync(int eventId);
    Task ReplaceSharesAsync(int expenseId, IEnumerable<ExpenseShare> newShares);
}
