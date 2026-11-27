namespace EventOrganizer.Api.Contracts;

public sealed record SettlementTransferDto(
    int FromParticipationId,
    int FromUserId,
    string FromUserName,
    int ToParticipationId,
    int ToUserId,
    string ToUserName,
    decimal Amount);
