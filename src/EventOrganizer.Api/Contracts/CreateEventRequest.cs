namespace EventOrganizer.Api.Contracts;

public sealed record CreateEventRequest(int OrganizerUserId, string Title, DateTime Date, string Place, string Description);
