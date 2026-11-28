# Модель фактичної реалізації (Спринт 3 / v2.0.0)

## 1. Усунення дублювання HTTP-логіки шаблоном GoF «Фасад»

У попередніх версіях класи сторінок веб-клієнта містили `code smell` — пряме створення `HttpClient` у понад 15 місцях, дублювання формування URL/query-параметрів і ручний розбір `JsonElement` / кодів помилок API.

Під час рефакторингу у Спринті 3 застосовано структурний шаблон проєктування GoF **«Фасад»**:
* Виокремлено єдиний клієнтський інтерфейс **`IEventOrganizerApiFacade`** та його реалізацію **`EventOrganizerApiFacade(HttpClient)`** у просторі імен `EventOrganizer.Api.Pages.ApiFacade`.
* Сторінки Razor Pages більше не залежать від `HttpClient` і деталей серіалізації JSON — кожна сторінка делегує роботу з REST API одному методу фасаду.
* Передача `HttpClient` через конструктор фасаду дозволила покрити всю взаємодію з API захисними тестами (`WebApiFacadeTests`) із використанням тестового двійника `FakeHttpHandler` без запуску вебсервера.
* Поверх оновленої архітектури додано нову користувацьку операцію **`US-04` / `UC-12` «План взаєморозрахунків»** (`GET /api/events/{eventId}/settlements` → `IEventOrganizerApiFacade.GetSettlementsAsync`), яка обчислює мінімальний набір переказів між учасниками без зміни схеми бази даних.

---

## 2. Діаграма класів фактичної реалізації (Mermaid)

```mermaid
classDiagram
    direction TB

    namespace RazorPagesUI {
        class ApiPageModel {
            <<abstract>>
            #CurrentUserId int
            #CurrentUserText string
            #IsLoggedIn bool
        }
        class LoginModel {
            -IEventOrganizerApiFacade _api
            +OnPostAsync() Task~IActionResult~
        }
        class RegisterModel {
            -IEventOrganizerApiFacade _api
            +OnPostAsync() Task~IActionResult~
        }
        class IndexModel {
            -IEventOrganizerApiFacade _api
            +OnGetAsync() Task~IActionResult~
            +OnPostCreateEventAsync() Task~IActionResult~
            +OnPostJoinEventAsync() Task~IActionResult~
        }
        class DetailsModel {
            -IEventOrganizerApiFacade _api
            +SettlementTransferDto[] Settlements
            +OnGetAsync(id) Task~IActionResult~
            +OnPostAddExpenseAsync(id) Task~IActionResult~
            +OnPostDistributeAsync(id, expenseId) Task~IActionResult~
        }
    }

    namespace WebApiFacade_GoF {
        class IEventOrganizerApiFacade {
            <<interface>>
            +LoginAsync(email, password) Task~ApiCallResult~
            +RegisterAsync(name, email, password) Task~ApiCallResult~
            +GetEventsAsync(userId, status, search) Task~EventDetailsDto[]~
            +CreateEventAsync(...) Task~ApiCallResult~
            +JoinEventAsync(userId, inviteCode) Task~ApiCallResult~
            +LoadEventDetailsAsync(eventId) Task~EventDetailsViewData~
            +UpdateEventAsync(eventId, request) Task~ApiCallResult~
            +ChangeEventStatusAsync(eventId, userId, action) Task~ApiCallResult~
            +AddExpenseAsync(...) Task~ApiCallResult~
            +DistributeExpenseAsync(...) Task~ApiCallResult~
            +GetSettlementsAsync(eventId) Task~SettlementTransferDto[]~
        }
        class EventOrganizerApiFacade {
            -HttpClient _httpClient
            +EventOrganizerApiFacade(httpClient)
        }
        class HttpClient
    }

    namespace BackendAPI_And_Domain {
        class DistributionEndpoints {
            +MapDistributionEndpoints(app)
        }
        class IExpenseService {
            <<interface>>
            +GetBalancesAsync(eventId) Task
            +GetSettlementsAsync(eventId) Task
        }
        class ExpenseService {
            -IEventRepository _eventRepository
            -IExpenseRepository _expenseRepository
            +GetSettlementsAsync(eventId) Task
        }
        class ExpenseCalculator {
            <<static>>
            +SplitEqually(...) List~ExpenseShare~
            +GetBalances(...) BalanceDto[]
            +CalculateSettlements(balances) SettlementTransferDto[]
        }
        class SettlementTransferDto {
            +int FromParticipationId
            +int FromUserId
            +string FromUserName
            +int ToParticipationId
            +int ToUserId
            +string ToUserName
            +decimal Amount
        }
    }

    ApiPageModel <|-- LoginModel
    ApiPageModel <|-- RegisterModel
    ApiPageModel <|-- IndexModel
    ApiPageModel <|-- DetailsModel

    LoginModel --> IEventOrganizerApiFacade : використовує
    RegisterModel --> IEventOrganizerApiFacade : використовує
    IndexModel --> IEventOrganizerApiFacade : використовує
    DetailsModel --> IEventOrganizerApiFacade : використовує
    DetailsModel ..> SettlementTransferDto : відображає

    IEventOrganizerApiFacade <|.. EventOrganizerApiFacade : реалізує
    EventOrganizerApiFacade --> HttpClient : надсилає HTTP-запити

    DistributionEndpoints --> IExpenseService : GET /api/events/id/settlements
    IExpenseService <|.. ExpenseService : реалізує
    ExpenseService --> ExpenseCalculator : викликає CalculateSettlements
    ExpenseCalculator ..> SettlementTransferDto : формує список переказів
```

---

## 3. Відповідність елементів моделі файлам коду

| Компонент моделі | Шлях у репозиторії (`src/EventOrganizer.Api`) | Роль у системі |
|---|---|---|
| `IEventOrganizerApiFacade` | `Pages/ApiFacade/IEventOrganizerApiFacade.cs` | Контракт фасаду (шаблон GoF `Facade`) для всіх сторінок веб-клієнта |
| `EventOrganizerApiFacade` | `Pages/ApiFacade/EventOrganizerApiFacade.cs` | Інкапсуляція `HttpClient`, маршрутів `/api/...`, query-рядків та розбору JSON-відповідей і помилок |
| `ApiCallResult` / DTO клієнта | `Pages/ApiFacade/ApiCallResult.cs` | Моделі результатів викликів фасаду (`LoginResult`, `EventDetailsViewData`, `EventParticipantItem`) |
| `LoginModel`, `RegisterModel`, `IndexModel`, `DetailsModel` | `Pages/Account/*.cshtml.cs`, `Pages/Index.cshtml.cs`, `Pages/Events/Details.cshtml.cs` | UI-логіка Razor Pages, очищена від прямих викликів `HttpClient` |
| `DistributionEndpoints` | `Endpoints/DistributionEndpoints.cs` | Мінімальний ендпоінт `GET /api/events/{eventId:int}/settlements` |
| `ExpenseService` | `Services/Implementations/ExpenseService.cs` | Координація перевірки події, отримання балансів та розрахунку переказів (`GetSettlementsAsync`) |
| `ExpenseCalculator` | `Services/ExpenseCalculator.cs` | Чиста функція `CalculateSettlements` жадібного зведення боргів і кредитів із точністю до `0,01 грн` |
| `SettlementTransferDto` | `Contracts/SettlementTransferDto.cs` | Контракт одного переказу між боржником і кредитором |
