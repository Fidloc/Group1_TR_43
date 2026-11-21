using EventOrganizer.Api.Services;

namespace EventOrganizer.Tests;

public class BudgetRuleTests
{
    [Theory]
    [InlineData(null, 500.00, 1200.00, BudgetCheckStatus.Allowed)]
    [InlineData(1000.00, 200.00, 500.00, BudgetCheckStatus.Allowed)]
    [InlineData(1000.00, 0.00, 0.01, BudgetCheckStatus.Allowed)]
    [InlineData(1000.00, 0.00, 799.99, BudgetCheckStatus.Allowed)]
    [InlineData(1000.00, 500.00, 300.00, BudgetCheckStatus.Warning)]
    [InlineData(1000.00, 600.00, 350.00, BudgetCheckStatus.Warning)]
    [InlineData(1000.00, 700.00, 300.00, BudgetCheckStatus.Warning)]
    [InlineData(1000.00, 700.00, 300.01, BudgetCheckStatus.Rejected)]
    [InlineData(1000.00, 1200.00, 10.00, BudgetCheckStatus.Rejected)]
    public void Evaluate_ReturnsExpectedStatus(
        double? budgetLimit,
        double alreadySpent,
        double newExpense,
        BudgetCheckStatus expected)
    {
        decimal? limit = budgetLimit is null ? null : (decimal)budgetLimit.Value;
        
        var result = BudgetRule.Evaluate(limit, (decimal)alreadySpent, (decimal)newExpense);

        Assert.Equal(expected, result.Status);
    }

    [Theory]
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
        var result = BudgetRule.Evaluate((decimal)budgetLimit, (decimal)alreadySpent, (decimal)newExpense);

        Assert.Equal((decimal)budgetLimit, result.Limit);
        Assert.Equal((decimal)expectedProjected, result.ProjectedSpent);
        Assert.Equal((decimal)expectedPercent, result.UsagePercent);
    }

    [Fact]
    public void Evaluate_WhenLimitIsNull_ReturnsNoLimitAndNoPercent()
    {
        var result = BudgetRule.Evaluate(null, 500m, 1200m);

        Assert.Equal(BudgetCheckStatus.Allowed, result.Status);
        Assert.Null(result.Limit);
        Assert.Null(result.UsagePercent);
        Assert.Null(result.WarningMessage);
        Assert.Equal(1700m, result.ProjectedSpent);
    }

    [Fact]
    public void Evaluate_WhenWarning_ReturnsMessageWithUsageAndRemainder()
    {
        var result = BudgetRule.Evaluate(1000m, 600m, 350m);

        Assert.Equal(BudgetCheckStatus.Warning, result.Status);
        Assert.NotNull(result.WarningMessage);
        Assert.Contains("95.0%", result.WarningMessage);
        Assert.Contains("950.00 з 1000.00", result.WarningMessage);
        Assert.Contains("Залишок: 50.00", result.WarningMessage);
    }

    [Theory]
    [InlineData(1000.00, 200.00, 500.00)]
    [InlineData(1000.00, 700.00, 300.01)]
    public void Evaluate_WhenNotWarning_ReturnsNoMessage(double budgetLimit, double alreadySpent, double newExpense)
    {
        var result = BudgetRule.Evaluate((decimal)budgetLimit, (decimal)alreadySpent, (decimal)newExpense);

        Assert.Null(result.WarningMessage);
    }

    [Theory]
    [InlineData(0.00)]
    [InlineData(-0.01)]
    [InlineData(-100.00)]
    public void Evaluate_WhenNewExpenseNotPositive_Throws(double newExpense)
    {
        Action action = () => BudgetRule.Evaluate(1000m, 0m, (decimal)newExpense);

        var ex = Assert.Throws<ArgumentException>(action);
        Assert.Equal("newExpenseAmount", ex.ParamName);
    }

    [Fact]
    public void Evaluate_WhenAlreadySpentNegative_Throws()
    {
        Action action = () => BudgetRule.Evaluate(1000m, -0.01m, 100m);

        var ex = Assert.Throws<ArgumentException>(action);
        Assert.Equal("alreadySpent", ex.ParamName);
    }

    [Theory]
    [InlineData(0.00)]
    [InlineData(-500.00)]
    public void Evaluate_WhenLimitNotPositive_Throws(double budgetLimit)
    {
        Action action = () => BudgetRule.Evaluate((decimal)budgetLimit, 0m, 100m);

        var ex = Assert.Throws<ArgumentException>(action);
        Assert.Equal("budgetLimit", ex.ParamName);
    }
}
