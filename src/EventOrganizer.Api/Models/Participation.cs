namespace EventOrganizer.Api.Models;

public sealed class Participation
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int EventId { get; set; }
    public ParticipantRole Role { get; set; }
    public DateTime JoinedAt { get; set; }
}
