using System;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Services;

namespace ExpenseHub.UnitTests.TestDoubles;

/// <summary>
/// Monta reembolsos em qualquer estado passando pelas transições reais do domínio (criar, enviar, aprovar, reprovar
/// e pagar), sem reflexão. Um reembolso <c>Paid</c> tem aprovação e pagamento de verdade, e qualquer regra nova do
/// domínio passa a valer também no arrange dos testes.
/// </summary>
/// <example>
/// <code>Expense expense = ExpenseScenario.Of(TestActors.Employee1).At(clock.UtcNow).Approved();</code>
/// </example>
internal sealed class ExpenseScenario
{
    /// <summary>Justificativa válida usada nas reprovações do arrange.</summary>
    public const string RejectionJustification = "Comprovante ilegível, envie novamente.";

    private readonly string _ownerId;
    private DateTime _nowUtc = new FixedTimeProvider().UtcNow;
    private string _approverId = TestActors.Approver.UserId;
    private string _financeUserId = TestActors.Finance.UserId;

    private ExpenseScenario(string ownerId)
    {
        _ownerId = ownerId;
    }

    /// <summary>Começa um cenário cujo proprietário é o ator informado.</summary>
    public static ExpenseScenario Of(Actor owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return new ExpenseScenario(owner.UserId);
    }

    /// <summary>Começa um cenário cujo proprietário é o identificador informado.</summary>
    public static ExpenseScenario Of(string ownerId)
    {
        return new ExpenseScenario(ownerId);
    }

    /// <summary>Instante (UTC) de todas as transições; o padrão é o do <see cref="FixedTimeProvider"/>.</summary>
    public ExpenseScenario At(DateTime nowUtc)
    {
        _nowUtc = nowUtc;
        return this;
    }

    /// <summary>Quem aprova ou reprova; o padrão é <see cref="TestActors.Approver"/>.</summary>
    public ExpenseScenario DecidedBy(Actor approver)
    {
        ArgumentNullException.ThrowIfNull(approver);
        _approverId = approver.UserId;
        return this;
    }

    /// <summary>Quem registra o pagamento; o padrão é <see cref="TestActors.Finance"/>.</summary>
    public ExpenseScenario PaidBy(Actor finance)
    {
        ArgumentNullException.ThrowIfNull(finance);
        _financeUserId = finance.UserId;
        return this;
    }

    /// <summary>Rascunho recém-criado, com os dados válidos de <see cref="TestExpenses"/>.</summary>
    public Expense Draft()
    {
        return TestExpenses.Draft(_ownerId, _nowUtc);
    }

    /// <summary>Rascunho enviado: Draft → Submitted.</summary>
    public Expense Submitted()
    {
        Expense expense = Draft();
        expense.Submit(_nowUtc);
        return expense;
    }

    /// <summary>Enviado e aprovado: Draft → Submitted → Approved.</summary>
    public Expense Approved()
    {
        Expense expense = Submitted();
        expense.Approve(_approverId, _nowUtc);
        return expense;
    }

    /// <summary>Enviado e reprovado com <see cref="RejectionJustification"/>: Draft → Submitted → Rejected.</summary>
    public Expense Rejected()
    {
        Expense expense = Submitted();
        expense.Reject(_approverId, RejectionJustification, _nowUtc);
        return expense;
    }

    /// <summary>Aprovado e pago, com registro de pagamento: Draft → Submitted → Approved → Paid.</summary>
    public Expense Paid()
    {
        Expense expense = Approved();
        expense.MarkAsPaid(_financeUserId, _nowUtc);
        return expense;
    }

    /// <summary>Reembolso no estado pedido, sempre pelo caminho válido de transições.</summary>
    public Expense InStatus(ExpenseStatus status)
    {
        return status switch
        {
            ExpenseStatus.Draft => Draft(),
            ExpenseStatus.Submitted => Submitted(),
            ExpenseStatus.Approved => Approved(),
            ExpenseStatus.Rejected => Rejected(),
            ExpenseStatus.Paid => Paid(),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Estado desconhecido."),
        };
    }
}
