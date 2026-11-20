using System.Text.Json;
using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Data;
using EventOrganizer.Api.Data.Repositories;
using EventOrganizer.Api.Models;
using EventOrganizer.Api.Services;
using EventOrganizer.Api.Services.Implementations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EventOrganizer.Tests;

// US-03: службові тести сценарію «витрата + бюджет + миттєвий розподіл» на SQLite :memory:
public class ExpenseBudgetServiceTests
{
    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public AppDbContext Db { get; }
        public EventService Events { get; }
        public ExpenseService Expenses { get; }
        public SqliteExpenseRepository ExpenseRepo { get; }
        public EventDetailsDto Event { get; private set; } = null!;
        public int OrganizerUserId { get; private set; }
        public int OrganizerParticipationId { get; private set; }
        public int GuestParticipationId { get; private set; }

        private Fixture(SqliteConnection connection, AppDbContext db)
        {
            _connection = connection;
            Db = db;
            var userRepo = new SqliteUserRepository(db);
            var eventRepo = new SqliteEventRepository(db);
            ExpenseRepo = new SqliteExpenseRepository(db);
            Events = new EventService(eventRepo, userRepo, ExpenseRepo);
            Expenses = new ExpenseService(ExpenseRepo, eventRepo, userRepo);
        }

        public static async Task<Fixture> CreateAsync(decimal? budgetLimit)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
            var db = new AppDbContext(options);
            db.Database.EnsureCreated();

            var fixture = new Fixture(connection, db);
            var userRepo = new SqliteUserRepository(db);

            var organizer = await userRepo.AddUserAsync(new User { Email = "org@test.com", Name = "Орг", PasswordHash = "1" });
            var guest = await userRepo.AddUserAsync(new User { Email = "guest@test.com", Name = "Гість", PasswordHash = "1" });
            fixture.OrganizerUserId = organizer.Id;

            var (created, error) = await fixture.Events.CreateEventAsync(
                new CreateEventRequest(organizer.Id, "Похід", DateTime.Today.AddDays(5), "Карпати", "", budgetLimit));
            Assert.Null(error);
            fixture.Event = created!;
            fixture.OrganizerParticipationId = created!.Participants.Single().ParticipationId;

            var (_, guestParticipant, joinError) = await fixture.Events.JoinEventAsync(new JoinEventRequest(guest.Id, created.InviteCode));
            Assert.Null(joinError);
            fixture.GuestParticipationId = guestParticipant!.ParticipationId;
            return fixture;
        }

        public Task<(CreateExpenseResultDto? Result, string? Error)> AddAsync(decimal amount, params int[] shareIds) =>
            Expenses.AddExpenseAsync(Event.Id, new CreateExpenseRequest(
                OrganizerUserId, OrganizerParticipationId, amount, "Витрата", shareIds.Length == 0 ? null : shareIds));

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    [Fact] // Без ліміту: дозволено без попередження
    public async Task AddExpense_WithoutLimit_IsAllowedWithoutWarning()
    {
        await using var f = await Fixture.CreateAsync(null);

        var (result, error) = await f.AddAsync(1200m);

        Assert.Null(error);
        Assert.Null(result!.Warning);
        Assert.Equal(1200m, await f.ExpenseRepo.GetTotalSpentByEventIdAsync(f.Event.Id));
    }

    [Fact] // Рівно 80%: дозволено з попередженням, витрата збережена
    public async Task AddExpense_AtEightyPercent_SavesWithWarning()
    {
        await using var f = await Fixture.CreateAsync(1000m);
        Assert.Null((await f.AddAsync(500m)).Error);

        var (result, error) = await f.AddAsync(300m);

        Assert.Null(error);
        Assert.NotNull(result!.Warning);
        Assert.Contains("80.0%", result.Warning);
        Assert.Equal(800m, await f.ExpenseRepo.GetTotalSpentByEventIdAsync(f.Event.Id));
    }

    [Fact] // Рівно 100%: ще дозволено (з попередженням)
    public async Task AddExpense_AtExactLimit_SavesWithWarning()
    {
        await using var f = await Fixture.CreateAsync(1000m);
        Assert.Null((await f.AddAsync(700m)).Error);

        var (result, error) = await f.AddAsync(300m);

        Assert.Null(error);
        Assert.NotNull(result!.Warning);
        Assert.Equal(1000m, await f.ExpenseRepo.GetTotalSpentByEventIdAsync(f.Event.Id));
    }

    [Fact] // 100% + 1 коп.: відмова, у БД нічого не з'являється ні в Expenses, ні в ExpenseShares
    public async Task AddExpense_OverLimit_IsRejectedAndNothingSaved()
    {
        await using var f = await Fixture.CreateAsync(1000m);
        Assert.Null((await f.AddAsync(700m)).Error);

        var (result, error) = await f.AddAsync(300.01m, f.OrganizerParticipationId, f.GuestParticipationId);

        Assert.Null(result);
        Assert.Equal("budget_exceeded", error);
        Assert.Equal(700m, await f.ExpenseRepo.GetTotalSpentByEventIdAsync(f.Event.Id));
        Assert.Equal(1, await f.Db.Expenses.CountAsync());
        Assert.Equal(0, await f.Db.ExpenseShares.CountAsync());
    }

    [Fact] // Миттєвий розподіл: витрата і частки зберігаються разом, сума часток дорівнює сумі витрати
    public async Task AddExpense_WithShares_SavesExpenseAndSharesTogether()
    {
        await using var f = await Fixture.CreateAsync(null);

        var (result, error) = await f.AddAsync(100.01m, f.OrganizerParticipationId, f.GuestParticipationId);

        Assert.Null(error);
        Assert.Equal(2, result!.Shares.Length);
        Assert.Equal(100.01m, result.Shares.Sum(s => s.Amount));
        Assert.All(result.Shares, s => Assert.Equal(result.Expense.Id, s.ExpenseId));
        Assert.Equal(1, await f.Db.Expenses.CountAsync());
        Assert.Equal(2, await f.Db.ExpenseShares.CountAsync());
    }

    [Fact] // Без ShareParticipantIds частки не створюються
    public async Task AddExpense_WithoutShares_CreatesNoShares()
    {
        await using var f = await Fixture.CreateAsync(null);

        var (result, error) = await f.AddAsync(50m);

        Assert.Null(error);
        Assert.Empty(result!.Shares);
        Assert.Equal(0, await f.Db.ExpenseShares.CountAsync());
    }

    [Fact] // Чужий учасник у розподілі: відмова, витрата теж не зберігається
    public async Task AddExpense_WithUnknownShareParticipant_FailsWithoutSaving()
    {
        await using var f = await Fixture.CreateAsync(null);

        var (result, error) = await f.AddAsync(50m, f.OrganizerParticipationId, 9999);

        Assert.Null(result);
        Assert.Equal("participant_not_found", error);
        Assert.Equal(0, await f.Db.Expenses.CountAsync());
    }

    [Fact] // Подія з бюджетом: ліміт і TotalSpent повертаються в деталях
    public async Task GetEventById_ReturnsBudgetLimitAndTotalSpent()
    {
        await using var f = await Fixture.CreateAsync(1000m);
        Assert.Null((await f.AddAsync(250m)).Error);

        var (details, error) = await f.Events.GetEventByIdAsync(f.Event.Id);

        Assert.Null(error);
        Assert.Equal(1000m, details!.BudgetLimit);
        Assert.Equal(250m, details.TotalSpent);
    }

    [Fact] // Некоректний бюджет при створенні події
    public async Task CreateEvent_WithInvalidBudget_ReturnsInvalidBudget()
    {
        await using var f = await Fixture.CreateAsync(null);

        var (created, error) = await f.Events.CreateEventAsync(
            new CreateEventRequest(f.OrganizerUserId, "Погана подія", DateTime.Today.AddDays(2), "", "", -5m));

        Assert.Null(created);
        Assert.Equal("invalid_budget", error);
        Assert.Equal(1, await f.Db.Events.CountAsync());
    }

    [Fact] // Оновлення: BudgetLimit змінюється, а null залишає наявний бюджет
    public async Task UpdateEvent_ChangesBudget_AndNullKeepsIt()
    {
        await using var f = await Fixture.CreateAsync(1000m);
        var date = DateTime.Today.AddDays(6).ToString("yyyy-MM-dd");

        var (changed, error1) = await f.Events.UpdateEventAsync(f.Event.Id,
            new UpdateEventRequest(f.OrganizerUserId, "Похід", date, "Карпати", "", 2000m));
        Assert.Null(error1);
        Assert.Equal(2000m, changed!.BudgetLimit);

        var (kept, error2) = await f.Events.UpdateEventAsync(f.Event.Id,
            new UpdateEventRequest(f.OrganizerUserId, "Похід 2", date, "Карпати", ""));
        Assert.Null(error2);
        Assert.Equal(2000m, kept!.BudgetLimit);
    }

    [Fact] // Розбір JSON: необов'язковий список учасників для розподілу
    public void TryRead_ParsesShareParticipantIds()
    {
        using var doc = JsonDocument.Parse(
            """{"userId":1,"payerParticipationId":2,"amount":90,"description":"Піца","shareParticipantIds":[2,3]}""");

        var ok = ExpenseRegistration.TryRead(doc.RootElement, out var request, out var code);

        Assert.True(ok);
        Assert.Null(code);
        Assert.Equal(new[] { 2, 3 }, request!.ShareParticipantIds);
    }

    [Theory]
    [InlineData("""{"userId":1,"payerParticipationId":2,"amount":90,"shareParticipantIds":"2"}""")]
    [InlineData("""{"userId":1,"payerParticipationId":2,"amount":90,"shareParticipantIds":[2,"x"]}""")]
    [InlineData("""{"userId":1,"payerParticipationId":2,"amount":90,"shareParticipantIds":[0]}""")]
    public void TryRead_RejectsMalformedShareParticipantIds(string json)
    {
        using var doc = JsonDocument.Parse(json);

        var ok = ExpenseRegistration.TryRead(doc.RootElement, out var request, out var code);

        Assert.False(ok);
        Assert.Null(request);
        Assert.Equal("invalid_request", code);
    }

    [Fact] // Розбір JSON створення події: budgetLimit необов'язковий
    public void TryReadCreate_ParsesOptionalBudgetLimit()
    {
        using var withBudget = JsonDocument.Parse(
            """{"organizerUserId":1,"title":"Подія","date":"2030-01-01","budgetLimit":1500.50}""");
        using var withoutBudget = JsonDocument.Parse(
            """{"organizerUserId":1,"title":"Подія","date":"2030-01-01"}""");
        using var badBudget = JsonDocument.Parse(
            """{"organizerUserId":1,"title":"Подія","date":"2030-01-01","budgetLimit":"багато"}""");

        Assert.True(EventManagement.TryReadCreate(withBudget.RootElement, out var a, out _));
        Assert.Equal(1500.50m, a!.BudgetLimit);

        Assert.True(EventManagement.TryReadCreate(withoutBudget.RootElement, out var b, out _));
        Assert.Null(b!.BudgetLimit);

        Assert.False(EventManagement.TryReadCreate(badBudget.RootElement, out _, out var code));
        Assert.Equal("invalid_budget", code);
    }
}
