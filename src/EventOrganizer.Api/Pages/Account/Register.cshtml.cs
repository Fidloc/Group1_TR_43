using System.Net.Http.Json;
using EventOrganizer.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace EventOrganizer.Api.Pages.Account;

public sealed class RegisterModel : ApiPageModel
{
    [BindProperty]
    public string Name { get; set; } = "";

    [BindProperty]
    public string Email { get; set; } = "";

    [BindProperty]
    public string Password { get; set; } = "";

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public IActionResult OnGet()
    {
        if (IsLoggedIn)
            return RedirectToPage("/Index");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        using var http = CreateApiClient();
        var response = await http.PostAsJsonAsync("/api/users/register",
            new RegisterUserRequest(Email ?? "", Password ?? "", Name ?? ""));

        if (response.IsSuccessStatusCode)
        {
            SuccessMessage = $"Користувача «{Name}» ({Email}) успішно зареєстровано!";
            Name = "";
            Email = "";
            Password = "";
            return Page();
        }

        ErrorMessage = await ReadApiErrorAsync(response, "Не вдалося зареєструвати користувача.");
        return Page();
    }
}
