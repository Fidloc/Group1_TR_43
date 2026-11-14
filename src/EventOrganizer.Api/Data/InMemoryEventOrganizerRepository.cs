using EventOrganizer.Api.Models;

namespace EventOrganizer.Api.Data;

public sealed class InMemoryEventOrganizerRepository : IEventOrganizerRepository
{
    private readonly object _gate = new();
    private readonly List<User> _users = [];
    private readonly List<Event> _events = [];
    private readonly List<Participation> _participations = [];
    private readonly List<Expense> _expenses = [];
    private readonly List<ExpenseShare> _shares = [];

    public Task<User?> GetUserByIdAsync(int id)
    {
        lock (_gate)
        {
            return Task.FromResult(_users.FirstOrDefault(u => u.Id == id));
        }
    }

    public Task<User?> GetUserByEmailAsync(string email)
    {
        lock (_gate)
        {
            return Task.FromResult(_users.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public Task<List<User>> GetAllUsersAsync()
    {
        lock (_gate)
        {
            return Task.FromResult(_users.ToList());
        }
    }

    public Task<User> AddUserAsync(User user)
    {
        lock (_gate)
        {
            user.Id = _users.Count == 0 ? 1 : _users.Max(u => u.Id) + 1;
            _users.Add(user);
            return Task.FromResult(user);
        }
    }

    public Task<Event?> GetEventByIdAsync(int id)
    {
        lock (_gate)
        {
            return Task.FromResult(_events.FirstOrDefault(e => e.Id == id));
        }
    }

    public Task<List<Event>> GetEventsAsync(int? userId = null)
    {
        lock (_gate)
        {
            var list = _events
                .Where(e => userId is null || _participations.Any(p => p.EventId == e.Id && p.UserId == userId))
                .OrderBy(e => e.Id)
                .ToList();
            return Task.FromResult(list);
        }
    }

    public Task<Event> AddEventAsync(Event ev)
    {
        lock (_gate)
        {
            ev.Id = _events.Count == 0 ? 1 : _events.Max(e => e.Id) + 1;
            _events.Add(ev);
            return Task.FromResult(ev);
        }
    }

    public Task UpdateEventAsync(Event ev)
    {
        lock (_gate)
        {
            var existing = _events.FirstOrDefault(e => e.Id == ev.Id);
            if (existing is not null)
            {
                existing.Title = ev.Title;
                existing.Date = ev.Date;
                existing.Place = ev.Place;
                existing.Description = ev.Description;
                existing.Status = ev.Status;
            }
            return Task.CompletedTask;
        }
    }

    public Task<List<Participation>> GetParticipationsByEventIdAsync(int eventId)
    {
        lock (_gate)
        {
            return Task.FromResult(_participations.Where(p => p.EventId == eventId).ToList());
        }
    }

    public Task<Participation?> GetParticipationAsync(int eventId, int userId)
    {
        lock (_gate)
        {
            return Task.FromResult(_participations.FirstOrDefault(p => p.EventId == eventId && p.UserId == userId));
        }
    }

    public Task<Participation> AddParticipationAsync(Participation participation)
    {
        lock (_gate)
        {
            participation.Id = _participations.Count == 0 ? 1 : _participations.Max(p => p.Id) + 1;
            _participations.Add(participation);
            return Task.FromResult(participation);
        }
    }

    public Task<List<Expense>> GetExpensesByEventIdAsync(int eventId)
    {
        lock (_gate)
        {
            return Task.FromResult(_expenses.Where(x => x.EventId == eventId).OrderBy(x => x.Id).ToList());
        }
    }

    public Task<Expense?> GetExpenseByIdAsync(int eventId, int expenseId)
    {
        lock (_gate)
        {
            return Task.FromResult(_expenses.FirstOrDefault(x => x.EventId == eventId && x.Id == expenseId));
        }
    }

    public Task<Expense> AddExpenseAsync(Expense expense)
    {
        lock (_gate)
        {
            expense.Id = _expenses.Count == 0 ? 1 : _expenses.Max(x => x.Id) + 1;
            _expenses.Add(expense);
            return Task.FromResult(expense);
        }
    }

    public Task<List<ExpenseShare>> GetSharesByExpenseIdAsync(int expenseId)
    {
        lock (_gate)
        {
            return Task.FromResult(_shares.Where(s => s.ExpenseId == expenseId).ToList());
        }
    }

    public Task<List<ExpenseShare>> GetSharesByEventIdAsync(int eventId)
    {
        lock (_gate)
        {
            var eventExpenseIds = _expenses.Where(x => x.EventId == eventId).Select(x => x.Id).ToHashSet();
            return Task.FromResult(_shares.Where(s => eventExpenseIds.Contains(s.ExpenseId)).ToList());
        }
    }

    public Task ReplaceSharesAsync(int expenseId, IEnumerable<ExpenseShare> newShares)
    {
        lock (_gate)
        {
            _shares.RemoveAll(s => s.ExpenseId == expenseId);
            var nextId = _shares.Count == 0 ? 1 : _shares.Max(s => s.Id) + 1;
            foreach (var share in newShares)
            {
                share.Id = nextId++;
                _shares.Add(share);
            }
            return Task.CompletedTask;
        }
    }
}
