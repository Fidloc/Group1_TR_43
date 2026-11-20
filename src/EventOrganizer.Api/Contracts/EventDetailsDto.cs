namespace EventOrganizer.Api.Contracts;

// BudgetLimit і TotalSpent дозволяють клієнту одразу показати стан бюджету (US-03)
public sealed record EventDetailsDto(
    int Id,
    string Title,
    DateTime Date,
    string Place,
    string Description,
    string InviteCode,
    string Status,
    int OrganizerUserId,
    ParticipantDto[] Participants,
    decimal? BudgetLimit = null,
    decimal TotalSpent = 0m);
