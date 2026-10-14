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

namespace ExpenseHub.UnitTests.Workflow;

/// <summary>
/// Aprovação: somente um Approver que não é o proprietário aprova um reembolso enviado, uma única vez,
/// com aprovador, horário e histórico definidos pelo servidor e gravados juntos.
/// </summary>
[TestClass]
public sealed class ExpenseWorkflowServiceApproveTests
{
    private readonly InMemoryExpenseRepository _repository = new();
    private readonly FixedTimeProvider _clock = new();
    private readonly ExpenseWorkflowService _service;

    /// <summary>Monta o serviço com o repositório em memória e o relógio fixo.</summary>
    public ExpenseWorkflowServiceApproveTests()
    {
        _service = new ExpenseWorkflowService(_repository, _clock);
    }

    /// <summary>O Approver aprova o reembolso enviado de outra pessoa: o estado vira Approved, com aprovador e horário do servidor.</summary>
    [TestMethod]
    public async Task ApproveAsync_OthersSubmitted_ApprovesWithDeciderAndServerTime()
    {
        Expense expense = Seed(TestActors.Employee1, ExpenseStatus.Submitted);
        _clock.Advance(TimeSpan.FromHours(5));

        ServiceResult<ExpenseResponse> result = await _service.ApproveAsync(TestActors.Approver, expense.Id, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(ExpenseStatus.Approved, result.Value!.Status);
        Assert.AreEqual(TestActors.Approver.UserId, result.Value.DecidedById);
        Assert.AreEqual(_clock.UtcNow, result.Value.DecidedAtUtc);
        Assert.AreEqual(ExpenseStatus.Approved, expense.Status);
    }

    /// <summary>A mudança de estado e o histórico <c>Approved</c> (Submitted para Approved, com o aprovador) são gravados num único commit.</summary>
    [TestMethod]
    public async Task ApproveAsync_OthersSubmitted_RecordsApprovedHistoryInSingleSave()
    {
        Expense expense = Seed(TestActors.Employee1, ExpenseStatus.Submitted);
        _clock.Advance(TimeSpan.FromHours(5));

        await _service.ApproveAsync(TestActors.Approver, expense.Id, CancellationToken.None);

        ExpenseHistory entry = Assert.ContainsSingle(_repository.History);
        Assert.AreEqual(expense.Id, entry.ExpenseId);
        Assert.AreEqual(ExpenseAction.Approved, entry.Action);
        Assert.AreEqual(TestActors.Approver.UserId, entry.ActorId);
        Assert.AreEqual(_clock.UtcNow, entry.OccurredAtUtc);
        Assert.AreEqual(ExpenseStatus.Submitted, entry.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Approved, entry.NewStatus);
        Assert.IsNull(entry.Justification);
        Assert.AreEqual(1, _repository.SaveChangesCount);
    }

    /// <summary>Quem acumula Approver com Employee aprova o reembolso enviado de outra pessoa.</summary>
    [TestMethod]
    public async Task ApproveAsync_ApproverEmployeeOnOthersSubmitted_Approves()
    {
        Expense expense = Seed(TestActors.Employee1, ExpenseStatus.Submitted);

        ServiceResult<ExpenseResponse> result = await _service.ApproveAsync(TestActors.ApproverEmployee, expense.Id, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(TestActors.ApproverEmployee.UserId, expense.DecidedById);
    }

    /// <summary>Autoaprovação é proibida: Approver + Employee no próprio reembolso recebe 403, sem histórico e sem gravar.</summary>
    [TestMethod]
    public async Task ApproveAsync_ApproverEmployeeOnOwnSubmitted_ReturnsForbiddenWithoutHistory()
    {
        Expense expense = Seed(TestActors.ApproverEmployee, ExpenseStatus.Submitted);

        ServiceResult<ExpenseResponse> result = await _service.ApproveAsync(TestActors.ApproverEmployee, expense.Id, CancellationToken.None);

        Assert.AreEqual(ServiceError.Forbidden, result.Error);
        Assert.AreEqual(ExpenseStatus.Submitted, expense.Status);
        AssertNothingSaved();
    }

    /// <summary>Roles sem Approver recebem 403 (Employee, Finance, Auditor e Admin não decidem), e nada muda.</summary>
    [TestMethod]
    [DataRow(AppRoles.Employee)]
    [DataRow(AppRoles.Finance)]
    [DataRow(AppRoles.Auditor)]
    [DataRow(AppRoles.Admin)]
    public async Task ApproveAsync_ActorWithoutApproverRole_ReturnsForbidden(string role)
    {
        Expense expense = Seed(TestActors.Employee1, ExpenseStatus.Submitted);
        Actor actor = TestActors.Create("user-without-approver", role);

        ServiceResult<ExpenseResponse> result = await _service.ApproveAsync(actor, expense.Id, CancellationToken.None);

        Assert.AreEqual(ServiceError.Forbidden, result.Error);
        Assert.AreEqual(ExpenseStatus.Submitted, expense.Status);
        AssertNothingSaved();
    }

    /// <summary>Usuário sem nenhuma role recebe 403.</summary>
    [TestMethod]
    public async Task ApproveAsync_ActorWithNoRole_ReturnsForbidden()
    {
        Expense expense = Seed(TestActors.Employee1, ExpenseStatus.Submitted);

        ServiceResult<ExpenseResponse> result = await _service.ApproveAsync(TestActors.NoRole, expense.Id, CancellationToken.None);

        Assert.AreEqual(ServiceError.Forbidden, result.Error);
        AssertNothingSaved();
    }

    /// <summary>Identificador inexistente responde 404.</summary>
    [TestMethod]
    public async Task ApproveAsync_UnknownId_ReturnsNotFound()
    {
        ServiceResult<ExpenseResponse> result = await _service.ApproveAsync(TestActors.Approver, Guid.NewGuid(), CancellationToken.None);

        Assert.AreEqual(ServiceError.NotFound, result.Error);
        AssertNothingSaved();
    }

    /// <summary>O rascunho de outra pessoa responde 404: o Approver nem enxerga rascunhos, então não descobre que ele existe.</summary>
    [TestMethod]
    public async Task ApproveAsync_OthersDraft_ReturnsNotFoundAndKeepsDraft()
    {
        Expense draft = Seed(TestActors.Employee1, ExpenseStatus.Draft);

        ServiceResult<ExpenseResponse> result = await _service.ApproveAsync(TestActors.Approver, draft.Id, CancellationToken.None);

        Assert.AreEqual(ServiceError.NotFound, result.Error);
        Assert.AreEqual(ExpenseStatus.Draft, draft.Status);
        AssertNothingSaved();
    }

    /// <summary>Aprovar duas vezes: a segunda responde 409 e existe exatamente um histórico <c>Approved</c>.</summary>
    [TestMethod]
    public async Task ApproveAsync_SecondApproval_ReturnsConflictWithSingleApprovedHistory()
    {
        Expense expense = Seed(TestActors.Employee1, ExpenseStatus.Submitted);
        await _service.ApproveAsync(TestActors.Approver, expense.Id, CancellationToken.None);

        ServiceResult<ExpenseResponse> second = await _service.ApproveAsync(TestActors.Approver, expense.Id, CancellationToken.None);

        Assert.AreEqual(ServiceError.Conflict, second.Error);
        Assert.AreEqual(1, _repository.History.Count(entry => entry.Action == ExpenseAction.Approved));
        Assert.AreEqual(1, _repository.SaveChangesCount);
    }

    /// <summary>Rejected é final: depois da reprovação, aprovar responde 409 e a reprovação continua valendo.</summary>
    [TestMethod]
    public async Task ApproveAsync_AfterRejection_ReturnsConflictAndKeepsRejection()
    {
        Expense expense = Seed(TestActors.Employee1, ExpenseStatus.Submitted);
        RejectExpenseRequest rejection = new() { Justification = "Comprovante ilegível, envie novamente." };
        await _service.RejectAsync(TestActors.Approver, expense.Id, rejection, CancellationToken.None);

        ServiceResult<ExpenseResponse> result = await _service.ApproveAsync(TestActors.Approver, expense.Id, CancellationToken.None);

        Assert.AreEqual(ServiceError.Conflict, result.Error);
        Assert.AreEqual(ExpenseStatus.Rejected, expense.Status);
        Assert.AreEqual(ExpenseAction.Rejected, Assert.ContainsSingle(_repository.History).Action);
    }

    /// <summary>Reembolsos já decididos ou pagos não recebem nova decisão: 409, sem histórico e sem gravar.</summary>
    [TestMethod]
    [DataRow(ExpenseStatus.Approved)]
    [DataRow(ExpenseStatus.Rejected)]
    [DataRow(ExpenseStatus.Paid)]
    public async Task ApproveAsync_ExpenseAlreadyDecidedOrPaid_ReturnsConflictAndKeepsStatus(ExpenseStatus status)
    {
        Expense expense = Seed(TestActors.Employee1, status);

        ServiceResult<ExpenseResponse> result = await _service.ApproveAsync(TestActors.Approver, expense.Id, CancellationToken.None);

        Assert.AreEqual(ServiceError.Conflict, result.Error);
        Assert.AreEqual(status, expense.Status);
        AssertNothingSaved();
    }

    /// <summary>Conflito de concorrência na gravação vira 409 e o histórico não é confirmado.</summary>
    [TestMethod]
    public async Task ApproveAsync_ConcurrencyConflictOnSave_ReturnsConflictWithoutHistory()
    {
        Expense expense = Seed(TestActors.Employee1, ExpenseStatus.Submitted);
        _repository.ConflictOnNextSave = true;

        ServiceResult<ExpenseResponse> result = await _service.ApproveAsync(TestActors.Approver, expense.Id, CancellationToken.None);

        Assert.AreEqual(ServiceError.Conflict, result.Error);
        Assert.IsEmpty(_repository.History);
    }

    private Expense Seed(Actor owner, ExpenseStatus status)
    {
        return _repository.Seed(ExpenseScenario.Of(owner).At(_clock.UtcNow).InStatus(status));
    }

    private void AssertNothingSaved()
    {
        Assert.IsEmpty(_repository.History);
        Assert.AreEqual(0, _repository.SaveChangesCount);
    }
}
