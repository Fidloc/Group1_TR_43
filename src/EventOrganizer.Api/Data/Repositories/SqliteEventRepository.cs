using EventOrganizer.Api.Data.Interfaces;
using EventOrganizer.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EventOrganizer.Api.Data.Repositories;

/// <summary>
/// Реалізація сховища подій та участей у SQLite через EF Core.
/// </summary>
public sealed class SqliteEventRepository : IEventRepository
{
    private readonly AppDbContext _db;

    public SqliteEventRepository(AppDbContext db)
    {
        _db = db;
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
    }public async Task<List<Event>> GetEventsAsync(int? userId = null, EventStatus? status = null, string? search = null)
    {
        IQueryable<Event> query = _db.Events.AsNoTracking();

        if (userId is not null)
        {
            var eventIds = await _db.Participations
                .AsNoTracking()
                .Where(p => p.UserId == userId.Value)
                .Select(p => p.EventId)
                .Distinct()
                .ToListAsync();

            query = query.Where(e => eventIds.Contains(e.Id));
        }

        if (status is not null)
        {
            query = query.Where(e => e.Status == status.Value);
        }

        var events = await query.OrderBy(e => e.Id).ToListAsync();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmedSearch = search.Trim();
            events = events
                .Where(e => e.Title.Contains(trimmedSearch, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return events;
    }

    public async Task<Event> AddEventAsync(Event ev)
    {
        ev.Id = 0; // Дозволяємо SQLite призначити автоінкрементний первинний ключ
        _db.Events.Add(ev);
        await _db.SaveChangesAsync();
        return ev;
    }

    public async Task<Event> AddEventWithOrganizerAsync(Event ev, Participation organizer)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();

        ev.Id = 0; // Дозволяємо SQLite призначити автоінкрементний первинний ключ
        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        organizer.Id = 0;
        organizer.EventId = ev.Id;
        _db.Participations.Add(organizer);
        await _db.SaveChangesAsync();

        await transaction.CommitAsync();
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
        participation.Id = 0; // Дозволяємо SQLite призначити автоінкрементний первинний ключ
        _db.Participations.Add(participation);
        await _db.SaveChangesAsync();
        return participation;
    }
}
