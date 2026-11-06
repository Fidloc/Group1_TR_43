using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Models;

namespace EventOrganizer.Api.Infrastructure;
// UC 09 про розподіл витрат 
public static class ExpenseCalculator
{
    public static List<ExpenseShare> SplitEqually(
        int expenseId,
        decimal amount,
        int[] participantIds,
        int firstId = 1)
    {
        var result = new List<ExpenseShare>();
        if (participantIds == null || participantIds.Length == 0)
            return result;

        var total = decimal.Round(amount, 2);
        var count = participantIds.Length;
        var baseShare = Math.Floor((total / count) * 100m) / 100m;
        var remainder = total - baseShare * count;

        for (int i = 0; i < count; i++)
        {
            result.Add(new ExpenseShare
            {
                Id = firstId + i,
                ExpenseId = expenseId,
                ParticipationId = participantIds[i],
                Amount = i == 0 ? baseShare + remainder : baseShare
            });
        }

        return result;
    }

    public static BalanceDto[] GetBalances(
        int eventId,
        List<User> users,
        List<Participation> participations,
        List<Expense> expenses,
        List<ExpenseShare> shares)
    {
        var eventMembers = participations.Where(p => p.EventId == eventId).ToList();
        var eventExpenses = expenses.Where(e => e.EventId == eventId).ToList();
        var expenseIds = eventExpenses.Select(e => e.Id).ToList();

        var list = new List<BalanceDto>();
        foreach (var member in eventMembers)
        {
            var user = users.FirstOrDefault(u => u.Id == member.UserId);
            var userName = user != null ? user.Name : $"User #{member.UserId}";

            var paid = eventExpenses
                .Where(e => e.PayerParticipationId == member.Id)
                .Sum(e => e.Amount);

            var owed = shares
                .Where(s => expenseIds.Contains(s.ExpenseId) && s.ParticipationId == member.Id)
                .Sum(s => s.Amount);

            list.Add(new BalanceDto(member.Id, member.UserId, userName, paid, owed, paid - owed));
        }

        return list.ToArray();
    }
}
