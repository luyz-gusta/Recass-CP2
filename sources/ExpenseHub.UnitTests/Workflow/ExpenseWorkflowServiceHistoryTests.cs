using System;
using System.Collections.Generic;
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
/// Histórico: cada ação gera uma entrada com ator, horário UTC, estado anterior e posterior, e a consulta segue
/// a mesma visibilidade do detalhe do reembolso.
/// </summary>
[TestClass]
public sealed class ExpenseWorkflowServiceHistoryTests
{
    private const string Justification = "Comprovante ilegível, envie novamente.";

    private readonly InMemoryExpenseRepository _repository = new();
    private readonly FixedTimeProvider _clock = new();
    private readonly ExpenseService _expenseService;
    private readonly ExpenseWorkflowService _service;

    /// <summary>Monta os serviços de reembolso e de fluxo sobre o mesmo repositório em memória e o mesmo relógio fixo.</summary>
    public ExpenseWorkflowServiceHistoryTests()
    {
        _expenseService = new ExpenseService(_repository, _clock);
        _service = new ExpenseWorkflowService(_repository, _clock);
    }

    /// <summary>
    /// Fluxo completo (criar, editar, enviar, aprovar e pagar): uma entrada por ação, em ordem cronológica,
    /// com o ator, o horário do servidor e os estados anterior e posterior de cada uma.
    /// </summary>
    [TestMethod]
    public async Task GetHistoryAsync_FullPaymentFlow_ListsEveryActionInChronologicalOrder()
    {
        Guid id = await CreateAsync(TestActors.Employee1);
        DateTime createdAt = _clock.UtcNow;
        DateTime updatedAt = await AtNextHourAsync(() => _expenseService.UpdateAsync(TestActors.Employee1, id, UpdateRequest(210m), CancellationToken.None));
        DateTime submittedAt = await AtNextHourAsync(() => _expenseService.SubmitAsync(TestActors.Employee1, id, CancellationToken.None));
        DateTime approvedAt = await AtNextHourAsync(() => _service.ApproveAsync(TestActors.Approver, id, CancellationToken.None));
        DateTime paidAt = await AtNextHourAsync(() => _service.PayAsync(TestActors.Finance, id, CancellationToken.None));

        IReadOnlyList<ExpenseHistoryResponse> history = await GetHistoryAsync(TestActors.Auditor, id);

        Assert.HasCount(5, history);
        AssertEntry(history[0], ExpenseAction.Created, TestActors.Employee1, createdAt, null, ExpenseStatus.Draft);
        AssertEntry(history[1], ExpenseAction.Updated, TestActors.Employee1, updatedAt, ExpenseStatus.Draft, ExpenseStatus.Draft);
        AssertEntry(history[2], ExpenseAction.Submitted, TestActors.Employee1, submittedAt, ExpenseStatus.Draft, ExpenseStatus.Submitted);
        AssertEntry(history[3], ExpenseAction.Approved, TestActors.Approver, approvedAt, ExpenseStatus.Submitted, ExpenseStatus.Approved);
        AssertEntry(history[4], ExpenseAction.Paid, TestActors.Finance, paidAt, ExpenseStatus.Approved, ExpenseStatus.Paid);
    }

    /// <summary>A edição em Draft aparece no histórico com o texto das alterações.</summary>
    [TestMethod]
    public async Task GetHistoryAsync_DraftEdit_ExposesChanges()
    {
        Guid id = await CreateAsync(TestActors.Employee1);
        await _expenseService.UpdateAsync(TestActors.Employee1, id, UpdateRequest(210m), CancellationToken.None);

        IReadOnlyList<ExpenseHistoryResponse> history = await GetHistoryAsync(TestActors.Employee1, id);

        ExpenseHistoryResponse updated = history.Single(entry => entry.Action == ExpenseAction.Updated);
        Assert.AreEqual("amount: 150.50 -> 210.00", updated.Changes);
        Assert.IsNull(updated.Justification);
    }

    /// <summary>A reprovação aparece no histórico com a justificativa, sem os espaços nas pontas.</summary>
    [TestMethod]
    public async Task GetHistoryAsync_Rejection_ExposesJustification()
    {
        Guid id = await CreateAsync(TestActors.Employee1);
        await _expenseService.SubmitAsync(TestActors.Employee1, id, CancellationToken.None);
        await _service.RejectAsync(TestActors.Approver, id, new RejectExpenseRequest { Justification = "  " + Justification + " " }, CancellationToken.None);

        IReadOnlyList<ExpenseHistoryResponse> history = await GetHistoryAsync(TestActors.Employee1, id);

        ExpenseHistoryResponse rejected = history[^1];
        Assert.AreEqual(ExpenseAction.Rejected, rejected.Action);
        Assert.AreEqual(ExpenseStatus.Submitted, rejected.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Rejected, rejected.NewStatus);
        Assert.AreEqual(Justification, rejected.Justification);
        Assert.IsNull(rejected.Changes);
    }

    /// <summary>O histórico de um reembolso não traz entradas de outro reembolso.</summary>
    [TestMethod]
    public async Task GetHistoryAsync_TwoExpenses_ReturnsOnlyEntriesOfRequestedExpense()
    {
        Guid first = await CreateAsync(TestActors.Employee1);
        Guid second = await CreateAsync(TestActors.Employee1);
        await _expenseService.SubmitAsync(TestActors.Employee1, second, CancellationToken.None);

        IReadOnlyList<ExpenseHistoryResponse> history = await GetHistoryAsync(TestActors.Employee1, first);

        Assert.AreEqual(ExpenseAction.Created, Assert.ContainsSingle(history).Action);
    }

    /// <summary>O dono lê o histórico do próprio reembolso, em qualquer estado.</summary>
    [TestMethod]
    [DataRow(ExpenseStatus.Draft)]
    [DataRow(ExpenseStatus.Submitted)]
    [DataRow(ExpenseStatus.Approved)]
    [DataRow(ExpenseStatus.Rejected)]
    [DataRow(ExpenseStatus.Paid)]
    public async Task GetHistoryAsync_Owner_SeesOwnHistoryInAnyStatus(ExpenseStatus status)
    {
        Expense expense = SeedInStatus(TestActors.Employee1, status);

        ServiceResult<IReadOnlyList<ExpenseHistoryResponse>> result = await _service.GetHistoryAsync(TestActors.Employee1, expense.Id, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
    }

    /// <summary>Outro Employee não lê o histórico alheio (404), em nenhum estado.</summary>
    [TestMethod]
    [DataRow(ExpenseStatus.Draft)]
    [DataRow(ExpenseStatus.Submitted)]
    [DataRow(ExpenseStatus.Paid)]
    public async Task GetHistoryAsync_AnotherEmployee_ReturnsNotFound(ExpenseStatus status)
    {
        Expense expense = SeedInStatus(TestActors.Employee1, status);

        ServiceResult<IReadOnlyList<ExpenseHistoryResponse>> result = await _service.GetHistoryAsync(TestActors.Employee2, expense.Id, CancellationToken.None);

        Assert.AreEqual(ServiceError.NotFound, result.Error);
    }

    /// <summary>O Approver lê o histórico dos reembolsos enviados.</summary>
    [TestMethod]
    public async Task GetHistoryAsync_ApproverOnSubmitted_SeesHistory()
    {
        Expense expense = SeedInStatus(TestActors.Employee1, ExpenseStatus.Submitted);

        ServiceResult<IReadOnlyList<ExpenseHistoryResponse>> result = await _service.GetHistoryAsync(TestActors.Approver, expense.Id, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
    }

    /// <summary>Fora de Submitted, o Approver não enxerga o reembolso nem o histórico (404).</summary>
    [TestMethod]
    [DataRow(ExpenseStatus.Draft)]
    [DataRow(ExpenseStatus.Approved)]
    [DataRow(ExpenseStatus.Rejected)]
    [DataRow(ExpenseStatus.Paid)]
    public async Task GetHistoryAsync_ApproverOutsideSubmitted_ReturnsNotFound(ExpenseStatus status)
    {
        Expense expense = SeedInStatus(TestActors.Employee1, status);

        ServiceResult<IReadOnlyList<ExpenseHistoryResponse>> result = await _service.GetHistoryAsync(TestActors.Approver, expense.Id, CancellationToken.None);

        Assert.AreEqual(ServiceError.NotFound, result.Error);
    }

    /// <summary>O Finance lê o histórico dos aprovados e dos pagos.</summary>
    [TestMethod]
    [DataRow(ExpenseStatus.Approved)]
    [DataRow(ExpenseStatus.Paid)]
    public async Task GetHistoryAsync_FinanceOnApprovedOrPaid_SeesHistory(ExpenseStatus status)
    {
        Expense expense = SeedInStatus(TestActors.Employee1, status);

        ServiceResult<IReadOnlyList<ExpenseHistoryResponse>> result = await _service.GetHistoryAsync(TestActors.Finance, expense.Id, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
    }

    /// <summary>Rascunho, enviado e reprovado ficam fora do escopo do Finance (404).</summary>
    [TestMethod]
    [DataRow(ExpenseStatus.Draft)]
    [DataRow(ExpenseStatus.Submitted)]
    [DataRow(ExpenseStatus.Rejected)]
    public async Task GetHistoryAsync_FinanceOutsideApprovedOrPaid_ReturnsNotFound(ExpenseStatus status)
    {
        Expense expense = SeedInStatus(TestActors.Employee1, status);

        ServiceResult<IReadOnlyList<ExpenseHistoryResponse>> result = await _service.GetHistoryAsync(TestActors.Finance, expense.Id, CancellationToken.None);

        Assert.AreEqual(ServiceError.NotFound, result.Error);
    }

    /// <summary>O Auditor lê o histórico de qualquer reembolso, em qualquer estado.</summary>
    [TestMethod]
    [DataRow(ExpenseStatus.Draft)]
    [DataRow(ExpenseStatus.Submitted)]
    [DataRow(ExpenseStatus.Approved)]
    [DataRow(ExpenseStatus.Rejected)]
    [DataRow(ExpenseStatus.Paid)]
    public async Task GetHistoryAsync_Auditor_SeesAnyHistory(ExpenseStatus status)
    {
        Expense expense = SeedInStatus(TestActors.Employee1, status);

        ServiceResult<IReadOnlyList<ExpenseHistoryResponse>> result = await _service.GetHistoryAsync(TestActors.Auditor, expense.Id, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
    }

    /// <summary>Sem role de leitura (Admin sozinho ou sem role) a consulta responde 403.</summary>
    [TestMethod]
    [DataRow(AppRoles.Admin)]
    [DataRow(null)]
    public async Task GetHistoryAsync_ActorWithoutReadRole_ReturnsForbidden(string? role)
    {
        Expense expense = SeedInStatus(TestActors.Employee1, ExpenseStatus.Submitted);
        Actor actor = role is null ? TestActors.NoRole : TestActors.Create("user-without-read-role", role);

        ServiceResult<IReadOnlyList<ExpenseHistoryResponse>> result = await _service.GetHistoryAsync(actor, expense.Id, CancellationToken.None);

        Assert.AreEqual(ServiceError.Forbidden, result.Error);
    }

    /// <summary>Inexistente e fora do escopo respondem exatamente igual (mesmo erro e mesma mensagem), sem vazar que o reembolso existe.</summary>
    [TestMethod]
    public async Task GetHistoryAsync_MissingAndInvisible_AreIndistinguishable()
    {
        Expense expense = SeedInStatus(TestActors.Employee1, ExpenseStatus.Draft);

        ServiceResult<IReadOnlyList<ExpenseHistoryResponse>> invisible = await _service.GetHistoryAsync(TestActors.Employee2, expense.Id, CancellationToken.None);
        ServiceResult<IReadOnlyList<ExpenseHistoryResponse>> missing = await _service.GetHistoryAsync(TestActors.Employee2, Guid.NewGuid(), CancellationToken.None);

        Assert.AreEqual(ServiceError.NotFound, invisible.Error);
        Assert.AreEqual(missing.Error, invisible.Error);
        Assert.AreEqual(missing.Message, invisible.Message);
    }

    private static UpdateExpenseRequest UpdateRequest(decimal amount)
    {
        return new UpdateExpenseRequest
        {
            Description = TestExpenses.ValidDescription,
            Amount = amount,
            ExpenseDate = TestExpenses.ValidDate,
            CategoryId = TestExpenses.ValidCategoryId,
        };
    }

    private static void AssertEntry(
        ExpenseHistoryResponse entry,
        ExpenseAction action,
        Actor actor,
        DateTime occurredAtUtc,
        ExpenseStatus? previousStatus,
        ExpenseStatus newStatus)
    {
        Assert.AreEqual(action, entry.Action);
        Assert.AreEqual(actor.UserId, entry.ActorId);
        Assert.AreEqual(occurredAtUtc, entry.OccurredAtUtc);
        Assert.AreEqual(DateTimeKind.Utc, entry.OccurredAtUtc.Kind);
        Assert.AreEqual(previousStatus, entry.PreviousStatus);
        Assert.AreEqual(newStatus, entry.NewStatus);
    }

    private async Task<Guid> CreateAsync(Actor owner)
    {
        CreateExpenseRequest request = new()
        {
            Description = TestExpenses.ValidDescription,
            Amount = TestExpenses.ValidAmount,
            ExpenseDate = TestExpenses.ValidDate,
            CategoryId = TestExpenses.ValidCategoryId,
        };

        ServiceResult<ExpenseResponse> created = await _expenseService.CreateAsync(owner, request, CancellationToken.None);
        return created.Value!.Id;
    }

    /// <summary>Avança o relógio uma hora, executa a ação (que precisa dar certo) e devolve o horário em que ela aconteceu.</summary>
    private async Task<DateTime> AtNextHourAsync(Func<Task<ServiceResult<ExpenseResponse>>> action)
    {
        _clock.Advance(TimeSpan.FromHours(1));
        ServiceResult<ExpenseResponse> result = await action();
        Assert.IsTrue(result.Succeeded, result.Message);
        return _clock.UtcNow;
    }

    private async Task<IReadOnlyList<ExpenseHistoryResponse>> GetHistoryAsync(Actor actor, Guid id)
    {
        ServiceResult<IReadOnlyList<ExpenseHistoryResponse>> result = await _service.GetHistoryAsync(actor, id, CancellationToken.None);
        Assert.IsTrue(result.Succeeded, result.Message);
        return result.Value!;
    }

    /// <summary>Leva um reembolso até o estado pedido pelas transições reais do domínio e o grava como confirmado.</summary>
    private Expense SeedInStatus(Actor owner, ExpenseStatus status)
    {
        Expense expense = TestExpenses.Draft(owner.UserId, _clock.UtcNow);

        if (status != ExpenseStatus.Draft)
        {
            expense.Submit(_clock.UtcNow);
        }

        if (status == ExpenseStatus.Rejected)
        {
            expense.Reject(TestActors.Approver.UserId, Justification, _clock.UtcNow);
        }
        else if (status is ExpenseStatus.Approved or ExpenseStatus.Paid)
        {
            expense.Approve(TestActors.Approver.UserId, _clock.UtcNow);
        }

        if (status == ExpenseStatus.Paid)
        {
            expense.MarkAsPaid(TestActors.Finance.UserId, _clock.UtcNow);
        }

        _repository.Seed(expense);
        return expense;
    }
}
