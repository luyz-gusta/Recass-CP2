using System;

namespace ExpenseHub.Api.Domain;

/// <summary>
/// Solicitação de reembolso com um único valor.
/// Proprietário, estado, atores e horários são definidos pelo servidor, nunca pelo cliente.
/// O comportamento (criar, editar, enviar, aprovar, reprovar e pagar) é implementado nas issues I04 a I08.
/// </summary>
internal sealed class Expense
{
    private Expense()
    {
    }

    public Guid Id { get; private set; }

    public string OwnerId { get; private set; } = string.Empty;

    public int CategoryId { get; private set; }

    public ExpenseCategory? Category { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public DateOnly ExpenseDate { get; private set; }

    public ExpenseStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public DateTime? SubmittedAtUtc { get; private set; }

    public string? DecidedById { get; private set; }

    public DateTime? DecidedAtUtc { get; private set; }

    public string? RejectionReason { get; private set; }

    /// <summary>
    /// Token de concorrência otimista, renovado a cada alteração.
    /// </summary>
    public Guid ConcurrencyStamp { get; private set; }

    public PaymentRecord? Payment { get; private set; }
}
