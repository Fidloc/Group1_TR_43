using System.Text.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EventOrganizer.Api.Pages;

public abstract class ApiPageModel : PageModel
{
    protected int CurrentUserId => HttpContext.Session.GetInt32("UserId") ?? 0;
    protected string CurrentUserText => HttpContext.Session.GetString("UserText") ?? "Не авторизовано";
    protected bool IsLoggedIn => CurrentUserId > 0;

    protected HttpClient CreateApiClient()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        return new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    protected static async Task<string> ReadApiErrorAsync(HttpResponseMessage response, string fallback)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (body.ValueKind == JsonValueKind.Object &&
                body.TryGetProperty("message", out var msg) &&
                !string.IsNullOrWhiteSpace(msg.GetString()))
            {
                return msg.GetString()!;
            }
        }
        catch
        {
            // Ігноруємо помилку розбору тіла та повертаємо базове повідомлення
        }

        return fallback;
    }
}
