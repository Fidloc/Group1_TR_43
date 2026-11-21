using System.Globalization;

namespace EventOrganizer.Api.Services;

public enum BudgetCheckStatus
{
    Allowed,
    
    Warning,

    Rejected
}

public sealed record BudgetCheckResult(
    BudgetCheckStatus Status,
    decimal? Limit,
    decimal ProjectedSpent,
    decimal? UsagePercent,
    string? WarningMessage);

public static class BudgetRule
{
    public const decimal WarningThreshold = 0.80m;

    public static BudgetCheckResult Evaluate(decimal? budgetLimit, decimal alreadySpent, decimal newExpenseAmount)
    {
        if (alreadySpent < 0m)
            throw new ArgumentException("Сума вже зареєстрованих витрат не може бути від'ємною.", nameof(alreadySpent));

        if (newExpenseAmount <= 0m)
            throw new ArgumentException("Сума нової витрати має бути більшою за нуль.", nameof(newExpenseAmount));

        if (budgetLimit is <= 0m)
            throw new ArgumentException("Ліміт бюджету має бути більшим за нуль.", nameof(budgetLimit));

        var projected = alreadySpent + newExpenseAmount;

        if (budgetLimit is not { } limit)
            return new BudgetCheckResult(BudgetCheckStatus.Allowed, null, projected, null, null);

        var usagePercent = projected * 100m / limit;

        if (projected > limit)
            return new BudgetCheckResult(BudgetCheckStatus.Rejected, limit, projected, usagePercent, null);

        if (projected >= limit * WarningThreshold)
        {
            var message = string.Format(
                CultureInfo.InvariantCulture,
                "Увага: використано {0:0.0}% бюджету ({1:0.00} з {2:0.00} грн). Залишок: {3:0.00} грн.",
                usagePercent, projected, limit, limit - projected);

            return new BudgetCheckResult(BudgetCheckStatus.Warning, limit, projected, usagePercent, message);
        }

        return new BudgetCheckResult(BudgetCheckStatus.Allowed, limit, projected, usagePercent, null);
    }
}
