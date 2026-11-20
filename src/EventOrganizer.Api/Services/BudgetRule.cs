using System.Globalization;

namespace EventOrganizer.Api.Services;

/// <summary>
/// Результат перевірки бюджету події перед додаванням нової витрати (US-03).
/// </summary>
public enum BudgetCheckStatus
{
    /// <summary>Витрата дозволена без попереджень (ліміт не задано або використано менше 80%).</summary>
    Allowed,

    /// <summary>Витрата дозволена, але після неї використано від 80% до 100% бюджету включно.</summary>
    Warning,

    /// <summary>Витрата відхиляється: після неї сума витрат перевищить бюджет.</summary>
    Rejected
}

/// <summary>
/// Підсумок перевірки бюджету.
/// </summary>
/// <param name="Status">Рішення правила.</param>
/// <param name="Limit">Ліміт бюджету події або <c>null</c>, якщо ліміт не задано.</param>
/// <param name="ProjectedSpent">Сума витрат події після додавання нової витрати.</param>
/// <param name="UsagePercent">Частка використаного бюджету у відсотках або <c>null</c>, якщо ліміт не задано.</param>
/// <param name="WarningMessage">Текст попередження; заповнюється лише для <see cref="BudgetCheckStatus.Warning"/>.</param>
public sealed record BudgetCheckResult(
    BudgetCheckStatus Status,
    decimal? Limit,
    decimal ProjectedSpent,
    decimal? UsagePercent,
    string? WarningMessage);

/// <summary>
/// Предметне правило контролю бюджету події (US-03).
/// Чиста функція: не звертається до бази, HTTP чи годинника, тому перевіряється модульними тестами.
/// </summary>
public static class BudgetRule
{
    /// <summary>Поріг, з якого витрата дозволяється з попередженням (80% бюджету).</summary>
    public const decimal WarningThreshold = 0.80m;

    /// <summary>
    /// Оцінює, чи можна додати нову витрату з огляду на ліміт бюджету події.
    /// </summary>
    /// <param name="budgetLimit">Ліміт бюджету; <c>null</c> означає, що бюджет не обмежено.</param>
    /// <param name="alreadySpent">Сума вже зареєстрованих витрат події.</param>
    /// <param name="newExpenseAmount">Сума нової витрати.</param>
    /// <exception cref="ArgumentException">Некоректні вхідні дані: від'ємна вже витрачена сума,
    /// нова витрата, не більша за нуль, або ліміт, що не є додатним.</exception>
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
