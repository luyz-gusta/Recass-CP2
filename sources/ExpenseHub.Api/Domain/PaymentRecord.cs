using System;

namespace ExpenseHub.Api.Domain;

/// <summary>
/// Registro do pagamento simulado de um reembolso aprovado (relação 1:1 com <see cref="Expense"/>).
/// </summary>
internal sealed class PaymentRecord
{
    private PaymentRecord()
    {
    }

    public Guid Id { get; private set; }

    public Guid ExpenseId { get; private set; }

    public decimal Amount { get; private set; }

    public string PaidById { get; private set; } = string.Empty;

    public DateTime PaidAtUtc { get; private set; }

    public static PaymentRecord Create(Guid expenseId, decimal amount, string paidById, DateTime paidAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paidById);

        return new PaymentRecord
        {
            Id = Guid.NewGuid(),
            ExpenseId = expenseId,
            Amount = amount,
            PaidById = paidById,
            PaidAtUtc = paidAtUtc,
        };
    }
}
