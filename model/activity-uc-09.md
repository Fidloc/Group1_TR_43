```mermaid
---
config:
  theme: forest
  fontFamily: "'Merriweather Variable', serif"
  themeVariables:
    fontFamily: "'Merriweather Variable', serif"
  look: handDrawn
---                                                                                                                                                                                                                                                 flowchart TB
    A(["Початок"]) --> B["1. Обрати витрату"]
    B --> C["2. Переглянути суму та список учасників"]
    C --> D["3. Обрати залучених учасників з списку"]
    D --> E["4. Підтвердити розподіл"]
    E --> F{"Обрано хоча б одного учасника?"}
    F -- Ні --> G["A1. Повідомити, що потрібен хоча б один учасник"]
    G --> D
    F -- Так --> H["6. Обчислити частки порівну"]
    H --> I{"Сума часток дорівнює сумі витрати?"}
    I -- Ні --> J["A2. Повідомити про помилку розрахунку"]
    J --> K(["Завершення з помилкою"])
    I -- Так --> L["8. Зберегти розподіл"]
    L --> M["9. Показати частки та підтвердження"]
    M --> N(["Завершення успішно"])

    linkStyle 0 stroke:#E1BEE7,fill:none
    linkStyle 1 stroke:#E1BEE7,fill:none
    linkStyle 2 stroke:#E1BEE7,fill:none
    linkStyle 3 stroke:#E1BEE7,fill:none
    linkStyle 4 stroke:#E1BEE7
    linkStyle 5 stroke:#E1BEE7,fill:none
    linkStyle 6 stroke:#E1BEE7,fill:none
    linkStyle 7 stroke:#E1BEE7,fill:none
    linkStyle 8 stroke:#E1BEE7,fill:none
    linkStyle 9 stroke:#E1BEE7,fill:none
    linkStyle 10 stroke:#E1BEE7,fill:none
    linkStyle 11 stroke:#E1BEE7,fill:none
    linkStyle 12 stroke:#E1BEE7,fill:none
    linkStyle 13 stroke:#E1BEE7,fill:none
