namespace EventOrganizer.Api.Contracts;

// BudgetLimit необов'язковий: null означає, що бюджет не задано (US-03)
public sealed record CreateEventRequest(
    int OrganizerUserId,
    string Title,
    DateTime Date,
    string Place,
    string Description,
    decimal? BudgetLimit = null);
