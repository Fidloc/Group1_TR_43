namespace EventOrganizer.Api.Contracts;

// Учасник події. Role: Organizer | Participant.
public sealed record ParticipantDto(int ParticipationId, int UserId, string Role, DateTime JoinedAt);
