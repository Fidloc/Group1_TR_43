Console.OutputEncoding = System.Text.Encoding.UTF8;

var baseUrl = Environment.GetEnvironmentVariable("API_BASE_URL") ?? "http://localhost:5076";
using var http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(10) };

await EventActions.RunAsync(http);
await ExpenseActions.RunAsync(http);
