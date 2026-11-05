using System.Text.Json;
using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Models;

namespace EventOrganizer.Api.Infrastructure;

// UC-05 «Зареєструвати витрату» (FR-09, FR-10, FR-15)
public static class ExpenseRegistration
{
    public const decimal MaxAmount = 1_000_000m;

    // Перевіряє формат тіла запиту. Сума перевіряється тут же, бо для неї
    // треба повернути окремий код invalid_amount (FR-10), а не загальний invalid_request.
    public static bool TryRead(JsonElement body, out CreateExpenseRequest? request, out string? code)
    {
        request = null;
        code = "invalid_request";

        if (body.ValueKind != JsonValueKind.Object)
            return false;

        if (!body.TryGetProperty("payerParticipationId", out var payer) ||
            payer.ValueKind != JsonValueKind.Number ||
            !payer.TryGetInt32(out var payerId) || payerId <= 0)
            return false;

        var description = "";
        if (body.TryGetProperty("description", out var text))
        {
            if (text.ValueKind == JsonValueKind.String)
                description = text.GetString()!.Trim();
            else if (text.ValueKind != JsonValueKind.Null)
                return false;
        }

        code = "invalid_amount";
        if (!body.TryGetProperty("amount", out var number) ||
            number.ValueKind != JsonValueKind.Number ||
            !number.TryGetDecimal(out var amount) ||
            !IsValidAmount(amount))
            return false;

        request = new CreateExpenseRequest(payerId, amount, description);
        code = null;
        return true;
    }

    // Сума > 0, не більше за ліміт і не точніша за 0,01 грн.
    public static bool IsValidAmount(decimal amount) =>
        amount > 0m && amount <= MaxAmount && decimal.Round(amount, 2) == amount;

    // Викликати під lock(gate). Повертає null при успіху або код відмови.
    // При відмові колекція expenses не змінюється.
    public static string? TryAdd(
        List<Event> events,
        List<Participation> participations,
        List<Expense> expenses,
        int eventId,
        CreateExpenseRequest request,
        DateTime now,
        out Expense? expense)
    {
        expense = null;

        if (!IsValidAmount(request.Amount))
            return "invalid_amount";

        var ev = events.SingleOrDefault(e => e.Id == eventId);
        if (ev is null)
            return "event_not_found";

        if (ev.Status != EventStatus.Created)
            return "event_not_active";

        if (!participations.Any(p => p.Id == request.PayerParticipationId && p.EventId == eventId))
            return "participant_not_found";

        expense = new Expense
        {
            Id = expenses.Count == 0 ? 1 : expenses.Max(x => x.Id) + 1,
            EventId = eventId,
            PayerParticipationId = request.PayerParticipationId,
            Amount = request.Amount,
            Description = request.Description,
            CreatedAt = now
        };
        expenses.Add(expense);
        return null;
    }
}
