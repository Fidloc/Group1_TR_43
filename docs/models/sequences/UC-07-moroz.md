# Діаграма послідовності UC-07 «Створити подію»

Автор: Іван Мороз (@Fidloc)

Діаграма v1.0: 
Клієнт — `EventActions.RunAsync` (src/EventOrganizer.Client/EventActions.cs)
Сервер — маршрут `POST /api/events` у Program.cs 
Клас `EventManagement` (src/EventOrganizer.Api/Infrastructure/EventManagement.cs)
Колекції `events`, `participations` списки в пам'яті сервера

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



## Відповідність

| Повідомлення на діаграмі | В коді |
|---|---|
| POST /api/events | `app.MapPost("/api/events", ...)` у Program.cs |
| TryReadCreate | `EventManagement.TryReadCreate` — перевірка назви, потім дати |
| TryCreate під lock(gate) | `EventManagement.TryCreate` — дата в минулому, `events.Add`, `participations.Add` |
| ToDetails | `EventManagement.ToDetails` — подія разом зі списком учасників |
| GET /api/events?userId= | `app.MapGet("/api/events", ...)` у Program.cs |
