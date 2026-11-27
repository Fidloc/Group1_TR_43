using System.Net;
using System.Text.Json;
using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Pages.ApiFacade;

namespace EventOrganizer.Tests;

public class WebApiFacadeTests
{
    private static (EventOrganizerApiFacade Facade, FakeHttpHandler Handler) CreateFacade()
    {
        var handler = new FakeHttpHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        return (new EventOrganizerApiFacade(httpClient), handler);
    }

    private static JsonElement BodyOf(FakeHttpHandler.RecordedRequest request) =>
        JsonDocument.Parse(request.Body!).RootElement;

    private const string EventJson = """
        {"id":3,"title":"Пікнік","date":"2027-05-01T00:00:00","place":"Парк","description":"",
         "inviteCode":"ABC123","status":"Active","organizerUserId":1,"participants":[],
         "budgetLimit":1000.00,"totalSpent":300.00}
        """;

    [Fact]
    public async Task Login_Success_PostsCredentials_AndBuildsUserTextFromUserName()
    {
        var (facade, handler) = CreateFacade();
        handler
            .When(HttpMethod.Post, "/api/users/login", HttpStatusCode.OK, """{"userId":2}""")
            .When(HttpMethod.Get, "/api/users", HttpStatusCode.OK,
                """[{"id":1,"email":"m@test.com","name":"Маргарита"},{"id":2,"email":"i@test.com","name":"Іван"}]""");

        var result = await facade.LoginAsync("i@test.com", "secret1");

        Assert.True(result.IsSuccess);
        Assert.Equal(new LoginResult(2, "Іван (i@test.com)"), result.Data);

        var login = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, login.Method);
        Assert.Equal("/api/users/login", login.Uri.AbsolutePath);
        Assert.Equal("i@test.com", BodyOf(login).GetProperty("email").GetString());
        Assert.Equal("secret1", BodyOf(login).GetProperty("password").GetString());

        Assert.Equal(HttpMethod.Get, handler.Requests[1].Method);
        Assert.Equal("/api/users", handler.Requests[1].Uri.AbsolutePath);
    }

    [Fact]
    public async Task Login_Success_UserNotInList_UsesEmailAsDisplayName()
    {
        var (facade, handler) = CreateFacade();
        handler
            .When(HttpMethod.Post, "/api/users/login", HttpStatusCode.OK, """{"userId":7}""")
            .When(HttpMethod.Get, "/api/users", HttpStatusCode.OK, "[]");

        var result = await facade.LoginAsync("new@test.com", "secret1");

        Assert.Equal("new@test.com (new@test.com)", result.Data!.UserText);
    }

    [Fact]
    public async Task Login_401_ReturnsServerMessage_AndDoesNotRequestUsers()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Post, "/api/users/login", HttpStatusCode.Unauthorized,
            """{"code":"invalid_credentials","message":"Невірний email або пароль."}""");

        var result = await facade.LoginAsync("i@test.com", "wrong");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal("Невірний email або пароль.", result.ErrorMessage);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Login_ErrorWithoutJsonBody_ReturnsFallbackMessage()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Post, "/api/users/login", HttpStatusCode.Unauthorized);

        var result = await facade.LoginAsync("i@test.com", "wrong");

        Assert.Equal("Невірна електронна пошта або пароль.", result.ErrorMessage);
    }

    [Fact]
    public async Task Register_Success_PostsNameEmailPassword()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Post, "/api/users/register", HttpStatusCode.Created, """{"id":5}""");

        var result = await facade.RegisterAsync("Настя", "n@test.com", "secret1");

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpMethod.Post, handler.RequestMethod);
        Assert.Equal("/api/users/register", handler.RequestUri!.AbsolutePath);
        var body = BodyOf(handler.Requests[0]);
        Assert.Equal("Настя", body.GetProperty("name").GetString());
        Assert.Equal("n@test.com", body.GetProperty("email").GetString());
        Assert.Equal("secret1", body.GetProperty("password").GetString());
    }

    [Fact]
    public async Task Register_409_ReturnsServerMessage()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Post, "/api/users/register", HttpStatusCode.Conflict,
            """{"code":"email_exists","message":"Користувач із таким email уже зареєстрований."}""");

        var result = await facade.RegisterAsync("Настя", "n@test.com", "secret1");

        Assert.False(result.IsSuccess);
        Assert.Equal("Користувач із таким email уже зареєстрований.", result.ErrorMessage);
    }

    [Fact]
    public async Task Register_ErrorWithInvalidBody_ReturnsFallbackMessage()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Post, "/api/users/register", HttpStatusCode.BadRequest, "not json");

        var result = await facade.RegisterAsync("", "", "");

        Assert.Equal("Не вдалося зареєструвати користувача.", result.ErrorMessage);
    }

    [Fact]
    public async Task GetEvents_WithoutFilters_RequestsPlainUrl()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Get, "/api/events", HttpStatusCode.OK, $"[{EventJson}]");

        var events = await facade.GetEventsAsync();

        Assert.Equal(HttpMethod.Get, handler.RequestMethod);
        Assert.Equal("/api/events", handler.RequestUri!.PathAndQuery);
        var ev = Assert.Single(events);
        Assert.Equal("Пікнік", ev.Title);
        Assert.Equal(1000.00m, ev.BudgetLimit);
    }

    [Fact]
    public async Task GetEvents_WithAllFilters_BuildsTrimmedEscapedQuery()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Get, "/api/events", HttpStatusCode.OK, "[]");

        await facade.GetEventsAsync(userId: 4, status: " Active ", search: " день народження ");

        Assert.Equal(
            "/api/events?userId=4&status=Active&search=%D0%B4%D0%B5%D0%BD%D1%8C%20%D0%BD%D0%B0%D1%80%D0%BE%D0%B4%D0%B6%D0%B5%D0%BD%D0%BD%D1%8F",
            handler.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task GetEvents_BlankFilters_AreNotAddedToQuery()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Get, "/api/events", HttpStatusCode.OK, "[]");

        await facade.GetEventsAsync(userId: null, status: "  ", search: "");

        Assert.Equal("/api/events", handler.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task GetEvents_ServerError_ThrowsLikeCurrentPage()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Get, "/api/events", HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<HttpRequestException>(() => facade.GetEventsAsync());
    }

    [Fact]
    public async Task CreateEvent_Success_PostsAllFields_AndReturnsIdAndInviteCode()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Post, "/api/events", HttpStatusCode.Created,
            """{"id":12,"inviteCode":"XYZ789"}""");

        var result = await facade.CreateEventAsync(1, "Пікнік", "2027-05-01", "Парк", "Опис", 1500.50m);

        Assert.True(result.IsSuccess);
        Assert.Equal((12, "XYZ789"), result.Data);

        var body = BodyOf(handler.Requests[0]);
        Assert.Equal(HttpMethod.Post, handler.RequestMethod);
        Assert.Equal("/api/events", handler.RequestUri!.AbsolutePath);
        Assert.Equal(1, body.GetProperty("organizerUserId").GetInt32());
        Assert.Equal("Пікнік", body.GetProperty("title").GetString());
        Assert.Equal("2027-05-01", body.GetProperty("date").GetString());
        Assert.Equal("Парк", body.GetProperty("place").GetString());
        Assert.Equal("Опис", body.GetProperty("description").GetString());
        Assert.Equal(1500.50m, body.GetProperty("budgetLimit").GetDecimal());
    }

    [Fact]
    public async Task CreateEvent_WithoutBudget_SendsNullBudgetLimit()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Post, "/api/events", HttpStatusCode.Created, """{"id":13,"inviteCode":"Q1"}""");

        await facade.CreateEventAsync(1, "Зустріч", "2027-05-01", "", "", null);

        Assert.Equal(JsonValueKind.Null, BodyOf(handler.Requests[0]).GetProperty("budgetLimit").ValueKind);
    }

    [Fact]
    public async Task CreateEvent_NonNumericBudget_IsSentAsString()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Post, "/api/events", HttpStatusCode.BadRequest,
            """{"code":"invalid_budget","message":"Бюджет має бути додатним числом (не більше 10 000 000 грн) із точністю до копійок."}""");

        var result = await facade.CreateEventAsync(1, "Зустріч", "2027-05-01", "", "", "abc");

        Assert.Equal("abc", BodyOf(handler.Requests[0]).GetProperty("budgetLimit").GetString());
        Assert.False(result.IsSuccess);
        Assert.Equal(
            "Бюджет має бути додатним числом (не більше 10 000 000 грн) із точністю до копійок.",
            result.ErrorMessage);
    }

    [Fact]
    public async Task CreateEvent_ErrorWithoutBody_ReturnsFallbackMessage()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Post, "/api/events", HttpStatusCode.BadRequest);

        var result = await facade.CreateEventAsync(1, "", "", "", "", null);

        Assert.Equal("Не вдалося створити подію.", result.ErrorMessage);
    }
    
    [Fact]
    public async Task JoinEvent_Success_ReadsEventIdFromLocationHeader()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Post, "/api/events/join", HttpStatusCode.Created,
            """{"id":20}""", location: "/api/events/7");

        var result = await facade.JoinEventAsync(4, "ABC123");

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Data);
        var body = BodyOf(handler.Requests[0]);
        Assert.Equal(4, body.GetProperty("userId").GetInt32());
        Assert.Equal("ABC123", body.GetProperty("inviteCode").GetString());
    }

    [Fact]
    public async Task JoinEvent_SuccessWithoutLocation_ReturnsNullEventId()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Post, "/api/events/join", HttpStatusCode.OK, "{}");

        var result = await facade.JoinEventAsync(4, "ABC123");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task JoinEvent_409_ReturnsServerMessage()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Post, "/api/events/join", HttpStatusCode.Conflict,
            """{"code":"already_joined","message":"Користувач уже є учасником цієї події."}""");

        var result = await facade.JoinEventAsync(4, "ABC123");

        Assert.False(result.IsSuccess);
        Assert.Equal("Користувач уже є учасником цієї події.", result.ErrorMessage);
    }

    [Fact]
    public async Task LoadEventDetails_CombinesAllRequestsIntoOneResult()
    {
        var (facade, handler) = CreateFacade();
        handler
            .When(HttpMethod.Get, "/api/events/3", HttpStatusCode.OK, EventJson)
            .When(HttpMethod.Get, "/api/events/3/participants", HttpStatusCode.OK,
                """[{"id":10,"userId":1,"userName":"Маргарита","role":"Organizer"},{"id":11,"userId":2,"userName":"Іван","role":"Participant"}]""")
            .When(HttpMethod.Get, "/api/events/3/balances", HttpStatusCode.OK,
                """[{"participationId":10,"userId":1,"userName":"Маргарита","paid":300,"owed":150,"balance":150},{"participationId":11,"userId":2,"userName":"Іван","paid":0,"owed":150,"balance":-150}]""")
            .When(HttpMethod.Get, "/api/events/3/expenses", HttpStatusCode.OK,
                """[{"id":5,"eventId":3,"payerParticipationId":10,"amount":300,"description":"Продукти"},{"id":6,"eventId":3,"payerParticipationId":99,"amount":10,"description":"Інше"}]""")
            .When(HttpMethod.Get, "/api/events/3/expenses/5/shares", HttpStatusCode.OK,
                """[{"id":1,"expenseId":5,"participationId":10,"amount":150},{"id":2,"expenseId":5,"participationId":11,"amount":150}]""")
            .When(HttpMethod.Get, "/api/events/3/expenses/6/shares", HttpStatusCode.OK, "[]");

        var data = await facade.LoadEventDetailsAsync(3);

        Assert.NotNull(data);
        Assert.Equal("Пікнік", data.Event.Title);
        Assert.Equal(2, data.Participants.Length);
        Assert.Equal(-150m, data.Balances.Single(b => b.UserId == 2).Balance);
        Assert.Equal(2, data.Expenses.Length);
        Assert.Equal("Маргарита", data.Expenses[0].PayerName);
        Assert.Equal(2, data.Expenses[0].Shares.Length);
        Assert.Equal("Учасник #99", data.Expenses[1].PayerName);

        Assert.All(handler.Requests, r => Assert.Equal(HttpMethod.Get, r.Method));
        Assert.Equal(
            [
                "/api/events/3",
                "/api/events/3/participants",
                "/api/events/3/balances",
                "/api/events/3/expenses",
                "/api/events/3/expenses/5/shares",
                "/api/events/3/expenses/6/shares"
            ],
            handler.Requests.Select(r => r.Uri.AbsolutePath));
    }

    [Fact]
    public async Task LoadEventDetails_EventNotFound_ReturnsNull_WithoutFurtherRequests()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Get, "/api/events/404", HttpStatusCode.NotFound,
            """{"code":"event_not_found","message":"Подію не знайдено."}""");

        var data = await facade.LoadEventDetailsAsync(404);

        Assert.Null(data);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task LoadEventDetails_InvalidId_ReturnsNull_WithoutRequests(int eventId)
    {
        var (facade, handler) = CreateFacade();

        var data = await facade.LoadEventDetailsAsync(eventId);

        Assert.Null(data);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task UpdateEvent_Success_SendsPutWithBudget()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Put, "/api/events/3", HttpStatusCode.OK, EventJson);

        var result = await facade.UpdateEventAsync(3,
            new UpdateEventRequest(1, "Пікнік 2", "2027-06-01", "Ліс", "Новий опис", 2000m));

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpMethod.Put, handler.RequestMethod);
        Assert.Equal("/api/events/3", handler.RequestUri!.AbsolutePath);
        var body = BodyOf(handler.Requests[0]);
        Assert.Equal(1, body.GetProperty("userId").GetInt32());
        Assert.Equal("Пікнік 2", body.GetProperty("title").GetString());
        Assert.Equal(2000m, body.GetProperty("budgetLimit").GetDecimal());
    }

    [Fact]
    public async Task UpdateEvent_403_ReturnsServerMessage()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Put, "/api/events/3", HttpStatusCode.Forbidden,
            """{"code":"forbidden_not_organizer","message":"Цю дію може виконати лише Організатор події."}""");

        var result = await facade.UpdateEventAsync(3, new UpdateEventRequest(2, "X", "2027-06-01", "", ""));

        Assert.False(result.IsSuccess);
        Assert.Equal("Цю дію може виконати лише Організатор події.", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateEvent_ErrorWithoutBody_ReturnsFallbackMessage()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Put, "/api/events/3", HttpStatusCode.BadRequest);

        var result = await facade.UpdateEventAsync(3, new UpdateEventRequest(1, "", "", "", ""));

        Assert.Equal("Не вдалося оновити подію.", result.ErrorMessage);
    }

    [Theory]
    [InlineData("close")]
    [InlineData("cancel")]
    public async Task ChangeEventStatus_PostsUserIdToActionRoute(string action)
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Post, $"/api/events/3/{action}", HttpStatusCode.OK, EventJson);

        var result = await facade.ChangeEventStatusAsync(3, 1, action);

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpMethod.Post, handler.RequestMethod);
        Assert.Equal($"/api/events/3/{action}", handler.RequestUri!.AbsolutePath);
        Assert.Equal(1, BodyOf(handler.Requests[0]).GetProperty("userId").GetInt32());
    }

    [Fact]
    public async Task ChangeEventStatus_409_ReturnsServerMessage()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Post, "/api/events/3/cancel", HttpStatusCode.Conflict,
            """{"code":"event_not_active","message":"Дія заборонена: подія закрита або скасована."}""");

        var result = await facade.ChangeEventStatusAsync(3, 1, "cancel");

        Assert.False(result.IsSuccess);
        Assert.Equal("Дія заборонена: подія закрита або скасована.", result.ErrorMessage);
    }

    [Fact]
    public async Task ChangeEventStatus_ErrorWithoutBody_ReturnsFallbackMessage()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Post, "/api/events/3/close", HttpStatusCode.Forbidden);

        var result = await facade.ChangeEventStatusAsync(3, 2, "close");

        Assert.Equal("Не вдалося змінити статус події.", result.ErrorMessage);
    }

    [Fact]
    public async Task AddExpense_Success_PostsFields_AndReturnsSharesAndWarning()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Post, "/api/events/3/expenses", HttpStatusCode.Created, """
            {"expense":{"id":5,"eventId":3,"payerParticipationId":10,"amount":850,"description":"Оренда"},
             "warning":"Використано 85% бюджету події. Залишок: 150.00 грн.",
             "shares":[{"id":1,"expenseId":5,"participationId":10,"amount":425},{"id":2,"expenseId":5,"participationId":11,"amount":425}]}
            """);

        var result = await facade.AddExpenseAsync(3, 1, 10, 850m, "Оренда", [10, 11]);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Shares.Length);
        Assert.Equal("Використано 85% бюджету події. Залишок: 150.00 грн.", result.WarningMessage);

        var body = BodyOf(handler.Requests[0]);
        Assert.Equal(HttpMethod.Post, handler.RequestMethod);
        Assert.Equal("/api/events/3/expenses", handler.RequestUri!.AbsolutePath);
        Assert.Equal(1, body.GetProperty("userId").GetInt32());
        Assert.Equal(10, body.GetProperty("payerParticipationId").GetInt32());
        Assert.Equal(850m, body.GetProperty("amount").GetDecimal());
        Assert.Equal("Оренда", body.GetProperty("description").GetString());
        Assert.Equal([10, 11], body.GetProperty("shareParticipantIds").EnumerateArray().Select(e => e.GetInt32()));
    }

    [Fact]
    public async Task AddExpense_WithoutWarning_ReturnsNullWarning()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Post, "/api/events/3/expenses", HttpStatusCode.Created,
            """{"expense":{"id":5,"eventId":3,"payerParticipationId":10,"amount":100,"description":"Чай"},"warning":null,"shares":[]}""");

        var result = await facade.AddExpenseAsync(3, 1, 10, 100m, "Чай", null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.WarningMessage);
        Assert.Empty(result.Data!.Shares);
    }

    [Fact]
    public async Task AddExpense_EmptyShareSelection_IsSentAsNull()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Post, "/api/events/3/expenses", HttpStatusCode.Created,
            """{"expense":{"id":5,"eventId":3,"payerParticipationId":10,"amount":100,"description":"Чай"},"warning":null,"shares":[]}""");

        await facade.AddExpenseAsync(3, 1, 10, 100m, "Чай", []);

        Assert.Equal(JsonValueKind.Null,
            BodyOf(handler.Requests[0]).GetProperty("shareParticipantIds").ValueKind);
    }

    [Fact]
    public async Task AddExpense_NonNumericAmount_IsSentAsString_And400MessageReturned()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Post, "/api/events/3/expenses", HttpStatusCode.BadRequest,
            """{"code":"invalid_amount","message":"Сума витрати має бути числом, більшим за 0."}""");

        var result = await facade.AddExpenseAsync(3, 1, 10, "abc", "Чай", null);

        Assert.Equal("abc", BodyOf(handler.Requests[0]).GetProperty("amount").GetString());
        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal("Сума витрати має бути числом, більшим за 0.", result.ErrorMessage);
    }

    [Fact]
    public async Task AddExpense_409BudgetExceeded_ReturnsServerMessage()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Post, "/api/events/3/expenses", HttpStatusCode.Conflict,
            """{"code":"budget_exceeded","message":"Перевищено встановлений бюджет події."}""");

        var result = await facade.AddExpenseAsync(3, 1, 10, 5000m, "Оренда", [10]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Перевищено встановлений бюджет події.", result.ErrorMessage);
    }

    [Fact]
    public async Task AddExpense_ErrorWithoutBody_ReturnsFallbackMessage()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Post, "/api/events/3/expenses", HttpStatusCode.BadRequest);

        var result = await facade.AddExpenseAsync(3, 1, 10, 1m, "", null);

        Assert.Equal("Помилка додавання витрати.", result.ErrorMessage);
    }

    [Fact]
    public async Task DistributeExpense_Success_PostsRequest_AndLoadsParticipants()
    {
        var (facade, handler) = CreateFacade();
        handler
            .When(HttpMethod.Post, "/api/events/3/expenses/5/distribute", HttpStatusCode.OK,
                """[{"id":1,"expenseId":5,"participationId":10,"amount":50},{"id":2,"expenseId":5,"participationId":11,"amount":50}]""")
            .When(HttpMethod.Get, "/api/events/3/participants", HttpStatusCode.OK,
                """[{"id":10,"userId":1,"userName":"Маргарита","role":"Organizer"},{"id":11,"userId":2,"userName":"Іван","role":"Participant"}]""");

        var result = await facade.DistributeExpenseAsync(3, 5, 1, [10, 11]);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data.Shares.Length);
        Assert.Equal("Іван", result.Data.Participants.Single(p => p.Id == 11).UserName);

        var post = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, post.Method);
        Assert.Equal("/api/events/3/expenses/5/distribute", post.Uri.AbsolutePath);
        Assert.Equal(1, BodyOf(post).GetProperty("organizerUserId").GetInt32());
        Assert.Equal([10, 11], BodyOf(post).GetProperty("participantIds").EnumerateArray().Select(e => e.GetInt32()));
        Assert.Equal("/api/events/3/participants", handler.Requests[1].Uri.AbsolutePath);
    }

    [Fact]
    public async Task DistributeExpense_400_ReturnsServerMessage_AndDoesNotLoadParticipants()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Post, "/api/events/3/expenses/5/distribute", HttpStatusCode.BadRequest,
            """{"code":"no_participants_selected","message":"Потрібно обрати щонайменше одного учасника для розподілу."}""");

        var result = await facade.DistributeExpenseAsync(3, 5, 1, []);

        Assert.False(result.IsSuccess);
        Assert.Equal("Потрібно обрати щонайменше одного учасника для розподілу.", result.ErrorMessage);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task DistributeExpense_ErrorWithoutBody_ReturnsFallbackMessage()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Post, "/api/events/3/expenses/5/distribute", HttpStatusCode.Forbidden);

        var result = await facade.DistributeExpenseAsync(3, 5, 2, [10]);

        Assert.Equal("Помилка розподілу витрати.", result.ErrorMessage);
    }

    [Fact]
    public async Task GetSettlements_Success_ReturnsTransfers()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Get, "/api/events/3/settlements", HttpStatusCode.OK,
            """[{"fromParticipationId":11,"fromUserId":2,"fromUserName":"Іван","toParticipationId":10,"toUserId":1,"toUserName":"Маргарита","amount":150.00}]""");

        var result = await facade.GetSettlementsAsync(3);

        var transfer = Assert.Single(result);
        Assert.Equal("Іван", transfer.FromUserName);
        Assert.Equal("Маргарита", transfer.ToUserName);
        Assert.Equal(150.00m, transfer.Amount);
        Assert.Equal(HttpMethod.Get, handler.RequestMethod);
        Assert.Equal("/api/events/3/settlements", handler.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetSettlements_EmptyList_ReturnsEmptyArray()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Get, "/api/events/3/settlements", HttpStatusCode.OK, "[]");

        var result = await facade.GetSettlementsAsync(3);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSettlements_404_ReturnsEmptyArray()
    {
        var (facade, handler) = CreateFacade();
        handler.When(HttpMethod.Get, "/api/events/99/settlements", HttpStatusCode.NotFound);

        var result = await facade.GetSettlementsAsync(99);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task GetSettlements_InvalidId_ReturnsEmptyWithoutRequest(int eventId)
    {
        var (facade, handler) = CreateFacade();

        var result = await facade.GetSettlementsAsync(eventId);

        Assert.Empty(result);
        Assert.Empty(handler.Requests);
    }
}
