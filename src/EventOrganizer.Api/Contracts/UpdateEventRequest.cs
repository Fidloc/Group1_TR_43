namespace EventOrganizer.Api.Contracts;

public sealed record UpdateEventRequest(
    int UserId,
    string Title,
    string Date,
    string Place,
    string Description
);
