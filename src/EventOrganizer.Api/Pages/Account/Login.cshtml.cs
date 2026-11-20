using System.Net.Http.Json;
using System.Text.Json;
using EventOrganizer.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace EventOrganizer.Api.Pages.Account;

public sealed class LoginModel : ApiPageModel
{
    [BindProperty]
    public string Email { get; set; } = "";

    [BindProperty]
    public string Password { get; set; } = "";

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet()
    {
        if (IsLoggedIn)
            return RedirectToPage("/Index");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        using var http = CreateApiClient();
        var response = await http.PostAsJsonAsync("/api/users/login",
            new LoginRequest(Email ?? "", Password ?? ""));

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = await ReadApiErrorAsync(response, "Невірна електронна пошта або пароль.");
            return Page();
        }

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var userId = body.TryGetProperty("userId", out var idProp) ? idProp.GetInt32() : 0;

        var users = await http.GetFromJsonAsync<UserDto[]>("/api/users") ?? [];
        var userObj = users.FirstOrDefault(u => u.Id == userId);
        var displayName = !string.IsNullOrWhiteSpace(userObj?.Name) ? userObj.Name : Email;

        HttpContext.Session.SetInt32("UserId", userId);
        HttpContext.Session.SetString("UserText", $"{displayName} ({Email})");
        TempData["DashboardOk"] = $"Ви успішно увійшли в акаунт {displayName} ({Email})!";

        return RedirectToPage("/Index");
    }
}
