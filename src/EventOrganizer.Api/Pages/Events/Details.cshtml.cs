using System.Globalization;
using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Pages.ApiFacade;
using Microsoft.AspNetCore.Mvc;

namespace EventOrganizer.Api.Pages.Events;

public sealed class DetailsModel : ApiPageModel
{
    private readonly IEventOrganizerApiFacade _api;

    public DetailsModel(IEventOrganizerApiFacade api) => _api = api;

    public int EventId { get; set; }
    public EventDetailsDto? Event { get; set; }
    public EventParticipantItem[] Participants { get; set; } = [];
    public BalanceDto[] Balances { get; set; } = [];
    public SettlementTransferDto[] Settlements { get; set; } = [];
    public ExpenseWithSharesItem[] Expenses { get; set; } = [];

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

        var result = await _api.UpdateEventAsync(id, new UpdateEventRequest(
            CurrentUserId,
            editTitle ?? "",
            editDate ?? "",
            editPlace ?? "",
            editDesc ?? "",
            parsedBudget));

        if (result.IsSuccess)
        {
            TempData["EditOk"] = $"Подію «{editTitle}» успішно оновлено!";
        }
        else
        {
            TempData["EditError"] = result.ErrorMessage;
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

        var result = await _api.AddExpenseAsync(
            id,
            CurrentUserId,
            payerId,
            amountValue,
            expenseDesc ?? "",
            shareParticipantIds);

        if (result.IsSuccess)
        {
            var sharesCount = result.Data?.Shares?.Length ?? 0;

            TempData["ExpenseOk"] = sharesCount > 0
                ? $"Витрату «{expenseDesc}» додано та миттєво розподілено між {sharesCount} учасниками!"
                : $"Витрату «{expenseDesc}» успішно додано!";

            if (!string.IsNullOrWhiteSpace(result.WarningMessage))
            {
                TempData["ExpenseWarning"] = result.WarningMessage;
            }
        }
        else
        {
            TempData["ExpenseError"] = result.ErrorMessage;
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

        var result = await _api.DistributeExpenseAsync(id, distExpenseId, CurrentUserId, participantIds ?? []);
        if (result.IsSuccess)
        {
            var (shares, participants) = result.Data;
            var summary = string.Join(", ", shares.Select(s =>
            {
                var name = participants.FirstOrDefault(p => p.Id == s.ParticipationId)?.UserName ?? $"#{s.ParticipationId}";
                return $"{name}: {s.Amount:F2} грн";
            }));

            TempData["DistOk"] = $"Витрату успішно розподілено порівну ({summary}).";
        }
        else
        {
            TempData["DistError"] = result.ErrorMessage;
        }

        return RedirectToPage(new { id });
    }

    private async Task<IActionResult> ChangeStatusAsync(int id, string action, string okMessage)
    {
        if (!IsLoggedIn)
            return RedirectToPage("/Account/Login");

        var result = await _api.ChangeEventStatusAsync(id, CurrentUserId, action);
        if (result.IsSuccess)
        {
            TempData["EventOk"] = okMessage;
        }
        else
        {
            TempData["EventError"] = result.ErrorMessage;
        }

        return RedirectToPage(new { id });
    }

    private async Task LoadEventDataAsync(int id)
    {
        var data = await _api.LoadEventDetailsAsync(id);
        if (data is null)
            return;

        Event = data.Event;
        Participants = data.Participants;
        Balances = data.Balances;
        Expenses = data.Expenses;
        Settlements = await _api.GetSettlementsAsync(id);
    }
}
