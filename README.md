# Group1_TR_43 — EventOrganizer v1.0.0

Виконано лабораторні роботи №1–4. Поточна версія — **v1.0.0**.

Командний репозиторій першої групи ТР-43 для лабораторних робіт з ТРПЗ.
Продукт: організатор спільних подій і витрат. Можна зареєструватися, створити подію, запросити учасників, внести спільні витрати й розподілити їх порівну.

## Склад команди та внесок у v1.0

| Учасник | GitHub | Сценарій v1.0 |
|---|---|---|
| Марія Ткаченко | @tkmarichka27-jpg | UC-01 «Зареєструватися», UC-02 «Увійти до системи» |
| Іван Мороз | @Fidloc | UC-07 «Створити подію» (а також приєднання, закриття, скасування) |
| Анастасія Халаминдик | @ansstassya | UC-05 «Зареєструвати витрату» |
| Маргарита Тур | @voodoomeggy | UC-09 «Розподілити витрату», UC-06 «Переглянути баланс» |

## Що реалізовано у v1.0

- реєстрація та вхід користувача (FR-01, FR-02);
- створення, перегляд, закриття та скасування події, приєднання за кодом запрошення (FR-03 – FR-05, FR-07, FR-08, FR-13 – FR-15);
- реєстрація спільної витрати (FR-09, FR-10);
- рівний розподіл витрати та перегляд балансів (FR-11, FR-12).

Повна відповідність вимог коду й тестам наведена в [docs/traceability.md](docs/traceability.md).

## Структура репозиторію

| Шлях | Вміст |
|---|---|
| `src/EventOrganizer.Api` | Сервер ASP.NET Core: REST API + веб-сторінка (Razor Page) |
| `src/EventOrganizer.Client` | Консольний клієнт, що викликає API |
| `tests/EventOrganizer.Tests` | Автоматичні тести (xUnit) |
| `docs/` | Вимоги, use cases, проєкт, матриця простежуваності, протоколи перевірок |
| `docs/models/sequences/` | Діаграми послідовності (Mermaid) кожного учасника |

## Вимоги до середовища

- .NET SDK 9.0

## Запуск із вихідного коду

```bash
git clone https://github.com/Fidloc/Group1_TR_43.git
cd Group1_TR_43
dotnet build
dotnet test
```

**1. Сервер**:

```bash
dotnet run --project src/EventOrganizer.Api --launch-profile http
```

Сервер слухає `http://localhost:5076`. Перевірка: `http://localhost:5076/health` → `{"status":"ok"}`.
Веб-інтерфейс відкривається за адресою `http://localhost:5076/`.

**2. Консольний клієнт**:

```bash
dotnet run --project src/EventOrganizer.Client
```

Клієнт спочатку автоматично проходить контрольний сценарій UC-07. Потім він виконує UC-05: запитує ID події, ID платника й суму витрати.
Якщо сервер працює на іншій адресі, задайте змінну середовища `API_BASE_URL`, наприклад:

```bash
# Windows PowerShell
$env:API_BASE_URL = "http://localhost:5076"
# Linux / macOS
export API_BASE_URL=http://localhost:5076
```

## Запуск із пакета випуску v1.0.0

1. Завантажте `EventOrganizer-v1.0.0.zip` зі сторінки [Releases](https://github.com/Fidloc/Group1_TR_43/releases) і розпакуйте.
2. Сервер:
   ```bash
   cd server
   dotnet EventOrganizer.Api.dll --urls http://localhost:5076
   ```
3. Клієнт (в іншому терміналі):
   ```bash
   cd client
   dotnet EventOrganizer.Client.dll
   ```

## Обмеження v1.0: дані зберігаються лише в пам'яті

Усі робочі дані зберігаються у пам'яті серверного процесу у вигляді колекцій `List<T>` у `Program.cs`.

- Дані доступні, поки сервер працює.
- Після зупинки або перезапуску сервера всі дані втрачаються. Після нового запуску колекції порожні.
- Вимога NFR-06, база даних і Docker у v1.0 не реалізовані й заплановані на наступні версії.

## Документація

- [Вимоги](docs/requirements.md) · [Use cases](docs/use-cases.md) · [Проєкт](docs/design.md)
- [План реалізації](docs/implementation-plan.md) · [Матриця простежуваності](docs/traceability.md)
- Протоколи перевірок: [docs/tests/](docs/tests/)
