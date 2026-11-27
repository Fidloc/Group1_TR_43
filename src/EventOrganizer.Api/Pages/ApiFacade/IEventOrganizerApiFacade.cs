using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Models;

namespace EventOrganizer.Api.Pages.ApiFacade;

// Фасад (GoF, структурний шаблон): спрощений інтерфейс сторінок Razor Pages до REST API.
// Сторінки викликають одну операцію, а фасад формує запит, надсилає його та обробляє відповідь.
public interface IEventOrganizerApiFacade
{
    Task<ApiCallResult<LoginResult>> LoginAsync(string email, string password);

    Task<ApiCallResult> RegisterAsync(string name, string email, string password);

    Task<EventDetailsDto[]> GetEventsAsync(int? userId = null, string? status = null, string? search = null);

    Task<ApiCallResult<(int Id, string InviteCode)>> CreateEventAsync(
        int organizerUserId,
        string title,
        string date,
        string place,
        string description,
        object? budgetLimit);

    Task<ApiCallResult<int?>> JoinEventAsync(int userId, string inviteCode);

    Task<EventDetailsViewData?> LoadEventDetailsAsync(int eventId);

    Task<ApiCallResult> UpdateEventAsync(int eventId, UpdateEventRequest request);

    Task<ApiCallResult> ChangeEventStatusAsync(int eventId, int userId, string action);

    Task<ApiCallResult<CreateExpenseResultDto>> AddExpenseAsync(
        int eventId,
        int userId,
        int payerParticipationId,
        object amountValue,
        string description,
        int[]? shareParticipantIds);

    Task<ApiCallResult<(ExpenseShare[] Shares, EventParticipantItem[] Participants)>> DistributeExpenseAsync(
        int eventId,
        int expenseId,
        int organizerUserId,
        int[] participantIds);
}
