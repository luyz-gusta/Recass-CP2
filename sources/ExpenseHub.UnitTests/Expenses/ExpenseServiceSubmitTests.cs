using System;
using System.Linq;
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
/// Envio de rascunhos: só o proprietário com a role Employee, só uma vez, com histórico gravado junto com a mudança de estado.
/// </summary>
[TestClass]
public sealed class ExpenseServiceSubmitTests
{
    private readonly InMemoryExpenseRepository _repository = new();
    private readonly FixedTimeProvider _clock = new();
    private readonly ExpenseService _service;

    /// <summary>Monta o serviço com o repositório em memória e o relógio fixo.</summary>
    public ExpenseServiceSubmitTests()
    {
        _service = new ExpenseService(_repository, _clock);
    }

    /// <summary>O proprietário envia o rascunho: o estado vira Submitted e o instante do envio é do servidor.</summary>
    [TestMethod]
    public async Task SubmitAsync_OwnerDraft_ChangesStatusAndFillsSubmissionTime()
    {
        Expense draft = SeedDraft(TestActors.Employee1);
        _clock.Advance(TimeSpan.FromHours(3));

        ServiceResult<ExpenseResponse> result = await _service.SubmitAsync(TestActors.Employee1, draft.Id, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(ExpenseStatus.Submitted, result.Value!.Status);
        Assert.AreEqual(_clock.UtcNow, result.Value.SubmittedAtUtc);
        Assert.AreEqual(_clock.UtcNow, result.Value.UpdatedAtUtc);
        Assert.AreEqual(ExpenseStatus.Submitted, draft.Status);
    }

    /// <summary>A mudança de estado e o histórico <c>Submitted</c> (Draft para Submitted) são gravados juntos, num único commit.</summary>
    [TestMethod]
    public async Task SubmitAsync_OwnerDraft_RecordsSubmittedHistoryInSingleSave()
    {
        Expense draft = SeedDraft(TestActors.Employee1);
        _clock.Advance(TimeSpan.FromHours(3));

        await _service.SubmitAsync(TestActors.Employee1, draft.Id, CancellationToken.None);

        ExpenseHistory entry = Assert.ContainsSingle(_repository.History);
        Assert.AreEqual(draft.Id, entry.ExpenseId);
        Assert.AreEqual(ExpenseAction.Submitted, entry.Action);
        Assert.AreEqual(TestActors.Employee1.UserId, entry.ActorId);
        Assert.AreEqual(_clock.UtcNow, entry.OccurredAtUtc);
        Assert.AreEqual(ExpenseStatus.Draft, entry.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Submitted, entry.NewStatus);
        Assert.AreEqual(1, _repository.SaveChangesCount);
    }

    /// <summary>Quem acumula Employee com Approver envia o próprio rascunho.</summary>
    [TestMethod]
    public async Task SubmitAsync_ApproverEmployeeOnOwnDraft_Succeeds()
    {
        Expense draft = SeedDraft(TestActors.ApproverEmployee);

        ServiceResult<ExpenseResponse> result = await _service.SubmitAsync(TestActors.ApproverEmployee, draft.Id, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(ExpenseStatus.Submitted, draft.Status);
    }

    /// <summary>Enviar duas vezes: o segundo envio é 409 e existe exatamente um histórico <c>Submitted</c>.</summary>
    [TestMethod]
    public async Task SubmitAsync_SecondSubmission_ReturnsConflictWithSingleSubmittedHistory()
    {
        Expense draft = SeedDraft(TestActors.Employee1);
        await _service.SubmitAsync(TestActors.Employee1, draft.Id, CancellationToken.None);

        ServiceResult<ExpenseResponse> second = await _service.SubmitAsync(TestActors.Employee1, draft.Id, CancellationToken.None);

        Assert.AreEqual(ServiceError.Conflict, second.Error);
        Assert.AreEqual(1, _repository.History.Count(entry => entry.Action == ExpenseAction.Submitted));
        Assert.AreEqual(1, _repository.SaveChangesCount);
    }

    /// <summary>Estados fora de Draft não são enviados (409), sem histórico e sem gravar.</summary>
    [TestMethod]
    [DataRow(ExpenseStatus.Submitted)]
    [DataRow(ExpenseStatus.Approved)]
    [DataRow(ExpenseStatus.Rejected)]
    [DataRow(ExpenseStatus.Paid)]
    public async Task SubmitAsync_ExpenseNotInDraft_ReturnsConflictAndKeepsStatus(ExpenseStatus status)
    {
        Expense expense = TestExpenses.InStatus(SeedDraft(TestActors.Employee1), status);

        ServiceResult<ExpenseResponse> result = await _service.SubmitAsync(TestActors.Employee1, expense.Id, CancellationToken.None);

        Assert.AreEqual(ServiceError.Conflict, result.Error);
        Assert.AreEqual(status, expense.Status);
        AssertNothingSaved();
    }

    /// <summary>Outro Employee não envia o rascunho alheio (404) e o estado não muda.</summary>
    [TestMethod]
    public async Task SubmitAsync_AnotherEmployeesDraft_ReturnsNotFoundAndKeepsDraft()
    {
        Expense draft = SeedDraft(TestActors.Employee1);

        ServiceResult<ExpenseResponse> result = await _service.SubmitAsync(TestActors.Employee2, draft.Id, CancellationToken.None);

        Assert.AreEqual(ServiceError.NotFound, result.Error);
        Assert.AreEqual(ExpenseStatus.Draft, draft.Status);
        AssertNothingSaved();
    }

    /// <summary>Acumular Employee com Approver não dá acesso ao rascunho de outra pessoa.</summary>
    [TestMethod]
    public async Task SubmitAsync_ApproverEmployeeOnAnotherDraft_ReturnsNotFound()
    {
        Expense draft = SeedDraft(TestActors.Employee1);

        ServiceResult<ExpenseResponse> result = await _service.SubmitAsync(TestActors.ApproverEmployee, draft.Id, CancellationToken.None);

        Assert.AreEqual(ServiceError.NotFound, result.Error);
        AssertNothingSaved();
    }

    /// <summary>Identificador inexistente responde 404.</summary>
    [TestMethod]
    public async Task SubmitAsync_UnknownId_ReturnsNotFound()
    {
        ServiceResult<ExpenseResponse> result = await _service.SubmitAsync(TestActors.Employee1, Guid.NewGuid(), CancellationToken.None);

        Assert.AreEqual(ServiceError.NotFound, result.Error);
        AssertNothingSaved();
    }

    /// <summary>Roles sem Employee recebem 403 (o Auditor não ganha escrita e o Admin não ganha acesso funcional), e nada muda.</summary>
    [TestMethod]
    [DataRow(AppRoles.Approver)]
    [DataRow(AppRoles.Finance)]
    [DataRow(AppRoles.Auditor)]
    [DataRow(AppRoles.Admin)]
    public async Task SubmitAsync_ActorWithoutEmployeeRole_ReturnsForbidden(string role)
    {
        Expense draft = SeedDraft(TestActors.Employee1);
        Actor actor = TestActors.Create("user-without-employee", role);

        ServiceResult<ExpenseResponse> result = await _service.SubmitAsync(actor, draft.Id, CancellationToken.None);

        Assert.AreEqual(ServiceError.Forbidden, result.Error);
        Assert.AreEqual(ExpenseStatus.Draft, draft.Status);
        AssertNothingSaved();
    }

    /// <summary>Usuário sem nenhuma role recebe 403.</summary>
    [TestMethod]
    public async Task SubmitAsync_ActorWithNoRole_ReturnsForbidden()
    {
        Expense draft = SeedDraft(TestActors.Employee1);

        ServiceResult<ExpenseResponse> result = await _service.SubmitAsync(TestActors.NoRole, draft.Id, CancellationToken.None);

        Assert.AreEqual(ServiceError.Forbidden, result.Error);
        AssertNothingSaved();
    }

    /// <summary>Depois do envio o reembolso não é mais editado: a edição responde 409 e não gera histórico.</summary>
    [TestMethod]
    public async Task UpdateAsync_AfterSubmission_ReturnsConflictWithoutNewHistory()
    {
        Expense draft = SeedDraft(TestActors.Employee1);
        await _service.SubmitAsync(TestActors.Employee1, draft.Id, CancellationToken.None);
        UpdateExpenseRequest request = new()
        {
            Description = "Tentativa de editar depois de enviar",
            Amount = 1m,
            ExpenseDate = TestExpenses.ValidDate,
            CategoryId = TestExpenses.ValidCategoryId,
        };

        ServiceResult<ExpenseResponse> result = await _service.UpdateAsync(TestActors.Employee1, draft.Id, request, CancellationToken.None);

        Assert.AreEqual(ServiceError.Conflict, result.Error);
        Assert.AreEqual(TestExpenses.ValidDescription, draft.Description);
        Assert.HasCount(1, _repository.History);
    }

    /// <summary>Conflito de concorrência na gravação vira 409 e o histórico não é persistido.</summary>
    [TestMethod]
    public async Task SubmitAsync_ConcurrencyConflictOnSave_ReturnsConflictWithoutHistory()
    {
        Expense draft = SeedDraft(TestActors.Employee1);
        _repository.ConflictOnNextSave = true;

        ServiceResult<ExpenseResponse> result = await _service.SubmitAsync(TestActors.Employee1, draft.Id, CancellationToken.None);

        Assert.AreEqual(ServiceError.Conflict, result.Error);
        Assert.IsEmpty(_repository.History);
    }

    /// <summary>Uma falha de persistência não deixa histórico pela metade.</summary>
    [TestMethod]
    public async Task SubmitAsync_SaveFails_PersistsNoHistory()
    {
        Expense draft = SeedDraft(TestActors.Employee1);
        _repository.FailNextSave = true;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _service.SubmitAsync(TestActors.Employee1, draft.Id, CancellationToken.None));

        Assert.IsEmpty(_repository.History);
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
