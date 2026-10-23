# Activity Diagram UC-05 «Зареєструвати витрату»

Діаграма показує, як учасник додає спільну витрату до події і де система може її відхилити: некоректна сума або скасування дії. Розподіл витрати між учасниками виконується окремо в UC-09. Пов'язані вимоги: FR-09, FR-10.

```mermaid
flowchart TB
    A(("Початок")) --> B["1. Відкрити сторінку події"]
    B --> S{"Подія у стані «Створена»?"}
    S -- Ні --> A3["A3. Повідомити, що подія закрита або скасована<br>(додавання витрати заблоковано)"]
    A3 --> K((("Завершення<br>з помилкою")))
    S -- Так --> C["2. Обрати «Додати витрату»"]
    C --> D["3. Ввести суму та опис"]
    D --> E["4. Вказати, хто заплатив"]
    E --> F{"5. Підтвердити чи скасувати?"}
    F -- Скасувати --> G((("A2. Завершення<br>без змін")))
    F -- Підтвердити --> H{"6. Сума — число і більша за 0?"}
    H -- Ні --> I["A1. Повідомити про некоректну суму"]
    I --> D
    H -- Так --> J["7. Зберегти витрату"]
    J --> L["8. Показати витрату в обліку події"]
    L --> N((("Завершення<br>успішно")))

    A:::startNode
    B:::action
    S:::decision
    A3:::error
    K:::endErr
    C:::action
    D:::action
    E:::action
    F:::decision
    G:::endErr
    H:::decision
    I:::error
    J:::action
    L:::action
    N:::endOk

    classDef action fill:#dbeafe,stroke:#2563eb,color:#000
    classDef decision fill:#fef08a,stroke:#ca8a04,color:#000
    classDef error fill:#fecaca,stroke:#dc2626,color:#000
    classDef startNode fill:#222,stroke:#222,color:#fff
    classDef endErr fill:#fff,stroke:#000,stroke-width:3px,color:#000
    classDef endOk fill:#fff,stroke:#16a34a,stroke-width:3px,color:#000

    linkStyle 0 stroke:#E1BEE7,fill:none
    linkStyle 1 stroke:#E1BEE7,fill:none
    linkStyle 2 stroke:#E1BEE7,fill:none
    linkStyle 3 stroke:#E1BEE7,fill:none
    linkStyle 4 stroke:#E1BEE7,fill:none
    linkStyle 5 stroke:#E1BEE7,fill:none
    linkStyle 6 stroke:#E1BEE7,fill:none
    linkStyle 7 stroke:#E1BEE7,fill:none
    linkStyle 8 stroke:#E1BEE7,fill:none
    linkStyle 9 stroke:#E1BEE7,fill:none
    linkStyle 10 stroke:#E1BEE7,fill:none
    linkStyle 11 stroke:#E1BEE7,fill:none
    linkStyle 12 stroke:#E1BEE7,fill:none
    linkStyle 13 stroke:#E1BEE7,fill:none
    linkStyle 14 stroke:#E1BEE7,fill:none
    linkStyle 15 stroke:#E1BEE7
```

| Крок специфікації | Елемент діаграми |
|---|---|
| 1–4 | Послідовні дії на початку |
| 5, A2 | Рішення «Підтвердити чи скасувати?», гілка «Скасувати» завершує без змін |
| 6, A1 | Рішення «Сума — число і більша за 0?», гілка «Ні» повертає до введення суми |
| 7–8 | Успішна гілка: збереження та показ витрати |
