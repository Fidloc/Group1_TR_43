namespace EventOrganizer.Api.Models;

public sealed class Participation
{
    public int Id { get; init; }
    public int UserId { get; init; }
    public int EventId { get; init; }
    public ParticipantRole Role { get; init; }
    public DateTime JoinedAt { get; init; }
}
