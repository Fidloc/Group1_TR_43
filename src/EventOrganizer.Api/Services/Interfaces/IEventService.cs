using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Models;

namespace EventOrganizer.Api.Services.Interfaces;

/// <summary>
/// Сервіс прикладної логіки для сценаріїв роботи з подіями (UC-03, UC-04, UC-07, UC-08, UC-10, UC-11, US-02).
/// </summary>
public interface IEventService
{
    Task<(EventDetailsDto? Event, string? Error)> CreateEventAsync(CreateEventRequest request);
    Task<EventDetailsDto[]> GetEventsAsync(int? userId, EventStatus? status = null, string? search = null);
    Task<(EventDetailsDto? Event, string? Error)> GetEventByIdAsync(int eventId);
    Task<(int EventId, ParticipantDto? Participant, string? Error)> JoinEventAsync(JoinEventRequest request);
    Task<(EventDetailsDto? Event, string? Error)> UpdateEventAsync(int eventId, UpdateEventRequest request);
    Task<(EventDetailsDto? Event, string? Error)> ChangeStatusAsync(int eventId, int userId, EventStatus target);
    Task<(object[]? Participants, string? Error)> GetParticipantsAsync(int eventId);
}