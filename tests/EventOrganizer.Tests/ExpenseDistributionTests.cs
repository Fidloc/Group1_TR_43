using EventOrganizer.Api.Infrastructure;
using EventOrganizer.Api.Models;

namespace EventOrganizer.Tests;

public class ExpenseDistributionTests
{
    [Theory]
    [InlineData(100.00, 3, 33.34, 33.33)]
    [InlineData(10.00, 7, 1.48, 1.42)]
    [InlineData(0.01, 2, 0.01, 0.00)]
    [InlineData(100.00, 1, 100.00, 100.00)]
    public void SplitEqually_ShouldMatchExpenseTotalAmount(
        decimal amount,
        int count,
        decimal firstExpected,
        decimal restExpected)
    {
        var participantIds = Enumerable.Range(1, count).ToArray();

        var shares = ExpenseCalculator.SplitEqually(1, amount, participantIds);

        Assert.Equal(count, shares.Count);
        Assert.Equal(amount, shares.Sum(s => s.Amount));
        Assert.Equal(firstExpected, shares[0].Amount);

        for (int i = 1; i < shares.Count; i++)
        {
            Assert.Equal(restExpected, shares[i].Amount);
        }
    }

    [Fact]
    public void SplitEqually_ShouldReturnEmptyList_WhenNoParticipantsProvided()
    {
        var shares = ExpenseCalculator.SplitEqually(1, 100.00m, Array.Empty<int>());

        Assert.Empty(shares);
    }

    [Fact]
    public void GetBalances_ShouldCalculatePaidMinusOwedForParticipants()
    {
        var users = new List<User>
        {
            new() { Id = 1, Name = "Маргарита" },
            new() { Id = 2, Name = "Іван" },
            new() { Id = 3, Name = "Анастасія" }
        };
        var participations = new List<Participation>
        {
            new() { Id = 1, EventId = 1, UserId = 1, Role = ParticipantRole.Organizer },
            new() { Id = 2, EventId = 1, UserId = 2, Role = ParticipantRole.Participant },
            new() { Id = 3, EventId = 1, UserId = 3, Role = ParticipantRole.Participant }
        };
        var expenses = new List<Expense>
        {
            new() { Id = 1, EventId = 1, PayerParticipationId = 1, Amount = 100.00m, Description = "Піца" }
        };
        var shares = ExpenseCalculator.SplitEqually(1, 100.00m, new[] { 1, 2, 3 });

        var balances = ExpenseCalculator.GetBalances(1, users, participations, expenses, shares);

        Assert.Equal(3, balances.Length);
        Assert.Equal(66.66m, balances.Single(b => b.ParticipationId == 1).Balance);
        Assert.Equal(-33.33m, balances.Single(b => b.ParticipationId == 2).Balance);
        Assert.Equal(-33.33m, balances.Single(b => b.ParticipationId == 3).Balance);
    }
}
