using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;

// Cценарій UC-07
public static class EventActions
{
    private const int OrganizerId = 1;
    private const int GuestId = 2;
    private const int LateGuestId = 3;

    private static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static async Task RunAsync(HttpClient http)
    {
        Console.WriteLine();
        Console.WriteLine("=== UC-07: Створити подію ===");

        var nextWeek = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd");
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        var yesterday = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");

        Step("1. Коректна подія (очікуємо 201, створювач — Організатор)");
        var created = await PostAsync(http, "/api/events", new
        {
            organizerUserId = OrganizerId,
            title = "Похід у Карпати",
            date = nextWeek,
            place = "Ворохта",
            description = "Вихідні в горах"
        });
        if (created is null)
        {
            Console.WriteLine("Подію не створено — далі перевіряти нема що. Чи запущено сервер?");
            return;
        }
        var eventId = created.Value.GetProperty("id").GetInt32();
        var inviteCode = created.Value.GetProperty("inviteCode").GetString()!;

        Step("2. A1: назву не вказано (очікуємо 400 missing_title)");
        await PostAsync(http, "/api/events", new
        {
            organizerUserId = OrganizerId, title = "   ", date = nextWeek, place = "Київ", description = ""
        });

        Step("3. A2: дата в минулому (очікуємо 400 invalid_date)");
        await PostAsync(http, "/api/events", new
        {
            organizerUserId = OrganizerId, title = "Вчорашня зустріч", date = yesterday, place = "Київ", description = ""
        });

        Step("4. A2: дату не вказано (очікуємо 400 invalid_date)");
        await PostAsync(http, "/api/events", new
        {
            organizerUserId = OrganizerId, title = "Без дати", place = "Київ", description = ""
        });

        Step($"5. Список подій організатора: після відмов має бути рівно одна подія (id {eventId})");
        var list = await GetAsync(http, $"/api/events?userId={OrganizerId}");
        if (list is not null)
            Console.WriteLine($"Кількість подій організатора: {list.Value.GetArrayLength()}");

        Step("6. UC-03: сторінка події (очікуємо 200)");
        await GetAsync(http, $"/api/events/{eventId}");

        Step("7. UC-04: користувач 2 приєднується за кодом запрошення (очікуємо 201)");
        await PostAsync(http, "/api/events/join", new { userId = GuestId, inviteCode });

        Step("8. Повторне приєднання того самого користувача (очікуємо 409 already_joined)");
        await PostAsync(http, "/api/events/join", new { userId = GuestId, inviteCode });

        Step("9. UC-10: учасник (не Організатор) намагається закрити подію (очікуємо 403)");
        await PostAsync(http, $"/api/events/{eventId}/close", new { userId = GuestId });

        Step("10. UC-10: Організатор закриває подію (очікуємо 200, статус Closed)");
        await PostAsync(http, $"/api/events/{eventId}/close", new { userId = OrganizerId });

        Step("11. FR-15: приєднання до закритої події (очікуємо 409 event_not_active)");
        await PostAsync(http, "/api/events/join", new { userId = LateGuestId, inviteCode });

        Step("12. UC-11: подія на сьогодні створюється, потім Організатор її скасовує (201, потім 200)");
        var second = await PostAsync(http, "/api/events", new
        {
            organizerUserId = OrganizerId, title = "Кава сьогодні", date = today, place = "Львів", description = ""
        });
        if (second is not null)
        {
            var secondId = second.Value.GetProperty("id").GetInt32();
            await PostAsync(http, $"/api/events/{secondId}/cancel", new { userId = OrganizerId });

            Step("13. Повторне скасування (очікуємо 409 event_not_active)");
            await PostAsync(http, $"/api/events/{secondId}/cancel", new { userId = OrganizerId });
        }

        Step("14. Підсумок: сторінка першої події — статус Closed, двоє учасників");
        await GetAsync(http, $"/api/events/{eventId}");
    }

    private static void Step(string title)
    {
        Console.WriteLine();
        Console.WriteLine($"--- {title}");
    }

    private static async Task<JsonElement?> PostAsync(HttpClient http, string route, object body)
    {
        Console.WriteLine($"POST {route} {JsonSerializer.Serialize(body, Pretty).ReplaceLineEndings(" ")}");
        using var response = await http.PostAsJsonAsync(route, body);
        return await ShowAsync(response);
    }

    private static async Task<JsonElement?> GetAsync(HttpClient http, string route)
    {
        Console.WriteLine($"GET {route}");
        using var response = await http.GetAsync(route);
        return await ShowAsync(response);
    }

    private static async Task<JsonElement?> ShowAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"HTTP {(int)response.StatusCode}");
        if (string.IsNullOrWhiteSpace(text))
            return null;

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        if (!response.IsSuccessStatusCode)
        {
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("code", out var code) &&
                root.TryGetProperty("message", out var message))
                Console.WriteLine($"Відмова: {code.GetString()} — {message.GetString()}");
            else
                Console.WriteLine(text);
            return null;
        }

        Console.WriteLine(JsonSerializer.Serialize(root, Pretty));
        return root.Clone();
    }
}
