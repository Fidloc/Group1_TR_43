namespace EventOrganizer.Api.Contracts;

public sealed record DistributeExpenseRequest(int OrganizerUserId, int[] ParticipantIds);
