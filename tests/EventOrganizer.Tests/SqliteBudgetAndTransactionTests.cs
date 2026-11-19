using EventOrganizer.Api.Data;
using EventOrganizer.Api.Data.Repositories;
using EventOrganizer.Api.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EventOrganizer.Tests;

public sealed class SqliteBudgetAndTransactionTests
{
    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<SqliteConnection> CreateMigratedConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateContext(connection);
        await db.Database.MigrateAsync();
        return connection;
    }

    [Fact]
    public async Task Migration_AddEventBudgetLimit_PreservesExistingSprint1Records()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using (var dbS1 = CreateContext(connection))
        {
            var migrator = dbS1.GetService<IMigrator>();
            await migrator.MigrateAsync("20261111201132_InitialCreate");

            await dbS1.Database.ExecuteSqlRawAsync(
                "INSERT INTO Events (Id, Title, Date, Place, Description, InviteCode, Status) " +
                "VALUES (1, 'Стара подія з S1', '2026-12-01T10:00:00', 'Київ', 'Опис S1', 'S1CODE01', 'Created')");
        }

        await using (var dbS2 = CreateContext(connection))
        {
            await dbS2.Database.MigrateAsync();
        }

        await using (var verifyDb = CreateContext(connection))
        {
            var preserved = await verifyDb.Events.AsNoTracking().SingleAsync(e => e.Id == 1);
            Assert.Equal("Стара подія з S1", preserved.Title);
            Assert.Equal("Київ", preserved.Place);
            Assert.Equal("S1CODE01", preserved.InviteCode);
            Assert.Null(preserved.BudgetLimit);
        }
    }

    [Fact] 
    public async Task Event_TrySetBudgetLimit_PersistsValidAndClearsNull_ViaSqliteRepository()
    {
        await using var connection = await CreateMigratedConnectionAsync();

        int eventId;
        await using (var db = CreateContext(connection))
        {
            var repo = new SqliteEventRepository(db);
            var ev = new Event
            {
                Title = "Пікнік",
                Date = DateTime.Today.AddDays(5),
                Place = "Парк",
                Description = "Тест",
                InviteCode = "BUDGET01"
            };
            Assert.Null(ev.TrySetBudgetLimit(1000.50m));
            var created = await repo.AddEventAsync(ev);
            eventId = created.Id;
        }

        await using (var verifyDb = CreateContext(connection))
        {
            var repo = new SqliteEventRepository(verifyDb);
            var loaded = await repo.GetEventByIdAsync(eventId);
            Assert.NotNull(loaded);
            Assert.Equal(1000.50m, loaded.BudgetLimit);

            Assert.Null(loaded.TrySetBudgetLimit(null));
            await repo.UpdateEventAsync(loaded);
        }

        await using (var verifyClearedDb = CreateContext(connection))
        {
            var reloaded = await verifyClearedDb.Events.AsNoTracking().SingleAsync(e => e.Id == eventId);
            Assert.Null(reloaded.BudgetLimit);
        }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-10")]
    [InlineData("10.005")]
    [InlineData("10000001")]
    public void Event_TrySetBudgetLimit_RejectsInvalidValues_WithoutMutatingState(string rawLimit)
    {
        var limit = decimal.Parse(rawLimit, System.Globalization.CultureInfo.InvariantCulture);
        var ev = new Event { Title = "Тест", Date = DateTime.Today.AddDays(2), InviteCode = "TST00001" };
        Assert.Null(ev.TrySetBudgetLimit(500m));

        var error = ev.TrySetBudgetLimit(limit);

        Assert.Equal("invalid_budget", error);
        Assert.Equal(500m, ev.BudgetLimit);
    }

    [Theory]
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Cancelled)]
    public void Event_TrySetBudgetLimit_RejectsInactiveEvent_WithoutMutatingState(EventStatus targetStatus)
    {
        var ev = new Event { Title = "Тест", Date = DateTime.Today.AddDays(2), InviteCode = "TST00002" };
        Assert.Null(ev.TrySetBudgetLimit(500m));
        Assert.Null(ev.TryTransitionTo(targetStatus));

        var error = ev.TrySetBudgetLimit(1200m);

        Assert.Equal("event_not_active", error);
        Assert.Equal(500m, ev.BudgetLimit);
    }

    [Fact]
    public async Task SqliteExpenseRepo_GetTotalSpentByEventId_ReturnsExactSumForEventOnly()
    {
        await using var connection = await CreateMigratedConnectionAsync();
        await using var db = CreateContext(connection);
        var userRepo = new SqliteUserRepository(db);
        var eventRepo = new SqliteEventRepository(db);
        var expenseRepo = new SqliteExpenseRepository(db);

        var user = await userRepo.AddUserAsync(new User { Email = "sum@test.com", Name = "Сума", PasswordHash = "1" });
        var ev1 = await eventRepo.AddEventWithOrganizerAsync(
            new Event { Title = "Подія 1", Date = DateTime.Today.AddDays(1), InviteCode = "SUM00001" },
            new Participation { UserId = user.Id, Role = ParticipantRole.Organizer, JoinedAt = DateTime.UtcNow });
        var ev2 = await eventRepo.AddEventWithOrganizerAsync(
            new Event { Title = "Подія 2", Date = DateTime.Today.AddDays(2), InviteCode = "SUM00002" },
            new Participation { UserId = user.Id, Role = ParticipantRole.Organizer, JoinedAt = DateTime.UtcNow });

        Assert.Equal(0m, await expenseRepo.GetTotalSpentByEventIdAsync(ev1.Id));

        var p1 = (await eventRepo.GetParticipationsByEventIdAsync(ev1.Id)).Single();
        var p2 = (await eventRepo.GetParticipationsByEventIdAsync(ev2.Id)).Single();

        await expenseRepo.AddExpenseAsync(new Expense { EventId = ev1.Id, PayerParticipationId = p1.Id, Amount = 125.55m, Description = "А", CreatedAt = DateTime.UtcNow });
        await expenseRepo.AddExpenseAsync(new Expense { EventId = ev1.Id, PayerParticipationId = p1.Id, Amount = 74.45m, Description = "Б", CreatedAt = DateTime.UtcNow });
        await expenseRepo.AddExpenseAsync(new Expense { EventId = ev2.Id, PayerParticipationId = p2.Id, Amount = 999.00m, Description = "Чужа", CreatedAt = DateTime.UtcNow });

        Assert.Equal(200.00m, await expenseRepo.GetTotalSpentByEventIdAsync(ev1.Id));
    }

    [Fact] 
    public async Task SqliteExpenseRepo_AddExpenseWithShares_RollsBackAtomicallyOnTriggerFailure_AndCommitsAfterDrop()
    {
        await using var connection = await CreateMigratedConnectionAsync();

        int eventId;
        int p1Id;
        int p2Id;

        await using (var seedDb = CreateContext(connection))
        {
            var userRepo = new SqliteUserRepository(seedDb);
            var eventRepo = new SqliteEventRepository(seedDb);

            var u1 = await userRepo.AddUserAsync(new User { Email = "org@tx.com", Name = "Орг", PasswordHash = "1" });
            var u2 = await userRepo.AddUserAsync(new User { Email = "guest@tx.com", Name = "Гість", PasswordHash = "1" });

            var ev = await eventRepo.AddEventWithOrganizerAsync(
                new Event { Title = "Транзакція", Date = DateTime.Today.AddDays(3), InviteCode = "TXCODE01" },
                new Participation { UserId = u1.Id, Role = ParticipantRole.Organizer, JoinedAt = DateTime.UtcNow });
            var p2 = await eventRepo.AddParticipationAsync(
                new Participation { EventId = ev.Id, UserId = u2.Id, Role = ParticipantRole.Participant, JoinedAt = DateTime.UtcNow });
            var p1 = (await eventRepo.GetParticipationsByEventIdAsync(ev.Id)).First(p => p.UserId == u1.Id);

            eventId = ev.Id;
            p1Id = p1.Id;
            p2Id = p2.Id;

            await seedDb.Database.ExecuteSqlRawAsync(
                """
                CREATE TRIGGER test_reject_detail
                BEFORE INSERT ON ExpenseShares
                BEGIN
                    SELECT RAISE(ABORT, 'controlled failure');
                END;
                """);
        }

        // Спроба збереження з активним тригером: падає при вставці у ExpenseShares -> має відкотити й Expense
        await using (var failDb = CreateContext(connection))
        {
            var expenseRepo = new SqliteExpenseRepository(failDb);
            var expense = new Expense
            {
                EventId = eventId,
                PayerParticipationId = p1Id,
                Amount = 300m,
                Description = "Піца з відкатом",
                CreatedAt = DateTime.UtcNow
            };
            var shares = new[]
            {
                new ExpenseShare { ParticipationId = p1Id, Amount = 150m },
                new ExpenseShare { ParticipationId = p2Id, Amount = 150m }
            };

            await Assert.ThrowsAsync<DbUpdateException>(() => expenseRepo.AddExpenseWithSharesAsync(expense, shares));
        }

        // Читаємо через новий контекст: обидві таблиці повинні бути порожні
        await using (var checkRollbackDb = CreateContext(connection))
        {
            Assert.Equal(0, await checkRollbackDb.Expenses.CountAsync());
            Assert.Equal(0, await checkRollbackDb.ExpenseShares.CountAsync());

            await checkRollbackDb.Database.ExecuteSqlRawAsync("DROP TRIGGER test_reject_detail;");
        }

        // Повторюємо після видалення тригера: обидва записи мають зберегтися узгоджено
        await using (var successDb = CreateContext(connection))
        {
            var expenseRepo = new SqliteExpenseRepository(successDb);
            var expense = new Expense
            {
                EventId = eventId,
                PayerParticipationId = p1Id,
                Amount = 300m,
                Description = "Піца успішна",
                CreatedAt = DateTime.UtcNow
            };
            var shares = new[]
            {
                new ExpenseShare { ParticipationId = p1Id, Amount = 150m },
                new ExpenseShare { ParticipationId = p2Id, Amount = 150m }
            };

            var (savedExpense, savedShares) = await expenseRepo.AddExpenseWithSharesAsync(expense, shares);
            Assert.True(savedExpense.Id > 0);
            Assert.Equal(2, savedShares.Count);
        }

        await using (var checkCommitDb = CreateContext(connection))
        {
            var dbExpenses = await checkCommitDb.Expenses.AsNoTracking().ToListAsync();
            var dbShares = await checkCommitDb.ExpenseShares.AsNoTracking().ToListAsync();

            var onlyExpense = Assert.Single(dbExpenses);
            Assert.Equal("Піца успішна", onlyExpense.Description);
            Assert.Equal(2, dbShares.Count);
            Assert.All(dbShares, s => Assert.Equal(onlyExpense.Id, s.ExpenseId));
            Assert.Equal(300m, dbShares.Sum(s => s.Amount));
        }
    }
}
