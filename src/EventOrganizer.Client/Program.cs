var baseUrl = Environment.GetEnvironmentVariable("API_BASE_URL") ?? "http://localhost:5076";
using var http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(10) };

// UC-05: реєстрація витрати
await ExpenseActions.RunAsync(http);
