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
/// Reprovação: somente um Approver que não é o proprietário reprova um reembolso enviado, com justificativa de 10 a 500
/// caracteres gravada no reembolso e no histórico, uma única vez e numa única gravação.
/// </summary>
[TestClass]
public sealed class ExpenseWorkflowServiceRejectTests
{
    private const string Justification = "Comprovante ilegível, envie novamente.";

    private readonly InMemoryExpenseRepository _repository = new();
    private readonly FixedTimeProvider _clock = new();
    private readonly ExpenseWorkflowService _service;

    /// <summary>Monta o serviço com o repositório em memória e o relógio fixo.</summary>
    public ExpenseWorkflowServiceRejectTests()
    {
        _service = new ExpenseWorkflowService(_repository, _clock);
    }

    /// <summary>O Approver reprova o reembolso enviado de outra pessoa: o estado vira Rejected, com aprovador e horário do servidor.</summary>
    [TestMethod]
    public async Task RejectAsync_OthersSubmitted_RejectsWithDeciderAndServerTime()
    {
        Expense expense = Seed(TestActors.Employee1, ExpenseStatus.Submitted);
        _clock.Advance(TimeSpan.FromHours(5));

        ServiceResult<ExpenseResponse> result = await _service.RejectAsync(TestActors.Approver, expense.Id, Request(Justification), CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(ExpenseStatus.Rejected, result.Value!.Status);
        Assert.AreEqual(TestActors.Approver.UserId, result.Value.DecidedById);
        Assert.AreEqual(_clock.UtcNow, result.Value.DecidedAtUtc);
        Assert.AreEqual(Justification, result.Value.RejectionReason);
    }

    /// <summary>
    /// A justificativa, sem os espaços nas pontas, fica no reembolso e no histórico <c>Rejected</c> (Submitted para Rejected),
    /// gravados num único commit.
    /// </summary>
    [TestMethod]
    public async Task RejectAsync_OthersSubmitted_PersistsJustificationInExpenseAndHistoryInSingleSave()
    {
        Expense expense = Seed(TestActors.Employee1, ExpenseStatus.Submitted);

        await _service.RejectAsync(TestActors.Approver, expense.Id, Request("  " + Justification + "   "), CancellationToken.None);

        ExpenseHistory entry = Assert.ContainsSingle(_repository.History);
        Assert.AreEqual(ExpenseAction.Rejected, entry.Action);
        Assert.AreEqual(TestActors.Approver.UserId, entry.ActorId);
        Assert.AreEqual(_clock.UtcNow, entry.OccurredAtUtc);
        Assert.AreEqual(ExpenseStatus.Submitted, entry.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Rejected, entry.NewStatus);
        Assert.AreEqual(Justification, entry.Justification);
        Assert.AreEqual(Justification, expense.RejectionReason);
        Assert.AreEqual(1, _repository.SaveChangesCount);
    }

    /// <summary>Justificativas com 10 e com 500 caracteres são aceitas.</summary>
    [TestMethod]
    [DataRow(10)]
    [DataRow(500)]
    public async Task RejectAsync_JustificationAtLengthLimit_Rejects(int length)
    {
        Expense expense = Seed(TestActors.Employee1, ExpenseStatus.Submitted);

        ServiceResult<ExpenseResponse> result = await _service.RejectAsync(TestActors.Approver, expense.Id, Request(new string('j', length)), CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(ExpenseStatus.Rejected, expense.Status);
    }

    /// <summary>Justificativa nula, vazia ou só com espaços responde 400 no campo <c>Justification</c>, sem gravar.</summary>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("            ")]
    public async Task RejectAsync_BlankJustification_ReturnsValidationWithoutSaving(string? justification)
    {
        Expense expense = Seed(TestActors.Employee1, ExpenseStatus.Submitted);

        ServiceResult<ExpenseResponse> result = await _service.RejectAsync(TestActors.Approver, expense.Id, Request(justification!), CancellationToken.None);

        AssertJustificationRejected(result, expense);
    }

    /// <summary>Justificativa com 9 ou com 501 caracteres responde 400 no campo <c>Justification</c>, sem gravar.</summary>
    [TestMethod]
    [DataRow(9)]
    [DataRow(501)]
    public async Task RejectAsync_JustificationOutOfLength_ReturnsValidationWithoutSaving(int length)
    {
        Expense expense = Seed(TestActors.Employee1, ExpenseStatus.Submitted);

        ServiceResult<ExpenseResponse> result = await _service.RejectAsync(TestActors.Approver, expense.Id, Request(new string('j', length)), CancellationToken.None);

        AssertJustificationRejected(result, expense);
    }

    /// <summary>O tamanho é medido sem os espaços nas pontas: 9 caracteres cercados de espaços não bastam.</summary>
    [TestMethod]
    public async Task RejectAsync_JustificationShortAfterTrim_ReturnsValidationWithoutSaving()
    {
        Expense expense = Seed(TestActors.Employee1, ExpenseStatus.Submitted);

        ServiceResult<ExpenseResponse> result = await _service.RejectAsync(TestActors.Approver, expense.Id, Request("    123456789    "), CancellationToken.None);

        AssertJustificationRejected(result, expense);
    }

    /// <summary>A entrada é validada antes do escopo: justificativa inválida num id inexistente responde 400, e não 404.</summary>
    [TestMethod]
    public async Task RejectAsync_InvalidJustificationOnUnknownId_ReturnsValidation()
    {
        ServiceResult<ExpenseResponse> result = await _service.RejectAsync(TestActors.Approver, Guid.NewGuid(), Request("curta"), CancellationToken.None);

        Assert.AreEqual(ServiceError.Validation, result.Error);
    }

    /// <summary>A role é verificada antes da entrada: sem Approver, até uma justificativa inválida responde 403.</summary>
    [TestMethod]
    public async Task RejectAsync_ActorWithoutApproverRoleAndInvalidJustification_ReturnsForbidden()
    {
        Expense expense = Seed(TestActors.Employee1, ExpenseStatus.Submitted);

        ServiceResult<ExpenseResponse> result = await _service.RejectAsync(TestActors.Employee2, expense.Id, Request("curta"), CancellationToken.None);

        Assert.AreEqual(ServiceError.Forbidden, result.Error);
    }

    /// <summary>Autorreprovação é proibida: Approver + Employee no próprio reembolso recebe 403, sem histórico e sem gravar.</summary>
    [TestMethod]
    public async Task RejectAsync_ApproverEmployeeOnOwnSubmitted_ReturnsForbiddenWithoutHistory()
    {
        Expense expense = Seed(TestActors.ApproverEmployee, ExpenseStatus.Submitted);

        ServiceResult<ExpenseResponse> result = await _service.RejectAsync(TestActors.ApproverEmployee, expense.Id, Request(Justification), CancellationToken.None);

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
    public async Task RejectAsync_ActorWithoutApproverRole_ReturnsForbidden(string role)
    {
        Expense expense = Seed(TestActors.Employee1, ExpenseStatus.Submitted);
        Actor actor = TestActors.Create("user-without-approver", role);

        ServiceResult<ExpenseResponse> result = await _service.RejectAsync(actor, expense.Id, Request(Justification), CancellationToken.None);

        Assert.AreEqual(ServiceError.Forbidden, result.Error);
        Assert.AreEqual(ExpenseStatus.Submitted, expense.Status);
        AssertNothingSaved();
    }

    /// <summary>Usuário sem nenhuma role recebe 403.</summary>
    [TestMethod]
    public async Task RejectAsync_ActorWithNoRole_ReturnsForbidden()
    {
        Expense expense = Seed(TestActors.Employee1, ExpenseStatus.Submitted);

        ServiceResult<ExpenseResponse> result = await _service.RejectAsync(TestActors.NoRole, expense.Id, Request(Justification), CancellationToken.None);

        Assert.AreEqual(ServiceError.Forbidden, result.Error);
        AssertNothingSaved();
    }

    /// <summary>Identificador inexistente responde 404.</summary>
    [TestMethod]
    public async Task RejectAsync_UnknownId_ReturnsNotFound()
    {
        ServiceResult<ExpenseResponse> result = await _service.RejectAsync(TestActors.Approver, Guid.NewGuid(), Request(Justification), CancellationToken.None);

        Assert.AreEqual(ServiceError.NotFound, result.Error);
        AssertNothingSaved();
    }

    /// <summary>O rascunho de outra pessoa responde 404, como se não existisse, e continua em Draft.</summary>
    [TestMethod]
    public async Task RejectAsync_OthersDraft_ReturnsNotFoundAndKeepsDraft()
    {
        Expense draft = Seed(TestActors.Employee1, ExpenseStatus.Draft);

        ServiceResult<ExpenseResponse> result = await _service.RejectAsync(TestActors.Approver, draft.Id, Request(Justification), CancellationToken.None);

        Assert.AreEqual(ServiceError.NotFound, result.Error);
        Assert.AreEqual(ExpenseStatus.Draft, draft.Status);
        AssertNothingSaved();
    }

    /// <summary>Reprovar duas vezes: a segunda responde 409, a primeira justificativa continua e existe um único histórico <c>Rejected</c>.</summary>
    [TestMethod]
    public async Task RejectAsync_SecondRejection_ReturnsConflictWithSingleRejectedHistory()
    {
        Expense expense = Seed(TestActors.Employee1, ExpenseStatus.Submitted);
        await _service.RejectAsync(TestActors.Approver, expense.Id, Request(Justification), CancellationToken.None);

        ServiceResult<ExpenseResponse> second = await _service.RejectAsync(TestActors.Approver, expense.Id, Request("Outra justificativa qualquer"), CancellationToken.None);

        Assert.AreEqual(ServiceError.Conflict, second.Error);
        Assert.AreEqual(Justification, expense.RejectionReason);
        Assert.AreEqual(1, _repository.History.Count(entry => entry.Action == ExpenseAction.Rejected));
        Assert.AreEqual(1, _repository.SaveChangesCount);
    }

    /// <summary>Depois da aprovação, reprovar responde 409 e a aprovação continua valendo, sem justificativa.</summary>
    [TestMethod]
    public async Task RejectAsync_AfterApproval_ReturnsConflictAndKeepsApproval()
    {
        Expense expense = Seed(TestActors.Employee1, ExpenseStatus.Submitted);
        await _service.ApproveAsync(TestActors.Approver, expense.Id, CancellationToken.None);

        ServiceResult<ExpenseResponse> result = await _service.RejectAsync(TestActors.Approver, expense.Id, Request(Justification), CancellationToken.None);

        Assert.AreEqual(ServiceError.Conflict, result.Error);
        Assert.AreEqual(ExpenseStatus.Approved, expense.Status);
        Assert.IsNull(expense.RejectionReason);
        Assert.AreEqual(ExpenseAction.Approved, Assert.ContainsSingle(_repository.History).Action);
    }

    /// <summary>Reembolsos já decididos ou pagos não recebem nova decisão: 409, sem histórico e sem gravar.</summary>
    [TestMethod]
    [DataRow(ExpenseStatus.Approved)]
    [DataRow(ExpenseStatus.Rejected)]
    [DataRow(ExpenseStatus.Paid)]
    public async Task RejectAsync_ExpenseAlreadyDecidedOrPaid_ReturnsConflictAndKeepsStatus(ExpenseStatus status)
    {
        Expense expense = Seed(TestActors.Employee1, status);

        ServiceResult<ExpenseResponse> result = await _service.RejectAsync(TestActors.Approver, expense.Id, Request(Justification), CancellationToken.None);

        Assert.AreEqual(ServiceError.Conflict, result.Error);
        Assert.AreEqual(status, expense.Status);
        AssertNothingSaved();
    }

    /// <summary>Conflito de concorrência na gravação vira 409 e o histórico não é confirmado.</summary>
    [TestMethod]
    public async Task RejectAsync_ConcurrencyConflictOnSave_ReturnsConflictWithoutHistory()
    {
        Expense expense = Seed(TestActors.Employee1, ExpenseStatus.Submitted);
        _repository.ConflictOnNextSave = true;

        ServiceResult<ExpenseResponse> result = await _service.RejectAsync(TestActors.Approver, expense.Id, Request(Justification), CancellationToken.None);

        Assert.AreEqual(ServiceError.Conflict, result.Error);
        Assert.IsEmpty(_repository.History);
    }

    private static RejectExpenseRequest Request(string justification)
    {
        return new RejectExpenseRequest { Justification = justification };
    }

    private Expense Seed(Actor owner, ExpenseStatus status)
    {
        return _repository.Seed(ExpenseScenario.Of(owner).At(_clock.UtcNow).InStatus(status));
    }

    private void AssertJustificationRejected(ServiceResult<ExpenseResponse> result, Expense expense)
    {
        Assert.AreEqual(ServiceError.Validation, result.Error);
        Assert.IsTrue(result.ValidationErrors.ContainsKey(nameof(RejectExpenseRequest.Justification)));
        Assert.AreEqual(ExpenseStatus.Submitted, expense.Status);
        AssertNothingSaved();
    }

    private void AssertNothingSaved()
    {
        Assert.IsEmpty(_repository.History);
        Assert.AreEqual(0, _repository.SaveChangesCount);
    }
}
