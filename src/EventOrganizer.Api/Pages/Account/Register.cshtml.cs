using EventOrganizer.Api.Pages.ApiFacade;
using Microsoft.AspNetCore.Mvc;

namespace EventOrganizer.Api.Pages.Account;

public sealed class RegisterModel : ApiPageModel
{
    private readonly IEventOrganizerApiFacade _api;

    public RegisterModel(IEventOrganizerApiFacade api) => _api = api;

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
        var result = await _api.RegisterAsync(Name ?? "", Email ?? "", Password ?? "");
        if (result.IsSuccess)
        {
            SuccessMessage = $"Користувача «{Name}» ({Email}) успішно зареєстровано!";
            Name = "";
            Email = "";
            Password = "";
            return Page();
        }

        ErrorMessage = result.ErrorMessage;
        return Page();
    }
}
