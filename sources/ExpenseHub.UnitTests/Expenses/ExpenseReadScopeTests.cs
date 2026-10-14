using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Services;
using ExpenseHub.UnitTests.TestDoubles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Expenses;

/// <summary>
/// Visibilidade de leitura por perfil. O filtro é a expressão <c>ReadScope</c>, aplicada na consulta do repositório
/// (como o banco faz), sobre reembolsos de vários donos e em todos os estados.
/// </summary>
[TestClass]
public sealed class ExpenseReadScopeTests
{
    private readonly InMemoryExpenseRepository _repository = new();
    private readonly Expense _employee1Draft;
    private readonly Expense _employee1Submitted;
    private readonly Expense _employee1Rejected;
    private readonly Expense _employee2Draft;
    private readonly Expense _employee2Submitted;
    private readonly Expense _employee2Approved;
    private readonly Expense _employee2Paid;
    private readonly Expense _approverEmployeeDraft;
    private readonly Expense _financeEmployeeApproved;

    /// <summary>Semeia nove reembolsos: rascunhos, enviados, aprovados, reprovado e pago, de cinco donos diferentes.</summary>
    public ExpenseReadScopeTests()
    {
        _employee1Draft = Seed(TestActors.Employee1, ExpenseStatus.Draft);
        _employee1Submitted = Seed(TestActors.Employee1, ExpenseStatus.Submitted);
        _employee1Rejected = Seed(TestActors.Employee1, ExpenseStatus.Rejected);
        _employee2Draft = Seed(TestActors.Employee2, ExpenseStatus.Draft);
        _employee2Submitted = Seed(TestActors.Employee2, ExpenseStatus.Submitted);
        _employee2Approved = Seed(TestActors.Employee2, ExpenseStatus.Approved);
        _employee2Paid = Seed(TestActors.Employee2, ExpenseStatus.Paid);
        _approverEmployeeDraft = Seed(TestActors.ApproverEmployee, ExpenseStatus.Draft);
        _financeEmployeeApproved = Seed(TestActors.FinanceEmployee, ExpenseStatus.Approved);
    }

    /// <summary>Employee vê só os próprios reembolsos, em qualquer estado.</summary>
    [TestMethod]
    public async Task ReadScope_Employee_SeesOnlyOwnExpensesInAnyStatus()
    {
        IReadOnlyList<Guid> visible = await VisibleToAsync(TestActors.Employee1);

        CollectionAssert.AreEquivalent(Ids(_employee1Draft, _employee1Submitted, _employee1Rejected), visible.ToList());
    }

    /// <summary>O outro Employee vê só os dele, e nenhum dos reembolsos do primeiro.</summary>
    [TestMethod]
    public async Task ReadScope_OtherEmployee_SeesOnlyOwnExpenses()
    {
        IReadOnlyList<Guid> visible = await VisibleToAsync(TestActors.Employee2);

        CollectionAssert.AreEquivalent(Ids(_employee2Draft, _employee2Submitted, _employee2Approved, _employee2Paid), visible.ToList());
    }

    /// <summary>Approver vê somente os enviados: nunca rascunho alheio, nem aprovado, reprovado ou pago.</summary>
    [TestMethod]
    public async Task ReadScope_Approver_SeesOnlySubmitted()
    {
        IReadOnlyList<Guid> visible = await VisibleToAsync(TestActors.Approver);

        CollectionAssert.AreEquivalent(Ids(_employee1Submitted, _employee2Submitted), visible.ToList());
    }

    /// <summary>Finance vê somente aprovados e pagos.</summary>
    [TestMethod]
    public async Task ReadScope_Finance_SeesOnlyApprovedAndPaid()
    {
        IReadOnlyList<Guid> visible = await VisibleToAsync(TestActors.Finance);

        CollectionAssert.AreEquivalent(Ids(_employee2Approved, _employee2Paid, _financeEmployeeApproved), visible.ToList());
    }

    /// <summary>Auditor vê todos os reembolsos, de todos os donos e estados.</summary>
    [TestMethod]
    public async Task ReadScope_Auditor_SeesEverything()
    {
        IReadOnlyList<Guid> visible = await VisibleToAsync(TestActors.Auditor);

        Assert.HasCount(_repository.Expenses.Count, visible);
    }

    /// <summary>Approver + Employee vê os próprios (inclusive rascunho) mais todos os enviados, mas não o rascunho alheio.</summary>
    [TestMethod]
    public async Task ReadScope_ApproverWithEmployee_SeesOwnPlusAllSubmitted()
    {
        IReadOnlyList<Guid> visible = await VisibleToAsync(TestActors.ApproverEmployee);

        CollectionAssert.AreEquivalent(Ids(_approverEmployeeDraft, _employee1Submitted, _employee2Submitted), visible.ToList());
    }

    /// <summary>Finance + Employee vê os próprios mais todos os aprovados e pagos.</summary>
    [TestMethod]
    public async Task ReadScope_FinanceWithEmployee_SeesOwnPlusApprovedAndPaid()
    {
        IReadOnlyList<Guid> visible = await VisibleToAsync(TestActors.FinanceEmployee);

        CollectionAssert.AreEquivalent(Ids(_employee2Approved, _employee2Paid, _financeEmployeeApproved), visible.ToList());
    }

    /// <summary>Quem acumula as quatro roles de leitura vê tudo (união das permissões).</summary>
    [TestMethod]
    public async Task ReadScope_AllReaderRoles_SeesEverything()
    {
        Actor actor = TestActors.Create("everything-1", AppRoles.Employee, AppRoles.Approver, AppRoles.Finance, AppRoles.Auditor);

        IReadOnlyList<Guid> visible = await VisibleToAsync(actor);

        Assert.HasCount(_repository.Expenses.Count, visible);
    }

    /// <summary>Admin sozinho não recebe acesso funcional: não vê nenhum reembolso.</summary>
    [TestMethod]
    public async Task ReadScope_AdminOnly_SeesNothing()
    {
        IReadOnlyList<Guid> visible = await VisibleToAsync(TestActors.Admin);

        Assert.IsEmpty(visible);
    }

    /// <summary>A role Admin não amplia as outras: Admin + Employee vê só os próprios.</summary>
    [TestMethod]
    public async Task ReadScope_AdminWithEmployee_SeesOnlyOwnExpenses()
    {
        Actor actor = TestActors.Create(TestActors.Employee1.UserId, AppRoles.Admin, AppRoles.Employee);

        IReadOnlyList<Guid> visible = await VisibleToAsync(actor);

        CollectionAssert.AreEquivalent(Ids(_employee1Draft, _employee1Submitted, _employee1Rejected), visible.ToList());
    }

    /// <summary>Usuário sem role não vê nenhum reembolso.</summary>
    [TestMethod]
    public async Task ReadScope_NoRole_SeesNothing()
    {
        IReadOnlyList<Guid> visible = await VisibleToAsync(TestActors.NoRole);

        Assert.IsEmpty(visible);
    }

    /// <summary>Admin e usuário sem role não têm role de leitura; todas as demais roles têm.</summary>
    [TestMethod]
    [DataRow(AppRoles.Employee, true)]
    [DataRow(AppRoles.Approver, true)]
    [DataRow(AppRoles.Finance, true)]
    [DataRow(AppRoles.Auditor, true)]
    [DataRow(AppRoles.Admin, false)]
    public void HasReadRole_SingleRole_MatchesTheAccessMatrix(string role, bool expected)
    {
        Actor actor = TestActors.Create("user-1", role);

        Assert.AreEqual(expected, ExpenseAccessPolicy.HasReadRole(actor));
    }

    /// <summary>Usuário sem nenhuma role não tem role de leitura.</summary>
    [TestMethod]
    public void HasReadRole_NoRole_ReturnsFalse()
    {
        Assert.IsFalse(ExpenseAccessPolicy.HasReadRole(TestActors.NoRole));
    }

    /// <summary>Admin acumulado com uma role de leitura passa a ter role de leitura.</summary>
    [TestMethod]
    public void HasReadRole_AdminWithAuditor_ReturnsTrue()
    {
        Actor actor = TestActors.Create("user-1", AppRoles.Admin, AppRoles.Auditor);

        Assert.IsTrue(ExpenseAccessPolicy.HasReadRole(actor));
    }

    private static List<Guid> Ids(params Expense[] expenses)
    {
        return expenses.Select(expense => expense.Id).ToList();
    }

    private Expense Seed(Actor owner, ExpenseStatus status)
    {
        Expense expense = TestExpenses.InStatus(TestExpenses.Draft(owner.UserId), status);
        _repository.Seed(expense);
        return expense;
    }

    private async Task<IReadOnlyList<Guid>> VisibleToAsync(Actor actor)
    {
        IReadOnlyList<Expense> visible = await _repository.ListAsync(ExpenseAccessPolicy.ReadScope(actor), CancellationToken.None);
        return visible.Select(expense => expense.Id).ToList();
    }
}
