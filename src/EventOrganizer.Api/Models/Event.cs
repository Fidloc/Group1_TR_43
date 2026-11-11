namespace EventOrganizer.Api.Models;

public sealed class Event
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public DateTime Date { get; set; }
    public string Place { get; set; } = "";
    public string Description { get; set; } = "";
    public string InviteCode { get; set; } = "";
    public EventStatus Status { get; set; } = EventStatus.Created;
}
