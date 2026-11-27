using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Data;
using EventOrganizer.Api.Data.Repositories;
using EventOrganizer.Api.Models;
using EventOrganizer.Api.Services;
using EventOrganizer.Api.Services.Implementations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EventOrganizer.Tests;

// US-04 / UC-12: план взаєморозрахунків (хто кому скільки переказує)
public class SettlementTests
{
    // ---------- Юніт-тести чистої функції CalculateSettlements ----------

    private static BalanceDto B(int id, string name, decimal balance) =>
        new BalanceDto(id, id, name, 0m, 0m, balance);

    // Після виконання всіх переказів баланс кожного учасника має стати нульовим
    private static void AssertSettlesEverything(BalanceDto[] balances, SettlementTransferDto[] transfers)
    {
        Assert.All(transfers, t => Assert.True(t.Amount > 0m));

        foreach (var b in balances)
        {
            var received = transfers.Where(t => t.ToParticipationId == b.ParticipationId).Sum(t => t.Amount);
            var sent = transfers.Where(t => t.FromParticipationId == b.ParticipationId).Sum(t => t.Amount);
            Assert.Equal(b.Balance, received - sent);
        }
    }

    [Fact] // Двоє учасників: один переказ на всю суму боргу
    public void CalculateSettlements_TwoParticipants_ReturnsSingleTransfer()
    {
        var balances = new[] { B(1, "Маргарита", 100m), B(2, "Іван", -100m) };

        var result = ExpenseCalculator.CalculateSettlements(balances);

        var transfer = Assert.Single(result);
        Assert.Equal(2, transfer.FromParticipationId);
        Assert.Equal("Іван", transfer.FromUserName);
        Assert.Equal(1, transfer.ToParticipationId);
        Assert.Equal("Маргарита", transfer.ToUserName);
        Assert.Equal(100m, transfer.Amount);
    }

    [Fact] // Приклад із UC-06: Маргарита +300, Іван -100, Настя -200
    public void CalculateSettlements_ThreeParticipants_ReturnsTwoTransfersToCreditor()
    {
        var balances = new[]
        {
            B(1, "Маргарита", 300m),
            B(2, "Іван", -100m),
            B(3, "Настя", -200m)
        };

        var result = ExpenseCalculator.CalculateSettlements(balances);

        Assert.Equal(2, result.Length);
        Assert.All(result, t => Assert.Equal(1, t.ToParticipationId));
        Assert.Contains(result, t => t.FromParticipationId == 3 && t.Amount == 200m);
        Assert.Contains(result, t => t.FromParticipationId == 2 && t.Amount == 100m);
        AssertSettlesEverything(balances, result);
    }

    [Fact] // Нерівний поділ із копійками: 100.00 на трьох = 33.34 / 33.33 / 33.33
    public void CalculateSettlements_UnevenSplitWithKopecks_KeepsExactAmounts()
    {
        var balances = new[]
        {
            B(1, "Маргарита", 66.66m),
            B(2, "Іван", -33.33m),
            B(3, "Настя", -33.33m)
        };

        var result = ExpenseCalculator.CalculateSettlements(balances);

        Assert.Equal(2, result.Length);
        Assert.All(result, t => Assert.Equal(33.33m, t.Amount));
        Assert.Equal(66.66m, result.Sum(t => t.Amount));
        AssertSettlesEverything(balances, result);
    }

    [Fact] // Кілька боржників і кілька кредиторів: усі баланси закриваються
    public void CalculateSettlements_MultipleDebtorsAndCreditors_SettlesEveryone()
    {
        var balances = new[]
        {
            B(1, "А", 50m),
            B(2, "Б", 50m),
            B(3, "В", -30m),
            B(4, "Г", -70m)
        };

        var result = ExpenseCalculator.CalculateSettlements(balances);

        Assert.Equal(3, result.Length);
        AssertSettlesEverything(balances, result);
    }

    [Fact] // Усі баланси нульові: переказів немає
    public void CalculateSettlements_AllBalancesZero_ReturnsEmpty()
    {
        var balances = new[] { B(1, "Маргарита", 0m), B(2, "Іван", 0m) };

        var result = ExpenseCalculator.CalculateSettlements(balances);

        Assert.Empty(result);
    }

    [Fact] // Порожній список балансів: переказів немає
    public void CalculateSettlements_NoBalances_ReturnsEmpty()
    {
        var result = ExpenseCalculator.CalculateSettlements(Array.Empty<BalanceDto>());

        Assert.Empty(result);
    }

    // ---------- Інтеграційні тести ExpenseService.GetSettlementsAsync на SQLite :memory: ----------

    private sealed record Member(int UserId, int ParticipationId);

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public AppDbContext Db { get; }
        public EventService Events { get; }
        public ExpenseService Expenses { get; }
        public EventDetailsDto Event { get; private set; } = null!;
        public Member Margarita { get; private set; } = null!;
        public Member Ivan { get; private set; } = null!;
        public Member Nastia { get; private set; } = null!;

        private Fixture(SqliteConnection connection, AppDbContext db)
        {
            _connection = connection;
            Db = db;
            var userRepo = new SqliteUserRepository(db);
            var eventRepo = new SqliteEventRepository(db);
            var expenseRepo = new SqliteExpenseRepository(db);
            Events = new EventService(eventRepo, userRepo, expenseRepo);
            Expenses = new ExpenseService(expenseRepo, eventRepo, userRepo);
        }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
            var db = new AppDbContext(options);
            db.Database.EnsureCreated();

            var fixture = new Fixture(connection, db);
            var userRepo = new SqliteUserRepository(db);

            var margarita = await userRepo.AddUserAsync(new User { Email = "m@test.com", Name = "Маргарита", PasswordHash = "1" });
            var ivan = await userRepo.AddUserAsync(new User { Email = "i@test.com", Name = "Іван", PasswordHash = "1" });
            var nastia = await userRepo.AddUserAsync(new User { Email = "n@test.com", Name = "Настя", PasswordHash = "1" });

            var (created, error) = await fixture.Events.CreateEventAsync(
                new CreateEventRequest(margarita.Id, "Похід", DateTime.Today.AddDays(5), "Карпати", "", (decimal?)null));
            Assert.Null(error);
            fixture.Event = created!;
            fixture.Margarita = new Member(margarita.Id, created!.Participants.Single().ParticipationId);

            var (_, ivanParticipant, ivanError) = await fixture.Events.JoinEventAsync(new JoinEventRequest(ivan.Id, created.InviteCode));
            Assert.Null(ivanError);
            fixture.Ivan = new Member(ivan.Id, ivanParticipant!.ParticipationId);

            var (_, nastiaParticipant, nastiaError) = await fixture.Events.JoinEventAsync(new JoinEventRequest(nastia.Id, created.InviteCode));
            Assert.Null(nastiaError);
            fixture.Nastia = new Member(nastia.Id, nastiaParticipant!.ParticipationId);

            return fixture;
        }

        public Task<(CreateExpenseResultDto? Result, string? Error)> AddAsync(Member payer, decimal amount, params int[] shareIds) =>
            Expenses.AddExpenseAsync(Event.Id, new CreateExpenseRequest(
                payer.UserId, payer.ParticipationId, amount, "Витрата", shareIds.Length == 0 ? null : shareIds));

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    [Fact] // Маргарита платить 300 за трьох: Іван і Настя переказують їй по 100.00
    public async Task GetSettlements_OnePayerSplitBetweenThree_ReturnsTwoTransfersOf100()
    {
        await using var f = await Fixture.CreateAsync();
        var (_, addError) = await f.AddAsync(f.Margarita, 300m,
            f.Margarita.ParticipationId, f.Ivan.ParticipationId, f.Nastia.ParticipationId);
        Assert.Null(addError);

        var (settlements, error) = await f.Expenses.GetSettlementsAsync(f.Event.Id);

        Assert.Null(error);
        Assert.Equal(2, settlements!.Length);
        Assert.All(settlements, t => Assert.Equal(f.Margarita.ParticipationId, t.ToParticipationId));
        Assert.All(settlements, t => Assert.Equal(100m, t.Amount));
        Assert.Contains(settlements, t => t.FromParticipationId == f.Ivan.ParticipationId && t.FromUserName == "Іван");
        Assert.Contains(settlements, t => t.FromParticipationId == f.Nastia.ParticipationId && t.FromUserName == "Настя");
    }

    [Fact] // Копійки: 100.00 на трьох, платить Маргарита — сума переказів дорівнює її чистому балансу
    public async Task GetSettlements_UnevenSplit_TransfersCoverCreditorBalanceExactly()
    {
        await using var f = await Fixture.CreateAsync();
        var (_, addError) = await f.AddAsync(f.Margarita, 100m,
            f.Margarita.ParticipationId, f.Ivan.ParticipationId, f.Nastia.ParticipationId);
        Assert.Null(addError);

        var (balances, _) = await f.Expenses.GetBalancesAsync(f.Event.Id);
        var (settlements, error) = await f.Expenses.GetSettlementsAsync(f.Event.Id);

        Assert.Null(error);
        var margaritaBalance = balances!.Single(b => b.ParticipationId == f.Margarita.ParticipationId).Balance;
        Assert.Equal(margaritaBalance, settlements!.Sum(t => t.Amount));
    }

    [Fact] // Витрат ще немає: порожній список без помилки
    public async Task GetSettlements_NoExpenses_ReturnsEmptyList()
    {
        await using var f = await Fixture.CreateAsync();

        var (settlements, error) = await f.Expenses.GetSettlementsAsync(f.Event.Id);

        Assert.Null(error);
        Assert.Empty(settlements!);
    }

    [Fact] // Платник розподілив витрату лише на себе: боргів немає
    public async Task GetSettlements_PayerCoversOnlyOwnShare_ReturnsEmptyList()
    {
        await using var f = await Fixture.CreateAsync();
        var (_, addError) = await f.AddAsync(f.Margarita, 100m, f.Margarita.ParticipationId);
        Assert.Null(addError);

        var (settlements, error) = await f.Expenses.GetSettlementsAsync(f.Event.Id);

        Assert.Null(error);
        Assert.Empty(settlements!);
    }

    [Theory] // eventId <= 0: invalid_request
    [InlineData(0)]
    [InlineData(-5)]
    public async Task GetSettlements_NonPositiveEventId_ReturnsInvalidRequest(int eventId)
    {
        await using var f = await Fixture.CreateAsync();

        var (settlements, error) = await f.Expenses.GetSettlementsAsync(eventId);

        Assert.Null(settlements);
        Assert.Equal("invalid_request", error);
    }

    [Fact] // Неіснуюча подія: event_not_found
    public async Task GetSettlements_UnknownEvent_ReturnsEventNotFound()
    {
        await using var f = await Fixture.CreateAsync();

        var (settlements, error) = await f.Expenses.GetSettlementsAsync(9999);

        Assert.Null(settlements);
        Assert.Equal("event_not_found", error);
    }
}
