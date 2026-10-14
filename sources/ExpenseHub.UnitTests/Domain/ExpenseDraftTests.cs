using System;
using ExpenseHub.Api.Domain;
using ExpenseHub.UnitTests.TestDoubles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Domain;

/// <summary>
/// Criação e edição de rascunhos no domínio: o servidor define identificador, estado, horários e carimbo de concorrência.
/// </summary>
[TestClass]
public sealed class ExpenseDraftTests
{
    private static readonly DateTime _createdAt = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _later = new(2026, 10, 2, 9, 30, 0, DateTimeKind.Utc);

    /// <summary>O rascunho nasce em Draft, com o proprietário e os dados informados.</summary>
    [TestMethod]
    public void CreateDraft_ValidData_StartsAsDraftWithGivenData()
    {
        Expense expense = Expense.CreateDraft("employee-1", 2, "Táxi para a reunião", 45.90m, new DateOnly(2026, 9, 29), _createdAt);

        Assert.AreEqual(ExpenseStatus.Draft, expense.Status);
        Assert.AreEqual("employee-1", expense.OwnerId);
        Assert.AreEqual(2, expense.CategoryId);
        Assert.AreEqual("Táxi para a reunião", expense.Description);
        Assert.AreEqual(45.90m, expense.Amount);
        Assert.AreEqual(new DateOnly(2026, 9, 29), expense.ExpenseDate);
    }

    /// <summary>Identificador e carimbo de concorrência são gerados pelo domínio, e cada rascunho recebe os seus.</summary>
    [TestMethod]
    public void CreateDraft_TwoDrafts_GenerateDistinctIdentifiersAndStamps()
    {
        Expense first = TestExpenses.Draft("employee-1");
        Expense second = TestExpenses.Draft("employee-1");

        Assert.AreNotEqual(Guid.Empty, first.Id);
        Assert.AreNotEqual(first.Id, second.Id);
        Assert.AreNotEqual(Guid.Empty, first.ConcurrencyStamp);
        Assert.AreNotEqual(first.ConcurrencyStamp, second.ConcurrencyStamp);
    }

    /// <summary>Criação e última alteração usam o mesmo instante; o que ainda não aconteceu fica vazio.</summary>
    [TestMethod]
    public void CreateDraft_ValidData_SetsTimestampsAndLeavesLaterStepsEmpty()
    {
        Expense expense = TestExpenses.Draft("employee-1", _createdAt);

        Assert.AreEqual(_createdAt, expense.CreatedAtUtc);
        Assert.AreEqual(_createdAt, expense.UpdatedAtUtc);
        Assert.IsNull(expense.SubmittedAtUtc);
        Assert.IsNull(expense.DecidedById);
        Assert.IsNull(expense.DecidedAtUtc);
        Assert.IsNull(expense.RejectionReason);
        Assert.IsNull(expense.Payment);
    }

    /// <summary>Rascunho sem proprietário é rejeitado.</summary>
    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void CreateDraft_BlankOwner_Throws(string ownerId)
    {
        Assert.ThrowsExactly<ArgumentException>(() => TestExpenses.Draft(ownerId));
    }

    /// <summary>Editar todos os campos devolve o texto das alterações no formato <c>campo: antigo -> novo</c>, com valor e data em cultura invariante.</summary>
    [TestMethod]
    public void UpdateDraft_AllFieldsChanged_ReturnsEveryChangeInInvariantFormat()
    {
        Expense expense = TestExpenses.Draft("employee-1", _createdAt);

        string? changes = expense.UpdateDraft(3, "Hotel durante o evento", 1200m, new DateOnly(2026, 10, 1), _later);

        Assert.AreEqual(
            "categoryId: 1 -> 3; description: Almoço com cliente em São Paulo -> Hotel durante o evento; amount: 150.50 -> 1200.00; expenseDate: 2026-09-30 -> 2026-10-01",
            changes);
    }

    /// <summary>Somente os campos que mudaram aparecem no texto das alterações.</summary>
    [TestMethod]
    public void UpdateDraft_OnlyAmountChanged_ListsOnlyAmount()
    {
        Expense expense = TestExpenses.Draft("employee-1", _createdAt);

        string? changes = expense.UpdateDraft(TestExpenses.ValidCategoryId, TestExpenses.ValidDescription, 99.99m, TestExpenses.ValidDate, _later);

        Assert.AreEqual("amount: 150.50 -> 99.99", changes);
    }

    /// <summary>A edição aplica os novos dados, atualiza o horário e renova o carimbo de concorrência.</summary>
    [TestMethod]
    public void UpdateDraft_WithChanges_AppliesDataAndRenewsStampAndUpdatedAt()
    {
        Expense expense = TestExpenses.Draft("employee-1", _createdAt);
        Guid previousStamp = expense.ConcurrencyStamp;

        expense.UpdateDraft(2, "Táxi para o aeroporto", 80m, new DateOnly(2026, 9, 28), _later);

        Assert.AreEqual(2, expense.CategoryId);
        Assert.AreEqual("Táxi para o aeroporto", expense.Description);
        Assert.AreEqual(80m, expense.Amount);
        Assert.AreEqual(new DateOnly(2026, 9, 28), expense.ExpenseDate);
        Assert.AreEqual(_later, expense.UpdatedAtUtc);
        Assert.AreNotEqual(previousStamp, expense.ConcurrencyStamp);
    }

    /// <summary>A edição nunca troca proprietário, estado nem horário de criação.</summary>
    [TestMethod]
    public void UpdateDraft_WithChanges_KeepsOwnerStatusAndCreationTime()
    {
        Expense expense = TestExpenses.Draft("employee-1", _createdAt);

        expense.UpdateDraft(2, "Táxi para o aeroporto", 80m, new DateOnly(2026, 9, 28), _later);

        Assert.AreEqual("employee-1", expense.OwnerId);
        Assert.AreEqual(ExpenseStatus.Draft, expense.Status);
        Assert.AreEqual(_createdAt, expense.CreatedAtUtc);
    }

    /// <summary>Sem nenhuma diferença, nada é alterado e o resultado é nulo.</summary>
    [TestMethod]
    public void UpdateDraft_NothingChanged_ReturnsNullAndKeepsUpdatedAtAndStamp()
    {
        Expense expense = TestExpenses.Draft("employee-1", _createdAt);
        Guid previousStamp = expense.ConcurrencyStamp;

        string? changes = expense.UpdateDraft(TestExpenses.ValidCategoryId, TestExpenses.ValidDescription, TestExpenses.ValidAmount, TestExpenses.ValidDate, _later);

        Assert.IsNull(changes);
        Assert.AreEqual(_createdAt, expense.UpdatedAtUtc);
        Assert.AreEqual(previousStamp, expense.ConcurrencyStamp);
    }

    /// <summary>Um valor numericamente igual, mesmo com outra escala decimal, não conta como alteração.</summary>
    [TestMethod]
    public void UpdateDraft_SameAmountWithDifferentScale_ReturnsNull()
    {
        Expense expense = TestExpenses.Draft("employee-1", _createdAt);

        string? changes = expense.UpdateDraft(TestExpenses.ValidCategoryId, TestExpenses.ValidDescription, 150.5m, TestExpenses.ValidDate, _later);

        Assert.IsNull(changes);
    }

    /// <summary>Somente Draft pode ser editado: nos demais estados o domínio lança exceção (defesa em profundidade).</summary>
    [TestMethod]
    [DataRow(ExpenseStatus.Submitted)]
    [DataRow(ExpenseStatus.Approved)]
    [DataRow(ExpenseStatus.Rejected)]
    [DataRow(ExpenseStatus.Paid)]
    public void UpdateDraft_NotDraft_Throws(ExpenseStatus status)
    {
        Expense expense = TestExpenses.InStatus(TestExpenses.Draft("employee-1", _createdAt), status);

        Assert.ThrowsExactly<InvalidOperationException>(() => expense.UpdateDraft(2, "Táxi para o aeroporto", 80m, new DateOnly(2026, 9, 28), _later));
    }
}
