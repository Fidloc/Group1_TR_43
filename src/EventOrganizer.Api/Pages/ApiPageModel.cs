using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EventOrganizer.Api.Pages;

public abstract class ApiPageModel : PageModel
{
    protected int CurrentUserId => HttpContext.Session.GetInt32("UserId") ?? 0;
    protected string CurrentUserText => HttpContext.Session.GetString("UserText") ?? "Не авторизовано";
    protected bool IsLoggedIn => CurrentUserId > 0;
}
