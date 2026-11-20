using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Data;
using EventOrganizer.Api.Data.Repositories;
using EventOrganizer.Api.Models;
using EventOrganizer.Api.Services.Implementations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EventOrganizer.Tests;

public class RepositoryAndEditEventTests
{
    private static (SqliteConnection Connection, AppDbContext Db) CreateSqliteDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return (connection, db);
    }

    [Fact]
    public async Task SqliteRepo_AddAndRetrieveUser_Succeeds()
    {
        await using var pair = new DbLease(CreateSqliteDatabase());
        var userRepo = new SqliteUserRepository(pair.Db);

        var user = new User { Email = "test@example.com", Name = "Тестер", PasswordHash = "secret" };
        var added = await userRepo.AddUserAsync(user);
        Assert.True(added.Id > 0);

        var retrieved = await userRepo.GetUserByEmailAsync("TEST@example.com");
        Assert.NotNull(retrieved);
        Assert.Equal("Тестер", retrieved.Name);
    }

    [Fact]
    public async Task SqliteRepo_AddAndEditEvent_Succeeds()
    {
        await using var pair = new DbLease(CreateSqliteDatabase());
        var eventRepo = new SqliteEventRepository(pair.Db);

        var ev = new Event
        {
            Title = "Стара назва",
            Date = DateTime.Today.AddDays(5),
            Place = "Київ",
            Description = "Опис",
            InviteCode = "ABC12345",
            Status = EventStatus.Created
        };

        var created = await eventRepo.AddEventAsync(ev);
        Assert.True(created.Id > 0);

        created.Title = "Оновлена назва";
        created.Place = "Львів";
        await eventRepo.UpdateEventAsync(created);

        var updated = await eventRepo.GetEventByIdAsync(created.Id);
        Assert.NotNull(updated);
        Assert.Equal("Оновлена назва", updated.Title);
        Assert.Equal("Львів", updated.Place);
    }

    [Fact]
    public async Task SqliteRepo_AddExpensesAcrossMultipleEvents_AndReplaceShares_Succeeds()
    {
        await using var pair = new DbLease(CreateSqliteDatabase());
        var userRepo = new SqliteUserRepository(pair.Db);
        var eventRepo = new SqliteEventRepository(pair.Db);
        var expenseRepo = new SqliteExpenseRepository(pair.Db);

        var u1 = await userRepo.AddUserAsync(new User { Email = "u1@test.com", Name = "Маргарита", PasswordHash = "1" });
        var u2 = await userRepo.AddUserAsync(new User { Email = "u2@test.com", Name = "Марія", PasswordHash = "1" });

        var ev1 = await eventRepo.AddEventWithOrganizerAsync(
            new Event { Title = "Подія 1", Date = DateTime.Today.AddDays(3), InviteCode = "CODE0001" },
            new Participation { UserId = u1.Id, Role = ParticipantRole.Organizer, JoinedAt = DateTime.UtcNow });
        var p2InEv1 = await eventRepo.AddParticipationAsync(
            new Participation { EventId = ev1.Id, UserId = u2.Id, Role = ParticipantRole.Participant, JoinedAt = DateTime.UtcNow });
        var parts1 = await eventRepo.GetParticipationsByEventIdAsync(ev1.Id);

        var ev2 = await eventRepo.AddEventWithOrganizerAsync(
            new Event { Title = "Подія 2", Date = DateTime.Today.AddDays(4), InviteCode = "CODE0002" },
            new Participation { UserId = u2.Id, Role = ParticipantRole.Organizer, JoinedAt = DateTime.UtcNow });
        var parts2 = await eventRepo.GetParticipationsByEventIdAsync(ev2.Id);

        // Перевірка автоінкременту витрат у різних подіях (відсутність UNIQUE constraint failed)
        var expense1 = await expenseRepo.AddExpenseAsync(new Expense
        {
            EventId = ev1.Id,
            PayerParticipationId = parts1[0].Id,
            Amount = 150m,
            Description = "Піца",
            CreatedAt = DateTime.UtcNow
        });
        var expense2 = await expenseRepo.AddExpenseAsync(new Expense
        {
            EventId = ev2.Id,
            PayerParticipationId = parts2[0].Id,
            Amount = 200m,
            Description = "Напої",
            CreatedAt = DateTime.UtcNow
        });

        Assert.True(expense1.Id > 0);
        Assert.True(expense2.Id > expense1.Id);

        var shares = new List<ExpenseShare>
        {
            new() { ExpenseId = expense1.Id, ParticipationId = parts1[0].Id, Amount = 75m },
            new() { ExpenseId = expense1.Id, ParticipationId = p2InEv1.Id, Amount = 75m }
        };

        await expenseRepo.ReplaceSharesAsync(expense1.Id, shares);

        var retrievedShares = await expenseRepo.GetSharesByExpenseIdAsync(expense1.Id);
        Assert.Equal(2, retrievedShares.Count);
        Assert.Equal(150m, retrievedShares.Sum(s => s.Amount));
    }

    [Fact] // UC-08 AC-01: Успішне редагування події Організатором із збереженням в SQLite
    public async Task EventService_UpdateEvent_Succeeds_WhenOrganizerAndValidData()
    {
        await using var pair = new DbLease(CreateSqliteDatabase());
        var userRepo = new SqliteUserRepository(pair.Db);
        var eventRepo = new SqliteEventRepository(pair.Db);
        var service = new EventService(eventRepo, userRepo, new SqliteExpenseRepository(pair.Db));

        var organizer = await userRepo.AddUserAsync(new User { Email = "org@test.com", Name = "Організатор", PasswordHash = "1" });
        var (created, createErr) = await service.CreateEventAsync(
            new CreateEventRequest(organizer.Id, "Похід", DateTime.Today.AddDays(7), "Київ", "Стартовий опис"));
        Assert.Null(createErr);
        Assert.NotNull(created);

        var newDate = DateTime.Today.AddDays(10).ToString("yyyy-MM-dd");
        var (updated, updateErr) = await service.UpdateEventAsync(
            created.Id,
            new UpdateEventRequest(organizer.Id, "Оновлений похід", newDate, "Карпати", "Новий опис"));

        Assert.Null(updateErr);
        Assert.NotNull(updated);
        Assert.Equal("Оновлений похід", updated.Title);
        Assert.Equal("Карпати", updated.Place);
        Assert.Equal("Новий опис", updated.Description);

        var persisted = await eventRepo.GetEventByIdAsync(created.Id);
        Assert.NotNull(persisted);
        Assert.Equal("Оновлений похід", persisted.Title);
    }

    [Theory] // UC-08 AC-02: Відхилення порожньої назви
    [InlineData("")]
    [InlineData("   ")]
    public async Task EventService_UpdateEvent_ReturnsMissingTitle_WhenTitleIsEmpty(string badTitle)
    {
        await using var pair = new DbLease(CreateSqliteDatabase());
        var userRepo = new SqliteUserRepository(pair.Db);
        var eventRepo = new SqliteEventRepository(pair.Db);
        var service = new EventService(eventRepo, userRepo, new SqliteExpenseRepository(pair.Db));

        var organizer = await userRepo.AddUserAsync(new User { Email = "org@test.com", Name = "Організатор", PasswordHash = "1" });
        var (created, _) = await service.CreateEventAsync(
            new CreateEventRequest(organizer.Id, "Похід", DateTime.Today.AddDays(7), "Київ", "Опис"));

        var (updated, err) = await service.UpdateEventAsync(
            created!.Id,
            new UpdateEventRequest(organizer.Id, badTitle, DateTime.Today.AddDays(8).ToString("yyyy-MM-dd"), "Львів", "Опис"));

        Assert.Null(updated);
        Assert.Equal("missing_title", err);
    }

    [Fact] // UC-08 AC-02: Відхилення дати в минулому
    public async Task EventService_UpdateEvent_ReturnsInvalidDate_WhenDateInPast()
    {
        await using var pair = new DbLease(CreateSqliteDatabase());
        var userRepo = new SqliteUserRepository(pair.Db);
        var eventRepo = new SqliteEventRepository(pair.Db);
        var service = new EventService(eventRepo, userRepo, new SqliteExpenseRepository(pair.Db));

        var organizer = await userRepo.AddUserAsync(new User { Email = "org@test.com", Name = "Організатор", PasswordHash = "1" });
        var (created, _) = await service.CreateEventAsync(
            new CreateEventRequest(organizer.Id, "Похід", DateTime.Today.AddDays(7), "Київ", "Опис"));

        var (updated, err) = await service.UpdateEventAsync(
            created!.Id,
            new UpdateEventRequest(organizer.Id, "Нова назва", DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd"), "Львів", "Опис"));

        Assert.Null(updated);
        Assert.Equal("invalid_date", err);
    }

    [Fact] // UC-08 AC-03: Захист прав доступу (не Організатор -> forbidden_not_organizer)
    public async Task EventService_UpdateEvent_ReturnsForbidden_WhenNotOrganizer()
    {
        await using var pair = new DbLease(CreateSqliteDatabase());
        var userRepo = new SqliteUserRepository(pair.Db);
        var eventRepo = new SqliteEventRepository(pair.Db);
        var service = new EventService(eventRepo, userRepo, new SqliteExpenseRepository(pair.Db));

        var organizer = await userRepo.AddUserAsync(new User { Email = "org@test.com", Name = "Організатор", PasswordHash = "1" });
        var participant = await userRepo.AddUserAsync(new User { Email = "guest@test.com", Name = "Учасник", PasswordHash = "1" });
        var (created, _) = await service.CreateEventAsync(
            new CreateEventRequest(organizer.Id, "Похід", DateTime.Today.AddDays(7), "Київ", "Опис"));

        await service.JoinEventAsync(new JoinEventRequest(participant.Id, created!.InviteCode));

        var (updated, err) = await service.UpdateEventAsync(
            created.Id,
            new UpdateEventRequest(participant.Id, "Зловмисна назва", DateTime.Today.AddDays(8).ToString("yyyy-MM-dd"), "Київ", "Опис"));

        Assert.Null(updated);
        Assert.Equal("forbidden_not_organizer", err);
    }

    [Theory] // UC-08 AC-04: Захист неактивної події (Closed / Cancelled -> event_not_active)
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Cancelled)]
    public async Task EventService_UpdateEvent_ReturnsEventNotActive_WhenClosedOrCancelled(EventStatus status)
    {
        await using var pair = new DbLease(CreateSqliteDatabase());
        var userRepo = new SqliteUserRepository(pair.Db);
        var eventRepo = new SqliteEventRepository(pair.Db);
        var service = new EventService(eventRepo, userRepo, new SqliteExpenseRepository(pair.Db));

        var organizer = await userRepo.AddUserAsync(new User { Email = "org@test.com", Name = "Організатор", PasswordHash = "1" });
        var (created, _) = await service.CreateEventAsync(
            new CreateEventRequest(organizer.Id, "Похід", DateTime.Today.AddDays(7), "Київ", "Опис"));

        await service.ChangeStatusAsync(created!.Id, organizer.Id, status);

        var (updated, err) = await service.UpdateEventAsync(
            created.Id,
            new UpdateEventRequest(organizer.Id, "Нова назва", DateTime.Today.AddDays(8).ToString("yyyy-MM-dd"), "Київ", "Опис"));

        Assert.Null(updated);
        Assert.Equal("event_not_active", err);
    }

    private sealed class DbLease((SqliteConnection Connection, AppDbContext Db) pair) : IAsyncDisposable
    {
        public AppDbContext Db { get; } = pair.Db;

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await pair.Connection.DisposeAsync();
        }
    }
}
