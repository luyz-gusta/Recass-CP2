using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Data;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Dtos;

namespace ExpenseHub.Api.Services;

internal sealed class ExpenseWorkflowService : IExpenseWorkflowService
{
    private const string ApproverOnlyMessage = "Somente usuários com a role Approver aprovam ou reprovam reembolsos.";
    private const string OwnExpenseDecisionMessage = "Ninguém aprova ou reprova o próprio reembolso, mesmo acumulando a role Approver.";
    private const string FinanceOnlyMessage = "Somente usuários com a role Finance registram pagamentos.";
    private const string OwnExpensePaymentMessage = "Ninguém paga o próprio reembolso, mesmo acumulando a role Finance.";
    private const string ReaderOnlyMessage = "Somente usuários com a role Employee, Approver, Finance ou Auditor consultam o histórico de reembolsos.";
    private const string ExpenseNotFoundMessage = "Reembolso não encontrado.";
    private const string OnlySubmittedDecisionMessage = "Somente reembolsos enviados podem ser aprovados ou reprovados, e a decisão não pode ser repetida.";
    private const string OnlyApprovedPaymentMessage = "Somente reembolsos aprovados podem ser pagos, e um reembolso nunca é pago duas vezes.";
    private const string InvalidJustificationMessage = "A justificativa da reprovação precisa ter de 10 a 500 caracteres, sem contar os espaços nas pontas.";

    private readonly IExpenseRepository _repository;
    private readonly TimeProvider _clock;

    public ExpenseWorkflowService(IExpenseRepository repository, TimeProvider clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<ServiceResult<ExpenseResponse>> ApproveAsync(Actor actor, Guid id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        if (!actor.IsInRole(AppRoles.Approver))
        {
            return ServiceResult.Forbidden<ExpenseResponse>(ApproverOnlyMessage);
        }

        ServiceResult<Expense> target = await FindSubmittedForDecisionAsync(actor, id, cancellationToken);
        if (!target.Succeeded)
        {
            return target.ToFailure<ExpenseResponse>();
        }

        Expense expense = target.Value!;
        DateTime nowUtc = _clock.GetUtcNow().UtcDateTime;

        // O aprovador e o horário vêm do ator autenticado e do relógio do servidor, nunca do cliente.
        expense.Approve(actor.UserId, nowUtc);
        _repository.AddHistory(ExpenseHistory.Record(
            expense.Id,
            ExpenseAction.Approved,
            actor.UserId,
            nowUtc,
            previousStatus: ExpenseStatus.Submitted,
            newStatus: ExpenseStatus.Approved));

        return await SaveAsync(expense, cancellationToken);
    }

    public async Task<ServiceResult<ExpenseResponse>> RejectAsync(Actor actor, Guid id, RejectExpenseRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);

        if (!actor.IsInRole(AppRoles.Approver))
        {
            return ServiceResult.Forbidden<ExpenseResponse>(ApproverOnlyMessage);
        }

        // As DataAnnotations já barram o corpo inválido no controller; aqui o tamanho é medido sem os espaços nas pontas.
        string justification = request.Justification?.Trim() ?? string.Empty;
        if (justification.Length is < Expense.RejectionReasonMinLength or > Expense.RejectionReasonMaxLength)
        {
            return ServiceResult.Invalid<ExpenseResponse>(nameof(RejectExpenseRequest.Justification), InvalidJustificationMessage);
        }

        ServiceResult<Expense> target = await FindSubmittedForDecisionAsync(actor, id, cancellationToken);
        if (!target.Succeeded)
        {
            return target.ToFailure<ExpenseResponse>();
        }

        Expense expense = target.Value!;
        DateTime nowUtc = _clock.GetUtcNow().UtcDateTime;

        expense.Reject(actor.UserId, justification, nowUtc);
        _repository.AddHistory(ExpenseHistory.Record(
            expense.Id,
            ExpenseAction.Rejected,
            actor.UserId,
            nowUtc,
            previousStatus: ExpenseStatus.Submitted,
            newStatus: ExpenseStatus.Rejected,
            justification: expense.RejectionReason));

        return await SaveAsync(expense, cancellationToken);
    }

    public async Task<ServiceResult<ExpenseResponse>> PayAsync(Actor actor, Guid id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        if (!actor.IsInRole(AppRoles.Finance))
        {
            return ServiceResult.Forbidden<ExpenseResponse>(FinanceOnlyMessage);
        }

        Expense? expense = await _repository.FindAsync(id, item => true, cancellationToken);
        if (expense is null)
        {
            return ServiceResult.NotFound<ExpenseResponse>(ExpenseNotFoundMessage);
        }

        AccessDecision decision = ExpenseAccessPolicy.CanPay(actor, expense.OwnerId, expense.Status);
        if (decision != AccessDecision.Allowed)
        {
            return decision.ToDenied<ExpenseResponse>(ExpenseNotFoundMessage, OwnExpensePaymentMessage);
        }

        // A transição é checada antes de tocar na entidade: um segundo pagamento é conflito e não gera pagamento nem histórico.
        if (expense.Status != ExpenseStatus.Approved)
        {
            return ServiceResult.Conflict<ExpenseResponse>(OnlyApprovedPaymentMessage);
        }

        DateTime nowUtc = _clock.GetUtcNow().UtcDateTime;

        // Estado, pagamento e histórico entram na mesma gravação: ou os três são persistidos, ou nenhum.
        PaymentRecord payment = expense.MarkAsPaid(actor.UserId, nowUtc);
        _repository.AddPayment(payment);
        _repository.AddHistory(ExpenseHistory.Record(
            expense.Id,
            ExpenseAction.Paid,
            actor.UserId,
            nowUtc,
            previousStatus: ExpenseStatus.Approved,
            newStatus: ExpenseStatus.Paid));

        return await SaveAsync(expense, cancellationToken);
    }

    public async Task<ServiceResult<IReadOnlyList<ExpenseHistoryResponse>>> GetHistoryAsync(Actor actor, Guid id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        if (!ExpenseAccessPolicy.HasReadRole(actor))
        {
            return ServiceResult.Forbidden<IReadOnlyList<ExpenseHistoryResponse>>(ReaderOnlyMessage);
        }

        // Mesma visibilidade do detalhe: o escopo vai na consulta, e inexistente e fora do escopo respondem o mesmo 404.
        Expense? expense = await _repository.FindAsync(id, ExpenseAccessPolicy.ReadScope(actor), cancellationToken);
        if (expense is null)
        {
            return ServiceResult.NotFound<IReadOnlyList<ExpenseHistoryResponse>>(ExpenseNotFoundMessage);
        }

        IReadOnlyList<ExpenseHistory> entries = await _repository.ListHistoryAsync(expense.Id, cancellationToken);
        List<ExpenseHistoryResponse> response = entries.Select(entry => entry.ToResponse()).ToList();

        return ServiceResult.Success<IReadOnlyList<ExpenseHistoryResponse>>(response);
    }

    /// <summary>
    /// Escopo, ownership e estado de uma decisão, nesta ordem: inexistente ou rascunho alheio (404), próprio reembolso (403)
    /// e estado diferente de Submitted (409). Nada é alterado antes de todas as verificações passarem.
    /// </summary>
    private async Task<ServiceResult<Expense>> FindSubmittedForDecisionAsync(Actor actor, Guid id, CancellationToken cancellationToken)
    {
        Expense? expense = await _repository.FindAsync(id, item => true, cancellationToken);
        if (expense is null)
        {
            return ServiceResult.NotFound<Expense>(ExpenseNotFoundMessage);
        }

        AccessDecision decision = ExpenseAccessPolicy.CanDecide(actor, expense.OwnerId, expense.Status);
        if (decision != AccessDecision.Allowed)
        {
            return decision.ToDenied<Expense>(ExpenseNotFoundMessage, OwnExpenseDecisionMessage);
        }

        // A transição é checada antes de tocar na entidade: repetir a decisão é conflito e não gera histórico duplicado.
        if (expense.Status != ExpenseStatus.Submitted)
        {
            return ServiceResult.Conflict<Expense>(OnlySubmittedDecisionMessage);
        }

        return ServiceResult.Success(expense);
    }

    /// <summary>
    /// Confirma a mudança de estado, o histórico e o pagamento, quando houver, juntos, num único <c>SaveChangesAsync</c>.
    /// Uma falha de persistência propaga a exceção sem confirmar nada; um conflito de concorrência vira 409.
    /// </summary>
    private async Task<ServiceResult<ExpenseResponse>> SaveAsync(Expense expense, CancellationToken cancellationToken)
    {
        try
        {
            await _repository.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException exception)
        {
            return ServiceResult.Conflict<ExpenseResponse>(exception.Message);
        }

        return ServiceResult.Success(expense.ToResponse());
    }
}
