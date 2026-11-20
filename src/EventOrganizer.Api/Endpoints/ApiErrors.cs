using EventOrganizer.Api.Contracts;

namespace EventOrganizer.Api.Endpoints;

public static class ApiErrors
{
    public static IResult Result(string code)
    {
        var (status, message) = code switch
        {
            "email_exists" or "email_already_exists" => (409, "Користувач із таким email уже зареєстрований."),
            "missing_credentials" => (400, "Email та пароль обов'язкові."),
            "invalid_credentials" => (401, "Невірний email або пароль."),
            "user_not_found" => (404, "Користувача не знайдено."),
            "missing_title" => (400, "Назву події обов'язково потрібно вказати."),
            "invalid_date" => (400, "Дату події не вказано або вона в минулому."),
            "event_not_found" => (404, "Подію не знайдено."),
            "event_not_active" => (409, "Дія заборонена: подія закрита або скасована."),
            "forbidden_not_organizer" => (403, "Цю дію може виконати лише Організатор події."),
            "participant_not_found" => (404, "Учасника події не знайдено."),
            "already_joined" => (409, "Користувач уже є учасником цієї події."),
            "invalid_amount" => (400, "Сума витрати має бути числом, більшим за 0."),
            "expense_not_found" => (404, "Витрату не знайдено."),
            "no_participants_selected" => (400, "Потрібно обрати щонайменше одного учасника для розподілу."),
            "sum_mismatch" => (400, "Сума часток не збігається із сумою витрати."),
            "budget_exceeded" => (409, "Перевищено встановлений бюджет події."),
            "invalid_budget" => (400, "Бюджет має бути додатним числом (не більше 10 000 000 грн) із точністю до копійок."),
            _ => (400, "Некоректний запит.")
        };
        return Results.Json(new ApiError(code, message), statusCode: status);
    }
}
