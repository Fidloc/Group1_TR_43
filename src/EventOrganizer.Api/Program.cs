using System.Text.Json;
using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Data;
using EventOrganizer.Api.Data.Interfaces;
using EventOrganizer.Api.Data.Repositories;
using EventOrganizer.Api.Endpoints;
using EventOrganizer.Api.Services.Implementations;
using EventOrganizer.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Налаштування серіалізації JSON у camelCase
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

builder.Services.AddRazorPages();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var dbPath = Path.Combine(builder.Environment.ContentRootPath, "eventorganizer.db");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Реєстрація розділені репозиторії SQLite у DI (ISP)
builder.Services.AddScoped<IUserRepository, SqliteUserRepository>();
builder.Services.AddScoped<IEventRepository, SqliteEventRepository>();
builder.Services.AddScoped<IExpenseRepository, SqliteExpenseRepository>();

// Реєстрація сервіси прикладної та предметної логіки у DI (BLL)
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();

var app = builder.Build();

// Автоматичне застосування міграцій бази даних при запуску
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseStaticFiles();
app.UseSession();

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

// Підключення модульних груп ендпоінтів 
app.MapUserEndpoints();
app.MapEventEndpoints();
app.MapExpenseEndpoints();
app.MapDistributionEndpoints();

// Razor Pages
app.MapRazorPages();

app.Run();

public partial class Program { }
