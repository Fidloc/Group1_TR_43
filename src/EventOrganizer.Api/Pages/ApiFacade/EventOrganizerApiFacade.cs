using System.Net.Http.Json;
using System.Text.Json;
using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Models;

namespace EventOrganizer.Api.Pages.ApiFacade;

public sealed class EventOrganizerApiFacade : IEventOrganizerApiFacade
{
    public const string LoginFallback = "Невірна електронна пошта або пароль.";
    public const string RegisterFallback = "Не вдалося зареєструвати користувача.";
    public const string CreateEventFallback = "Не вдалося створити подію.";
    public const string JoinEventFallback = "Не вдалося приєднатися до події.";
    public const string UpdateEventFallback = "Не вдалося оновити подію.";
    public const string ChangeStatusFallback = "Не вдалося змінити статус події.";
    public const string AddExpenseFallback = "Помилка додавання витрати.";
    public const string DistributeFallback = "Помилка розподілу витрати.";

    private readonly HttpClient _httpClient;

    public EventOrganizerApiFacade(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ApiCallResult<LoginResult>> LoginAsync(string email, string password)
    {
        using var response = await _httpClient.PostAsJsonAsync("/api/users/login",
            new LoginRequest(email, password));

        if (!response.IsSuccessStatusCode)
            return new(false, ErrorMessage: await ReadApiErrorAsync(response, LoginFallback));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var userId = body.TryGetProperty("userId", out var idProp) ? idProp.GetInt32() : 0;

        var users = await _httpClient.GetFromJsonAsync<UserDto[]>("/api/users") ?? [];
        var userObj = users.FirstOrDefault(u => u.Id == userId);
        var displayName = !string.IsNullOrWhiteSpace(userObj?.Name) ? userObj.Name : email;

        return new(true, new LoginResult(userId, $"{displayName} ({email})"));
    }

    public async Task<ApiCallResult> RegisterAsync(string name, string email, string password)
    {
        using var response = await _httpClient.PostAsJsonAsync("/api/users/register",
            new RegisterUserRequest(email, password, name));

        return response.IsSuccessStatusCode
            ? new ApiCallResult(true)
            : new ApiCallResult(false, await ReadApiErrorAsync(response, RegisterFallback));
    }

    public async Task<EventDetailsDto[]> GetEventsAsync(int? userId = null, string? status = null, string? search = null)
    {
        var queryParts = new List<string>();
        if (userId is not null)
            queryParts.Add($"userId={userId}");

        if (!string.IsNullOrWhiteSpace(status))
            queryParts.Add($"status={Uri.EscapeDataString(status.Trim())}");

        if (!string.IsNullOrWhiteSpace(search))
            queryParts.Add($"search={Uri.EscapeDataString(search.Trim())}");

        var url = queryParts.Count == 0
            ? "/api/events"
            : $"/api/events?{string.Join("&", queryParts)}";

        return await _httpClient.GetFromJsonAsync<EventDetailsDto[]>(url) ?? [];
    }

    public async Task<ApiCallResult<(int Id, string InviteCode)>> CreateEventAsync(
        int organizerUserId,
        string title,
        string date,
        string place,
        string description,
        object? budgetLimit)
    {
        using var response = await _httpClient.PostAsJsonAsync("/api/events", new
        {
            organizerUserId,
            title,
            date,
            place,
            description,
            budgetLimit
        });

        if (!response.IsSuccessStatusCode)
            return new(false, ErrorMessage: await ReadApiErrorAsync(response, CreateEventFallback));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var createdId = body.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;
        var inviteCode = body.TryGetProperty("inviteCode", out var codeProp) ? codeProp.GetString() : "";

        return new(true, (createdId, inviteCode ?? ""));
    }

    public async Task<ApiCallResult<int?>> JoinEventAsync(int userId, string inviteCode)
    {
        using var response = await _httpClient.PostAsJsonAsync("/api/events/join", new
        {
            userId,
            inviteCode
        });

        if (!response.IsSuccessStatusCode)
            return new(false, ErrorMessage: await ReadApiErrorAsync(response, JoinEventFallback));

        var location = response.Headers.Location?.ToString();
        var lastSegment = location?.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        int? joinedEventId = int.TryParse(lastSegment, out var id) && id > 0 ? id : null;

        return new(true, joinedEventId);
    }

    public async Task<EventDetailsViewData?> LoadEventDetailsAsync(int eventId)
    {
        if (eventId <= 0)
            return null;

        using var evResponse = await _httpClient.GetAsync($"/api/events/{eventId}");
        if (!evResponse.IsSuccessStatusCode)
            return null;

        var ev = await evResponse.Content.ReadFromJsonAsync<EventDetailsDto>();
        if (ev is null)
            return null;

        var participants = await _httpClient.GetFromJsonAsync<EventParticipantItem[]>(
            $"/api/events/{eventId}/participants") ?? [];
        var balances = await _httpClient.GetFromJsonAsync<BalanceDto[]>(
            $"/api/events/{eventId}/balances") ?? [];
        var rawExpenses = await _httpClient.GetFromJsonAsync<Expense[]>(
            $"/api/events/{eventId}/expenses") ?? [];

        var expenses = new List<ExpenseWithSharesItem>();
        foreach (var ex in rawExpenses)
        {
            var shares = await _httpClient.GetFromJsonAsync<ExpenseShare[]>(
                $"/api/events/{eventId}/expenses/{ex.Id}/shares") ?? [];
            var payerName = participants.FirstOrDefault(p => p.Id == ex.PayerParticipationId)?.UserName
                ?? $"Учасник #{ex.PayerParticipationId}";
            expenses.Add(new ExpenseWithSharesItem(ex, payerName, shares));
        }

        return new EventDetailsViewData(ev, participants, balances, expenses.ToArray());
    }

    public async Task<ApiCallResult> UpdateEventAsync(int eventId, UpdateEventRequest request)
    {
        using var response = await _httpClient.PutAsJsonAsync($"/api/events/{eventId}", request);

        return response.IsSuccessStatusCode
            ? new ApiCallResult(true)
            : new ApiCallResult(false, await ReadApiErrorAsync(response, UpdateEventFallback));
    }

    public async Task<ApiCallResult> ChangeEventStatusAsync(int eventId, int userId, string action)
    {
        using var response = await _httpClient.PostAsJsonAsync($"/api/events/{eventId}/{action}", new { userId });

        return response.IsSuccessStatusCode
            ? new ApiCallResult(true)
            : new ApiCallResult(false, await ReadApiErrorAsync(response, ChangeStatusFallback));
    }

    public async Task<ApiCallResult<CreateExpenseResultDto>> AddExpenseAsync(
        int eventId,
        int userId,
        int payerParticipationId,
        object amountValue,
        string description,
        int[]? shareParticipantIds)
    {
        int[]? selectedShares = shareParticipantIds is { Length: > 0 } ? shareParticipantIds : null;

        using var response = await _httpClient.PostAsJsonAsync($"/api/events/{eventId}/expenses", new
        {
            userId,
            payerParticipationId,
            amount = amountValue,
            description,
            shareParticipantIds = selectedShares
        });

        if (!response.IsSuccessStatusCode)
            return new(false, ErrorMessage: await ReadApiErrorAsync(response, AddExpenseFallback));

        var result = await response.Content.ReadFromJsonAsync<CreateExpenseResultDto>();
        var warning = string.IsNullOrWhiteSpace(result?.Warning) ? null : result.Warning;

        return new(true, result, WarningMessage: warning);
    }

    public async Task<ApiCallResult<(ExpenseShare[] Shares, EventParticipantItem[] Participants)>> DistributeExpenseAsync(
        int eventId,
        int expenseId,
        int organizerUserId,
        int[] participantIds)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"/api/events/{eventId}/expenses/{expenseId}/distribute",
            new DistributeExpenseRequest(organizerUserId, participantIds));

        if (!response.IsSuccessStatusCode)
            return new(false, ErrorMessage: await ReadApiErrorAsync(response, DistributeFallback));

        var shares = await response.Content.ReadFromJsonAsync<ExpenseShare[]>() ?? [];
        var participants = await _httpClient.GetFromJsonAsync<EventParticipantItem[]>(
            $"/api/events/{eventId}/participants") ?? [];

        return new(true, (shares, participants));
    }

    public async Task<SettlementTransferDto[]> GetSettlementsAsync(int eventId)
    {
        if (eventId <= 0)
            return [];

        using var response = await _httpClient.GetAsync($"/api/events/{eventId}/settlements");
        if (!response.IsSuccessStatusCode)
            return [];

        return await response.Content.ReadFromJsonAsync<SettlementTransferDto[]>() ?? [];
    }

    private static async Task<string> ReadApiErrorAsync(HttpResponseMessage response, string fallback)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (body.ValueKind == JsonValueKind.Object &&
                body.TryGetProperty("message", out var msg) &&
                !string.IsNullOrWhiteSpace(msg.GetString()))
            {
                return msg.GetString()!;
            }
        }
        catch
        {
        
        }

        return fallback;
    }
}
