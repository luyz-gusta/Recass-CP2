using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Services;
using ExpenseHub.UnitTests.TestDoubles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Workflow;

/// <summary>
/// Pagamento: somente um Finance que não é o proprietário paga um reembolso aprovado, uma única vez. Estado, registro
/// de pagamento e histórico são gravados juntos, ou nada é gravado.
/// </summary>
[TestClass]
public sealed class ExpenseWorkflowServicePayTests
{
    private readonly InMemoryExpenseRepository _repository = new();
    private readonly FixedTimeProvider _clock = new();
    private readonly ExpenseWorkflowService _service;

    /// <summary>Monta o serviço com o repositório em memória e o relógio fixo.</summary>
    public ExpenseWorkflowServicePayTests()
    {
        _service = new ExpenseWorkflowService(_repository, _clock);
    }

    /// <summary>O Finance paga o reembolso aprovado de outra pessoa: o estado vira Paid, com quem pagou e quando, definidos pelo servidor.</summary>
    [TestMethod]
    public async Task PayAsync_OthersApproved_PaysWithPayerAndServerTime()
    {
        Expense expense = Seed(TestActors.Employee1, ExpenseStatus.Approved);
        _clock.Advance(TimeSpan.FromDays(2));

        ServiceResult<ExpenseResponse> result = await _service.PayAsync(TestActors.Finance, expense.Id, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(ExpenseStatus.Paid, result.Value!.Status);
        Assert.AreEqual(TestActors.Finance.UserId, result.Value.PaidById);
        Assert.AreEqual(_clock.UtcNow, result.Value.PaidAtUtc);
    }

    /// <summary>O registro de pagamento confirmado guarda o reembolso, o valor dele, quem pagou e o horário do servidor.</summary>
    [TestMethod]
    public async Task PayAsync_OthersApproved_RecordsPaymentWithAmountPayerAndTime()
    {
        Expense expense = Seed(TestActors.Employee1, ExpenseStatus.Approved);
        _clock.Advance(TimeSpan.FromDays(2));

        await _service.PayAsync(TestActors.Finance, expense.Id, CancellationToken.None);

        PaymentRecord payment = Assert.ContainsSingle(_repository.Payments);
        Assert.AreEqual(expense.Id, payment.ExpenseId);
        Assert.AreEqual(TestExpenses.ValidAmount, payment.Amount);
        Assert.AreEqual(TestActors.Finance.UserId, payment.PaidById);
        Assert.AreEqual(_clock.UtcNow, payment.PaidAtUtc);
    }

    /// <summary>O histórico <c>Paid</c> (Approved para Paid, com quem pagou) é gravado no mesmo commit do pagamento.</summary>
    [TestMethod]
    public async Task PayAsync_OthersApproved_RecordsPaidHistoryInSingleSave()
    {
        Expense expense = Seed(TestActors.Employee1, ExpenseStatus.Approved);
        _clock.Advance(TimeSpan.FromDays(2));

        await _service.PayAsync(TestActors.Finance, expense.Id, CancellationToken.None);

        ExpenseHistory entry = Assert.ContainsSingle(_repository.History);
        Assert.AreEqual(expense.Id, entry.ExpenseId);
        Assert.AreEqual(ExpenseAction.Paid, entry.Action);
        Assert.AreEqual(TestActors.Finance.UserId, entry.ActorId);
        Assert.AreEqual(_clock.UtcNow, entry.OccurredAtUtc);
        Assert.AreEqual(ExpenseStatus.Approved, entry.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Paid, entry.NewStatus);
        Assert.AreEqual(1, _repository.SaveChangesCount);
    }

    /// <summary>Quem acumula Finance com Employee paga o reembolso aprovado de outra pessoa.</summary>
    [TestMethod]
    public async Task PayAsync_FinanceEmployeeOnOthersApproved_Pays()
    {
        Expense expense = Seed(TestActors.Employee1, ExpenseStatus.Approved);

        ServiceResult<ExpenseResponse> result = await _service.PayAsync(TestActors.FinanceEmployee, expense.Id, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(TestActors.FinanceEmployee.UserId, Assert.ContainsSingle(_repository.Payments).PaidById);
    }

    /// <summary>Autopagamento é proibido: Finance + Employee no próprio reembolso recebe 403, sem pagamento nem histórico.</summary>
    [TestMethod]
    public async Task PayAsync_FinanceEmployeeOnOwnApproved_ReturnsForbiddenWithoutPayment()
    {
        Expense expense = Seed(TestActors.FinanceEmployee, ExpenseStatus.Approved);

        ServiceResult<ExpenseResponse> result = await _service.PayAsync(TestActors.FinanceEmployee, expense.Id, CancellationToken.None);

        Assert.AreEqual(ServiceError.Forbidden, result.Error);
        Assert.AreEqual(ExpenseStatus.Approved, expense.Status);
        AssertNothingSaved();
    }

    /// <summary>Nenhuma combinação de roles libera o autopagamento: Finance + Approver + Employee no próprio reembolso também recebe 403.</summary>
    [TestMethod]
    public async Task PayAsync_EveryWorkflowRoleOnOwnApproved_ReturnsForbiddenWithoutPayment()
    {
        Actor owner = TestActors.Create("all-roles-1", AppRoles.Employee, AppRoles.Approver, AppRoles.Finance);
        Expense expense = Seed(owner, ExpenseStatus.Approved);

        ServiceResult<ExpenseResponse> result = await _service.PayAsync(owner, expense.Id, CancellationToken.None);

        Assert.AreEqual(ServiceError.Forbidden, result.Error);
        Assert.AreEqual(ExpenseStatus.Approved, expense.Status);
        AssertNothingSaved();
    }

    /// <summary>Roles sem Finance recebem 403 (Employee, Approver, Auditor e Admin não pagam), e nada muda.</summary>
    [TestMethod]
    [DataRow(AppRoles.Employee)]
    [DataRow(AppRoles.Approver)]
    [DataRow(AppRoles.Auditor)]
    [DataRow(AppRoles.Admin)]
    public async Task PayAsync_ActorWithoutFinanceRole_ReturnsForbidden(string role)
    {
        Expense expense = Seed(TestActors.Employee1, ExpenseStatus.Approved);
        Actor actor = TestActors.Create("user-without-finance", role);

        ServiceResult<ExpenseResponse> result = await _service.PayAsync(actor, expense.Id, CancellationToken.None);

        Assert.AreEqual(ServiceError.Forbidden, result.Error);
        Assert.AreEqual(ExpenseStatus.Approved, expense.Status);
        AssertNothingSaved();
    }

    /// <summary>Usuário sem nenhuma role recebe 403.</summary>
    [TestMethod]
    public async Task PayAsync_ActorWithNoRole_ReturnsForbidden()
    {
        Expense expense = Seed(TestActors.Employee1, ExpenseStatus.Approved);

        ServiceResult<ExpenseResponse> result = await _service.PayAsync(TestActors.NoRole, expense.Id, CancellationToken.None);

        Assert.AreEqual(ServiceError.Forbidden, result.Error);
        AssertNothingSaved();
    }

    /// <summary>Identificador inexistente responde 404.</summary>
    [TestMethod]
    public async Task PayAsync_UnknownId_ReturnsNotFound()
    {
        ServiceResult<ExpenseResponse> result = await _service.PayAsync(TestActors.Finance, Guid.NewGuid(), CancellationToken.None);

        Assert.AreEqual(ServiceError.NotFound, result.Error);
        AssertNothingSaved();
    }

    /// <summary>O rascunho de outra pessoa responde 404: o Finance nem enxerga rascunhos.</summary>
    [TestMethod]
    public async Task PayAsync_OthersDraft_ReturnsNotFoundAndKeepsDraft()
    {
        Expense draft = Seed(TestActors.Employee1, ExpenseStatus.Draft);

        ServiceResult<ExpenseResponse> result = await _service.PayAsync(TestActors.Finance, draft.Id, CancellationToken.None);

        Assert.AreEqual(ServiceError.NotFound, result.Error);
        Assert.AreEqual(ExpenseStatus.Draft, draft.Status);
        AssertNothingSaved();
    }

    /// <summary>Submitted, Rejected e Paid não são pagos: 409, sem pagamento, sem histórico e sem gravar.</summary>
    [TestMethod]
    [DataRow(ExpenseStatus.Submitted)]
    [DataRow(ExpenseStatus.Rejected)]
    [DataRow(ExpenseStatus.Paid)]
    public async Task PayAsync_ExpenseNotApproved_ReturnsConflictWithoutPayment(ExpenseStatus status)
    {
        Expense expense = Seed(TestActors.Employee1, status);

        ServiceResult<ExpenseResponse> result = await _service.PayAsync(TestActors.Finance, expense.Id, CancellationToken.None);

        Assert.AreEqual(ServiceError.Conflict, result.Error);
        Assert.AreEqual(status, expense.Status);
        AssertNothingSaved();
    }

    /// <summary>Pagar duas vezes: o segundo pagamento responde 409 e existem exatamente um pagamento e um histórico <c>Paid</c>.</summary>
    [TestMethod]
    public async Task PayAsync_SecondPayment_ReturnsConflictWithSinglePaymentAndPaidHistory()
    {
        Expense expense = Seed(TestActors.Employee1, ExpenseStatus.Approved);
        await _service.PayAsync(TestActors.Finance, expense.Id, CancellationToken.None);

        ServiceResult<ExpenseResponse> second = await _service.PayAsync(TestActors.FinanceEmployee, expense.Id, CancellationToken.None);

        Assert.AreEqual(ServiceError.Conflict, second.Error);
        Assert.AreEqual(TestActors.Finance.UserId, Assert.ContainsSingle(_repository.Payments).PaidById);
        Assert.AreEqual(1, _repository.History.Count(entry => entry.Action == ExpenseAction.Paid));
        Assert.AreEqual(1, _repository.SaveChangesCount);
    }

    /// <summary>
    /// Atomicidade: se a gravação falha, a exceção se propaga e nem o pagamento nem o histórico são confirmados,
    /// então estado persistido, pagamento e histórico nunca divergem.
    /// </summary>
    [TestMethod]
    public async Task PayAsync_SaveFails_PropagatesAndPersistsNeitherPaymentNorHistory()
    {
        Expense expense = Seed(TestActors.Employee1, ExpenseStatus.Approved);
        _repository.FailNextSave = true;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _service.PayAsync(TestActors.Finance, expense.Id, CancellationToken.None));

        Assert.IsEmpty(_repository.Payments);
        Assert.IsEmpty(_repository.History);
        Assert.AreEqual(1, _repository.SaveChangesCount);
    }

    /// <summary>Conflito de concorrência na gravação vira 409, sem pagamento nem histórico confirmados.</summary>
    [TestMethod]
    public async Task PayAsync_ConcurrencyConflictOnSave_ReturnsConflictWithoutPaymentOrHistory()
    {
        Expense expense = Seed(TestActors.Employee1, ExpenseStatus.Approved);
        _repository.ConflictOnNextSave = true;

        ServiceResult<ExpenseResponse> result = await _service.PayAsync(TestActors.Finance, expense.Id, CancellationToken.None);

        Assert.AreEqual(ServiceError.Conflict, result.Error);
        Assert.IsEmpty(_repository.Payments);
        Assert.IsEmpty(_repository.History);
    }

    private Expense Seed(Actor owner, ExpenseStatus status)
    {
        return _repository.Seed(ExpenseScenario.Of(owner).At(_clock.UtcNow).InStatus(status));
    }

    private void AssertNothingSaved()
    {
        Assert.IsEmpty(_repository.Payments);
        Assert.IsEmpty(_repository.History);
        Assert.AreEqual(0, _repository.SaveChangesCount);
    }
}
