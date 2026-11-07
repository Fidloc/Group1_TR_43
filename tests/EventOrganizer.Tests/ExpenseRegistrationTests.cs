using System.Text.Json;
using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Infrastructure;
using EventOrganizer.Api.Models;

namespace EventOrganizer.Tests;

// UC-05: AC-09, AC-10, AC-15
public class ExpenseRegistrationTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    // Подія 1 (активна) з двома учасниками, подія 2 з одним учасником
    private static (List<Event>, List<Participation>, List<Expense>) Seed(
        EventStatus status = EventStatus.Created)
    {
        var events = new List<Event>
        {
            new() { Id = 1, Title = "Похід", Date = Now.AddDays(7), Status = status },
            new() { Id = 2, Title = "Футбол", Date = Now.AddDays(3) }
        };
        var participations = new List<Participation>
        {
            new() { Id = 1, UserId = 1, EventId = 1, Role = ParticipantRole.Organizer },
            new() { Id = 2, UserId = 2, EventId = 1, Role = ParticipantRole.Participant },
            new() { Id = 3, UserId = 3, EventId = 2, Role = ParticipantRole.Organizer }
        };
        return (events, participations, new List<Expense>());
    }

    [Fact]
    public void ValidExpense_IsSaved() // AC-09
    {
        var (events, participations, expenses) = Seed();

        var error = ExpenseRegistration.TryAdd(events, participations, expenses, 1,
            new CreateExpenseRequest(1, 2, 450.50m, "Продукти"), Now, out var expense);

        Assert.Null(error);
        Assert.NotNull(expense);
        var saved = Assert.Single(expenses);
        Assert.Equal(1, saved.EventId);
        Assert.Equal(2, saved.PayerParticipationId);
        Assert.Equal(450.50m, saved.Amount);
        Assert.Equal("Продукти", saved.Description);
    }

    [Fact]
    public void SecondExpense_GetsNextId()
    {
        var (events, participations, expenses) = Seed();

        ExpenseRegistration.TryAdd(events, participations, expenses, 1,
            new CreateExpenseRequest(1, 1, 100m, "Оренда"), Now, out _);
        ExpenseRegistration.TryAdd(events, participations, expenses, 1,
            new CreateExpenseRequest(1, 2, 50m, "Квитки"), Now, out var second);

        Assert.Equal(2, expenses.Count);
        Assert.Equal(2, second!.Id);
    }

    [Theory] // AC-10
    [InlineData("0")]
    [InlineData("-100")]
    [InlineData("10.005")]
    [InlineData("1000000.01")]
    public void InvalidAmount_IsRejected_AndNothingSaved(string amountText)
    {
        var (events, participations, expenses) = Seed();
        var amount = decimal.Parse(amountText, System.Globalization.CultureInfo.InvariantCulture);

        var error = ExpenseRegistration.TryAdd(events, participations, expenses, 1,
            new CreateExpenseRequest(1, 1, amount, "Помилка"), Now, out var expense);

        Assert.Equal("invalid_amount", error);
        Assert.Null(expense);
        Assert.Empty(expenses);
    }

    [Theory] // AC-15
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Cancelled)]
    public void InactiveEvent_IsRejected_AndNothingSaved(EventStatus status)
    {
        var (events, participations, expenses) = Seed(status);

        var error = ExpenseRegistration.TryAdd(events, participations, expenses, 1,
            new CreateExpenseRequest(1, 1, 100m, "Піца"), Now, out _);

        Assert.Equal("event_not_active", error);
        Assert.Empty(expenses);
    }

    [Fact]
    public void UnknownEvent_IsRejected()
    {
        var (events, participations, expenses) = Seed();

        var error = ExpenseRegistration.TryAdd(events, participations, expenses, 99,
            new CreateExpenseRequest(1, 1, 100m, "Піца"), Now, out _);

        Assert.Equal("event_not_found", error);
        Assert.Empty(expenses);
    }

    [Fact]
    public void PayerFromAnotherEvent_IsRejected()
    {
        var (events, participations, expenses) = Seed();

        // учасник 3 належить до події 2, а не 1
        var error = ExpenseRegistration.TryAdd(events, participations, expenses, 1,
            new CreateExpenseRequest(1, 3, 100m, "Піца"), Now, out _);

        Assert.Equal("participant_not_found", error);
        Assert.Empty(expenses);
    }

    [Fact]
    public void RejectedRequest_DoesNotChangeExistingExpenses()
    {
        var (events, participations, expenses) = Seed();
        ExpenseRegistration.TryAdd(events, participations, expenses, 1,
            new CreateExpenseRequest(1, 1, 300m, "Оренда"), Now, out var first);

        var error = ExpenseRegistration.TryAdd(events, participations, expenses, 1,
            new CreateExpenseRequest(1, 1, -5m, "Помилка"), Now, out _);

        Assert.Equal("invalid_amount", error);
        var only = Assert.Single(expenses);
        Assert.Equal(first!.Id, only.Id);
        Assert.Equal(300m, only.Amount);
    }

    [Fact] // FR-09: витрату вносить лише Організатор
    public void NotOrganizer_IsRejected_AndNothingSaved()
    {
        var (events, participations, expenses) = Seed();

        // користувач 2 — звичайний учасник події 1
        var error = ExpenseRegistration.TryAdd(events, participations, expenses, 1,
            new CreateExpenseRequest(2, 2, 100m, "Піца"), Now, out var expense);

        Assert.Equal("forbidden_not_organizer", error);
        Assert.Null(expense);
        Assert.Empty(expenses);
    }

    [Fact]
    public void OrganizerOfAnotherEvent_IsRejected()
    {
        var (events, participations, expenses) = Seed();

        // користувач 3 організатор події 2, але не події 1
        var error = ExpenseRegistration.TryAdd(events, participations, expenses, 1,
            new CreateExpenseRequest(3, 1, 100m, "Піца"), Now, out _);

        Assert.Equal("forbidden_not_organizer", error);
        Assert.Empty(expenses);
    }

    [Fact]
    public void Organizer_CanRegisterExpensePaidByParticipant()
    {
        var (events, participations, expenses) = Seed();

        // організатор (користувач 1) вносить витрату, яку оплатив учасник 2
        var error = ExpenseRegistration.TryAdd(events, participations, expenses, 1,
            new CreateExpenseRequest(1, 2, 80m, "Квитки"), Now, out var expense);

        Assert.Null(error);
        Assert.Equal(2, expense!.PayerParticipationId);
        Assert.Single(expenses);
    }

    [Theory] // формат тіла запиту
    [InlineData("""{"userId":1,"payerParticipationId":1,"amount":"abc","description":"x"}""", "invalid_amount")]
    [InlineData("""{"userId":1,"payerParticipationId":1,"description":"x"}""", "invalid_amount")]
    [InlineData("""{"userId":1,"payerParticipationId":1,"amount":0}""", "invalid_amount")]
    [InlineData("""{"userId":1,"amount":100}""", "invalid_request")]
    [InlineData("""{"payerParticipationId":1,"amount":100}""", "invalid_request")]
    [InlineData("""[1,2,3]""", "invalid_request")]
    public void TryRead_RejectsBadBody(string json, string expectedCode)
    {
        using var doc = JsonDocument.Parse(json);

        var ok = ExpenseRegistration.TryRead(doc.RootElement, out var request, out var code);

        Assert.False(ok);
        Assert.Null(request);
        Assert.Equal(expectedCode, code);
    }

    [Fact]
    public void TryRead_AcceptsValidBody()
    {
        using var doc = JsonDocument.Parse(
            """{"userId":1,"payerParticipationId":2,"amount":120.75,"description":"  Таксі  "}""");

        var ok = ExpenseRegistration.TryRead(doc.RootElement, out var request, out var code);

        Assert.True(ok);
        Assert.Null(code);
        Assert.Equal(new CreateExpenseRequest(1, 2, 120.75m, "Таксі"), request);
    }
}
