using EventOrganizer.Api.Services;

namespace EventOrganizer.Tests;

// Модульні тести предметного правила бюджету події (US-03).
// Атрибути не приймають decimal, тому значення передаються як double і перетворюються в тесті.
public class BudgetRuleTests
{
    [Theory]
    // Без ліміту — будь-яка сума дозволена
    [InlineData(null, 500.00, 1200.00, BudgetCheckStatus.Allowed)]
    // Звичайний випадок: 70%
    [InlineData(1000.00, 200.00, 500.00, BudgetCheckStatus.Allowed)]
    // Мінімальна витрата при порожньому бюджеті
    [InlineData(1000.00, 0.00, 0.01, BudgetCheckStatus.Allowed)]
    // Межа до попередження: 79.999%
    [InlineData(1000.00, 0.00, 799.99, BudgetCheckStatus.Allowed)]
    // Нижня межа попередження: рівно 80%
    [InlineData(1000.00, 500.00, 300.00, BudgetCheckStatus.Warning)]
    // Між 80% і 100%: 95%
    [InlineData(1000.00, 600.00, 350.00, BudgetCheckStatus.Warning)]
    // Верхня межа: рівно 100% ще дозволено з попередженням
    [InlineData(1000.00, 700.00, 300.00, BudgetCheckStatus.Warning)]
    // Перевищення на 1 коп.: 100.001%
    [InlineData(1000.00, 700.00, 300.01, BudgetCheckStatus.Rejected)]
    // Ліміт уже нижчий за витрачене — будь-яка нова витрата відхиляється
    [InlineData(1000.00, 1200.00, 10.00, BudgetCheckStatus.Rejected)]
    public void Evaluate_ReturnsExpectedStatus(
        double? budgetLimit,
        double alreadySpent,
        double newExpense,
        BudgetCheckStatus expected)
    {
        // Підготовка
        decimal? limit = budgetLimit is null ? null : (decimal)budgetLimit.Value;

        // Дія
        var result = BudgetRule.Evaluate(limit, (decimal)alreadySpent, (decimal)newExpense);

        // Перевірка
        Assert.Equal(expected, result.Status);
    }

    [Theory]
    // Очікувані значення пораховано окремо: ProjectedSpent = витрачено + нова, UsagePercent = ProjectedSpent / ліміт * 100
    [InlineData(1000.00, 200.00, 500.00, 700.00, 70.00)]
    [InlineData(1000.00, 0.00, 799.99, 799.99, 79.999)]
    [InlineData(1000.00, 500.00, 300.00, 800.00, 80.00)]
    [InlineData(1000.00, 700.00, 300.00, 1000.00, 100.00)]
    [InlineData(1000.00, 700.00, 300.01, 1000.01, 100.001)]
    public void Evaluate_CalculatesProjectedSpentAndUsagePercent(
        double budgetLimit,
        double alreadySpent,
        double newExpense,
        double expectedProjected,
        double expectedPercent)
    {
        // Дія
        var result = BudgetRule.Evaluate((decimal)budgetLimit, (decimal)alreadySpent, (decimal)newExpense);

        // Перевірка
        Assert.Equal((decimal)budgetLimit, result.Limit);
        Assert.Equal((decimal)expectedProjected, result.ProjectedSpent);
        Assert.Equal((decimal)expectedPercent, result.UsagePercent);
    }

    [Fact]
    public void Evaluate_WhenLimitIsNull_ReturnsNoLimitAndNoPercent()
    {
        // Дія
        var result = BudgetRule.Evaluate(null, 500m, 1200m);

        // Перевірка
        Assert.Equal(BudgetCheckStatus.Allowed, result.Status);
        Assert.Null(result.Limit);
        Assert.Null(result.UsagePercent);
        Assert.Null(result.WarningMessage);
        Assert.Equal(1700m, result.ProjectedSpent);
    }

    [Fact]
    public void Evaluate_WhenWarning_ReturnsMessageWithUsageAndRemainder()
    {
        // Підготовка: 600 + 350 = 950 з 1000 → 95%, залишок 50
        // Дія
        var result = BudgetRule.Evaluate(1000m, 600m, 350m);

        // Перевірка: формат не залежить від мови системи (InvariantCulture)
        Assert.Equal(BudgetCheckStatus.Warning, result.Status);
        Assert.NotNull(result.WarningMessage);
        Assert.Contains("95.0%", result.WarningMessage);
        Assert.Contains("950.00 з 1000.00", result.WarningMessage);
        Assert.Contains("Залишок: 50.00", result.WarningMessage);
    }

    [Theory]
    [InlineData(1000.00, 200.00, 500.00)] // Allowed
    [InlineData(1000.00, 700.00, 300.01)] // Rejected
    public void Evaluate_WhenNotWarning_ReturnsNoMessage(double budgetLimit, double alreadySpent, double newExpense)
    {
        // Дія
        var result = BudgetRule.Evaluate((decimal)budgetLimit, (decimal)alreadySpent, (decimal)newExpense);

        // Перевірка
        Assert.Null(result.WarningMessage);
    }

    [Theory]
    [InlineData(0.00)]
    [InlineData(-0.01)]
    [InlineData(-100.00)]
    public void Evaluate_WhenNewExpenseNotPositive_Throws(double newExpense)
    {
        // Підготовка
        Action action = () => BudgetRule.Evaluate(1000m, 0m, (decimal)newExpense);

        // Дія та перевірка
        var ex = Assert.Throws<ArgumentException>(action);
        Assert.Equal("newExpenseAmount", ex.ParamName);
    }

    [Fact]
    public void Evaluate_WhenAlreadySpentNegative_Throws()
    {
        // Підготовка
        Action action = () => BudgetRule.Evaluate(1000m, -0.01m, 100m);

        // Дія та перевірка
        var ex = Assert.Throws<ArgumentException>(action);
        Assert.Equal("alreadySpent", ex.ParamName);
    }

    [Theory]
    [InlineData(0.00)]
    [InlineData(-500.00)]
    public void Evaluate_WhenLimitNotPositive_Throws(double budgetLimit)
    {
        // Підготовка
        Action action = () => BudgetRule.Evaluate((decimal)budgetLimit, 0m, 100m);

        // Дія та перевірка
        var ex = Assert.Throws<ArgumentException>(action);
        Assert.Equal("budgetLimit", ex.ParamName);
    }
}
