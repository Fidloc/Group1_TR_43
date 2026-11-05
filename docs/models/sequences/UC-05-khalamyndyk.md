# Діаграма послідовності UC-05 «Зареєструвати витрату»

Автор: Анастасія Халаминдик (@ansstassya)

Діаграма зроблена за фактичним кодом v1.0: клієнт — `ExpenseActions.RunAsync` (src/EventOrganizer.Client/ExpenseActions.cs), сервер — маршрут `POST /api/events/{eventId}/expenses` у Program.cs і клас `ExpenseRegistration` (src/EventOrganizer.Api/Infrastructure/ExpenseRegistration.cs). Колекції `events`, `participations`, `expenses` — це списки в пам'яті сервера, а не окремий клас сховища.

```mermaid
sequenceDiagram
    participant C as Клієнт (ExpenseActions)
    participant A as API (Program.cs)
    participant R as ExpenseRegistration
    participant M as Колекції в пам'яті

    C->>A: POST /api/events/{eventId}/expenses (payerParticipationId, amount, description)
    A->>R: TryRead(body)
    alt сума ≤ 0, не число або більше 2 знаків після коми
        R-->>A: false, invalid_amount
        A-->>C: 400 ApiError
    else формат коректний
        R-->>A: true, CreateExpenseRequest
        A->>R: TryAdd(events, participations, expenses, eventId, request) під lock(gate)
        R->>M: знайти подію за eventId
        M-->>R: event
        alt подію не знайдено
            R-->>A: event_not_found
            A-->>C: 404 ApiError
        else подія закрита або скасована
            R-->>A: event_not_active
            A-->>C: 409 ApiError
        else платник не є учасником цієї події
            R-->>A: participant_not_found
            A-->>C: 404 ApiError
        else все добре
            R->>M: expenses.Add(expense)
            R-->>A: null (успіх), expense
            A-->>C: 201 Created + витрата
        end
    end
    C->>A: GET /api/events/{eventId}/expenses
    A-->>C: 200 список витрат події
```

У гілках з відмовою немає виклику `expenses.Add`, тому облік події не змінюється. Останній GET клієнт робить, щоб показати, що витрата справді з'явилась (AC-09) або що після відмови список такий самий, як був (AC-10, AC-15).
