# Діаграма послідовності UC-01 «Зареєструватися» та UC-02 «Увійти до системи»

Діаграма де клієнтська частина — Razor Page `Pages/Index.cshtml` (`IndexModel.OnPostRegisterAsync`, `IndexModel.OnPostLoginAsync`), серверна частина — маршрути `POST /api/users/register` та `POST /api/users/login` у `Program.cs` і колекція `users` у пам'яті.

```mermaid
sequenceDiagram
    participant U as Користувач (Браузер)
    participant P as Razor Page (IndexModel)
    participant S as Сервер API (Program.cs)
    participant M as Колекція users

    Note over U,M: UC-01: Реєстрація користувача
    U->>P: Вводить ім'я, email і пароль, натискає «Зареєструватися»
    P->>S: POST /api/users/register (RegisterUserRequest)
    alt Email або пароль порожні
        S-->>P: 400 Bad Request (invalid_request)
        P-->>U: Повідомлення про помилку
    else Поля заповнені
        S->>M: Перевірка users.Any(u => u.Email == request.Email)
        alt Email вже використовується
            S-->>P: 409 Conflict (email_already_exists)
            P-->>U: Повідомлення про помилку
        else Email вільний (AC-01)
            S->>M: users.Add(newUser)
            S-->>P: 201 Created (User)
            P-->>U: Повідомлення про успішну реєстрацію
        end
    end

    Note over U,M: UC-02: Вхід у систему
    U->>P: Вводить email і пароль, натискає «Увійти»
    P->>S: POST /api/users/login (LoginRequest)
    S->>M: Пошук users.FirstOrDefault(Email, PasswordHash)
    alt Користувача не знайдено або пароль невірний (A1, A2)
        S-->>P: 401 Unauthorized
        P-->>U: Повідомлення «Невірна електронна пошта або пароль»
    else Облікові дані вірні (AC-01)
        S-->>P: 200 OK (userId, message)
        P-->>U: Оновити поточний акаунт і показати події
    end
```
