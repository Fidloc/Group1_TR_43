namespace EventOrganizer.Api.Contracts;

public sealed record UpdateEventRequest(int UserId, string Title, DateTime Date, string Place, string Description);
