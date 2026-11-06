namespace EventOrganizer.Api.Contracts;

public sealed record BalanceDto(int ParticipationId, int UserId, string UserName, decimal Paid, decimal Owed, decimal Balance);
