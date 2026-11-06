# Діаграма послідовності UC-07 «Створити подію»

Автор: Іван Мороз (@Fidloc)

Діаграма зроблена за фактичним кодом v1.0: клієнт — `EventActions.RunAsync` (src/EventOrganizer.Client/EventActions.cs), сервер — маршрут `POST /api/events` у Program.cs і клас `EventManagement` (src/EventOrganizer.Api/Infrastructure/EventManagement.cs). Колекції `events` і `participations` — це списки в пам'яті сервера, а не окремий клас сховища.

```mermaid
sequenceDiagram
    participant C as Клієнт (EventActions)
    participant A as API (Program.cs)
    participant E as EventManagement
    participant M as Колекції в пам'яті

    C->>A: POST /api/events (organizerUserId, title, date, place, description)
    A->>E: TryReadCreate(body)
    alt назву не вказано (A1)
        E-->>A: false, missing_title
        A-->>C: 400 ApiError
    else дату не вказано або вона не є датою (A2)
        E-->>A: false, invalid_date
        A-->>C: 400 ApiError
    else формат коректний
        E-->>A: true, CreateEventRequest
        A->>E: TryCreate(events, participations, request, DateTime.Now) під lock(gate)
        alt дата в минулому (A2)
            E-->>A: invalid_date
            A-->>C: 400 ApiError
        else все добре
            E->>M: events.Add(event) — стан Created, новий InviteCode
            E->>M: participations.Add(participation) — роль Organizer
            E-->>A: null (успіх), event
            A->>E: ToDetails(event, participations)
            E-->>A: EventDetailsDto
            A-->>C: 201 Created + сторінка події
        end
    end
    C->>A: GET /api/events?userId={organizerUserId}
    A-->>C: 200 список подій користувача
```

У гілках з відмовою немає викликів `events.Add` і `participations.Add`, тому список подій не змінюється. Останній GET клієнт робить, щоб показати, що подія з'явилась у списку (AC-03) або що після відмови список такий самий, як був (AC-04).

## Відповідність коду

| Повідомлення на діаграмі | Де в коді |
|---|---|
| POST /api/events | `app.MapPost("/api/events", ...)` у Program.cs |
| TryReadCreate | `EventManagement.TryReadCreate` — перевірка назви, потім дати |
| TryCreate під lock(gate) | `EventManagement.TryCreate` — дата в минулому, `events.Add`, `participations.Add` |
| ToDetails | `EventManagement.ToDetails` — подія разом зі списком учасників |
| GET /api/events?userId= | `app.MapGet("/api/events", ...)` у Program.cs |
