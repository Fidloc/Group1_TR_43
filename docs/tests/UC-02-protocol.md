# Протокол перевірок UC-01 «Зареєструватися» та UC-02 «Увійти до системи»

## Автоматичні тести

Команда: `dotnet test --filter FullyQualifiedName~AuthTests`

Результат: total: 3; failed: 0; succeeded: 3; skipped: 0 (загалом по рішенню: 34/34 пройдено)

## Перевірки через клієнт і HTTP

| Критерій | Дія | Очікування | Фактичний результат |
|---|---|---|---|
| AC-01 (UC-01) | `POST /api/users/register` з коректними даними (`maria@test.com`, `password123`, `Марія`) | 201 Created; користувача додано до списку `users` з `Id = 1` | 201 Created; користувача збережено |
| AC-02 (UC-01) | Повторний `POST /api/users/register` з тим самим `maria@test.com` | 409 Conflict (`email_already_exists`); дубль не створено | 409 Conflict (`email_already_exists`) |
| AC-02 (A1) | `POST /api/users/register` з порожнім email або паролем | 400 Bad Request (`invalid_request`); запис не створено | 400 Bad Request (`invalid_request`) |
| AC-01 (UC-02) | `POST /api/users/login` з вірним email і паролем (`maria@test.com`, `password123`) | 200 OK; повертається `userId = 1` | 200 OK (`Вхід успішний`, `userId = 1`) |
| AC-02 (UC-02, A2) | `POST /api/users/login` з невірним паролем (`wrongpassword`) | 401 Unauthorized; вхід відхилено | 401 Unauthorized |
