using System.Globalization;
using System.Net.Http.Json;
using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace EventOrganizer.Api.Pages.Events;

public sealed class DetailsModel : ApiPageModel
{
    public record ParticipantItem(int Id, int UserId, string UserName, string Role);

    public record ExpenseWithShares(Expense Expense, string PayerName, ExpenseShare[] Shares);

    public int EventId { get; set; }
    public EventDetailsDto? Event { get; set; }
    public ParticipantItem[] Participants { get; set; } = [];
    public BalanceDto[] Balances { get; set; } = [];
    public ExpenseWithShares[] Expenses { get; set; } = [];

    public bool IsCurrentUserOrganizer =>
        IsLoggedIn && Participants.Any(p => p.UserId == CurrentUserId && p.Role == "Organizer");

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!IsLoggedIn)
            return RedirectToPage("/Account/Login");

        EventId = id;
        await LoadEventDataAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostCloseEventAsync(int id) =>
        await ChangeStatusAsync(id, "close", "Подію закрито.");

    public async Task<IActionResult> OnPostCancelEventAsync(int id) =>
        await ChangeStatusAsync(id, "cancel", "Подію скасовано.");

    public async Task<IActionResult> OnPostEditEventAsync(
        int id,
        string editTitle,
        string editDate,
        string editPlace,
        string editDesc,
        string? editBudget)
    {
        if (!IsLoggedIn)
            return RedirectToPage("/Account/Login");

        decimal? parsedBudget = null;
        if (!string.IsNullOrWhiteSpace(editBudget))
        {
            var normalized = editBudget.Trim().Replace(',', '.');
            if (!decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
            {
                TempData["EditError"] = "Ліміт бюджету має бути коректним додатним числом.";
                return RedirectToPage(new { id });
            }
            parsedBudget = val;
        }

        using var http = CreateApiClient();
        var response = await http.PutAsJsonAsync($"/api/events/{id}", new UpdateEventRequest(
            CurrentUserId,
            editTitle ?? "",
            editDate ?? "",
            editPlace ?? "",
            editDesc ?? "",
            parsedBudget));

        if (response.IsSuccessStatusCode)
        {
            TempData["EditOk"] = $"Подію «{editTitle}» успішно оновлено!";
        }
        else
        {
            TempData["EditError"] = await ReadApiErrorAsync(response, "Не вдалося оновити подію.");
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAddExpenseAsync(
        int id,
        int payerId,
        string expenseAmount,
        string expenseDesc,
        int[]? shareParticipantIds)
    {
        if (!IsLoggedIn)
            return RedirectToPage("/Account/Login");

        var normalized = (expenseAmount ?? "").Trim().Replace(',', '.');
        object amountValue = decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : (expenseAmount ?? "");

        int[]? selectedShares = shareParticipantIds is { Length: > 0 } ? shareParticipantIds : null;

        using var http = CreateApiClient();
        var response = await http.PostAsJsonAsync($"/api/events/{id}/expenses", new
        {
            userId = CurrentUserId,
            payerParticipationId = payerId,
            amount = amountValue,
            description = expenseDesc ?? "",
            shareParticipantIds = selectedShares
        });

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<CreateExpenseResultDto>();
            var sharesCount = result?.Shares?.Length ?? 0;

            TempData["ExpenseOk"] = sharesCount > 0
                ? $"Витрату «{expenseDesc}» додано та миттєво розподілено між {sharesCount} учасниками!"
                : $"Витрату «{expenseDesc}» успішно додано!";

            if (!string.IsNullOrWhiteSpace(result?.Warning))
            {
                TempData["ExpenseWarning"] = result.Warning;
            }
        }
        else
        {
            TempData["ExpenseError"] = await ReadApiErrorAsync(response, "Помилка додавання витрати.");
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDistributeAsync(
        int id,
        int distExpenseId,
        int[]? participantIds)
    {
        if (!IsLoggedIn)
            return RedirectToPage("/Account/Login");

        using var http = CreateApiClient();
        var response = await http.PostAsJsonAsync(
            $"/api/events/{id}/expenses/{distExpenseId}/distribute",
            new DistributeExpenseRequest(CurrentUserId, participantIds ?? []));

        if (response.IsSuccessStatusCode)
        {
            var shares = await response.Content.ReadFromJsonAsync<ExpenseShare[]>() ?? [];
            var participants = await http.GetFromJsonAsync<ParticipantItem[]>($"/api/events/{id}/participants") ?? [];

            var summary = string.Join(", ", shares.Select(s =>
            {
                var name = participants.FirstOrDefault(p => p.Id == s.ParticipationId)?.UserName ?? $"#{s.ParticipationId}";
                return $"{name}: {s.Amount:F2} грн";
            }));

            TempData["DistOk"] = $"Витрату успішно розподілено порівну ({summary}).";
        }
        else
        {
            TempData["DistError"] = await ReadApiErrorAsync(response, "Помилка розподілу витрати.");
        }

        return RedirectToPage(new { id });
    }

    private async Task<IActionResult> ChangeStatusAsync(int id, string action, string okMessage)
    {
        if (!IsLoggedIn)
            return RedirectToPage("/Account/Login");

        using var http = CreateApiClient();
        var response = await http.PostAsJsonAsync($"/api/events/{id}/{action}", new { userId = CurrentUserId });

        if (response.IsSuccessStatusCode)
        {
            TempData["EventOk"] = okMessage;
        }
        else
        {
            TempData["EventError"] = await ReadApiErrorAsync(response, "Не вдалося змінити статус події.");
        }

        return RedirectToPage(new { id });
    }

    private async Task LoadEventDataAsync(int id)
    {
        if (id <= 0)
            return;

        using var http = CreateApiClient();
        var evResponse = await http.GetAsync($"/api/events/{id}");
        if (!evResponse.IsSuccessStatusCode)
            return;

        Event = await evResponse.Content.ReadFromJsonAsync<EventDetailsDto>();
        if (Event is null)
            return;

        Participants = await http.GetFromJsonAsync<ParticipantItem[]>($"/api/events/{id}/participants") ?? [];
        Balances = await http.GetFromJsonAsync<BalanceDto[]>($"/api/events/{id}/balances") ?? [];

        var rawExpenses = await http.GetFromJsonAsync<Expense[]>($"/api/events/{id}/expenses") ?? [];
        var list = new List<ExpenseWithShares>();
        foreach (var ex in rawExpenses)
        {
            var shares = await http.GetFromJsonAsync<ExpenseShare[]>($"/api/events/{id}/expenses/{ex.Id}/shares") ?? [];
            var payerName = Participants.FirstOrDefault(p => p.Id == ex.PayerParticipationId)?.UserName
                ?? $"Учасник #{ex.PayerParticipationId}";
            list.Add(new ExpenseWithShares(ex, payerName, shares));
        }

        Expenses = list.ToArray();
    }
}
