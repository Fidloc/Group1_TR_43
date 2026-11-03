using System.Net.Http.Json;

using var client = new HttpClient();
client.BaseAddress = new Uri("http://localhost:5076");

Console.WriteLine("=== Тестування UC-01 та UC-02 ===");

// 1. Успішна реєстрація
var registerData = new { Email = "maria@test.com", Password = "password123", Name = "Марія" };
var regResponse = await client.PostAsJsonAsync("/api/users/register", registerData);
Console.WriteLine($"Реєстрація Марії: {(int)regResponse.StatusCode}"); // Очікуємо 201

// 2. Відмова: реєстрація з таким самим email
var regResponse2 = await client.PostAsJsonAsync("/api/users/register", registerData);
Console.WriteLine($"Дубль email: {(int)regResponse2.StatusCode}"); // Очікуємо 409

// 3. Успішний вхід
var loginData = new { Email = "maria@test.com", Password = "password123" };
var loginResponse = await client.PostAsJsonAsync("/api/users/login", loginData);
Console.WriteLine($"Успішний вхід: {(int)loginResponse.StatusCode}"); // Очікуємо 200

// 4. Відмова: невірний пароль
var badLoginData = new { Email = "maria@test.com", Password = "wrongpassword" };
var badLoginResponse = await client.PostAsJsonAsync("/api/users/login", badLoginData);
Console.WriteLine($"Помилка входу: {(int)badLoginResponse.StatusCode}"); // Очікуємо 401