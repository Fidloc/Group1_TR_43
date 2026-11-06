# UC-09: порівняння діаграми діяльності з реалізацією v1.0

Діаграма діяльності: [model/activity-uc-09.md](../../model/activity-uc-09.md). Код: `ExpenseCalculator.cs`, маршрут `POST /api/events/{eventId}/expenses/{expenseId}/distribute` у `Program.cs`, веб-інтерфейс `Pages/Index.cshtml` (`IndexModel.OnPostDistributeAsync`).

| Елемент діаграми | Як зроблено в коді | Висновок |
|---|---|---|
| Крок 1, A3: перевірка стану події «Створена» | Сервер перевіряє `ev.Status != EventStatus.Created` і повертає 409 `event_not_active` | Відповідає (FR-15, AC-15) |
| Кроки 2–4: вибір витрати, позначення учасників та підтвердження | На сторінці `Pages/Index.cshtml` Організатор обирає витрату зі списку, позначає залучених учасників чекбоксами та натискає «Розподілити порівну» (`OnPostDistributeAsync`) | Відповідає |
| Крок 5, A1: перевірка, що обрано хоча б одного учасника | Якщо `selectedIds.Length == 0`, сервер повертає 400 `no_participants_selected` без зміни колекції `shares` | Відповідає |
| Крок 6: обчислення часток порівну (до 0,01 грн) | `ExpenseCalculator.SplitEqually` обчислює рівні частки до 0,01 грн і додає залишок копійок до першої частки | Відповідає (NFR-02) |
| Крок 7, A2: перевірка суми часток | Перевірка `calculated.Sum(s => s.Amount) != expense.Amount` (`sum_mismatch`) | Відповідає (AC-11) |
| Кроки 8–9: збереження та показ часток і балансів | Збереження часток у `shares`, перерахунок балансів (`ExpenseCalculator.GetBalances`) та виведення результату на сторінці `Index.cshtml` | Відповідає (AC-11, AC-12) |
