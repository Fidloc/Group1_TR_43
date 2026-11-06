namespace EventOrganizer.Api.Contracts;

// Учасник події у відповіді сервера (FR-08). Role: "Organizer" або "Participant".
public sealed record ParticipantDto(int ParticipationId, int UserId, string Role, DateTime JoinedAt);
