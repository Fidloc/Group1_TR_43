namespace EventOrganizer.Api.Models;

public sealed class ExpenseShare
{
    public int Id { get; init; }
    public int ExpenseId { get; init; }
    public int ParticipationId { get; init; }
    public decimal Amount { get; init; }
}
