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

// Реєстрація витрати (UC-05; FR-09, FR-10, FR-15)
app.MapPost("/api/events/{eventId}/expenses", (string eventId, JsonElement body) =>
{
    if (!int.TryParse(eventId, out var id) || id <= 0)
        return ApiErrors.Result("invalid_request");

    if (!ExpenseRegistration.TryRead(body, out var request, out var code))
        return ApiErrors.Result(code!);

    lock (gate)
    {
        var error = ExpenseRegistration.TryAdd(events, participations, expenses,
            id, request!, DateTime.UtcNow, out var expense);
        if (error is not null)
            return ApiErrors.Result(error);

        return Results.Created($"/api/events/{id}/expenses/{expense!.Id}", expense);
    }
});

// Облік витрат події (показ після UC-05, AC-09)
app.MapGet("/api/events/{eventId}/expenses", (string eventId) =>
{
    if (!int.TryParse(eventId, out var id) || id <= 0)
        return ApiErrors.Result("invalid_request");

    lock (gate)
    {
        if (!events.Any(e => e.Id == id))
            return ApiErrors.Result("event_not_found");

        return Results.Ok(expenses.Where(x => x.EventId == id).OrderBy(x => x.Id).ToArray());
    }
});

app.Run();

public partial class Program { }
