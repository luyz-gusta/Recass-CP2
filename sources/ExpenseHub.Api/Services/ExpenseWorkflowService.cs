using System;
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
    private const string ExpenseNotFoundMessage = "Reembolso não encontrado.";
    private const string OnlySubmittedDecisionMessage = "Somente reembolsos enviados podem ser aprovados ou reprovados, e a decisão não pode ser repetida.";
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
    /// Confirma a mudança de estado e o histórico juntos, num único <c>SaveChangesAsync</c>.
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
