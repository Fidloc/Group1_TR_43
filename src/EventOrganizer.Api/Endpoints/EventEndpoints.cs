using System.Text.Json;
using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Models;
using EventOrganizer.Api.Services;
using EventOrganizer.Api.Services.Interfaces;

namespace EventOrganizer.Api.Endpoints;

public static class EventEndpoints
{
    public static RouteGroupBuilder MapEventEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/events");

        // Створити подію (UC-07)
        group.MapPost("/", async (JsonElement body, IEventService eventService) =>
        {
            if (!EventManagement.TryReadCreate(body, out var request, out var code))
                return ApiErrors.Result(code!);

            var (created, error) = await eventService.CreateEventAsync(request!);
            if (error is not null)
                return ApiErrors.Result(error);

            return Results.Created($"/api/events/{created!.Id}", created);
        });

        // Список подій (UC-03, US-02 / Фільтр за користувачем, статусом і назвою)
        group.MapGet("/", async (int? userId, string? status, string? search, IEventService eventService) =>
        {
            EventStatus? parsedStatus = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<EventStatus>(status.Trim(), ignoreCase: true, out var statusValue) ||
                    !Enum.IsDefined(statusValue))
                {
                    return ApiErrors.Result("invalid_request");
                }

                parsedStatus = statusValue;
            }

            var events = await eventService.GetEventsAsync(userId, parsedStatus, search);
            return Results.Ok(events);
        });

        // Деталі події (UC-03)
        group.MapGet("/{eventId}", async (string eventId, IEventService eventService) =>
        {
            if (!int.TryParse(eventId, out var id) || id <= 0)
                return ApiErrors.Result("invalid_request");

            var (ev, error) = await eventService.GetEventByIdAsync(id);
            if (error is not null)
                return ApiErrors.Result(error);

            return Results.Ok(ev);
        });

        // Приєднатися за inviteCode (UC-04)
        group.MapPost("/join", async (JsonElement body, IEventService eventService) =>
        {
            if (!EventManagement.TryReadJoin(body, out var request))
                return ApiErrors.Result("invalid_request");

            var (eventId, participant, error) = await eventService.JoinEventAsync(request!);
            if (error is not null)
                return ApiErrors.Result(error);

            return Results.Created($"/api/events/{eventId}", participant);
        });

        // Редагувати подію (UC-08 — Нова можливість S1)
        group.MapPut("/{eventId:int}", async (int eventId, UpdateEventRequest request, IEventService eventService) =>
        {
            var (updated, error) = await eventService.UpdateEventAsync(eventId, request);
            if (error is not null)
                return ApiErrors.Result(error);

            return Results.Ok(updated);
        });

        // Закрити подію (UC-10)
        group.MapPost("/{eventId}/close", async (string eventId, JsonElement body, IEventService eventService) =>
        {
            return await ChangeStatusAsync(eventId, body, EventStatus.Closed, eventService);
        });

        // Скасувати подію (UC-11)
        group.MapPost("/{eventId}/cancel", async (string eventId, JsonElement body, IEventService eventService) =>
        {
            return await ChangeStatusAsync(eventId, body, EventStatus.Cancelled, eventService);
        });

        // Отримати учасників події
        group.MapGet("/{eventId:int}/participants", async (int eventId, IEventService eventService) =>
        {
            var (participants, error) = await eventService.GetParticipantsAsync(eventId);
            if (error is not null)
                return ApiErrors.Result(error);

            return Results.Ok(participants);
        });

        return group;
    }

    private static async Task<IResult> ChangeStatusAsync(
        string eventId,
        JsonElement body,
        EventStatus target,
        IEventService eventService)
    {
        if (!int.TryParse(eventId, out var id) || id <= 0 ||
            !EventManagement.TryReadUserId(body, out var userId))
            return ApiErrors.Result("invalid_request");

        var (updated, error) = await eventService.ChangeStatusAsync(id, userId, target);
        if (error is not null)
            return ApiErrors.Result(error);

        return Results.Ok(updated);
    }
}
