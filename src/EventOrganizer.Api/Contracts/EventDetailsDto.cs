namespace EventOrganizer.Api.Contracts;

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
