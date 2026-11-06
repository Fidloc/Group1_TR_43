# Діаграма послідовності UC-09 «Розподілити витрату»

Діаграма де клієнтська частина — Razor Page `Pages/Index.cshtml` (`IndexModel.OnPostDistributeAsync`), серверна частина — маршрут `POST /api/events/{eventId}/expenses/{expenseId}/distribute` у `Program.cs` та розрахунковий клас `ExpenseCalculator` (`src/EventOrganizer.Api/Infrastructure/ExpenseCalculator.cs`).

```mermaid
sequenceDiagram
    participant U as Організатор (Браузер)
    participant P as Razor Page (IndexModel)
    participant S as Сервер API (Program.cs)
    participant Calc as ExpenseCalculator
    participant M as Колекції в пам'яті

    U->>P: Обирає витрату, позначає учасників чекбоксами, натискає «Розподілити порівну»
    P->>S: POST /api/events/{eventId}/expenses/{expenseId}/distribute
    alt Не обрано жодного учасника (A1)
        S-->>P: 400 Bad Request (no_participants_selected)
        P-->>U: Повідомлення про помилку (попередній розподіл не змінено)
    else Учасників обрано
        S->>M: Перевірити подію, роль Організатора, витрату та учасників
        M-->>S: Event, Expense, Participations
        alt Подія у стані Closed або Cancelled (FR-15 / A3)
            S-->>P: 409 Conflict (event_not_active)
            P-->>U: Повідомлення про помилку
        else Подія активна і дані коректні
            S->>Calc: SplitEqually(expenseId, amount, selectedIds)
            Calc-->>S: List<ExpenseShare>
            alt Сума часток != сумі витрати (A2)
                S-->>P: 400 Bad Request (sum_mismatch)
                P-->>U: Повідомлення про помилку
            else Сума часток дорівнює сумі витрати (AC-11)
                S->>M: Зберегти частки у shares
                S-->>P: 200 OK (ExpenseShare[])
                P->>S: GET /api/events/{eventId}/balances (UC-06)
                S-->>P: 200 OK (BalanceDto[])
                P-->>U: Оновити частки витрати та баланси учасників на сторінці
            end
        end
    end
```
