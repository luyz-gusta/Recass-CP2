using System;
using ExpenseHub.Api.Domain;
using ExpenseHub.UnitTests.TestDoubles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Domain;

/// <summary>
/// Envio no domínio: somente Draft vira Submitted, uma única vez, com horário e carimbo definidos pelo servidor.
/// </summary>
[TestClass]
public sealed class ExpenseSubmitTests
{
    private static readonly DateTime _createdAt = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _submittedAt = new(2026, 10, 2, 8, 15, 0, DateTimeKind.Utc);

    /// <summary>Um rascunho enviado passa para Submitted e registra o instante do envio.</summary>
    [TestMethod]
    public void Submit_Draft_BecomesSubmittedWithSubmissionTime()
    {
        Expense expense = TestExpenses.Draft("employee-1", _createdAt);

        expense.Submit(_submittedAt);

        Assert.AreEqual(ExpenseStatus.Submitted, expense.Status);
        Assert.AreEqual(_submittedAt, expense.SubmittedAtUtc);
    }

    /// <summary>O envio atualiza o horário da última alteração e renova o carimbo de concorrência.</summary>
    [TestMethod]
    public void Submit_Draft_TouchesUpdatedAtAndRenewsStamp()
    {
        Expense expense = TestExpenses.Draft("employee-1", _createdAt);
        Guid previousStamp = expense.ConcurrencyStamp;

        expense.Submit(_submittedAt);

        Assert.AreEqual(_submittedAt, expense.UpdatedAtUtc);
        Assert.AreNotEqual(previousStamp, expense.ConcurrencyStamp);
    }

    /// <summary>O envio não altera proprietário, dados nem criação, e ainda não há decisão nem pagamento.</summary>
    [TestMethod]
    public void Submit_Draft_KeepsOwnerDataAndLeavesDecisionEmpty()
    {
        Expense expense = TestExpenses.Draft("employee-1", _createdAt);

        expense.Submit(_submittedAt);

        Assert.AreEqual("employee-1", expense.OwnerId);
        Assert.AreEqual(TestExpenses.ValidDescription, expense.Description);
        Assert.AreEqual(TestExpenses.ValidAmount, expense.Amount);
        Assert.AreEqual(_createdAt, expense.CreatedAtUtc);
        Assert.IsNull(expense.DecidedById);
        Assert.IsNull(expense.Payment);
    }

    /// <summary>Não existe reenvio: enviar de novo lança exceção e preserva o primeiro envio.</summary>
    [TestMethod]
    public void Submit_AlreadySubmitted_ThrowsAndKeepsFirstSubmission()
    {
        Expense expense = TestExpenses.Draft("employee-1", _createdAt);
        expense.Submit(_submittedAt);

        Assert.ThrowsExactly<InvalidOperationException>(() => expense.Submit(_submittedAt.AddHours(1)));

        Assert.AreEqual(_submittedAt, expense.SubmittedAtUtc);
    }

    /// <summary>Somente Draft pode ser enviado: nos demais estados o domínio lança exceção (defesa em profundidade).</summary>
    [TestMethod]
    [DataRow(ExpenseStatus.Submitted)]
    [DataRow(ExpenseStatus.Approved)]
    [DataRow(ExpenseStatus.Rejected)]
    [DataRow(ExpenseStatus.Paid)]
    public void Submit_NotDraft_Throws(ExpenseStatus status)
    {
        Expense expense = ExpenseScenario.Of("employee-1").At(_createdAt).InStatus(status);

        Assert.ThrowsExactly<InvalidOperationException>(() => expense.Submit(_submittedAt));
    }

    /// <summary>Depois de enviado, o reembolso não pode mais ser editado.</summary>
    [TestMethod]
    public void UpdateDraft_AfterSubmit_Throws()
    {
        Expense expense = TestExpenses.Draft("employee-1", _createdAt);
        expense.Submit(_submittedAt);

        Assert.ThrowsExactly<InvalidOperationException>(() => expense.UpdateDraft(2, "Táxi para o aeroporto", 80m, new DateOnly(2026, 9, 28), _submittedAt));
    }
}
