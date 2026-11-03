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
            PasswordHash = request.Password, // Для v1.0 зберігаємо як є (або пишіть хеш, якщо координатор додав функцію)
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

app.Run();

public partial class Program { }
