# Діаграма послідовності UC-05 «Зареєструвати витрату»

Автор: Анастасія Халаминдик (@ansstassya)

Діаграма зроблена за кодом v1.0. Клієнт — `ExpenseActions.RunAsync` (src/EventOrganizer.Client/ExpenseActions.cs), веб-сторінка `Pages/Index` (`OnPostAddExpenseAsync`) надсилає такий самий запит. Сервер — маршрут `POST /api/events/{eventId}/expenses` у Program.cs і клас `ExpenseRegistration` (src/EventOrganizer.Api/Infrastructure/ExpenseRegistration.cs). Колекції `events`, `participations`, `expenses` — списки в пам'яті сервера.

```mermaid
sequenceDiagram
    actor O as Організатор
    participant C as Клієнт (ExpenseActions)
    participant A as API (Program.cs)
    participant R as ExpenseRegistration
    participant M as Колекції в пам'яті

    O->>C: ID події, свій ID, хто заплатив
    loop поки сума некоректна (A1)
        O->>C: сума і опис
        break сума порожня — скасування (A2)
            C-->>O: «Скасовано», запит не надсилається
        end
        C->>A: POST /api/events/{eventId}/expenses (userId, payerParticipationId, amount, description)
        A->>R: TryRead(body)
        alt немає userId або платника
            R-->>A: false, invalid_request
            A-->>C: 400 ApiError
        else сума ≤ 0, не число або більше 2 знаків після коми
            R-->>A: false, invalid_amount
            A-->>C: 400 ApiError
            C-->>O: причина, ввести суму ще раз
        else формат коректний
            R-->>A: true, CreateExpenseRequest
            A->>R: TryAdd(events, participations, expenses, eventId, request) під lock(gate)
            R->>M: знайти подію за eventId
            M-->>R: event
            alt подію не знайдено
                R-->>A: event_not_found
                A-->>C: 404 ApiError
            else подія закрита або скасована (A3)
                R-->>A: event_not_active
                A-->>C: 409 ApiError
            else користувач не Організатор цієї події (A4)
                R-->>A: forbidden_not_organizer
                A-->>C: 403 ApiError
            else платник не є учасником цієї події
                R-->>A: participant_not_found
                A-->>C: 404 ApiError
            else все добре
                R->>M: expenses.Add(expense)
                R-->>A: null (успіх), expense
                A-->>C: 201 Created + витрата
            end
        end
    end
    C->>A: GET /api/events/{eventId}/expenses
    A-->>C: 200 список витрат події
    C-->>O: облік витрат події
```

У гілках з відмовою немає виклику `expenses.Add`, тому облік події не змінюється. Останній GET клієнт робить, щоб показати, що витрата з'явилась (AC-09) або що після відмови список такий самий (AC-10, AC-15). Після скасування (A2) клієнт нічого не надсилає і завершує дію без GET.
