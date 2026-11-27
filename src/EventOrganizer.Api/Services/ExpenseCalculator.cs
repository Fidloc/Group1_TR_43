using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Models;

namespace EventOrganizer.Api.Services;

// UC-09 про розподіл витрат та розрахунок балансів (UC-06)
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

    // UC-12: мінімальний список переказів між боржниками і кредиторами
    public static SettlementTransferDto[] CalculateSettlements(IEnumerable<BalanceDto> balances)
    {
        var list = balances.ToList();

        var debtors = list
            .Select(b => (P: b, Left: -decimal.Round(b.Balance, 2)))
            .Where(x => x.Left > 0)
            .ToList();

        var creditors = list
            .Select(b => (P: b, Left: decimal.Round(b.Balance, 2)))
            .Where(x => x.Left > 0)
            .ToList();

        var result = new List<SettlementTransferDto>();

        while (debtors.Count > 0 && creditors.Count > 0)
        {
            var di = IndexOfMax(debtors);
            var ci = IndexOfMax(creditors);

            var debtor = debtors[di];
            var creditor = creditors[ci];
            var amount = Math.Min(debtor.Left, creditor.Left);

            result.Add(new SettlementTransferDto(
                debtor.P.ParticipationId,
                debtor.P.UserId,
                debtor.P.UserName,
                creditor.P.ParticipationId,
                creditor.P.UserId,
                creditor.P.UserName,
                amount));

            debtors[di] = (debtor.P, debtor.Left - amount);
            creditors[ci] = (creditor.P, creditor.Left - amount);

            if (debtors[di].Left <= 0) debtors.RemoveAt(di);
            if (creditors[ci].Left <= 0) creditors.RemoveAt(ci);
        }

        return result.ToArray();
    }

    private static int IndexOfMax(List<(BalanceDto P, decimal Left)> items)
    {
        var best = 0;
        for (int i = 1; i < items.Count; i++)
        {
            if (items[i].Left > items[best].Left)
                best = i;
        }
        return best;
    }
}
