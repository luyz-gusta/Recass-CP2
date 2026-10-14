using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Data;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Dtos;

namespace ExpenseHub.Api.Services;

internal sealed class ExpenseService : IExpenseService
{
    private const string EmployeeOnlyMessage = "Somente usuários com a role Employee criam e editam os próprios reembolsos.";
    private const string ExpenseNotFoundMessage = "Reembolso não encontrado.";
    private const string InvalidDataMessage = "Os dados do reembolso são inválidos.";
    private const string OnlyDraftMessage = "Somente reembolsos em rascunho podem ser editados.";

    private readonly IExpenseRepository _repository;
    private readonly TimeProvider _clock;

    public ExpenseService(IExpenseRepository repository, TimeProvider clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<ServiceResult<ExpenseResponse>> CreateAsync(Actor actor, CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);

        if (!actor.IsInRole(AppRoles.Employee))
        {
            return ServiceResult.Forbidden<ExpenseResponse>(EmployeeOnlyMessage);
        }

        ServiceResult<ExpenseInput> validation = await ValidateAsync(request.CategoryId, request.Description, request.Amount, request.ExpenseDate, cancellationToken);
        if (!validation.Succeeded)
        {
            return validation.ToFailure<ExpenseResponse>();
        }

        ExpenseInput input = validation.Value!;
        DateTime nowUtc = _clock.GetUtcNow().UtcDateTime;

        // O proprietário vem sempre do ator autenticado, nunca do corpo da requisição.
        Expense expense = Expense.CreateDraft(actor.UserId, input.CategoryId, input.Description, input.Amount, input.ExpenseDate, nowUtc);
        _repository.Add(expense);
        _repository.AddHistory(ExpenseHistory.Record(
            expense.Id,
            ExpenseAction.Created,
            actor.UserId,
            nowUtc,
            previousStatus: null,
            newStatus: ExpenseStatus.Draft));

        await _repository.SaveChangesAsync(cancellationToken);

        // Recarrega para a resposta trazer o nome da categoria.
        Expense saved = await _repository.FindAsync(expense.Id, item => true, cancellationToken) ?? expense;
        return ServiceResult.Success(saved.ToResponse());
    }

    public async Task<ServiceResult<ExpenseResponse>> UpdateAsync(Actor actor, Guid id, UpdateExpenseRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);

        if (!actor.IsInRole(AppRoles.Employee))
        {
            return ServiceResult.Forbidden<ExpenseResponse>(EmployeeOnlyMessage);
        }

        ServiceResult<ExpenseInput> validation = await ValidateAsync(request.CategoryId, request.Description, request.Amount, request.ExpenseDate, cancellationToken);
        if (!validation.Succeeded)
        {
            return validation.ToFailure<ExpenseResponse>();
        }

        Expense? expense = await _repository.FindAsync(id, item => true, cancellationToken);
        if (expense is null)
        {
            return ServiceResult.NotFound<ExpenseResponse>(ExpenseNotFoundMessage);
        }

        AccessDecision decision = ExpenseAccessPolicy.CanManageDraft(actor, expense.OwnerId);
        if (decision != AccessDecision.Allowed)
        {
            return decision.ToDenied<ExpenseResponse>(ExpenseNotFoundMessage, EmployeeOnlyMessage);
        }

        if (expense.Status != ExpenseStatus.Draft)
        {
            return ServiceResult.Conflict<ExpenseResponse>(OnlyDraftMessage);
        }

        ExpenseInput input = validation.Value!;
        DateTime nowUtc = _clock.GetUtcNow().UtcDateTime;
        string? changes = expense.UpdateDraft(input.CategoryId, input.Description, input.Amount, input.ExpenseDate, nowUtc);

        if (changes is null)
        {
            return ServiceResult.Success(expense.ToResponse());
        }

        _repository.AddHistory(ExpenseHistory.Record(
            expense.Id,
            ExpenseAction.Updated,
            actor.UserId,
            nowUtc,
            previousStatus: ExpenseStatus.Draft,
            newStatus: ExpenseStatus.Draft,
            changes: changes));

        try
        {
            await _repository.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException exception)
        {
            return ServiceResult.Conflict<ExpenseResponse>(exception.Message);
        }

        // Recarrega para a resposta refletir o nome da categoria atual.
        Expense saved = await _repository.FindAsync(expense.Id, item => true, cancellationToken) ?? expense;
        return ServiceResult.Success(saved.ToResponse());
    }

    private async Task<ServiceResult<ExpenseInput>> ValidateAsync(
        int? categoryId,
        string? description,
        decimal? amount,
        DateOnly? expenseDate,
        CancellationToken cancellationToken)
    {
        // As DataAnnotations já barram campos ausentes no controller; aqui a regra vale também sem ele.
        Dictionary<string, string[]> errors = new(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(description))
        {
            errors[nameof(CreateExpenseRequest.Description)] = ["A descrição é obrigatória."];
        }

        if (amount is null)
        {
            errors[nameof(CreateExpenseRequest.Amount)] = ["O valor é obrigatório."];
        }

        if (expenseDate is null)
        {
            errors[nameof(CreateExpenseRequest.ExpenseDate)] = ["A data da despesa é obrigatória."];
        }
        else if (expenseDate.Value > DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime))
        {
            errors[nameof(CreateExpenseRequest.ExpenseDate)] = ["A data da despesa não pode ser futura."];
        }

        if (categoryId is null)
        {
            errors[nameof(CreateExpenseRequest.CategoryId)] = ["A categoria é obrigatória."];
        }
        else if (!await _repository.CategoryExistsAsync(categoryId.Value, cancellationToken))
        {
            errors[nameof(CreateExpenseRequest.CategoryId)] = ["A categoria informada não existe."];
        }

        if (errors.Count > 0)
        {
            return ServiceResult.Invalid<ExpenseInput>(errors, InvalidDataMessage);
        }

        return ServiceResult.Success(new ExpenseInput(categoryId!.Value, description!, amount!.Value, expenseDate!.Value));
    }
}
