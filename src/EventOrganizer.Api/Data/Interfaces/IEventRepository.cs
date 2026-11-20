using EventOrganizer.Api.Models;

namespace EventOrganizer.Api.Data.Interfaces;

public interface IEventRepository
{
    Task<Event?> GetEventByIdAsync(int id);
    Task<List<Event>> GetEventsAsync(int? userId = null, EventStatus? status = null, string? search = null);
    Task<Event> AddEventAsync(Event ev);
    Task<Event> AddEventWithOrganizerAsync(Event ev, Participation organizer);
    Task UpdateEventAsync(Event ev);

    Task<List<Participation>> GetParticipationsByEventIdAsync(int eventId);
    Task<Participation?> GetParticipationAsync(int eventId, int userId);
    Task<Participation> AddParticipationAsync(Participation participation);
}