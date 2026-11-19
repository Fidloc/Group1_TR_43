namespace EventOrganizer.Api.Models;

/// <summary>
/// Предметна сутність «Подія» з інкапсульованими правилами станів та редагування (Domain Model).
/// </summary>
public sealed class Event
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public DateTime Date { get; set; }
    public string Place { get; set; } = "";
    public string Description { get; set; } = "";
    public string InviteCode { get; set; } = "";
    public EventStatus Status { get; set; } = EventStatus.Created;

    /// <summary>
    /// Ознака активного стану, в якому дозволені зміни, приєднання та робота з витратами (FR-15).
    /// </summary>
    public bool IsActive => Status == EventStatus.Created;

    /// <summary>
    /// Предметне правило оновлення деталей події (UC-08; FR-04, FR-15).
    /// </summary>
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

    /// <summary>
    /// Предметне правило переходу події до термінального стану (UC-10, UC-11; FR-13, FR-14).
    /// </summary>
    public string? TryTransitionTo(EventStatus target)
    {
        if (target == EventStatus.Created)
            return "invalid_request";

        if (!IsActive)
            return "event_not_active";

        Status = target;
        return null;
    }
}
