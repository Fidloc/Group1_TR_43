namespace EventOrganizer.Api.Contracts;

public sealed record CreateExpenseRequest(int UserId, int PayerParticipationId, decimal Amount, string Description);
