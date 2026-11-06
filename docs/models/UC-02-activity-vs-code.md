# UC-02: порівняння діаграми діяльності з реалізацією v1.0

Діаграма діяльності: [model/activity-uc-02.md](../../model/activity-uc-02.md). Код: маршрути `POST /api/users/register` та `POST /api/users/login` у `Program.cs`, веб-інтерфейс `Pages/Index.cshtml` (`IndexModel.OnPostRegisterAsync`, `IndexModel.OnPostLoginAsync`).

| Елемент діаграми | Як зроблено в коді | Висновок |
|---|---|---|
| Кроки 1–3: відкриття форми, введення email і пароля та передавання даних | На сторінці `Pages/Index.cshtml` користувач заповнює поля форми й надсилає запит у `POST /api/users/register` або `POST /api/users/login` | Відповідає |
| Крок 4, рішення «Поля заповнені та коректні?», A1 | У `POST /api/users/register` перевіряється `string.IsNullOrWhiteSpace(request.Email)` і `string.IsNullOrWhiteSpace(request.Password)` → `400 Bad Request (invalid_request)`; при дублі email → `409 Conflict (email_already_exists)` | Відповідає (FR-01, FR-02) |
| Крок 5, рішення «Email і пароль вірні?», A2 | У `POST /api/users/login` виконується пошук `users.FirstOrDefault(u => u.Email == request.Email && u.PasswordHash == request.Password)`; якщо `null` → `401 Unauthorized` | Відповідає (AC-02) |
| Кроки 6–7: створення сесії та перехід на головну сторінку | При успішному вході сервер повертає `200 OK` з `userId`, а `IndexModel` фіксує поточний акаунт користувача та завантажує список подій | Відповідає (AC-01) |
