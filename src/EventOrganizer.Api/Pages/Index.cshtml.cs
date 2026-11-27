using System.Globalization;
using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Pages.ApiFacade;
using Microsoft.AspNetCore.Mvc;

namespace EventOrganizer.Api.Pages;

public sealed class IndexModel : ApiPageModel
{
    private readonly IEventOrganizerApiFacade _api;

    public IndexModel(IEventOrganizerApiFacade api) => _api = api;

    public EventDetailsDto[] Events { get; set; } = [];

    public string? Search { get; set; }
    public string? Status { get; set; }
    public bool OnlyMine { get; set; }

    public string? CreateEventMessage { get; set; }
    public bool CreateEventSuccess { get; set; }

    public string? JoinEventMessage { get; set; }
    public bool JoinEventSuccess { get; set; }

    public async Task<IActionResult> OnGetAsync(string? search = null, string? status = null, bool onlyMine = false)
    {
        if (!IsLoggedIn)
            return RedirectToPage("/Account/Login");

        Search = search;
        Status = status;
        OnlyMine = onlyMine;
        await LoadEventsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostCreateEventAsync(
        string eventTitle,
        string eventDate,
        string eventPlace,
        string eventDescription,
        string? budgetLimit)
    {
        if (!IsLoggedIn)
            return RedirectToPage("/Account/Login");

        object? budgetValue = null;
        if (!string.IsNullOrWhiteSpace(budgetLimit))
        {
            var normalized = budgetLimit.Trim().Replace(',', '.');
            budgetValue = decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : budgetLimit.Trim();
        }

        var result = await _api.CreateEventAsync(
            CurrentUserId,
            eventTitle ?? "",
            eventDate ?? "",
            eventPlace ?? "",
            eventDescription ?? "",
            budgetValue);

        if (result.IsSuccess)
        {
            var (createdId, inviteCode) = result.Data;
            TempData["EventOk"] = $"Подію «{eventTitle}» створено! Код запрошення для інших учасників: {inviteCode}";
            if (createdId > 0)
                return RedirectToPage("/Events/Details", new { id = createdId });

            return RedirectToPage("/Index");
        }

        CreateEventSuccess = false;
        CreateEventMessage = result.ErrorMessage;
        await LoadEventsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostJoinEventAsync(string inviteCode)
    {
        if (!IsLoggedIn)
            return RedirectToPage("/Account/Login");

        var result = await _api.JoinEventAsync(CurrentUserId, inviteCode ?? "");
        if (result.IsSuccess)
        {
            if (result.Data is > 0)
            {
                TempData["EventOk"] = $"Ви успішно приєдналися до події за кодом «{inviteCode}»!";
                return RedirectToPage("/Events/Details", new { id = result.Data.Value });
            }

            JoinEventSuccess = true;
            JoinEventMessage = $"Ви успішно приєдналися до події за кодом «{inviteCode}»!";
            await LoadEventsAsync();
            return Page();
        }

        JoinEventSuccess = false;
        JoinEventMessage = result.ErrorMessage;
        await LoadEventsAsync();
        return Page();
    }

    private async Task LoadEventsAsync()
    {
        Events = await _api.GetEventsAsync(
            OnlyMine && IsLoggedIn ? CurrentUserId : null,
            Status,
            Search);
    }
}
