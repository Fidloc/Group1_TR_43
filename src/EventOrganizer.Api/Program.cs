using System.Text.Json;
using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Infrastructure;
using EventOrganizer.Api.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

var app = builder.Build();

app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (BadHttpRequestException)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new ApiError("invalid_request", "Некоректний запит."));
    }
});

// Спільні колекції в пам'яті сервера (v1.0)
var gate = new object();
var users = new List<User>();
var events = new List<Event>();
var participations = new List<Participation>();
var expenses = new List<Expense>();
var shares = new List<ExpenseShare>();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// --- Сюди учасники додають свої маршрути (UC-01/02, UC-07, UC-05, UC-09) ---

// UC-07: створення події
app.MapPost("/api/events", (JsonElement body) =>
{
    if (!EventManagement.TryReadCreate(body, out var request, out var code))
        return ApiErrors.Result(code!);

    lock (gate)
    {
        var error = EventManagement.TryCreate(events, participations, request!,
            DateTime.Now, out var created, out _);
        if (error is not null)
            return ApiErrors.Result(error);

        return Results.Created($"/api/events/{created!.Id}",
            EventManagement.ToDetails(created, participations));
    }
});

// Список подій (?userId — лише події користувача)
app.MapGet("/api/events", (int? userId) =>
{
    lock (gate)
    {
        var list = events
            .Where(e => userId is null ||
                        participations.Any(p => p.EventId == e.Id && p.UserId == userId))
            .OrderBy(e => e.Id)
            .Select(e => EventManagement.ToDetails(e, participations))
            .ToArray();
        return Results.Ok(list);
    }
});

// UC-03: перегляд події
app.MapGet("/api/events/{eventId}", (string eventId) =>
{
    if (!int.TryParse(eventId, out var id) || id <= 0)
        return ApiErrors.Result("invalid_request");

    lock (gate)
    {
        var ev = events.SingleOrDefault(e => e.Id == id);
        if (ev is null)
            return ApiErrors.Result("event_not_found");

        return Results.Ok(EventManagement.ToDetails(ev, participations));
    }
});

// UC-04: приєднання за кодом
app.MapPost("/api/events/join", (JsonElement body) =>
{
    if (!EventManagement.TryReadJoin(body, out var request))
        return ApiErrors.Result("invalid_request");

    lock (gate)
    {
        var error = EventManagement.TryJoin(events, participations, request!,
            DateTime.Now, out var ev, out var participation);
        if (error is not null)
            return ApiErrors.Result(error);

        return Results.Created($"/api/events/{ev!.Id}",
            EventManagement.ToParticipant(participation!));
    }
});

// UC-10 / UC-11: закриття і скасування
IResult ChangeEventStatus(string eventId, JsonElement body, EventStatus target)
{
    if (!int.TryParse(eventId, out var id) || id <= 0 ||
        !EventManagement.TryReadUserId(body, out var userId))
        return ApiErrors.Result("invalid_request");

    lock (gate)
    {
        var error = EventManagement.TryChangeStatus(events, participations, id, userId, target, out var ev);
        if (error is not null)
            return ApiErrors.Result(error);

        return Results.Ok(EventManagement.ToDetails(ev!, participations));
    }
}

app.MapPost("/api/events/{eventId}/close", (string eventId, JsonElement body) =>
    ChangeEventStatus(eventId, body, EventStatus.Closed));

app.MapPost("/api/events/{eventId}/cancel", (string eventId, JsonElement body) =>
    ChangeEventStatus(eventId, body, EventStatus.Cancelled));

app.Run();

public partial class Program { }
