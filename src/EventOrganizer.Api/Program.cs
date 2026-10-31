using System.Text.Json;
using EventOrganizer.Api.Contracts;
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

app.Run();

public partial class Program { }
