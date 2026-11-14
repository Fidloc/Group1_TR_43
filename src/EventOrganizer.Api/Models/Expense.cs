namespace EventOrganizer.Api.Models;

public sealed class Expense
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public int PayerParticipationId { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
