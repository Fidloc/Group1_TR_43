using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using EventOrganizer.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace EventOrganizer.Api.Pages;

public sealed class IndexModel : ApiPageModel
{
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

        using var http = CreateApiClient();
        var response = await http.PostAsJsonAsync("/api/events", new
        {
            organizerUserId = CurrentUserId,
            title = eventTitle ?? "",
            date = eventDate ?? "",
            place = eventPlace ?? "",
            description = eventDescription ?? "",
            budgetLimit = budgetValue
        });

        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            var createdId = body.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;
            var inviteCode = body.TryGetProperty("inviteCode", out var codeProp) ? codeProp.GetString() : "";

            TempData["EventOk"] = $"Подію «{eventTitle}» створено! Код запрошення для інших учасників: {inviteCode}";
            if (createdId > 0)
                return RedirectToPage("/Events/Details", new { id = createdId });

            return RedirectToPage("/Index");
        }

        CreateEventSuccess = false;
        CreateEventMessage = await ReadApiErrorAsync(response, "Не вдалося створити подію.");
        await LoadEventsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostJoinEventAsync(string inviteCode)
    {
        if (!IsLoggedIn)
            return RedirectToPage("/Account/Login");

        using var http = CreateApiClient();
        var response = await http.PostAsJsonAsync("/api/events/join", new
        {
            userId = CurrentUserId,
            inviteCode = inviteCode ?? ""
        });

        if (response.IsSuccessStatusCode)
        {
            var location = response.Headers.Location?.ToString();
            var lastSegment = location?.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            if (int.TryParse(lastSegment, out var joinedEventId) && joinedEventId > 0)
            {
                TempData["EventOk"] = $"Ви успішно приєдналися до події за кодом «{inviteCode}»!";
                return RedirectToPage("/Events/Details", new { id = joinedEventId });
            }

            JoinEventSuccess = true;
            JoinEventMessage = $"Ви успішно приєдналися до події за кодом «{inviteCode}»!";
            await LoadEventsAsync();
            return Page();
        }

        JoinEventSuccess = false;
        JoinEventMessage = await ReadApiErrorAsync(response, "Не вдалося приєднатися до події.");
        await LoadEventsAsync();
        return Page();
    }

    private async Task LoadEventsAsync()
    {
        using var http = CreateApiClient();

        var queryParts = new List<string>();
        if (OnlyMine && IsLoggedIn)
            queryParts.Add($"userId={CurrentUserId}");

        if (!string.IsNullOrWhiteSpace(Status))
            queryParts.Add($"status={Uri.EscapeDataString(Status.Trim())}");

        if (!string.IsNullOrWhiteSpace(Search))
            queryParts.Add($"search={Uri.EscapeDataString(Search.Trim())}");

        var url = queryParts.Count == 0
            ? "/api/events"
            : $"/api/events?{string.Join("&", queryParts)}";

        Events = await http.GetFromJsonAsync<EventDetailsDto[]>(url) ?? [];
    }
}
