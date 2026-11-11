using EventOrganizer.Api.Data;
using EventOrganizer.Api.Models;
using Xunit;

namespace EventOrganizer.Tests;

public class RepositoryAndEditEventTests
{
    [Fact]
    public async Task InMemoryRepo_AddAndRetrieveUser_Succeeds()
    {
        var repo = new InMemoryEventOrganizerRepository();
        var user = new User { Email = "test@example.com", Name = "Тестер", PasswordHash = "secret" };

        var added = await repo.AddUserAsync(user);
        Assert.True(added.Id > 0);

        var retrieved = await repo.GetUserByEmailAsync("test@example.com");
        Assert.NotNull(retrieved);
        Assert.Equal("Тестер", retrieved.Name);
    }

    [Fact]
    public async Task InMemoryRepo_AddAndEditEvent_Succeeds()
    {
        var repo = new InMemoryEventOrganizerRepository();
        var ev = new Event
        {
            Title = "Стара назва",
            Date = DateTime.Today.AddDays(5),
            Place = "Київ",
            Description = "Опис",
            InviteCode = "ABC12345",
            Status = EventStatus.Created
        };

        var created = await repo.AddEventAsync(ev);
        Assert.True(created.Id > 0);

        created.Title = "Оновлена назва";
        created.Place = "Львів";
        await repo.UpdateEventAsync(created);

        var updated = await repo.GetEventByIdAsync(created.Id);
        Assert.NotNull(updated);
        Assert.Equal("Оновлена назва", updated.Title);
        Assert.Equal("Львів", updated.Place);
    }

    [Fact]
    public async Task InMemoryRepo_AddAndRetrieveExpensesAndShares_Succeeds()
    {
        var repo = new InMemoryEventOrganizerRepository();
        var expense = new Expense
        {
            EventId = 1,
            PayerParticipationId = 1,
            Amount = 150m,
            Description = "Піца",
            CreatedAt = DateTime.UtcNow
        };

        var addedExpense = await repo.AddExpenseAsync(expense);
        Assert.True(addedExpense.Id > 0);

        var shares = new List<ExpenseShare>
        {
            new() { ExpenseId = addedExpense.Id, ParticipationId = 1, Amount = 75m },
            new() { ExpenseId = addedExpense.Id, ParticipationId = 2, Amount = 75m }
        };

        await repo.ReplaceSharesAsync(addedExpense.Id, shares);

        var retrievedShares = await repo.GetSharesByExpenseIdAsync(addedExpense.Id);
        Assert.Equal(2, retrievedShares.Count);
        Assert.Equal(150m, retrievedShares.Sum(s => s.Amount));
    }
}
