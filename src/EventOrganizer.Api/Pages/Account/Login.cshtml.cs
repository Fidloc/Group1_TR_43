using EventOrganizer.Api.Pages.ApiFacade;
using Microsoft.AspNetCore.Mvc;

namespace EventOrganizer.Api.Pages.Account;

public sealed class LoginModel : ApiPageModel
{
    private readonly IEventOrganizerApiFacade _api;

    public LoginModel(IEventOrganizerApiFacade api) => _api = api;

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
        var result = await _api.LoginAsync(Email ?? "", Password ?? "");
        if (!result.IsSuccess || result.Data is null)
        {
            ErrorMessage = result.ErrorMessage;
            return Page();
        }

        HttpContext.Session.SetInt32("UserId", result.Data.UserId);
        HttpContext.Session.SetString("UserText", result.Data.UserText);
        TempData["DashboardOk"] = $"Ви успішно увійшли в акаунт {result.Data.UserText}!";

        return RedirectToPage("/Index");
    }
}
