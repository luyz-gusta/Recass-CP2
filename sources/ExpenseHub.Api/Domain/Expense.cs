using System;
using System.Collections.Generic;
using System.Globalization;

namespace ExpenseHub.Api.Domain;

/// <summary>
/// Solicitação de reembolso com um único valor.
/// Proprietário, estado, atores e horários são definidos pelo servidor, nunca pelo cliente.
/// O comportamento (criar, editar, enviar, aprovar, reprovar e pagar) é implementado nas issues I04 a I08.
/// </summary>
internal sealed class Expense
{
    /// <summary>Tamanho mínimo da justificativa de reprovação, sem contar os espaços nas pontas.</summary>
    public const int RejectionReasonMinLength = 10;

    /// <summary>Tamanho máximo da justificativa de reprovação, sem contar os espaços nas pontas.</summary>
    public const int RejectionReasonMaxLength = 500;

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

    /// <summary>
    /// Cria um reembolso em <see cref="ExpenseStatus.Draft"/>. O identificador é gerado aqui (o banco não gera)
    /// e o proprietário é sempre o usuário autenticado informado pelo serviço.
    /// </summary>
    /// <param name="ownerId">Identificador do usuário autenticado que passa a ser o proprietário.</param>
    /// <param name="categoryId">Categoria da despesa.</param>
    /// <param name="description">Descrição já validada pelo serviço.</param>
    /// <param name="amount">Valor já validado pelo serviço.</param>
    /// <param name="expenseDate">Data da despesa já validada pelo serviço.</param>
    /// <param name="nowUtc">Instante atual em UTC, obtido do <see cref="TimeProvider"/>.</param>
    /// <returns>O rascunho novo, ainda não persistido.</returns>
    public static Expense CreateDraft(string ownerId, int categoryId, string description, decimal amount, DateOnly expenseDate, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new Expense
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            CategoryId = categoryId,
            Description = description,
            Amount = amount,
            ExpenseDate = expenseDate,
            Status = ExpenseStatus.Draft,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            ConcurrencyStamp = Guid.NewGuid(),
        };
    }

    /// <summary>
    /// Substitui os dados editáveis de um rascunho. Só é permitido em <see cref="ExpenseStatus.Draft"/>.
    /// </summary>
    /// <param name="categoryId">Nova categoria.</param>
    /// <param name="description">Nova descrição já validada pelo serviço.</param>
    /// <param name="amount">Novo valor já validado pelo serviço.</param>
    /// <param name="expenseDate">Nova data da despesa já validada pelo serviço.</param>
    /// <param name="nowUtc">Instante atual em UTC, obtido do <see cref="TimeProvider"/>.</param>
    /// <returns>
    /// O texto das alterações no formato <c>campo: antigo -> novo; ...</c>, ou <see langword="null"/> quando nada mudou
    /// (nesse caso nem <see cref="UpdatedAtUtc"/> nem <see cref="ConcurrencyStamp"/> são alterados).
    /// </returns>
    /// <exception cref="InvalidOperationException">O reembolso não está em <see cref="ExpenseStatus.Draft"/>.</exception>
    public string? UpdateDraft(int categoryId, string description, decimal amount, DateOnly expenseDate, DateTime nowUtc)
    {
        if (Status != ExpenseStatus.Draft)
        {
            throw new InvalidOperationException($"Somente um rascunho pode ser editado; o estado atual é {Status}.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        List<string> changes = [];

        if (categoryId != CategoryId)
        {
            changes.Add(string.Create(CultureInfo.InvariantCulture, $"categoryId: {CategoryId} -> {categoryId}"));
        }

        if (!string.Equals(description, Description, StringComparison.Ordinal))
        {
            changes.Add($"description: {Description} -> {description}");
        }

        if (amount != Amount)
        {
            changes.Add(string.Create(CultureInfo.InvariantCulture, $"amount: {Amount:F2} -> {amount:F2}"));
        }

        if (expenseDate != ExpenseDate)
        {
            changes.Add(string.Create(CultureInfo.InvariantCulture, $"expenseDate: {ExpenseDate:yyyy-MM-dd} -> {expenseDate:yyyy-MM-dd}"));
        }

        if (changes.Count == 0)
        {
            return null;
        }

        CategoryId = categoryId;
        Description = description;
        Amount = amount;
        ExpenseDate = expenseDate;
        UpdatedAtUtc = nowUtc;
        ConcurrencyStamp = Guid.NewGuid();

        return string.Join("; ", changes);
    }

    /// <summary>
    /// Envia o rascunho para aprovação (<see cref="ExpenseStatus.Draft"/> para <see cref="ExpenseStatus.Submitted"/>).
    /// Não existe reenvio: um reembolso fora de Draft nunca volta a ser enviado.
    /// </summary>
    /// <param name="nowUtc">Instante atual em UTC, obtido do <see cref="TimeProvider"/>.</param>
    /// <exception cref="InvalidOperationException">O reembolso não está em <see cref="ExpenseStatus.Draft"/>.</exception>
    public void Submit(DateTime nowUtc)
    {
        if (Status != ExpenseStatus.Draft)
        {
            throw new InvalidOperationException($"Somente um rascunho pode ser enviado; o estado atual é {Status}.");
        }

        Status = ExpenseStatus.Submitted;
        SubmittedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
        ConcurrencyStamp = Guid.NewGuid();
    }

    /// <summary>
    /// Aprova um reembolso enviado (<see cref="ExpenseStatus.Submitted"/> para <see cref="ExpenseStatus.Approved"/>),
    /// registrando quem decidiu e quando. A decisão é única: um reembolso já decidido não é aprovado de novo.
    /// </summary>
    /// <param name="approverId">Identificador do aprovador autenticado, informado pelo serviço.</param>
    /// <param name="nowUtc">Instante atual em UTC, obtido do <see cref="TimeProvider"/>.</param>
    /// <exception cref="InvalidOperationException">O reembolso não está em <see cref="ExpenseStatus.Submitted"/>.</exception>
    public void Approve(string approverId, DateTime nowUtc)
    {
        if (Status != ExpenseStatus.Submitted)
        {
            throw new InvalidOperationException($"Somente um reembolso enviado pode ser aprovado; o estado atual é {Status}.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(approverId);

        Decide(ExpenseStatus.Approved, approverId, nowUtc);
    }

    /// <summary>
    /// Reprova um reembolso enviado (<see cref="ExpenseStatus.Submitted"/> para <see cref="ExpenseStatus.Rejected"/>),
    /// guardando a justificativa sem os espaços nas pontas. <see cref="ExpenseStatus.Rejected"/> é um estado final.
    /// </summary>
    /// <param name="approverId">Identificador do aprovador autenticado, informado pelo serviço.</param>
    /// <param name="justification">
    /// Justificativa já validada pelo serviço; sem os espaços nas pontas, precisa ter de
    /// <see cref="RejectionReasonMinLength"/> a <see cref="RejectionReasonMaxLength"/> caracteres.
    /// </param>
    /// <param name="nowUtc">Instante atual em UTC, obtido do <see cref="TimeProvider"/>.</param>
    /// <exception cref="InvalidOperationException">O reembolso não está em <see cref="ExpenseStatus.Submitted"/>.</exception>
    /// <exception cref="ArgumentException">A justificativa é nula ou está fora do tamanho permitido.</exception>
    public void Reject(string approverId, string justification, DateTime nowUtc)
    {
        if (Status != ExpenseStatus.Submitted)
        {
            throw new InvalidOperationException($"Somente um reembolso enviado pode ser reprovado; o estado atual é {Status}.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(approverId);

        // Defesa em profundidade: o serviço já valida (400), mas o domínio nunca grava uma reprovação sem justificativa válida.
        string reason = justification?.Trim() ?? string.Empty;
        if (reason.Length is < RejectionReasonMinLength or > RejectionReasonMaxLength)
        {
            throw new ArgumentException("A justificativa da reprovação precisa ter de 10 a 500 caracteres, sem contar os espaços nas pontas.", nameof(justification));
        }

        RejectionReason = reason;
        Decide(ExpenseStatus.Rejected, approverId, nowUtc);
    }

    /// <summary>
    /// Registra o pagamento de um reembolso aprovado (<see cref="ExpenseStatus.Approved"/> para <see cref="ExpenseStatus.Paid"/>).
    /// Cria o <see cref="PaymentRecord"/> com o valor do reembolso, quem pagou e quando, e o associa a <see cref="Payment"/>.
    /// <see cref="ExpenseStatus.Paid"/> é um estado final: um reembolso nunca é pago duas vezes.
    /// </summary>
    /// <param name="financeUserId">Identificador do usuário do financeiro autenticado, informado pelo serviço.</param>
    /// <param name="nowUtc">Instante atual em UTC, obtido do <see cref="TimeProvider"/>.</param>
    /// <returns>O registro de pagamento criado, ainda não persistido.</returns>
    /// <exception cref="InvalidOperationException">O reembolso não está em <see cref="ExpenseStatus.Approved"/>.</exception>
    public PaymentRecord MarkAsPaid(string financeUserId, DateTime nowUtc)
    {
        if (Status != ExpenseStatus.Approved)
        {
            throw new InvalidOperationException($"Somente um reembolso aprovado pode ser pago; o estado atual é {Status}.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(financeUserId);

        PaymentRecord payment = PaymentRecord.Create(Id, Amount, financeUserId, nowUtc);
        Payment = payment;
        Status = ExpenseStatus.Paid;
        UpdatedAtUtc = nowUtc;
        ConcurrencyStamp = Guid.NewGuid();

        return payment;
    }

    private void Decide(ExpenseStatus outcome, string approverId, DateTime nowUtc)
    {
        Status = outcome;
        DecidedById = approverId;
        DecidedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
        ConcurrencyStamp = Guid.NewGuid();
    }
}
