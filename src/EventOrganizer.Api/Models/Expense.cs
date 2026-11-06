namespace EventOrganizer.Api.Models;

public sealed class Expense
{
    public int Id { get; init; }
    public int EventId { get; init; }
    public int PayerParticipationId { get; init; }
    public decimal Amount { get; init; }
    public string Description { get; init; } = "";
    public DateTime CreatedAt { get; init; }
}
