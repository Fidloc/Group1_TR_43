namespace EventOrganizer.Api.Models;

public sealed class Event
{
    public int Id { get; init; }
    public string Title { get; set; } = "";
    public DateTime Date { get; set; }
    public string Place { get; set; } = "";
    public string Description { get; set; } = "";
    public string InviteCode { get; init; } = "";
    public EventStatus Status { get; set; } = EventStatus.Created;
}
