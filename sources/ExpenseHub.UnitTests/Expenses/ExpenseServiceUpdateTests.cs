using System;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Dtos;
using ExpenseHub.Api.Services;
using ExpenseHub.UnitTests.TestDoubles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Expenses;

/// <summary>
/// Edição de rascunhos: só o proprietário, só em Draft, com histórico das alterações e sem escrita quando nada muda.
/// </summary>
[TestClass]
public sealed class ExpenseServiceUpdateTests
{
    private const string NewDescription = "Hotel durante o evento anual";

    private readonly InMemoryExpenseRepository _repository = new();
    private readonly FixedTimeProvider _clock = new();
    private readonly ExpenseService _service;

    /// <summary>Monta o serviço com o repositório em memória e o relógio fixo.</summary>
    public ExpenseServiceUpdateTests()
    {
        _service = new ExpenseService(_repository, _clock);
    }

    /// <summary>O proprietário edita o rascunho e a resposta traz os novos dados.</summary>
    [TestMethod]
    public async Task UpdateAsync_OwnerChangesDraft_AppliesNewData()
    {
        Expense draft = SeedDraft(TestActors.Employee1);

        ServiceResult<ExpenseResponse> result = await _service.UpdateAsync(TestActors.Employee1, draft.Id, ChangedRequest(), CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(NewDescription, result.Value!.Description);
        Assert.AreEqual(800m, result.Value.Amount);
        Assert.AreEqual(3, result.Value.CategoryId);
        Assert.AreEqual(NewDescription, draft.Description);
    }

    /// <summary>A edição nunca altera proprietário, estado nem criação, e atualiza o horário com o relógio do servidor.</summary>
    [TestMethod]
    public async Task UpdateAsync_OwnerChangesDraft_KeepsOwnerAndStatusAndTouchesUpdatedAt()
    {
        Expense draft = SeedDraft(TestActors.Employee1);
        DateTime createdAt = draft.CreatedAtUtc;
        _clock.Advance(TimeSpan.FromHours(2));

        ServiceResult<ExpenseResponse> result = await _service.UpdateAsync(TestActors.Employee1, draft.Id, ChangedRequest(), CancellationToken.None);

        Assert.AreEqual(TestActors.Employee1.UserId, result.Value!.OwnerId);
        Assert.AreEqual(ExpenseStatus.Draft, result.Value.Status);
        Assert.AreEqual(createdAt, result.Value.CreatedAtUtc);
        Assert.AreEqual(_clock.UtcNow, result.Value.UpdatedAtUtc);
    }

    /// <summary>A alteração e o histórico <c>Updated</c> (Draft para Draft, com o detalhe) são gravados juntos.</summary>
    [TestMethod]
    public async Task UpdateAsync_OwnerChangesDraft_RecordsUpdatedHistoryWithChangesInSingleSave()
    {
        Expense draft = SeedDraft(TestActors.Employee1);
        _clock.Advance(TimeSpan.FromHours(2));

        await _service.UpdateAsync(TestActors.Employee1, draft.Id, ChangedRequest(), CancellationToken.None);

        ExpenseHistory entry = Assert.ContainsSingle(_repository.History);
        Assert.AreEqual(draft.Id, entry.ExpenseId);
        Assert.AreEqual(ExpenseAction.Updated, entry.Action);
        Assert.AreEqual(TestActors.Employee1.UserId, entry.ActorId);
        Assert.AreEqual(_clock.UtcNow, entry.OccurredAtUtc);
        Assert.AreEqual(ExpenseStatus.Draft, entry.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Draft, entry.NewStatus);
        Assert.Contains("amount: 150.50 -> 800.00", entry.Changes!);
        Assert.Contains("categoryId: 1 -> 3", entry.Changes!);
        Assert.AreEqual(1, _repository.SaveChangesCount);
    }

    /// <summary>Reenviar os mesmos dados responde 200, sem histórico e sem gravar nada.</summary>
    [TestMethod]
    public async Task UpdateAsync_SameData_SucceedsWithoutHistoryOrSave()
    {
        Expense draft = SeedDraft(TestActors.Employee1);
        _clock.Advance(TimeSpan.FromHours(2));

        ServiceResult<ExpenseResponse> result = await _service.UpdateAsync(TestActors.Employee1, draft.Id, SameDataRequest(), CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(draft.CreatedAtUtc, result.Value!.UpdatedAtUtc);
        Assert.IsEmpty(_repository.History);
        Assert.AreEqual(0, _repository.SaveChangesCount);
    }

    /// <summary>Reembolso de outro Employee responde 404 e não é alterado nem registrado.</summary>
    [TestMethod]
    public async Task UpdateAsync_AnotherEmployeesDraft_ReturnsNotFoundAndKeepsDraft()
    {
        Expense draft = SeedDraft(TestActors.Employee1);

        ServiceResult<ExpenseResponse> result = await _service.UpdateAsync(TestActors.Employee2, draft.Id, ChangedRequest(), CancellationToken.None);

        Assert.AreEqual(ServiceError.NotFound, result.Error);
        Assert.AreEqual(TestExpenses.ValidDescription, draft.Description);
        AssertNothingSaved();
    }

    /// <summary>Acumular Employee com Approver não dá acesso ao rascunho de outra pessoa.</summary>
    [TestMethod]
    public async Task UpdateAsync_ApproverEmployeeOnAnotherDraft_ReturnsNotFound()
    {
        Expense draft = SeedDraft(TestActors.Employee1);

        ServiceResult<ExpenseResponse> result = await _service.UpdateAsync(TestActors.ApproverEmployee, draft.Id, ChangedRequest(), CancellationToken.None);

        Assert.AreEqual(ServiceError.NotFound, result.Error);
        AssertNothingSaved();
    }

    /// <summary>Identificador inexistente responde 404.</summary>
    [TestMethod]
    public async Task UpdateAsync_UnknownId_ReturnsNotFound()
    {
        ServiceResult<ExpenseResponse> result = await _service.UpdateAsync(TestActors.Employee1, Guid.NewGuid(), ChangedRequest(), CancellationToken.None);

        Assert.AreEqual(ServiceError.NotFound, result.Error);
        AssertNothingSaved();
    }

    /// <summary>Roles sem Employee recebem 403, mesmo para um reembolso que existe.</summary>
    [TestMethod]
    [DataRow(AppRoles.Approver)]
    [DataRow(AppRoles.Finance)]
    [DataRow(AppRoles.Auditor)]
    [DataRow(AppRoles.Admin)]
    public async Task UpdateAsync_ActorWithoutEmployeeRole_ReturnsForbidden(string role)
    {
        Expense draft = SeedDraft(TestActors.Employee1);
        Actor actor = TestActors.Create("user-without-employee", role);

        ServiceResult<ExpenseResponse> result = await _service.UpdateAsync(actor, draft.Id, ChangedRequest(), CancellationToken.None);

        Assert.AreEqual(ServiceError.Forbidden, result.Error);
        Assert.AreEqual(TestExpenses.ValidDescription, draft.Description);
        AssertNothingSaved();
    }

    /// <summary>Usuário sem nenhuma role recebe 403.</summary>
    [TestMethod]
    public async Task UpdateAsync_ActorWithNoRole_ReturnsForbidden()
    {
        Expense draft = SeedDraft(TestActors.Employee1);

        ServiceResult<ExpenseResponse> result = await _service.UpdateAsync(TestActors.NoRole, draft.Id, ChangedRequest(), CancellationToken.None);

        Assert.AreEqual(ServiceError.Forbidden, result.Error);
        AssertNothingSaved();
    }

    /// <summary>Fora de Draft o reembolso não é editado (409), sem histórico e sem gravar.</summary>
    [TestMethod]
    [DataRow(ExpenseStatus.Submitted)]
    [DataRow(ExpenseStatus.Approved)]
    [DataRow(ExpenseStatus.Rejected)]
    [DataRow(ExpenseStatus.Paid)]
    public async Task UpdateAsync_ExpenseNotInDraft_ReturnsConflictAndKeepsData(ExpenseStatus status)
    {
        Expense expense = TestExpenses.InStatus(SeedDraft(TestActors.Employee1), status);

        ServiceResult<ExpenseResponse> result = await _service.UpdateAsync(TestActors.Employee1, expense.Id, ChangedRequest(), CancellationToken.None);

        Assert.AreEqual(ServiceError.Conflict, result.Error);
        Assert.AreEqual(TestExpenses.ValidDescription, expense.Description);
        AssertNothingSaved();
    }

    /// <summary>Data futura é rejeitada no campo <c>ExpenseDate</c> e o rascunho permanece igual.</summary>
    [TestMethod]
    public async Task UpdateAsync_FutureDate_ReturnsValidationOnExpenseDate()
    {
        Expense draft = SeedDraft(TestActors.Employee1);
        UpdateExpenseRequest request = RequestWith(expenseDate: _clock.Today.AddDays(1));

        ServiceResult<ExpenseResponse> result = await _service.UpdateAsync(TestActors.Employee1, draft.Id, request, CancellationToken.None);

        Assert.AreEqual(ServiceError.Validation, result.Error);
        Assert.IsTrue(result.ValidationErrors.ContainsKey(nameof(UpdateExpenseRequest.ExpenseDate)));
        Assert.AreEqual(TestExpenses.ValidDate, draft.ExpenseDate);
        AssertNothingSaved();
    }

    /// <summary>Categoria inexistente é rejeitada no campo <c>CategoryId</c>.</summary>
    [TestMethod]
    public async Task UpdateAsync_UnknownCategory_ReturnsValidationOnCategoryId()
    {
        Expense draft = SeedDraft(TestActors.Employee1);
        UpdateExpenseRequest request = RequestWith(categoryId: 99);

        ServiceResult<ExpenseResponse> result = await _service.UpdateAsync(TestActors.Employee1, draft.Id, request, CancellationToken.None);

        Assert.AreEqual(ServiceError.Validation, result.Error);
        Assert.IsTrue(result.ValidationErrors.ContainsKey(nameof(UpdateExpenseRequest.CategoryId)));
        AssertNothingSaved();
    }

    /// <summary>Na ordem fixa da regra de decisão, a entrada inválida (400) vem antes do escopo (404).</summary>
    [TestMethod]
    public async Task UpdateAsync_InvalidDataOnAnotherEmployeesDraft_ReturnsValidationBeforeScope()
    {
        Expense draft = SeedDraft(TestActors.Employee1);
        UpdateExpenseRequest request = RequestWith(categoryId: 99);

        ServiceResult<ExpenseResponse> result = await _service.UpdateAsync(TestActors.Employee2, draft.Id, request, CancellationToken.None);

        Assert.AreEqual(ServiceError.Validation, result.Error);
    }

    /// <summary>Conflito de concorrência na gravação vira 409 e o histórico não é persistido.</summary>
    [TestMethod]
    public async Task UpdateAsync_ConcurrencyConflictOnSave_ReturnsConflictWithoutHistory()
    {
        Expense draft = SeedDraft(TestActors.Employee1);
        _repository.ConflictOnNextSave = true;

        ServiceResult<ExpenseResponse> result = await _service.UpdateAsync(TestActors.Employee1, draft.Id, ChangedRequest(), CancellationToken.None);

        Assert.AreEqual(ServiceError.Conflict, result.Error);
        Assert.IsEmpty(_repository.History);
    }

    private static UpdateExpenseRequest ChangedRequest()
    {
        return RequestWith(description: NewDescription, amount: 800m, categoryId: 3);
    }

    private static UpdateExpenseRequest SameDataRequest()
    {
        return RequestWith();
    }

    private static UpdateExpenseRequest RequestWith(
        string description = TestExpenses.ValidDescription,
        decimal amount = TestExpenses.ValidAmount,
        DateOnly? expenseDate = null,
        int categoryId = TestExpenses.ValidCategoryId)
    {
        return new UpdateExpenseRequest
        {
            Description = description,
            Amount = amount,
            ExpenseDate = expenseDate ?? TestExpenses.ValidDate,
            CategoryId = categoryId,
        };
    }

    private Expense SeedDraft(Actor owner)
    {
        Expense draft = TestExpenses.Draft(owner.UserId, _clock.UtcNow);
        _repository.Seed(draft);
        return draft;
    }

    private void AssertNothingSaved()
    {
        Assert.IsEmpty(_repository.History);
        Assert.AreEqual(0, _repository.SaveChangesCount);
    }
}
