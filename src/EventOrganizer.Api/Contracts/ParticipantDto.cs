namespace EventOrganizer.Api.Contracts;

public sealed record ParticipantDto(int ParticipationId, int UserId, string Role, DateTime JoinedAt);
