using System;

namespace ExpenseHub.Api.Domain;

/// <summary>
/// Registro imutável de uma ação executada sobre um reembolso.
/// </summary>
internal sealed class ExpenseHistory
{
    private ExpenseHistory()
    {
    }

    public long Id { get; private set; }

    public Guid ExpenseId { get; private set; }

    public ExpenseAction Action { get; private set; }

    public string ActorId { get; private set; } = string.Empty;

    public DateTime OccurredAtUtc { get; private set; }

    public ExpenseStatus? PreviousStatus { get; private set; }

    public ExpenseStatus NewStatus { get; private set; }

    public string? Justification { get; private set; }

    public string? Changes { get; private set; }

    public static ExpenseHistory Record(
        Guid expenseId,
        ExpenseAction action,
        string actorId,
        DateTime occurredAtUtc,
        ExpenseStatus? previousStatus,
        ExpenseStatus newStatus,
        string? justification = null,
        string? changes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);

        return new ExpenseHistory
        {
            ExpenseId = expenseId,
            Action = action,
            ActorId = actorId,
            OccurredAtUtc = occurredAtUtc,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            Justification = justification,
            Changes = changes,
        };
    }
}
