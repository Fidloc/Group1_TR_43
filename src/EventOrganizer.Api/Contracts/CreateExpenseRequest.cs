namespace EventOrganizer.Api.Contracts;

public sealed record CreateExpenseRequest(int PayerParticipationId, decimal Amount, string Description);
