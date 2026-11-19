using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Data.Interfaces;
using EventOrganizer.Api.Models;
using EventOrganizer.Api.Services.Interfaces;

namespace EventOrganizer.Api.Services.Implementations;

public sealed class EventService : IEventService
{
    private readonly IEventRepository _eventRepository;
    private readonly IUserRepository _userRepository;

    public EventService(IEventRepository eventRepository, IUserRepository userRepository)
    {
        _eventRepository = eventRepository;
        _userRepository = userRepository;
    }

    public async Task<(EventDetailsDto? Event, string? Error)> CreateEventAsync(CreateEventRequest request)
    {
        var allEvents = await _eventRepository.GetEventsAsync();
        var allParticipations = new List<Participation>();
        foreach (var evItem in allEvents)
        {
            allParticipations.AddRange(await _eventRepository.GetParticipationsByEventIdAsync(evItem.Id));
        }

        var error = EventManagement.TryCreate(allEvents, allParticipations, request,
            DateTime.Now, out var created, out var participation);
        if (error is not null)
            return (null, error);

        created = await _eventRepository.AddEventWithOrganizerAsync(created!, participation!);

        var eventParticipations = await _eventRepository.GetParticipationsByEventIdAsync(created.Id);
        return (EventManagement.ToDetails(created, eventParticipations), null);
    }

    public async Task<EventDetailsDto[]> GetEventsAsync(int? userId)
    {
        var events = await _eventRepository.GetEventsAsync(userId);
        var result = new List<EventDetailsDto>();
        foreach (var ev in events)
        {
            var participations = await _eventRepository.GetParticipationsByEventIdAsync(ev.Id);
            result.Add(EventManagement.ToDetails(ev, participations));
        }
        return result.ToArray();
    }

    public async Task<(EventDetailsDto? Event, string? Error)> GetEventByIdAsync(int eventId)
    {
        if (eventId <= 0)
            return (null, "invalid_request");

        var ev = await _eventRepository.GetEventByIdAsync(eventId);
        if (ev is null)
            return (null, "event_not_found");

        var participations = await _eventRepository.GetParticipationsByEventIdAsync(ev.Id);
        return (EventManagement.ToDetails(ev, participations), null);
    }

    public async Task<(int EventId, ParticipantDto? Participant, string? Error)> JoinEventAsync(JoinEventRequest request)
    {
        var allEvents = await _eventRepository.GetEventsAsync();
        var allParticipations = new List<Participation>();
        foreach (var evItem in allEvents)
        {
            allParticipations.AddRange(await _eventRepository.GetParticipationsByEventIdAsync(evItem.Id));
        }

        var error = EventManagement.TryJoin(allEvents, allParticipations, request,
            DateTime.Now, out var ev, out var participation);
        if (error is not null)
            return (0, null, error);

        participation = await _eventRepository.AddParticipationAsync(participation!);
        return (ev!.Id, EventManagement.ToParticipant(participation), null);
    }

    // UC-08 «Редагувати подію» (AC-01..AC-04): координатор сценарію
    public async Task<(EventDetailsDto? Event, string? Error)> UpdateEventAsync(int eventId, UpdateEventRequest request)
    {
        var ev = await _eventRepository.GetEventByIdAsync(eventId);
        var participations = await _eventRepository.GetParticipationsByEventIdAsync(eventId);

        var error = EventManagement.TryUpdate(ev, participations, eventId, request, DateTime.Today);
        if (error is not null)
            return (null, error);

        await _eventRepository.UpdateEventAsync(ev!);
        return (EventManagement.ToDetails(ev!, participations), null);
    }

    public async Task<(EventDetailsDto? Event, string? Error)> ChangeStatusAsync(int eventId, int userId, EventStatus target)
    {
        if (eventId <= 0 || userId <= 0)
            return (null, "invalid_request");

        var allEvents = await _eventRepository.GetEventsAsync();
        var allParticipations = new List<Participation>();
        foreach (var evItem in allEvents)
        {
            allParticipations.AddRange(await _eventRepository.GetParticipationsByEventIdAsync(evItem.Id));
        }

        var error = EventManagement.TryChangeStatus(allEvents, allParticipations, eventId, userId, target, out var ev);
        if (error is not null)
            return (null, error);

        await _eventRepository.UpdateEventAsync(ev!);
        var eventParticipations = await _eventRepository.GetParticipationsByEventIdAsync(ev!.Id);
        return (EventManagement.ToDetails(ev, eventParticipations), null);
    }

    public async Task<(object[]? Participants, string? Error)> GetParticipantsAsync(int eventId)
    {
        var ev = await _eventRepository.GetEventByIdAsync(eventId);
        if (ev is null)
            return (null, "event_not_found");

        var parts = await _eventRepository.GetParticipationsByEventIdAsync(eventId);
        var users = await _userRepository.GetAllUsersAsync();

        var list = parts.Select(p => (object)new
        {
            p.Id,
            p.UserId,
            UserName = users.FirstOrDefault(u => u.Id == p.UserId)?.Name ?? "",
            Role = p.Role.ToString()
        }).ToArray();

        return (list, null);
    }
}
