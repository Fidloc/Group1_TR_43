using System.Text.Json;
using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Data;
using EventOrganizer.Api.Endpoints;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Налаштування серіалізації JSON у camelCase
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

// Підключення підтримки Razor Pages
builder.Services.AddRazorPages();

// Налаштування бази даних SQLite та репозиторію
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "eventorganizer.db");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Реєструємо репозиторій SQLite у DI
builder.Services.AddScoped<IEventOrganizerRepository, SqliteEventOrganizerRepository>();

var app = builder.Build();

// Автоматичне застосування міграцій бази даних при запуску
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseStaticFiles();

// Middleware перехоплення некоректного JSON
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

// Перевірка працездатності сервера
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Підключення модульних груп ендпоінтів (Розподіл обов'язків)
app.MapUserEndpoints();
app.MapEventEndpoints();
app.MapExpenseEndpoints();
app.MapDistributionEndpoints();

// Razor Pages
app.MapRazorPages();

app.Run();

public partial class Program { }
