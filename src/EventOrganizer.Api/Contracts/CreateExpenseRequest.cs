namespace EventOrganizer.Api.Contracts;

// ShareParticipantIds необов'язковий: якщо переданий і не порожній, витрата одразу ділиться
// порівну між цими учасниками (Participation.Id) в одній транзакції (US-03)
public sealed record CreateExpenseRequest(
    int UserId,
    int PayerParticipationId,
    decimal Amount,
    string Description,
    int[]? ShareParticipantIds = null);
