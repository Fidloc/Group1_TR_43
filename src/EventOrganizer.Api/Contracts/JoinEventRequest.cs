namespace EventOrganizer.Api.Contracts;

public sealed record JoinEventRequest(int UserId, string InviteCode);
