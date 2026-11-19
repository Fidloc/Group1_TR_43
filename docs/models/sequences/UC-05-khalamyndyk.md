# Діаграма послідовності UC-05 «Зареєструвати витрату» (Спринт 2 / US-03)

**Базовий автор:** Анастасія Халаминдик (@ansstassya)  
**Актуалізація під архітектуру Спринту 2:** Марія Ткаченко  

Діаграма відображає фактичний потік викликів у багатошаровій архітектурі `EventOrganizer` (Спринт 2): веб-клієнт Razor Page (`Pages/Events/Details.cshtml.cs`), мінімальний ендпоінт `ExpenseEndpoints` (`POST /api/events/{eventId}/expenses`), прикладний сервіс `ExpenseService`, чисті доменні правила `BudgetRule` та `ExpenseCalculator`, репозиторій `SqliteExpenseRepository` з явною транзакцією БД та реляційну базу даних `SQLite`.

```mermaid
sequenceDiagram
    actor O as Організатор
    participant P as Клієнт (Razor Page)
    participant E as ExpenseEndpoints
    participant S as ExpenseService
    participant B as BudgetRule
    participant Calc as ExpenseCalculator
    participant Repo as SqliteExpenseRepository
    database DB as SQLite

    O->>P: Вводить суму, опис, платника та позначає учасників розподілу
    P->>E: POST /api/events/{eventId}/expenses (CreateExpenseRequest)
    E->>E: ExpenseRegistration.TryRead(body)
    alt Некоректний формат або сума ≤ 0 (A1)
        E-->>P: 400 Bad Request (invalid_request / invalid_amount)
        P-->>O: Повідомлення про помилку валідації
    else Формат запиту коректний
        E->>S: CreateExpenseAsync(eventId, request)
        S->>Repo: GetEventByIdAsync(eventId), GetParticipationsByEventIdAsync(eventId)
        Repo->>DB: SELECT Events, Participations
        DB-->>Repo: event, participations
        alt Подію не знайдено / неактивна (A3) / не Організатор (A4) / платник не знайдений
            S-->>E: error (404 / 409 / 403)
            E-->>P: ApiError
            P-->>O: Повідомлення про відмову
        else Подія активна і права Організатора підтверджено
            S->>Repo: GetTotalSpentByEventIdAsync(eventId)
            Repo->>DB: SELECT SUM(Amount) FROM Expenses WHERE EventId = @eventId
            DB-->>Repo: alreadySpent
            S->>B: Evaluate(event.BudgetLimit, alreadySpent, request.Amount)
            alt Перевищення ліміту > 100% (A5 / Rejected)
                B-->>S: BudgetCheckResult(Rejected)
                S-->>E: budget_exceeded
                E-->>P: 409 Conflict (budget_exceeded)
                P-->>O: Червоне повідомлення про перевищення бюджету (дані не збережено)
            else Дозволено (Allowed < 80% або Warning 80%–100%)
                B-->>S: BudgetCheckResult(Allowed | Warning, warningMessage)
                opt Передано ShareParticipantIds (миттєвий розподіл)
                    S->>Calc: SplitEqually(0, request.Amount, shareParticipantIds)
                    Calc-->>S: List<ExpenseShare>
                end
                S->>Repo: AddExpenseWithSharesAsync(expense, shares)
                activate Repo
                Repo->>DB: BEGIN TRANSACTION
                Repo->>DB: INSERT INTO Expenses (...)
                opt shares не порожній
                    Repo->>DB: INSERT INTO ExpenseShares (...)
                end
                alt Помилка БД або спрацювання тригера SQLite
                    Repo->>DB: ROLLBACK
                    Repo-->>S: DbUpdateException
                    S-->>E: Помилка збереження (зміни відкочено)
                    E-->>P: 500 / 409 Помилка транзакції
                    P-->>O: Повідомлення про помилку (часткових записів у БД немає)
                else Транзакція успішна
                    Repo->>DB: COMMIT
                    deactivate Repo
                    Repo-->>S: expense, shares
                    S-->>E: CreateExpenseResultDto(expense, warningMessage, shares)
                    E-->>P: 201 Created + CreateExpenseResultDto
                    opt warningMessage != null (використано 80%–100% бюджету)
                        P-->>O: Жовтий банер попередження про наближення до ліміту
                    end
                    P-->>O: Оновити список витрат, панель бюджету та баланси
                end
            end
        end
    end