using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EventOrganizer.Api.Pages;

public class IndexModel : PageModel
{
    public record ParticipantItem(int Id, int UserId, string UserName, string Role);

    public record ExpenseWithShares(Expense Expense, string PayerName, ExpenseShare[] Shares);

    public UserDto[] Users { get; set; } = [];
    public EventDetailsDto[] Events { get; set; } = [];
    public ParticipantItem[] Participants { get; set; } = [];
    public BalanceDto[] Balances { get; set; } = [];
    public ExpenseWithShares[] Expenses { get; set; } = [];

    public int SelectedEventId { get; set; }
    public int CurrentUserId { get; set; }
    public string CurrentUserText { get; set; } = "Не авторизовано";

    public string? RegMessage { get; set; }
    public bool RegSuccess { get; set; }

    public string? LoginMessage { get; set; }
    public bool LoginSuccess { get; set; }

    public string? CreateEventMessage { get; set; }
    public bool CreateEventSuccess { get; set; }

    public string? JoinEventMessage { get; set; }
    public bool JoinEventSuccess { get; set; }

    public string? ExpenseMessage { get; set; }
    public bool ExpenseSuccess { get; set; }

    public string? DistMessage { get; set; }
    public bool DistSuccess { get; set; }

    private HttpClient CreateApiClient()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        return new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    private void RestoreSession(int selectedEventId, int currentUserId, string? currentUser)
    {
        SelectedEventId = selectedEventId;
        CurrentUserId = currentUserId;
        if (!string.IsNullOrWhiteSpace(currentUser))
            CurrentUserText = currentUser;
    }

    public async Task OnGetAsync(int eventId = 0, int currentUserId = 0, string? currentUser = null)
    {
        RestoreSession(eventId, currentUserId, currentUser);
        await LoadPageDataAsync();
    }

    public async Task<IActionResult> OnPostSelectEventAsync(
        int selectedEventId,
        int currentUserId,
        string? currentUser)
    {
        RestoreSession(selectedEventId, currentUserId, currentUser);
        await LoadPageDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRegisterAsync(
        int selectedEventId,
        int currentUserId,
        string? currentUser,
        string regName,
        string regEmail,
        string regPass)
    {
        RestoreSession(selectedEventId, currentUserId, currentUser);

        using var http = CreateApiClient();
        var response = await http.PostAsJsonAsync("/api/users/register",
            new RegisterUserRequest(regEmail ?? "", regPass ?? "", regName ?? ""));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (response.IsSuccessStatusCode)
        {
            RegSuccess = true;
            RegMessage = $"Користувача «{regName}» ({regEmail}) зареєстровано! Тепер увійдіть у систему справа.";
        }
        else
        {
            RegSuccess = false;
            RegMessage = body.TryGetProperty("message", out var msg)
                ? msg.GetString()
                : "Не вдалося зареєструвати користувача.";
        }

        await LoadPageDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostLoginAsync(
        int selectedEventId,
        int currentUserId,
        string? currentUser,
        string loginEmail,
        string loginPass)
    {
        RestoreSession(selectedEventId, currentUserId, currentUser);

        using var http = CreateApiClient();
        var response = await http.PostAsJsonAsync("/api/users/login",
            new LoginRequest(loginEmail ?? "", loginPass ?? ""));

        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            var userId = body.TryGetProperty("userId", out var idProp) ? idProp.GetInt32() : 0;
            await LoadPageDataAsync();

            var userObj = Users.FirstOrDefault(u => u.Id == userId);
            var displayName = !string.IsNullOrWhiteSpace(userObj?.Name) ? userObj.Name : loginEmail;
            CurrentUserId = userId;
            CurrentUserText = $"{displayName} ({loginEmail})";
            LoginSuccess = true;
            LoginMessage = $"Ви успішно увійшли в акаунт {CurrentUserText}!";
            return Page();
        }

        LoginSuccess = false;
        LoginMessage = "Невірна електронна пошта або пароль.";
        await LoadPageDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostCreateEventAsync(
        int selectedEventId,
        int currentUserId,
        string? currentUser,
        string eventTitle,
        string eventDate,
        string eventPlace,
        string eventDescription)
    {
        RestoreSession(selectedEventId, currentUserId, currentUser);

        if (CurrentUserId <= 0)
        {
            CreateEventSuccess = false;
            CreateEventMessage = "Спочатку увійдіть у свій акаунт у блоці «Увійдіть у систему».";
            await LoadPageDataAsync();
            return Page();
        }

        using var http = CreateApiClient();
        var response = await http.PostAsJsonAsync("/api/events", new
        {
            organizerUserId = CurrentUserId,
            title = eventTitle ?? "",
            date = eventDate ?? "",
            place = eventPlace ?? "",
            description = eventDescription ?? ""
        });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (response.IsSuccessStatusCode)
        {
            var createdId = body.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : SelectedEventId;
            var inviteCode = body.TryGetProperty("inviteCode", out var codeProp) ? codeProp.GetString() : "";
            SelectedEventId = createdId;
            CreateEventSuccess = true;
            CreateEventMessage = $"Подію «{eventTitle}» створено! Код запрошення для інших учасників: {inviteCode}";
        }
        else
        {
            CreateEventSuccess = false;
            CreateEventMessage = body.TryGetProperty("message", out var msg)
                ? msg.GetString()
                : "Не вдалося створити подію.";
        }

        await LoadPageDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostJoinEventAsync(
        int selectedEventId,
        int currentUserId,
        string? currentUser,
        string inviteCode)
    {
        RestoreSession(selectedEventId, currentUserId, currentUser);

        if (CurrentUserId <= 0)
        {
            JoinEventSuccess = false;
            JoinEventMessage = "Спочатку увійдіть у свій акаунт, щоб приєднатися до події.";
            await LoadPageDataAsync();
            return Page();
        }

        using var http = CreateApiClient();
        var response = await http.PostAsJsonAsync("/api/events/join", new
        {
            userId = CurrentUserId,
            inviteCode = inviteCode ?? ""
        });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (response.IsSuccessStatusCode)
        {
            JoinEventSuccess = true;
            JoinEventMessage = $"Ви ({CurrentUserText}) успішно приєдналися до події за кодом «{inviteCode}»!";
        }
        else
        {
            JoinEventSuccess = false;
            JoinEventMessage = body.TryGetProperty("message", out var msg)
                ? msg.GetString()
                : "Не вдалося приєднатися до події.";
        }

        await LoadPageDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAddExpenseAsync(
        int selectedEventId,
        int currentUserId,
        string? currentUser,
        int payerId,
        string expenseAmount,
        string expenseDesc)
    {
        RestoreSession(selectedEventId, currentUserId, currentUser);

        var normalized = (expenseAmount ?? "").Trim().Replace(',', '.');
        object amountValue = decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : (expenseAmount ?? "");

        using var http = CreateApiClient();
        var response = await http.PostAsJsonAsync($"/api/events/{SelectedEventId}/expenses", new
        {
            payerParticipationId = payerId,
            amount = amountValue,
            description = expenseDesc ?? ""
        });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (response.IsSuccessStatusCode)
        {
            ExpenseSuccess = true;
            ExpenseMessage = $"Витрату «{expenseDesc}» успішно додано!";
        }
        else
        {
            ExpenseSuccess = false;
            ExpenseMessage = body.TryGetProperty("message", out var msg)
                ? msg.GetString()
                : "Помилка додавання витрати.";
        }

        await LoadPageDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDistributeAsync(
        int selectedEventId,
        int currentUserId,
        string? currentUser,
        int distExpenseId,
        int[]? participantIds)
    {
        RestoreSession(selectedEventId, currentUserId, currentUser);

        if (CurrentUserId <= 0)
        {
            DistSuccess = false;
            DistMessage = "Спочатку увійдіть в акаунт Організатора події.";
            await LoadPageDataAsync();
            return Page();
        }

        using var http = CreateApiClient();
        var response = await http.PostAsJsonAsync(
            $"/api/events/{SelectedEventId}/expenses/{distExpenseId}/distribute",
            new DistributeExpenseRequest(CurrentUserId, participantIds ?? []));

        if (response.IsSuccessStatusCode)
        {
            var shares = await response.Content.ReadFromJsonAsync<ExpenseShare[]>() ?? [];
            await LoadPageDataAsync();

            var summary = string.Join(", ", shares.Select(s =>
            {
                var name = Participants.FirstOrDefault(p => p.Id == s.ParticipationId)?.UserName ?? $"#{s.ParticipationId}";
                return $"{name}: {s.Amount:F2} грн";
            }));

            DistSuccess = true;
            DistMessage = $"Витрату успішно розподілено порівну ({summary}).";
        }
        else
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            DistSuccess = false;
            DistMessage = body.TryGetProperty("message", out var msg)
                ? msg.GetString()
                : "Помилка розподілу витрати.";
            await LoadPageDataAsync();
        }

        return Page();
    }

    private async Task LoadPageDataAsync()
    {
        using var http = CreateApiClient();

        Users = await http.GetFromJsonAsync<UserDto[]>("/api/users") ?? [];
        Events = await http.GetFromJsonAsync<EventDetailsDto[]>("/api/events") ?? [];

        if (Events.Length == 0)
        {
            SelectedEventId = 0;
            Participants = [];
            Balances = [];
            Expenses = [];
            return;
        }

        if (!Events.Any(e => e.Id == SelectedEventId))
            SelectedEventId = Events[0].Id;

        Participants = await http.GetFromJsonAsync<ParticipantItem[]>(
            $"/api/events/{SelectedEventId}/participants") ?? [];

        Balances = await http.GetFromJsonAsync<BalanceDto[]>(
            $"/api/events/{SelectedEventId}/balances") ?? [];

        var rawExpenses = await http.GetFromJsonAsync<Expense[]>(
            $"/api/events/{SelectedEventId}/expenses") ?? [];

        var list = new List<ExpenseWithShares>();
        foreach (var ex in rawExpenses)
        {
            var shares = await http.GetFromJsonAsync<ExpenseShare[]>(
                $"/api/events/{SelectedEventId}/expenses/{ex.Id}/shares") ?? [];
            var payerName = Participants.FirstOrDefault(p => p.Id == ex.PayerParticipationId)?.UserName
                ?? $"Учасник #{ex.PayerParticipationId}";
            list.Add(new ExpenseWithShares(ex, payerName, shares));
        }

        Expenses = list.ToArray();
    }
}
