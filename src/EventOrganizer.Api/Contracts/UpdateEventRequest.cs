namespace EventOrganizer.Api.Contracts;

// BudgetLimit необов'язковий: null означає «залишити поточний бюджет без змін» (US-03)
public sealed record UpdateEventRequest(
    int UserId,
    string Title,
    string Date,
    string Place,
    string Description,
    decimal? BudgetLimit = null
);
