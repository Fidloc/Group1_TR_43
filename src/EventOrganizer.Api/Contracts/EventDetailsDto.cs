namespace EventOrganizer.Api.Contracts;

// Сторінка події у відповіді сервера (FR-05, FR-08). Status: "Created", "Closed" або "Cancelled".
public sealed record EventDetailsDto(
    int Id,
    string Title,
    DateTime Date,
    string Place,
    string Description,
    string InviteCode,
    string Status,
    int OrganizerUserId,
    ParticipantDto[] Participants);
