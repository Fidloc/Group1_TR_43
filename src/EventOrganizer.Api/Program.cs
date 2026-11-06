using System.Text.Json;
using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Infrastructure;
using EventOrganizer.Api.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
builder.Services.AddRazorPages();

var app = builder.Build();

app.UseStaticFiles();

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

var gate = new object();
var users = new List<User>();
var events = new List<Event>();
var participations = new List<Participation>();
var expenses = new List<Expense>();
var shares = new List<ExpenseShare>();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Реєстрація (UC-01)
app.MapPost("/api/users/register", (RegisterUserRequest request) =>
{
    lock (gate)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Results.BadRequest(new { error = "invalid_request", message = "Email та пароль обов'язкові" });

        if (users.Any(u => u.Email == request.Email))
            return Results.Conflict(new { error = "email_already_exists", message = "Користувач з таким email вже існує" });

        var newUser = new User 
        { 
            Id = users.Count + 1, 
            Email = request.Email, 
            PasswordHash = request.Password,
            Name = request.Name 
        };
        users.Add(newUser);
        return Results.Created($"/api/users/{newUser.Id}", newUser);
    }
});

// Вхід у систему (UC-02)
app.MapPost("/api/users/login", (LoginRequest request) =>
{
    lock (gate)
    {
        var user = users.FirstOrDefault(u => u.Email == request.Email && u.PasswordHash == request.Password);
        if (user == null)
            return Results.Unauthorized(); // 401 invalid_credentials

        return Results.Ok(new { message = "Вхід успішний", userId = user.Id });
    }
});

app.MapGet("/api/users", () =>
{
    lock (gate) return Results.Ok(users.Select(u => new UserDto(u.Id, u.Email, u.Name)).ToArray());
});

// UC-07
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

// Список подій
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

// UC-03
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

// UC-04
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

// UC-10 UC-11
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

app.MapGet("/api/events/{eventId:int}/participants", (int eventId) =>
{
    lock (gate)
    {
        if (!events.Any(e => e.Id == eventId))
            return ApiErrors.Result("event_not_found");

        var list = participations
            .Where(p => p.EventId == eventId)
            .Select(p => new
            {
                p.Id,
                p.UserId,
                UserName = users.FirstOrDefault(u => u.Id == p.UserId)?.Name ?? "",
                Role = p.Role.ToString()
            })
            .ToArray();
        return Results.Ok(list);
    }
});

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

app.MapPost("/api/events/{eventId:int}/expenses/{expenseId:int}/distribute",
    (int eventId, int expenseId, DistributeExpenseRequest request) =>
{
    if (eventId <= 0 || expenseId <= 0 || request.OrganizerUserId <= 0)
        return ApiErrors.Result("invalid_request");

    var selectedIds = request.ParticipantIds?.Distinct().ToArray() ?? Array.Empty<int>();
    if (selectedIds.Length == 0)
        return ApiErrors.Result("no_participants_selected");

    lock (gate)
    {
        var ev = events.SingleOrDefault(e => e.Id == eventId);
        if (ev is null)
            return ApiErrors.Result("event_not_found");

        if (ev.Status != EventStatus.Created)
            return ApiErrors.Result("event_not_active");

        var isOrganizer = participations.Any(p =>
            p.EventId == eventId &&
            p.UserId == request.OrganizerUserId &&
            p.Role == ParticipantRole.Organizer);
        if (!isOrganizer)
            return ApiErrors.Result("forbidden_not_organizer");

        var expense = expenses.SingleOrDefault(e => e.Id == expenseId && e.EventId == eventId);
        if (expense is null)
            return ApiErrors.Result("expense_not_found");

        var validMembers = participations
            .Where(p => p.EventId == eventId)
            .Select(p => p.Id)
            .ToHashSet();
        if (selectedIds.Any(id => !validMembers.Contains(id)))
            return ApiErrors.Result("participant_not_found");

        var nextId = shares.Count == 0 ? 1 : shares.Max(s => s.Id) + 1;
        var calculated = ExpenseCalculator.SplitEqually(expense.Id, expense.Amount, selectedIds, nextId);

        if (calculated.Sum(s => s.Amount) != expense.Amount)
            return ApiErrors.Result("sum_mismatch");

        shares.RemoveAll(s => s.ExpenseId == expense.Id);
        shares.AddRange(calculated);

        return Results.Ok(calculated.ToArray());
    }
});

app.MapGet("/api/events/{eventId:int}/expenses/{expenseId:int}/shares",
    (int eventId, int expenseId) =>
{
    lock (gate)
    {
        if (!events.Any(e => e.Id == eventId))
            return ApiErrors.Result("event_not_found");
        if (!expenses.Any(e => e.Id == expenseId && e.EventId == eventId))
            return ApiErrors.Result("expense_not_found");

        return Results.Ok(shares.Where(s => s.ExpenseId == expenseId).ToArray());
    }
});

app.MapGet("/api/events/{eventId:int}/balances", (int eventId) =>
{
    lock (gate)
    {
        if (!events.Any(e => e.Id == eventId))
            return ApiErrors.Result("event_not_found");

        var result = ExpenseCalculator.GetBalances(eventId, users, participations, expenses, shares);
        return Results.Ok(result);
    }
});

app.MapRazorPages();

app.Run();

public partial class Program { }
