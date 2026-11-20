namespace EventOrganizer.Api.Models;

public sealed class Event
{
    public const decimal MaxBudgetLimit = 10_000_000m;

    public int Id { get; set; }
    public string Title { get; set; } = "";
    public DateTime Date { get; set; }
    public string Place { get; set; } = "";
    public string Description { get; set; } = "";
    public string InviteCode { get; set; } = "";
    public EventStatus Status { get; set; } = EventStatus.Created;
    public decimal? BudgetLimit { get; private set; }

    public bool IsActive => Status == EventStatus.Created;

    public string? TryUpdateDetails(string? title, string? dateText, string? place, string? description, DateTime today)
    {
        if (!IsActive)
            return "event_not_active";

        if (string.IsNullOrWhiteSpace(title))
            return "missing_title";

        if (!DateTime.TryParse(dateText, out var parsedDate) || parsedDate.Date < today.Date)
            return "invalid_date";

        Title = title.Trim();
        Date = parsedDate;
        Place = place?.Trim() ?? "";
        Description = description?.Trim() ?? "";
        return null;
    }

    public string? TryTransitionTo(EventStatus target)
    {
        if (target == EventStatus.Created)
            return "invalid_request";

        if (!IsActive)
            return "event_not_active";

        Status = target;
        return null;
    }

    public string? TrySetBudgetLimit(decimal? limit)
    {
        if (!IsActive)
            return "event_not_active";

        if (limit is null)
        {
            BudgetLimit = null;
            return null;
        }

        if (limit.Value <= 0m || limit.Value > MaxBudgetLimit || decimal.Round(limit.Value, 2) != limit.Value)
            return "invalid_budget";

        BudgetLimit = limit.Value;
        return null;
    }
}
