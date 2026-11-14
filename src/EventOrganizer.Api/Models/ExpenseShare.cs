namespace EventOrganizer.Api.Models;

public sealed class ExpenseShare
{
    public int Id { get; set; }
    public int ExpenseId { get; set; }
    public int ParticipationId { get; set; }
    public decimal Amount { get; set; }
}
