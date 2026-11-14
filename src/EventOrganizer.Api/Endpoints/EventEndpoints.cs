using System.Text.Json;
using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Data;
using EventOrganizer.Api.Infrastructure;
using EventOrganizer.Api.Models;

namespace EventOrganizer.Api.Endpoints;

public static class EventEndpoints
{
    public static RouteGroupBuilder MapEventEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/events");

        // Створити подію (UC-07)
        group.MapPost("/", async (JsonElement body, IEventOrganizerRepository repo) =>
        {
            if (!EventManagement.TryReadCreate(body, out var request, out var code))
                return ApiErrors.Result(code!);

            var allEvents = await repo.GetEventsAsync();
            var allParticipations = new List<Participation>();
            foreach (var evItem in allEvents)
            {
                allParticipations.AddRange(await repo.GetParticipationsByEventIdAsync(evItem.Id));
            }

            var error = EventManagement.TryCreate(allEvents, allParticipations, request!,
                DateTime.Now, out var created, out var participation);
            if (error is not null)
                return ApiErrors.Result(error);

            created = await repo.AddEventAsync(created!);
            participation!.EventId = created.Id;
            await repo.AddParticipationAsync(participation);

            var eventParticipations = await repo.GetParticipationsByEventIdAsync(created.Id);
            return Results.Created($"/api/events/{created.Id}",
                EventManagement.ToDetails(created, eventParticipations));
        });

        // Список подій (UC-03 / Фільтр за користувачем)
        group.MapGet("/", async (int? userId, IEventOrganizerRepository repo) =>
        {
            var events = await repo.GetEventsAsync(userId);
            var result = new List<EventDetailsDto>();
            foreach (var ev in events)
            {
                var participations = await repo.GetParticipationsByEventIdAsync(ev.Id);
                result.Add(EventManagement.ToDetails(ev, participations));
            }
            return Results.Ok(result.ToArray());
        });

        // Деталі події (UC-03)
        group.MapGet("/{eventId}", async (string eventId, IEventOrganizerRepository repo) =>
        {
            if (!int.TryParse(eventId, out var id) || id <= 0)
                return ApiErrors.Result("invalid_request");

            var ev = await repo.GetEventByIdAsync(id);
            if (ev is null)
                return ApiErrors.Result("event_not_found");

            var participations = await repo.GetParticipationsByEventIdAsync(ev.Id);
            return Results.Ok(EventManagement.ToDetails(ev, participations));
        });

        // Приєднатися за inviteCode (UC-04)
        group.MapPost("/join", async (JsonElement body, IEventOrganizerRepository repo) =>
        {
            if (!EventManagement.TryReadJoin(body, out var request))
                return ApiErrors.Result("invalid_request");

            var allEvents = await repo.GetEventsAsync();
            var allParticipations = new List<Participation>();
            foreach (var evItem in allEvents)
            {
                allParticipations.AddRange(await repo.GetParticipationsByEventIdAsync(evItem.Id));
            }

            var error = EventManagement.TryJoin(allEvents, allParticipations, request!,
                DateTime.Now, out var ev, out var participation);
            if (error is not null)
                return ApiErrors.Result(error);

            participation = await repo.AddParticipationAsync(participation!);
            return Results.Created($"/api/events/{ev!.Id}", EventManagement.ToParticipant(participation));
        });

        // Редагувати подію (UC-08 — Нова можливість S1)
        group.MapPut("/{eventId:int}", async (int eventId, UpdateEventRequest request, IEventOrganizerRepository repo) =>
        {
            if (eventId <= 0 || request.UserId <= 0)
                return ApiErrors.Result("invalid_request");

            if (string.IsNullOrWhiteSpace(request.Title))
                return ApiErrors.Result("missing_title");

            if (!DateTime.TryParse(request.Date, out var parsedDate) || parsedDate.Date < DateTime.Today)
                return ApiErrors.Result("invalid_date");

            var ev = await repo.GetEventByIdAsync(eventId);
            if (ev is null)
                return ApiErrors.Result("event_not_found");

            if (ev.Status != EventStatus.Created)
                return ApiErrors.Result("event_not_active"); // FR-15

            var participations = await repo.GetParticipationsByEventIdAsync(eventId);
            var isOrganizer = participations.Any(p => p.UserId == request.UserId && p.Role == ParticipantRole.Organizer);
            if (!isOrganizer)
                return ApiErrors.Result("forbidden_not_organizer"); // FR-06

            ev.Title = request.Title.Trim();
            ev.Date = parsedDate;
            ev.Place = request.Place?.Trim() ?? "";
            ev.Description = request.Description?.Trim() ?? "";

            await repo.UpdateEventAsync(ev);
            return Results.Ok(EventManagement.ToDetails(ev, participations));
        });

        // Закрити подію (UC-10)
        group.MapPost("/{eventId}/close", async (string eventId, JsonElement body, IEventOrganizerRepository repo) =>
        {
            return await ChangeStatusAsync(eventId, body, EventStatus.Closed, repo);
        });

        // Скасувати подію (UC-11)
        group.MapPost("/{eventId}/cancel", async (string eventId, JsonElement body, IEventOrganizerRepository repo) =>
        {
            return await ChangeStatusAsync(eventId, body, EventStatus.Cancelled, repo);
        });

        // Отримати учасників події
        group.MapGet("/{eventId:int}/participants", async (int eventId, IEventOrganizerRepository repo) =>
        {
            var ev = await repo.GetEventByIdAsync(eventId);
            if (ev is null)
                return ApiErrors.Result("event_not_found");

            var parts = await repo.GetParticipationsByEventIdAsync(eventId);
            var users = await repo.GetAllUsersAsync();

            var list = parts.Select(p => new
            {
                p.Id,
                p.UserId,
                UserName = users.FirstOrDefault(u => u.Id == p.UserId)?.Name ?? "",
                Role = p.Role.ToString()
            }).ToArray();

            return Results.Ok(list);
        });

        return group;
    }

    private static async Task<IResult> ChangeStatusAsync(
        string eventId,
        JsonElement body,
        EventStatus target,
        IEventOrganizerRepository repo)
    {
        if (!int.TryParse(eventId, out var id) || id <= 0 ||
            !EventManagement.TryReadUserId(body, out var userId))
            return ApiErrors.Result("invalid_request");

        var allEvents = await repo.GetEventsAsync();
        var allParticipations = new List<Participation>();
        foreach (var evItem in allEvents)
        {
            allParticipations.AddRange(await repo.GetParticipationsByEventIdAsync(evItem.Id));
        }

        var error = EventManagement.TryChangeStatus(allEvents, allParticipations, id, userId, target, out var ev);
        if (error is not null)
            return ApiErrors.Result(error);

        await repo.UpdateEventAsync(ev!);
        var eventParticipations = await repo.GetParticipationsByEventIdAsync(ev!.Id);
        return Results.Ok(EventManagement.ToDetails(ev!, eventParticipations));
    }
}
