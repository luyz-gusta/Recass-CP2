using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Services;
using ExpenseHub.UnitTests.TestDoubles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Expenses;

/// <summary>
/// Listagem e detalhe: role de leitura (403), escopo aplicado na consulta (404 sem vazar a existência) e nenhuma escrita.
/// </summary>
[TestClass]
public sealed class ExpenseServiceQueryTests
{
    private readonly InMemoryExpenseRepository _repository = new();
    private readonly FixedTimeProvider _clock = new();
    private readonly ExpenseService _service;

    /// <summary>Monta o serviço com o repositório em memória e o relógio fixo.</summary>
    public ExpenseServiceQueryTests()
    {
        _service = new ExpenseService(_repository, _clock);
    }

    /// <summary>Employee lista somente os próprios reembolsos.</summary>
    [TestMethod]
    public async Task ListAsync_Employee_ReturnsOnlyOwnExpenses()
    {
        Expense own = Seed(TestActors.Employee1, ExpenseStatus.Draft);
        Seed(TestActors.Employee2, ExpenseStatus.Draft);

        ServiceResult<IReadOnlyList<ExpenseResponse>> result = await _service.ListAsync(TestActors.Employee1, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        ExpenseResponse item = Assert.ContainsSingle(result.Value!);
        Assert.AreEqual(own.Id, item.Id);
        Assert.AreEqual(TestActors.Employee1.UserId, item.OwnerId);
    }

    /// <summary>Approver lista os enviados e não vê o rascunho de ninguém.</summary>
    [TestMethod]
    public async Task ListAsync_Approver_ReturnsSubmittedAndNeverDrafts()
    {
        Seed(TestActors.Employee1, ExpenseStatus.Draft);
        Expense submitted = Seed(TestActors.Employee1, ExpenseStatus.Submitted);

        ServiceResult<IReadOnlyList<ExpenseResponse>> result = await _service.ListAsync(TestActors.Approver, CancellationToken.None);

        Assert.AreEqual(submitted.Id, Assert.ContainsSingle(result.Value!).Id);
    }

    /// <summary>Auditor lista tudo, do mais recente para o mais antigo.</summary>
    [TestMethod]
    public async Task ListAsync_Auditor_ReturnsEverythingMostRecentFirst()
    {
        Expense oldest = Seed(TestActors.Employee1, ExpenseStatus.Draft);
        _clock.Advance(TimeSpan.FromHours(1));
        Expense middle = Seed(TestActors.Employee2, ExpenseStatus.Submitted);
        _clock.Advance(TimeSpan.FromHours(1));
        Expense newest = Seed(TestActors.Employee1, ExpenseStatus.Approved);

        ServiceResult<IReadOnlyList<ExpenseResponse>> result = await _service.ListAsync(TestActors.Auditor, CancellationToken.None);

        CollectionAssert.AreEqual(new List<Guid> { newest.Id, middle.Id, oldest.Id }, result.Value!.Select(item => item.Id).ToList());
    }

    /// <summary>Sem nenhum reembolso visível, a lista é vazia (200), não um erro.</summary>
    [TestMethod]
    public async Task ListAsync_NothingVisible_ReturnsEmptyList()
    {
        Seed(TestActors.Employee2, ExpenseStatus.Draft);

        ServiceResult<IReadOnlyList<ExpenseResponse>> result = await _service.ListAsync(TestActors.Employee1, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.IsEmpty(result.Value!);
    }

    /// <summary>Admin sozinho e usuário sem role recebem 403 na listagem.</summary>
    [TestMethod]
    public async Task ListAsync_ActorWithoutReadRole_ReturnsForbidden()
    {
        Seed(TestActors.Employee1, ExpenseStatus.Draft);

        ServiceResult<IReadOnlyList<ExpenseResponse>> admin = await _service.ListAsync(TestActors.Admin, CancellationToken.None);
        ServiceResult<IReadOnlyList<ExpenseResponse>> noRole = await _service.ListAsync(TestActors.NoRole, CancellationToken.None);

        Assert.AreEqual(ServiceError.Forbidden, admin.Error);
        Assert.AreEqual(ServiceError.Forbidden, noRole.Error);
    }

    /// <summary>O proprietário consulta o detalhe do próprio reembolso.</summary>
    [TestMethod]
    public async Task GetAsync_Owner_ReturnsExpense()
    {
        Expense own = Seed(TestActors.Employee1, ExpenseStatus.Draft);

        ServiceResult<ExpenseResponse> result = await _service.GetAsync(TestActors.Employee1, own.Id, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(own.Id, result.Value!.Id);
    }

    /// <summary>Trocar o identificador na URL por um reembolso de outro Employee responde 404.</summary>
    [TestMethod]
    public async Task GetAsync_AnotherEmployeesExpense_ReturnsNotFound()
    {
        Expense other = Seed(TestActors.Employee1, ExpenseStatus.Submitted);

        ServiceResult<ExpenseResponse> result = await _service.GetAsync(TestActors.Employee2, other.Id, CancellationToken.None);

        Assert.AreEqual(ServiceError.NotFound, result.Error);
    }

    /// <summary>Approver vê o enviado, mas o rascunho alheio é 404, e não 403.</summary>
    [TestMethod]
    public async Task GetAsync_Approver_SeesSubmittedButNotAnotherEmployeesDraft()
    {
        Expense submitted = Seed(TestActors.Employee1, ExpenseStatus.Submitted);
        Expense draft = Seed(TestActors.Employee1, ExpenseStatus.Draft);

        ServiceResult<ExpenseResponse> visible = await _service.GetAsync(TestActors.Approver, submitted.Id, CancellationToken.None);
        ServiceResult<ExpenseResponse> hidden = await _service.GetAsync(TestActors.Approver, draft.Id, CancellationToken.None);

        Assert.IsTrue(visible.Succeeded);
        Assert.AreEqual(ServiceError.NotFound, hidden.Error);
    }

    /// <summary>Finance vê o aprovado, mas ainda não vê o enviado.</summary>
    [TestMethod]
    public async Task GetAsync_Finance_SeesApprovedButNotSubmitted()
    {
        Expense approved = Seed(TestActors.Employee1, ExpenseStatus.Approved);
        Expense submitted = Seed(TestActors.Employee1, ExpenseStatus.Submitted);

        ServiceResult<ExpenseResponse> visible = await _service.GetAsync(TestActors.Finance, approved.Id, CancellationToken.None);
        ServiceResult<ExpenseResponse> hidden = await _service.GetAsync(TestActors.Finance, submitted.Id, CancellationToken.None);

        Assert.IsTrue(visible.Succeeded);
        Assert.AreEqual(ServiceError.NotFound, hidden.Error);
    }

    /// <summary>Auditor consulta o detalhe de qualquer reembolso, inclusive rascunho de outra pessoa.</summary>
    [TestMethod]
    public async Task GetAsync_Auditor_SeesAnyExpense()
    {
        Expense draft = Seed(TestActors.Employee1, ExpenseStatus.Draft);

        ServiceResult<ExpenseResponse> result = await _service.GetAsync(TestActors.Auditor, draft.Id, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
    }

    /// <summary>Inexistente e fora do escopo respondem exatamente igual (404 com a mesma mensagem): nada vaza.</summary>
    [TestMethod]
    public async Task GetAsync_MissingAndInvisible_AreIndistinguishable()
    {
        Expense invisible = Seed(TestActors.Employee1, ExpenseStatus.Draft);

        ServiceResult<ExpenseResponse> missing = await _service.GetAsync(TestActors.Employee2, Guid.NewGuid(), CancellationToken.None);
        ServiceResult<ExpenseResponse> outOfScope = await _service.GetAsync(TestActors.Employee2, invisible.Id, CancellationToken.None);

        Assert.AreEqual(ServiceError.NotFound, missing.Error);
        Assert.AreEqual(ServiceError.NotFound, outOfScope.Error);
        Assert.AreEqual(missing.Message, outOfScope.Message);
    }

    /// <summary>Admin sozinho e usuário sem role recebem 403 no detalhe, mesmo para um reembolso que existe.</summary>
    [TestMethod]
    public async Task GetAsync_ActorWithoutReadRole_ReturnsForbidden()
    {
        Expense draft = Seed(TestActors.Employee1, ExpenseStatus.Draft);

        ServiceResult<ExpenseResponse> admin = await _service.GetAsync(TestActors.Admin, draft.Id, CancellationToken.None);
        ServiceResult<ExpenseResponse> noRole = await _service.GetAsync(TestActors.NoRole, draft.Id, CancellationToken.None);

        Assert.AreEqual(ServiceError.Forbidden, admin.Error);
        Assert.AreEqual(ServiceError.Forbidden, noRole.Error);
    }

    /// <summary>Consultar nunca grava: sem histórico e sem commit.</summary>
    [TestMethod]
    public async Task ListAndGet_AnyActor_NeverWrite()
    {
        Expense draft = Seed(TestActors.Employee1, ExpenseStatus.Draft);

        await _service.ListAsync(TestActors.Auditor, CancellationToken.None);
        await _service.GetAsync(TestActors.Auditor, draft.Id, CancellationToken.None);

        Assert.IsEmpty(_repository.History);
        Assert.AreEqual(0, _repository.SaveChangesCount);
    }

    private Expense Seed(Actor owner, ExpenseStatus status)
    {
        Expense expense = TestExpenses.InStatus(TestExpenses.Draft(owner.UserId, _clock.UtcNow), status);
        _repository.Seed(expense);
        return expense;
    }
}
