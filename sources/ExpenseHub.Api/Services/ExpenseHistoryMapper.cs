using System;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Services;

/// <summary>
/// Converte uma entrada de <see cref="ExpenseHistory"/> na resposta pública do histórico.
/// </summary>
internal static class ExpenseHistoryMapper
{
    public static ExpenseHistoryResponse ToResponse(this ExpenseHistory entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new ExpenseHistoryResponse(
            entry.Id,
            entry.Action,
            entry.ActorId,
            entry.OccurredAtUtc,
            entry.PreviousStatus,
            entry.NewStatus,
            entry.Justification,
            entry.Changes);
    }
}
