using System;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Services;

/// <summary>
/// Converte a entidade <see cref="Expense"/> na resposta pública, sem expor o token de concorrência.
/// </summary>
internal static class ExpenseMapper
{
    public static ExpenseResponse ToResponse(this Expense expense)
    {
        ArgumentNullException.ThrowIfNull(expense);

        return new ExpenseResponse(
            expense.Id,
            expense.OwnerId,
            expense.CategoryId,
            expense.Category?.Name ?? string.Empty,
            expense.Description,
            expense.Amount,
            expense.ExpenseDate,
            expense.Status,
            expense.CreatedAtUtc,
            expense.UpdatedAtUtc,
            expense.SubmittedAtUtc,
            expense.DecidedById,
            expense.DecidedAtUtc,
            expense.RejectionReason,
            expense.Payment?.PaidById,
            expense.Payment?.PaidAtUtc);
    }
}
