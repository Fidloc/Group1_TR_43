# Activity Diagram UC-09 «Розділити витрату»

```mermaid
flowchart TB
    A(("Початок")) --> B["1. Обрати витрату"]
    B --> S{"Подія у стані «Створена»?"}
    S -- Ні --> A3["A3. Повідомити, що подія закрита або скасована<br>(розподіл не виконується)"]
    A3 --> K((("Завершення<br>з помилкою")))
    S -- Так --> C["2. Показати суму витрати і список учасників"]
    C --> D["3. Обрати залучених учасників"]
    D --> E["4. Підтвердити розподіл"]
    E --> F{"Обрано хоча б одного учасника?"}
    F -- Ні --> G["A1. Повідомити про помилку<br>(розподіл не зберігається)"]
    G --> D
    F -- Так --> H["6. Обчислити частки порівну<br>(з точністю до 0,01 грн)"]
    H --> I{"Сума часток дорівнює сумі витрати?"}
    I -- Ні --> J["A2. Повідомити про помилку розрахунку<br>(розподіл не зберігається)"]
    J --> K
    I -- Так --> L["8. Зберегти розподіл"]
    L --> M["9. Показати частки та підтвердження"]
    M --> N((("Завершення<br>успішно")))

     A:::startNode
     B:::action
     S:::decision
     A3:::error
     K:::endErr
     C:::action
     D:::action
     E:::action
     F:::decision
     G:::error
     H:::action
     I:::decision
     J:::error
     L:::action
     M:::action
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
    linkStyle 15 stroke:#E1BEE7,fill:none
    linkStyle 16 stroke:#E1BEE7
```
| Крок специфікації UC-09 | Елемент Activity Diagram |
|---|---|
| 1. Вибір витрати та перевірка стану події | Дія «Обрати витрату» і вузол рішення «Подія у стані «Створена»?»; гілка «Ні» веде до A3. |
| 2–4. Перегляд, вибір учасників, підтвердження | Послідовні дії після перевірки стану. |
| 5. Перевірка вибору учасників | Вузол рішення «Обрано хоча б одного учасника?»; гілка «Ні» веде до A1 і назад до вибору учасників. |
| 6. Обчислення часток | Дія «Обчислити частки порівну». |
| 7. Перевірка суми | Вузол рішення «Сума часток дорівнює сумі витрати?»; гілка «Ні» веде до A2. |
| 8–9. Збереження та підтвердження | Успішна гілка після всіх перевірок. |
