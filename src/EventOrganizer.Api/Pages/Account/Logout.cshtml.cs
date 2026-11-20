using Microsoft.AspNetCore.Mvc;

namespace EventOrganizer.Api.Pages.Account;

public sealed class LogoutModel : ApiPageModel
{
    public IActionResult OnGet()
    {
        HttpContext.Session.Clear();
        return RedirectToPage("/Account/Login");
    }

    public IActionResult OnPost()
    {
        HttpContext.Session.Clear();
        return RedirectToPage("/Account/Login");
    }
}
