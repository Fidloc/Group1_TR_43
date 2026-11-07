using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

// Клієнтська дія UC-05 «Зареєструвати витрату»
public static class ExpenseActions
{
    public static async Task RunAsync(HttpClient http)
    {
        Console.WriteLine();
        Console.WriteLine("=== UC-05: Додати витрату ===");

        var eventId = ReadInt("ID події: ");
        var userId = ReadInt("Ваш ID користувача (Організатор події): ");
        var payerId = ReadInt("ID учасника, який заплатив (participationId): ");

        while (true)
        {
            Console.Write("Сума, грн (порожньо — скасувати): ");
            var amountText = (Console.ReadLine() ?? "").Trim().Replace(',', '.');
            if (amountText.Length == 0)
            {
                // A2: користувач скасував дію, запит не надсилається
                Console.WriteLine("Скасовано, витрату не збережено.");
                return;
            }
            Console.Write("Опис: ");
            var description = Console.ReadLine() ?? "";

            var body = new JsonObject
            {
                ["userId"] = userId,
                ["payerParticipationId"] = payerId,
                ["description"] = description
            };
            // Якщо введено не число, передаємо як є: сервер має відповісти invalid_amount (AC-10)
            if (decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
                body["amount"] = amount;
            else
                body["amount"] = amountText;

            using var response = await http.PostAsJsonAsync($"/api/events/{eventId}/expenses", body);
            var code = await ShowAsync(response);

            // A1: некоректна сума — повертаємось до введення суми
            if (code == "invalid_amount")
            {
                Console.WriteLine("Спробуйте ввести суму ще раз.");
                continue;
            }
            break;
        }

        Console.WriteLine("Облік витрат події після запиту:");
        using var list = await http.GetAsync($"/api/events/{eventId}/expenses");
        await ShowAsync(list);
    }

    private static int ReadInt(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            if (int.TryParse(Console.ReadLine(), out var value))
                return value;
            Console.WriteLine("Потрібно ввести ціле число.");
        }
    }

    // Показує відповідь сервера; повертає код відмови або null
    private static async Task<string?> ShowAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"HTTP {(int)response.StatusCode}");
        if (string.IsNullOrWhiteSpace(text))
            return response.IsSuccessStatusCode ? null : "invalid_request";

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        if (!response.IsSuccessStatusCode &&
            root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("code", out var code) &&
            root.TryGetProperty("message", out var message))
        {
            Console.WriteLine($"Відмова: {code.GetString()} — {message.GetString()}");
            return code.GetString();
        }

        Console.WriteLine(JsonSerializer.Serialize(root, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));
        return response.IsSuccessStatusCode ? null : "unknown_error";
    }
}
