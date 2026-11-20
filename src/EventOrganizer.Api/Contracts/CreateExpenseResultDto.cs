using EventOrganizer.Api.Models;

namespace EventOrganizer.Api.Contracts;

// Warning заповнений, коли після витрати використано 80%–100% бюджету
public sealed record CreateExpenseResultDto(Expense Expense, string? Warning, ExpenseShare[] Shares);
