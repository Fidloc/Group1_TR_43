using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Data;
using EventOrganizer.Api.Data.Repositories;
using EventOrganizer.Api.Models;
using EventOrganizer.Api.Services.Implementations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EventOrganizer.Tests;

public class EventFilterTests
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

    [Fact] // US-02: Фільтрація подій за статусом (Created / Closed / Cancelled)
    public async Task EventService_GetEvents_FiltersByStatus()
    {
        await using var pair = new DbLease(CreateSqliteDatabase());
        var userRepo = new SqliteUserRepository(pair.Db);
        var eventRepo = new SqliteEventRepository(pair.Db);
        var service = new EventService(eventRepo, userRepo);

        var organizer = await userRepo.AddUserAsync(new User { Email = "org@test.com", Name = "Марія", PasswordHash = "1" });

        var (activeEv, _) = await service.CreateEventAsync(
            new CreateEventRequest(organizer.Id, "Активна зустріч", DateTime.Today.AddDays(5), "Київ", ""));
        var (closedEv, _) = await service.CreateEventAsync(
            new CreateEventRequest(organizer.Id, "Завершена поїздка", DateTime.Today.AddDays(6), "Львів", ""));
        var (cancelledEv, _) = await service.CreateEventAsync(
            new CreateEventRequest(organizer.Id, "Скасований пікнік", DateTime.Today.AddDays(7), "Одеса", ""));

        await service.ChangeStatusAsync(closedEv!.Id, organizer.Id, EventStatus.Closed);
        await service.ChangeStatusAsync(cancelledEv!.Id, organizer.Id, EventStatus.Cancelled);

        var createdList = await service.GetEventsAsync(organizer.Id, EventStatus.Created);
        var closedList = await service.GetEventsAsync(organizer.Id, EventStatus.Closed);
        var cancelledList = await service.GetEventsAsync(organizer.Id, EventStatus.Cancelled);

        Assert.Single(createdList);
        Assert.Equal(activeEv!.Id, createdList[0].Id);

        Assert.Single(closedList);
        Assert.Equal(closedEv.Id, closedList[0].Id);

        Assert.Single(cancelledList);
        Assert.Equal(cancelledEv.Id, cancelledList[0].Id);
    }

    [Fact] // US-02: Пошук за частиною назви без урахування регістру та зайвих пробілів
    public async Task EventService_GetEvents_SearchesByTitleCaseInsensitive()
    {
        await using var pair = new DbLease(CreateSqliteDatabase());
        var userRepo = new SqliteUserRepository(pair.Db);
        var eventRepo = new SqliteEventRepository(pair.Db);
        var service = new EventService(eventRepo, userRepo);

        var organizer = await userRepo.AddUserAsync(new User { Email = "org@test.com", Name = "Марія", PasswordHash = "1" });

        await service.CreateEventAsync(
            new CreateEventRequest(organizer.Id, "Похід у Карпати", DateTime.Today.AddDays(5), "Яремче", ""));
        await service.CreateEventAsync(
            new CreateEventRequest(organizer.Id, "Вечір настільних ігор", DateTime.Today.AddDays(6), "Київ", ""));

        var found = await service.GetEventsAsync(organizer.Id, search: "  карпати ");

        Assert.Single(found);
        Assert.Equal("Похід у Карпати", found[0].Title);
    }

    [Fact] // US-02: Комбінована фільтрація за користувачем, статусом і назвою
    public async Task EventService_GetEvents_CombinesUserStatusAndSearchFilters()
    {
        await using var pair = new DbLease(CreateSqliteDatabase());
        var userRepo = new SqliteUserRepository(pair.Db);
        var eventRepo = new SqliteEventRepository(pair.Db);
        var service = new EventService(eventRepo, userRepo);

        var user1 = await userRepo.AddUserAsync(new User { Email = "u1@test.com", Name = "Марія", PasswordHash = "1" });
        var user2 = await userRepo.AddUserAsync(new User { Email = "u2@test.com", Name = "Іван", PasswordHash = "1" });

        var (ev1, _) = await service.CreateEventAsync(
            new CreateEventRequest(user1.Id, "Похід на Говерлу", DateTime.Today.AddDays(5), "Карпати", ""));
        var (ev2, _) = await service.CreateEventAsync(
            new CreateEventRequest(user1.Id, "Похід вихідного дня", DateTime.Today.AddDays(6), "Київ", ""));
        await service.CreateEventAsync(
            new CreateEventRequest(user2.Id, "Похід Івана", DateTime.Today.AddDays(7), "Львів", ""));

        await service.ChangeStatusAsync(ev2!.Id, user1.Id, EventStatus.Closed);

        var filtered = await service.GetEventsAsync(user1.Id, EventStatus.Created, "похід");

        Assert.Single(filtered);
        Assert.Equal(ev1!.Id, filtered[0].Id);
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